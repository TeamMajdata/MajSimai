using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>CMakeLists.txt</c>: the interface library <c>MajSimai::MajSimaiCpp</c>, which adds the include directory
/// and links the MajSimai library published with native AOT.
/// </summary>
/// <remarks>
/// Without a <see cref="NativePackage"/>, the file is the one of <c>Bindings/Cpp</c> and links the library named by the
/// <c>MAJSIMAI_NATIVE_LIBRARY</c> cache variable. With one, the file belongs to a package made by
/// <c>tools/build-cpp-package.py</c> and links the shared or static library of the package through the imported target
/// <c>MajSimai::Native</c>; for a static library that target also carries the native AOT runtime and the system
/// libraries, in link order.
/// </remarks>
internal static class CMakeEmitter
{
    /// <summary>
    /// The name of the file.
    /// </summary>
    public const string FileName = "CMakeLists.txt";

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="model">The native ABI, which gives the version.</param>
    /// <param name="package">The native build the wrapper is packaged with, or <see langword="null"/> for <c>Bindings/Cpp</c>.</param>
    /// <returns>The content of the file.</returns>
    public static string Emit(AbiModel model, NativePackage? package = null)
    {
        var writer = new CppWriter();
        writer.Banner(model.Version, "#");
        writer.Lines($"""
            cmake_minimum_required(VERSION 3.15)

            project(MajSimaiCpp VERSION {model.Version} LANGUAGES CXX)

            """);
        if (package is not null)
        {
            WriteNativeTarget(writer, package);
            writer.Line();
        }
        writer.Lines("""
            add_library(MajSimaiCpp INTERFACE)
            add_library(MajSimai::MajSimaiCpp ALIAS MajSimaiCpp)
            target_include_directories(MajSimaiCpp INTERFACE "${CMAKE_CURRENT_SOURCE_DIR}/include")
            target_compile_features(MajSimaiCpp INTERFACE cxx_std_17)
            """);
        if (package is null)
        {
            WriteLibraryVariable(writer);
        }
        else
        {
            foreach (var definition in package.CompileDefinitions)
            {
                writer.Line($"target_compile_definitions(MajSimaiCpp INTERFACE \"{definition}\")");
            }
            writer.Line("target_link_libraries(MajSimaiCpp INTERFACE MajSimai::Native)");
        }

        return writer.ToString();
    }

    /// <summary>
    /// Writes the imported target of the packaged native library.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="package">The native build.</param>
    private static void WriteNativeTarget(CppWriter writer, NativePackage package)
    {
        var type = package.IsStatic ? "STATIC" : "SHARED";
        writer.Line($"# MajSimai published with native AOT as a {package.Kind} library for {package.RuntimeIdentifier}.");
        writer.Line($"add_library(MajSimai::Native {type} IMPORTED GLOBAL)");
        if (package.RuntimeLibrary is null)
        {
            writer.Line($"set_target_properties(MajSimai::Native PROPERTIES IMPORTED_LOCATION \"{PackagePath(package.Library)}\")");
        }
        else
        {
            writer.Line("set_target_properties(MajSimai::Native PROPERTIES");
            writer.Line($"    IMPORTED_LOCATION \"{PackagePath(package.RuntimeLibrary)}\"");
            writer.Line($"    IMPORTED_IMPLIB \"{PackagePath(package.Library)}\")");
        }
        var libraries = package.Dependencies.Select(PackagePath).Concat(package.SystemLibraries).ToArray();
        if (libraries.Length != 0)
        {
            writer.Line("# The native AOT runtime and the system libraries the static library needs, in link order.");
            writer.Line("set_property(TARGET MajSimai::Native PROPERTY INTERFACE_LINK_LIBRARIES");
            for (var i = 0; i < libraries.Length; i++)
            {
                writer.Line($"    \"{libraries[i]}\"{(i == libraries.Length - 1 ? ")" : string.Empty)}");
            }
        }
        if (package.LinkOptions.Count != 0)
        {
            var options = string.Join(" ", package.LinkOptions.Select(o => $"\"{o}\""));
            writer.Line($"set_property(TARGET MajSimai::Native PROPERTY INTERFACE_LINK_OPTIONS {options})");
        }
    }

    /// <summary>
    /// Writes the cache variable that names the native library of <c>Bindings/Cpp</c>, and links it.
    /// </summary>
    /// <param name="writer">The writer.</param>
    private static void WriteLibraryVariable(CppWriter writer)
    {
        writer.Lines("""

            # Link the native library with MAJSIMAI_NATIVE_LIBRARY, or link it yourself. tools/build-cpp-package.py makes a
            # package that links its own shared or static library instead.
            set(MAJSIMAI_NATIVE_LIBRARY "" CACHE FILEPATH
                "MajSimai published with native AOT: the import library MajSimai.lib on Windows, MajSimai.so on Linux or MajSimai.dylib on macOS.")
            if(MAJSIMAI_NATIVE_LIBRARY)
                if(CMAKE_SYSTEM_NAME STREQUAL "Linux" OR CMAKE_SYSTEM_NAME STREQUAL "Android")
                    # A MajSimai.so published without a soname would be recorded with the absolute path it was linked from,
                    # so link it by its file name; the program then finds it through its rpath or LD_LIBRARY_PATH.
                    get_filename_component(MAJSIMAI_NATIVE_DIRECTORY "${MAJSIMAI_NATIVE_LIBRARY}" DIRECTORY)
                    get_filename_component(MAJSIMAI_NATIVE_NAME "${MAJSIMAI_NATIVE_LIBRARY}" NAME)
                    target_link_directories(MajSimaiCpp INTERFACE "${MAJSIMAI_NATIVE_DIRECTORY}")
                    target_link_libraries(MajSimaiCpp INTERFACE "-l:${MAJSIMAI_NATIVE_NAME}")
                else()
                    target_link_libraries(MajSimaiCpp INTERFACE "${MAJSIMAI_NATIVE_LIBRARY}")
                endif()
            endif()
            """);
    }

    /// <summary>
    /// Turns a path relative to the package into a CMake path.
    /// </summary>
    /// <param name="relativePath">The path relative to the package.</param>
    /// <returns>The path under <c>${CMAKE_CURRENT_SOURCE_DIR}</c>.</returns>
    private static string PackagePath(string relativePath)
    {
        return "${CMAKE_CURRENT_SOURCE_DIR}/" + relativePath.Replace('\\', '/');
    }
}
