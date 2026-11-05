namespace MajSimai.CppGen;

/// <summary>
/// A file of the generated C++ wrapper.
/// </summary>
/// <param name="RelativePath">The path of the file in the output directory, with <c>/</c> as the separator.</param>
/// <param name="Content">The content of the file, with <c>\n</c> line endings.</param>
internal sealed record GeneratedFile(string RelativePath, string Content);
