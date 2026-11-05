using System;
using System.Collections.Generic;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// Computes how a C++ compiler lays out a struct on a 64-bit platform: every field is aligned to its own size, and the
/// struct is aligned to its largest field.
/// </summary>
internal static class NativeLayout
{
    /// <summary>
    /// The size of a pointer on the platforms the layout is computed for.
    /// </summary>
    public const int PointerSize = 8;

    /// <summary>
    /// Computes the offsets and the size of a struct.
    /// </summary>
    /// <param name="fieldTypes">The types of the fields, in declaration order.</param>
    /// <returns>The offset of each field and the size of the struct, in bytes.</returns>
    public static (int[] Offsets, int Size) Compute(IReadOnlyList<AbiType> fieldTypes)
    {
        var offsets = new int[fieldTypes.Count];
        var offset = 0;
        var structAlignment = 1;
        for (var i = 0; i < fieldTypes.Count; i++)
        {
            var size = GetSize(fieldTypes[i]);
            offset = AlignUp(offset, size);
            offsets[i] = offset;
            offset += size;
            structAlignment = Math.Max(structAlignment, size);
        }

        return (offsets, AlignUp(offset, structAlignment));
    }

    /// <summary>
    /// Gets the size of a field type, which is also its alignment.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The size in bytes.</returns>
    /// <exception cref="CppGenException">Thrown for a type that cannot be a field, such as <c>void</c> or a struct.</exception>
    private static int GetSize(AbiType type)
    {
        return type switch
        {
            AbiPointerType => PointerSize,
            AbiEnumType enumType => enumType.Enum.UnderlyingType.Size,
            AbiPrimitiveType { Size: > 0 } primitive => primitive.Size,
            _ => throw new CppGenException($"The type {type.CppName} cannot be a field of a native struct."),
        };
    }

    /// <summary>
    /// Rounds an offset up to a multiple of an alignment.
    /// </summary>
    /// <param name="offset">The offset.</param>
    /// <param name="alignment">The alignment.</param>
    /// <returns>The smallest multiple of <paramref name="alignment"/> that is not less than <paramref name="offset"/>.</returns>
    private static int AlignUp(int offset, int alignment)
    {
        return (offset + alignment - 1) / alignment * alignment;
    }
}
