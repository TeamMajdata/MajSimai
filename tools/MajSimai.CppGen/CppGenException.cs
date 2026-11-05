using System;

namespace MajSimai.CppGen;

/// <summary>
/// Thrown when the native ABI cannot be turned into C++, for example because a pointer field does not say what it
/// points to. The message names the member and tells how to fix it.
/// </summary>
internal sealed class CppGenException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">The description of the problem.</param>
    public CppGenException(string message)
        : base(message)
    {
    }
}
