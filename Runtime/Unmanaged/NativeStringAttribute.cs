#if NET7_0_OR_GREATER
using System;

namespace MajSimai.Unmanaged;

/// <summary>
/// Marks a <c>byte*</c> field or parameter of the native ABI as UTF-8 text whose length in bytes is stored in another
/// field or parameter. The pointer is null when the text is empty.
/// </summary>
/// <remarks>
/// The C++ binding generator (<c>tools/MajSimai.CppGen</c>) reads this attribute: the pointer becomes a
/// <c>const char*</c>, and the views and the owned model expose the text as <c>std::string_view</c> and
/// <c>std::string</c>. The length field itself is hidden behind the text.
/// </remarks>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
internal sealed class NativeStringAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="lengthName">The name of the <see cref="int"/> field or parameter that holds the length in bytes.</param>
    public NativeStringAttribute(string lengthName)
    {
        LengthName = lengthName;
    }

    /// <summary>
    /// Gets the name of the <see cref="int"/> field or parameter that holds the length in bytes.
    /// </summary>
    public string LengthName { get; }
}
#endif
