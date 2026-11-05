using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MajSimai.CppGen;

/// <summary>
/// Writes generated files to a directory, or checks that the directory already has them.
/// </summary>
/// <remarks>
/// The generator owns the files it writes and every <c>*.g.h</c> file in the include directory; a <c>*.g.h</c> file
/// that is no longer generated is stale. Other files in the directory are left alone. Line endings are ignored when
/// comparing, so a checkout with CRLF line endings is up to date.
/// </remarks>
internal static class OutputDirectory
{
    /// <summary>
    /// Writes the files that are missing or different, and deletes stale files.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="files">The generated files.</param>
    /// <returns>The relative paths of the files that were written or deleted.</returns>
    public static IReadOnlyList<string> Write(string root, IReadOnlyList<GeneratedFile> files)
    {
        var changed = new List<string>();
        foreach (var file in files)
        {
            var path = GetPath(root, file.RelativePath);
            if (!IsUpToDate(path, file.Content))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, file.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                changed.Add(file.RelativePath);
            }
        }
        foreach (var stale in FindStaleFiles(root, files))
        {
            File.Delete(GetPath(root, stale));
            changed.Add(stale);
        }

        return changed;
    }

    /// <summary>
    /// Finds the files that are missing, different or stale.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="files">The generated files.</param>
    /// <returns>The relative paths of the files that are not up to date.</returns>
    public static IReadOnlyList<string> Check(string root, IReadOnlyList<GeneratedFile> files)
    {
        return files.Where(f => !IsUpToDate(GetPath(root, f.RelativePath), f.Content))
                    .Select(f => f.RelativePath)
                    .Concat(FindStaleFiles(root, files))
                    .ToArray();
    }

    /// <summary>
    /// Finds the <c>*.g.h</c> files in the include directory that are not generated anymore.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="files">The generated files.</param>
    /// <returns>The relative paths of the stale files.</returns>
    private static IEnumerable<string> FindStaleFiles(string root, IReadOnlyList<GeneratedFile> files)
    {
        var include = GetPath(root, CppBindingGenerator.IncludeDirectory);
        if (!Directory.Exists(include))
        {
            return Array.Empty<string>();
        }
        var generated = new HashSet<string>(files.Select(f => f.RelativePath), StringComparer.Ordinal);

        return Directory.EnumerateFiles(include, "*.g.h")
                        .Select(p => CppBindingGenerator.IncludeDirectory + "/" + Path.GetFileName(p))
                        .Where(p => !generated.Contains(p))
                        .OrderBy(p => p, StringComparer.Ordinal)
                        .ToArray();
    }

    /// <summary>
    /// Gets whether a file exists and has the given content, ignoring line endings.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="content">The expected content.</param>
    /// <returns><see langword="true"/> when the file is up to date.</returns>
    private static bool IsUpToDate(string path, string content)
    {
        return File.Exists(path) && NormalizeLineEndings(File.ReadAllText(path)) == NormalizeLineEndings(content);
    }

    /// <summary>
    /// Replaces CRLF line endings with LF.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with LF line endings.</returns>
    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n");
    }

    /// <summary>
    /// Turns a relative path of the output directory into a full path.
    /// </summary>
    /// <param name="root">The output directory.</param>
    /// <param name="relativePath">The relative path, with <c>/</c> as the separator.</param>
    /// <returns>The full path.</returns>
    private static string GetPath(string root, string relativePath)
    {
        return Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
