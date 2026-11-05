using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MajSimai.CppGen;

/// <summary>
/// Describes a native build of MajSimai that the C++ wrapper is packaged with, as written by
/// <c>tools/build-cpp-package.py</c> to <c>MajSimaiNative.json</c>. With it, the generated <c>CMakeLists.txt</c> links
/// that build instead of asking for <c>MAJSIMAI_NATIVE_LIBRARY</c>.
/// </summary>
/// <param name="Kind"><c>shared</c> or <c>static</c>.</param>
/// <param name="RuntimeIdentifier">The .NET runtime identifier the library was built for, such as <c>linux-x64</c>.</param>
/// <param name="Library">
/// The library to link, relative to the package: the shared library, the import library of a Windows DLL, or the static
/// library.
/// </param>
/// <param name="RuntimeLibrary">The Windows DLL relative to the package, for a shared library on Windows; otherwise null.</param>
/// <param name="Dependencies">
/// The files a static library needs after it on the link line (the native AOT runtime), relative to the package and in
/// link order.
/// </param>
/// <param name="SystemLibraries">The system libraries a static library needs, in link order, such as <c>dl</c>.</param>
/// <param name="LinkOptions">The linker options a static library needs, such as <c>-pthread</c>.</param>
/// <param name="CompileDefinitions">The preprocessor definitions for code that includes the wrapper.</param>
internal sealed record NativePackage(string Kind,
                                     string RuntimeIdentifier,
                                     string Library,
                                     string? RuntimeLibrary,
                                     IReadOnlyList<string> Dependencies,
                                     IReadOnlyList<string> SystemLibraries,
                                     IReadOnlyList<string> LinkOptions,
                                     IReadOnlyList<string> CompileDefinitions)
{
    /// <summary>
    /// The kind of a shared library.
    /// </summary>
    public const string SharedKind = "shared";

    /// <summary>
    /// The kind of a static library.
    /// </summary>
    public const string StaticKind = "static";

    /// <summary>
    /// The options that read the manifest: camel-case names, as the script writes them.
    /// </summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>
    /// Gets whether the library is static.
    /// </summary>
    public bool IsStatic => Kind == StaticKind;

    /// <summary>
    /// Reads and checks a manifest.
    /// </summary>
    /// <param name="path">The path of <c>MajSimaiNative.json</c>.</param>
    /// <returns>The description of the native build.</returns>
    /// <exception cref="CppGenException">Thrown when the file cannot be read or describes an invalid build.</exception>
    public static NativePackage Load(string path)
    {
        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new CppGenException($"The native manifest '{path}' cannot be read: {e.Message}");
        }

        return Parse(json, path);
    }

    /// <summary>
    /// Reads and checks a manifest from its JSON text.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="source">Where the text comes from, for error messages.</param>
    /// <returns>The description of the native build.</returns>
    /// <exception cref="CppGenException">Thrown when the text is not valid JSON or describes an invalid build.</exception>
    public static NativePackage Parse(string json, string source)
    {
        NativePackage? package;
        try
        {
            package = JsonSerializer.Deserialize<NativePackage>(json, s_jsonOptions);
        }
        catch (JsonException e)
        {
            throw new CppGenException($"The native manifest '{source}' is not valid JSON: {e.Message}");
        }
        if (package is null)
        {
            throw new CppGenException($"The native manifest '{source}' is empty.");
        }
        package = package with
        {
            Dependencies = package.Dependencies ?? Array.Empty<string>(),
            SystemLibraries = package.SystemLibraries ?? Array.Empty<string>(),
            LinkOptions = package.LinkOptions ?? Array.Empty<string>(),
            CompileDefinitions = package.CompileDefinitions ?? Array.Empty<string>(),
        };
        package.Check(source);

        return package;
    }

    /// <summary>
    /// Checks that the manifest describes a build the CMake file can link.
    /// </summary>
    /// <param name="source">Where the manifest comes from, for error messages.</param>
    /// <exception cref="CppGenException">Thrown when a value is missing or not valid.</exception>
    private void Check(string source)
    {
        if (Kind is not (SharedKind or StaticKind))
        {
            throw new CppGenException($"The native manifest '{source}' has the kind '{Kind}'; it must be '{SharedKind}' or '{StaticKind}'.");
        }
        if (string.IsNullOrWhiteSpace(RuntimeIdentifier) || string.IsNullOrWhiteSpace(Library))
        {
            throw new CppGenException($"The native manifest '{source}' must give runtimeIdentifier and library.");
        }
        if (!IsStatic && (Dependencies.Count != 0 || SystemLibraries.Count != 0))
        {
            throw new CppGenException($"The native manifest '{source}' lists dependencies for a shared library, which links them itself.");
        }
        foreach (var path in Dependencies.Append(Library).Concat(RuntimeLibrary is null ? Array.Empty<string>() : new[] { RuntimeLibrary }))
        {
            if (Path.IsPathRooted(path) || path.Split('/', '\\').Contains(".."))
            {
                throw new CppGenException($"The native manifest '{source}' has the path '{path}', which must be relative to the package and stay inside it.");
            }
        }
        if (Dependencies.Concat(SystemLibraries).Concat(LinkOptions).Concat(CompileDefinitions).Append(Library).Any(v => v.Contains('"') || v.Contains('\n')))
        {
            throw new CppGenException($"The native manifest '{source}' has a value with a quote or a line break.");
        }
    }
}
