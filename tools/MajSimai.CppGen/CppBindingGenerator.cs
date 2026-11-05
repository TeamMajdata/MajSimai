using System.Collections.Generic;
using System.Reflection;
using MajSimai.CppGen.Abi;
using MajSimai.CppGen.Emit;
using MajSimai.Unmanaged;

namespace MajSimai.CppGen;

/// <summary>
/// Generates the C++ wrapper of MajSimai from its native ABI.
/// </summary>
internal static class CppBindingGenerator
{
    /// <summary>
    /// The directory of the headers in the output directory.
    /// </summary>
    public const string IncludeDirectory = "include/MajSimai";

    /// <summary>
    /// Reads the native ABI of the MajSimai assembly this tool was built against.
    /// </summary>
    /// <returns>The ABI.</returns>
    /// <exception cref="CppGenException">Thrown when the ABI cannot be expressed in C++.</exception>
    public static AbiModel ReadMajSimai()
    {
        var assembly = typeof(UnmanagedSimaiParser).Assembly;

        return new AbiReader(XmlDocumentation.Load(assembly)).Read(AbiReader.FindExports(assembly), GetVersion(assembly));
    }

    /// <summary>
    /// Generates the files of the wrapper.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <param name="package">
    /// The native build the wrapper is packaged with, which the CMake file then links; <see langword="null"/> for the
    /// wrapper of <c>Bindings/Cpp</c>, whose CMake file asks for the library.
    /// </param>
    /// <returns>The files, with paths relative to the output directory.</returns>
    /// <exception cref="CppGenException">Thrown when the ABI does not have what the wrapper uses.</exception>
    public static IReadOnlyList<GeneratedFile> Generate(AbiModel model, NativePackage? package = null)
    {
        return new[]
        {
            new GeneratedFile($"{IncludeDirectory}/{ParserHeaderEmitter.FileName}", ParserHeaderEmitter.Emit(model)),
            new GeneratedFile($"{IncludeDirectory}/{CoreHeaderEmitter.FileName}", CoreHeaderEmitter.Emit(model)),
            new GeneratedFile($"{IncludeDirectory}/{ArrayViewHeaderEmitter.FileName}", ArrayViewHeaderEmitter.Emit(model)),
            new GeneratedFile($"{IncludeDirectory}/{ModelHeaderEmitter.FileName}", ModelHeaderEmitter.Emit(model)),
            new GeneratedFile($"{IncludeDirectory}/{ViewsHeaderEmitter.FileName}", ViewsHeaderEmitter.Emit(model)),
            new GeneratedFile(CMakeEmitter.FileName, CMakeEmitter.Emit(model, package)),
        };
    }

    /// <summary>
    /// Gets the version of an assembly without the revision, such as <c>3.0.0</c>.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The version.</returns>
    private static string GetVersion(Assembly assembly)
    {
        var version = assembly.GetName().Version;

        return version is null ? "0.0.0" : version.ToString(3);
    }
}
