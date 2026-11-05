#if NET7_0_OR_GREATER
using System;

namespace MajSimai.Unmanaged;

/// <summary>
/// Gives the real type of an untyped (<c>void*</c>) pointer field of the native ABI, which points to one instance of
/// <see cref="Type"/> or is null.
/// </summary>
/// <remarks>
/// Public ABI structs use <c>void*</c> for pointers to internal structs, because a public field cannot have an internal
/// type. The C++ binding generator (<c>tools/MajSimai.CppGen</c>) reads this attribute and declares the field with the
/// real pointer type, which has the same size.
/// </remarks>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
internal sealed class NativePointeeAttribute : Attribute
{
    /// <summary>
    /// Creates the attribute.
    /// </summary>
    /// <param name="type">The struct the pointer points to.</param>
    public NativePointeeAttribute(Type type)
    {
        Type = type;
    }

    /// <summary>
    /// Gets the struct the pointer points to.
    /// </summary>
    public Type Type { get; }
}
#endif
