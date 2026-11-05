#if NET7_0_OR_GREATER
using System;

namespace MajSimai.Unmanaged;

/// <summary>
/// Marks a pointer field of the native ABI as an array whose number of items is stored in another field. The pointer
/// is null when the array is empty.
/// </summary>
/// <remarks>
/// The C++ binding generator (<c>tools/MajSimai.CppGen</c>) reads this attribute: the views expose the items as an
/// <c>ArrayView</c>, and the owned model copies them into a <c>std::vector</c>. The length field itself is hidden
/// behind the array.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class NativeArrayAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="lengthName">The name of the <see cref="int"/> field that holds the number of items.</param>
    public NativeArrayAttribute(string lengthName)
    {
        LengthName = lengthName;
    }

    /// <summary>
    /// Gets the name of the <see cref="int"/> field that holds the number of items.
    /// </summary>
    public string LengthName { get; }
}
#endif
