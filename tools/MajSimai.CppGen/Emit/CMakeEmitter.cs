using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>CMakeLists.txt</c>: an interface library that adds the include directory and, when given, links the
/// MajSimai library published with native AOT.
/// </summary>
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
    /// <returns>The content of the file.</returns>
    public static string Emit(AbiModel model)
    {
        var writer = new CppWriter();
        writer.Banner(model.Version, "#");
        writer.Lines($$"""
            cmake_minimum_required(VERSION 3.15)

            project(MajSimaiCpp VERSION {{model.Version}} LANGUAGES CXX)

            # The wrapper is header-only. Link the native library with MAJSIMAI_NATIVE_LIBRARY, or link it yourself.
            add_library(MajSimaiCpp INTERFACE)
            add_library(MajSimai::MajSimaiCpp ALIAS MajSimaiCpp)
            target_include_directories(MajSimaiCpp INTERFACE "${CMAKE_CURRENT_SOURCE_DIR}/include")
            target_compile_features(MajSimaiCpp INTERFACE cxx_std_17)

            set(MAJSIMAI_NATIVE_LIBRARY "" CACHE FILEPATH
                "MajSimai published with native AOT: the import library MajSimai.lib on Windows, MajSimai.so on Linux or MajSimai.dylib on macOS.")
            if(MAJSIMAI_NATIVE_LIBRARY)
                if(CMAKE_SYSTEM_NAME STREQUAL "Linux" OR CMAKE_SYSTEM_NAME STREQUAL "Android")
                    # MajSimai.so has no soname and no lib prefix, so link it by its file name; the program then finds it
                    # through its rpath or LD_LIBRARY_PATH instead of the absolute path it was linked from.
                    get_filename_component(MAJSIMAI_NATIVE_DIRECTORY "${MAJSIMAI_NATIVE_LIBRARY}" DIRECTORY)
                    get_filename_component(MAJSIMAI_NATIVE_NAME "${MAJSIMAI_NATIVE_LIBRARY}" NAME)
                    target_link_directories(MajSimaiCpp INTERFACE "${MAJSIMAI_NATIVE_DIRECTORY}")
                    target_link_libraries(MajSimaiCpp INTERFACE "-l:${MAJSIMAI_NATIVE_NAME}")
                else()
                    target_link_libraries(MajSimaiCpp INTERFACE "${MAJSIMAI_NATIVE_LIBRARY}")
                endif()
            endif()
            """);

        return writer.ToString();
    }
}
