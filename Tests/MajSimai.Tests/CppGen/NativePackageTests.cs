using System;
using System.IO;
using MajSimai.CppGen;
using MajSimai.CppGen.Abi;
using MajSimai.CppGen.Emit;
using Xunit;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Tests reading <c>MajSimaiNative.json</c> and writing the CMake file of a package made by
/// <c>tools/build-cpp-package.py</c>.
/// </summary>
public sealed class NativePackageTests : IDisposable
{
    /// <summary>
    /// The manifest of a static Linux package, as the script writes it.
    /// </summary>
    private const string StaticLinux = """
        {
          "kind": "static",
          "runtimeIdentifier": "linux-x64",
          "library": "lib/MajSimai.a",
          "runtimeLibrary": null,
          "dependencies": ["lib/aot/libbootstrapperdll.o", "lib/aot/libRuntime.WorkstationGC.a"],
          "systemLibraries": ["dl", "m"],
          "linkOptions": ["-pthread"],
          "compileDefinitions": []
        }
        """;

    /// <summary>
    /// The ABI of MajSimai, read once.
    /// </summary>
    private static readonly Lazy<AbiModel> s_majSimai = new(CppBindingGenerator.ReadMajSimai);

    /// <summary>
    /// A temporary directory, deleted after each test.
    /// </summary>
    private readonly string _work = Path.Combine(Path.GetTempPath(), "MajSimai.NativePackage.Tests." + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Deletes the temporary directory.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    /// <summary>
    /// Parse: reads the manifest written by the script, and treats missing lists as empty.
    /// </summary>
    [Fact]
    public void Parse_ReadsTheManifestAndTreatsMissingListsAsEmpty()
    {
        var full = NativePackage.Parse(StaticLinux, "test");
        var minimal = NativePackage.Parse("""{ "kind": "shared", "runtimeIdentifier": "osx-arm64", "library": "lib/MajSimai.dylib" }""", "test");

        Assert.True(full.IsStatic);
        Assert.Equal(new[] { "lib/aot/libbootstrapperdll.o", "lib/aot/libRuntime.WorkstationGC.a" }, full.Dependencies);
        Assert.Equal(new[] { "dl", "m" }, full.SystemLibraries);
        Assert.False(minimal.IsStatic);
        Assert.Empty(minimal.Dependencies);
        Assert.Empty(minimal.CompileDefinitions);
        Assert.Null(minimal.RuntimeLibrary);
    }

    /// <summary>
    /// Parse: rejects manifests that would make a broken CMake file.
    /// </summary>
    /// <param name="json">The manifest.</param>
    /// <param name="expected">A part of the expected message.</param>
    [Theory]
    [InlineData("""{ "kind": "dynamic", "runtimeIdentifier": "linux-x64", "library": "lib/MajSimai.so" }""", "must be 'shared' or 'static'")]
    [InlineData("""{ "kind": "shared", "runtimeIdentifier": "linux-x64" }""", "must give runtimeIdentifier and library")]
    [InlineData("""{ "kind": "shared", "runtimeIdentifier": "linux-x64", "library": "lib/MajSimai.so", "systemLibraries": ["dl"] }""", "dependencies for a shared library")]
    [InlineData("""{ "kind": "static", "runtimeIdentifier": "linux-x64", "library": "../MajSimai.a" }""", "must be relative to the package")]
    [InlineData("""{ "kind": "static", "runtimeIdentifier": "linux-x64", "library": "/opt/MajSimai.a" }""", "must be relative to the package")]
    [InlineData("""{ "kind": "static", "runtimeIdentifier": "linux-x64", "library": "lib/MajSimai.a", "linkOptions": ["-x\""] }""", "quote or a line break")]
    [InlineData("""{ "kind": """, "not valid JSON")]
    [InlineData("null", "is empty")]
    public void Parse_RejectsManifestsThatWouldMakeABrokenCMakeFile(string json, string expected)
    {
        var error = Assert.Throws<CppGenException>(() => NativePackage.Parse(json, "test"));

        Assert.Contains(expected, error.Message);
    }

    /// <summary>
    /// CMake: a static package links the library, then the runtime and the system libraries in order.
    /// </summary>
    [Fact]
    public void CMake_AStaticPackageLinksTheRuntimeAndTheSystemLibrariesInOrder()
    {
        var cmake = CMakeEmitter.Emit(s_majSimai.Value, NativePackage.Parse(StaticLinux, "test"));

        Assert.Contains("add_library(MajSimai::Native STATIC IMPORTED GLOBAL)", cmake);
        Assert.Contains("IMPORTED_LOCATION \"${CMAKE_CURRENT_SOURCE_DIR}/lib/MajSimai.a\"", cmake);
        Assert.Contains("""
            set_property(TARGET MajSimai::Native PROPERTY INTERFACE_LINK_LIBRARIES
                "${CMAKE_CURRENT_SOURCE_DIR}/lib/aot/libbootstrapperdll.o"
                "${CMAKE_CURRENT_SOURCE_DIR}/lib/aot/libRuntime.WorkstationGC.a"
                "dl"
                "m")
            """.Replace("\r\n", "\n"), cmake);
        Assert.Contains("set_property(TARGET MajSimai::Native PROPERTY INTERFACE_LINK_OPTIONS \"-pthread\")", cmake);
        Assert.Contains("target_link_libraries(MajSimaiCpp INTERFACE MajSimai::Native)", cmake);
        Assert.DoesNotContain("MAJSIMAI_NATIVE_LIBRARY", cmake);
    }

    /// <summary>
    /// CMake: a Windows shared package links the import library, loads the DLL and imports the functions.
    /// </summary>
    [Fact]
    public void CMake_AWindowsSharedPackageLinksTheImportLibrary()
    {
        var package = NativePackage.Parse("""
            {
              "kind": "shared",
              "runtimeIdentifier": "win-x64",
              "library": "lib/MajSimai.lib",
              "runtimeLibrary": "bin/MajSimai.dll",
              "compileDefinitions": ["MAJSIMAI_API=__declspec(dllimport)"]
            }
            """, "test");

        var cmake = CMakeEmitter.Emit(s_majSimai.Value, package);

        Assert.Contains("add_library(MajSimai::Native SHARED IMPORTED GLOBAL)", cmake);
        Assert.Contains("    IMPORTED_LOCATION \"${CMAKE_CURRENT_SOURCE_DIR}/bin/MajSimai.dll\"\n    IMPORTED_IMPLIB \"${CMAKE_CURRENT_SOURCE_DIR}/lib/MajSimai.lib\")", cmake);
        Assert.Contains("target_compile_definitions(MajSimaiCpp INTERFACE \"MAJSIMAI_API=__declspec(dllimport)\")", cmake);
        Assert.DoesNotContain("INTERFACE_LINK_LIBRARIES", cmake);
    }

    /// <summary>
    /// Run: writes the CMake file of a package from its manifest, and reports a missing manifest.
    /// </summary>
    [Fact]
    public void Run_WritesTheCMakeFileOfAPackageFromItsManifest()
    {
        Directory.CreateDirectory(_work);
        var manifest = Path.Combine(_work, "MajSimaiNative.json");
        File.WriteAllText(manifest, StaticLinux);
        var error = new StringWriter();

        Assert.Equal(Program.Success, Program.Run(new[] { "--output", _work, "--native-manifest", manifest }, TextWriter.Null, TextWriter.Null));
        Assert.Equal(Program.Success, Program.Run(new[] { "--output", _work, "--native-manifest", manifest, "--check" }, TextWriter.Null, TextWriter.Null));
        Assert.Equal(Program.OutOfDate, Program.Run(new[] { "--output", _work, "--check" }, TextWriter.Null, TextWriter.Null));
        Assert.Contains("MajSimai::Native STATIC IMPORTED", File.ReadAllText(Path.Combine(_work, "CMakeLists.txt")));
        Assert.True(File.Exists(Path.Combine(_work, "include", "MajSimai", "MajSimai.h")));
        Assert.Equal(Program.Failure, Program.Run(new[] { "--output", _work, "--native-manifest", Path.Combine(_work, "missing.json") }, TextWriter.Null, error));
        Assert.Contains("cannot be read", error.ToString());
    }
}
