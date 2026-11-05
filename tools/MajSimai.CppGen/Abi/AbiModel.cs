using System.Collections.Generic;
using System.Linq;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// Everything the native ABI of MajSimai declares: its enums, structs and exported functions.
/// </summary>
/// <param name="Version">The version of MajSimai, such as <c>3.0.0</c>.</param>
/// <param name="Enums">The enums, in the order they were found.</param>
/// <param name="Structs">The structs; a struct comes after every struct it points to.</param>
/// <param name="Functions">The exported functions.</param>
internal sealed record AbiModel(string Version,
                                IReadOnlyList<AbiEnum> Enums,
                                IReadOnlyList<AbiStruct> Structs,
                                IReadOnlyList<AbiFunction> Functions)
{
    /// <summary>
    /// Gets the structs that hold parsed data, which get a view and an owned model in C++.
    /// </summary>
    public IEnumerable<AbiStruct> DataStructs => Structs.Where(s => !s.IsHandle);

    /// <summary>
    /// Finds a struct by name.
    /// </summary>
    /// <param name="name">The name of the struct.</param>
    /// <returns>The struct, or <see langword="null"/> when there is none with that name.</returns>
    public AbiStruct? FindStruct(string name)
    {
        return Structs.FirstOrDefault(s => s.Name == name);
    }

    /// <summary>
    /// Finds an exported function by entry point.
    /// </summary>
    /// <param name="entryPoint">The entry point.</param>
    /// <returns>The function, or <see langword="null"/> when there is none with that entry point.</returns>
    public AbiFunction? FindFunction(string entryPoint)
    {
        return Functions.FirstOrDefault(f => f.EntryPoint == entryPoint);
    }
}

/// <summary>
/// An enum used by the native ABI.
/// </summary>
/// <param name="Name">The name of the enum.</param>
/// <param name="UnderlyingType">The integer type of the enum.</param>
/// <param name="Members">The members, in declaration order.</param>
/// <param name="Doc">The documentation of the enum.</param>
internal sealed record AbiEnum(string Name, AbiPrimitiveType UnderlyingType, IReadOnlyList<AbiEnumMember> Members, DocComment Doc);

/// <summary>
/// A member of an <see cref="AbiEnum"/>.
/// </summary>
/// <param name="Name">The name of the member.</param>
/// <param name="Value">The value, written as a C++ literal.</param>
/// <param name="Doc">The documentation of the member.</param>
internal sealed record AbiEnumMember(string Name, string Value, DocComment Doc);

/// <summary>
/// A struct of the native ABI.
/// </summary>
/// <param name="Name">The name of the struct, such as <c>UnmanagedSimaiNote</c>.</param>
/// <param name="Size">The size of the struct in bytes on 64-bit platforms.</param>
/// <param name="Fields">The instance fields, in declaration order.</param>
/// <param name="Constants">The constants declared in the struct.</param>
/// <param name="IsHandle">
/// Whether exported functions take the struct by pointer. A handle struct (the parse result) owns the data structs it
/// points to; it gets no view or owned model.
/// </param>
/// <param name="Doc">The documentation of the struct.</param>
internal sealed record AbiStruct(string Name,
                                 int Size,
                                 IReadOnlyList<AbiField> Fields,
                                 IReadOnlyList<AbiConstant> Constants,
                                 bool IsHandle,
                                 DocComment Doc)
{
    /// <summary>
    /// Gets the fields that carry data, which leaves out the fields that only hold the length of another field.
    /// </summary>
    public IEnumerable<AbiField> DataFields => Fields.Where(f => f.Kind != AbiFieldKind.Length);

    /// <summary>
    /// Finds a field by name.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <returns>The field, or <see langword="null"/> when there is none with that name.</returns>
    public AbiField? FindField(string name)
    {
        return Fields.FirstOrDefault(f => f.Name == name);
    }
}

/// <summary>
/// What a field of the native ABI holds.
/// </summary>
internal enum AbiFieldKind
{
    /// <summary>
    /// A plain value such as a number, a flag or an enum.
    /// </summary>
    Value,

    /// <summary>
    /// The length of a <see cref="String"/> or <see cref="Array"/> field.
    /// </summary>
    Length,

    /// <summary>
    /// UTF-8 text (<c>[NativeString]</c>).
    /// </summary>
    String,

    /// <summary>
    /// An array (<c>[NativeArray]</c>).
    /// </summary>
    Array,

    /// <summary>
    /// A pointer to one struct (<c>[NativePointee]</c>).
    /// </summary>
    Pointer,
}

/// <summary>
/// An instance field of an <see cref="AbiStruct"/>.
/// </summary>
/// <param name="Name">The name of the field.</param>
/// <param name="Kind">What the field holds.</param>
/// <param name="Type">The C++ type of the field.</param>
/// <param name="Offset">The offset of the field in bytes on 64-bit platforms.</param>
/// <param name="LengthName">The field that holds the length, for <see cref="AbiFieldKind.String"/> and <see cref="AbiFieldKind.Array"/> fields.</param>
/// <param name="Doc">The documentation of the field.</param>
internal sealed record AbiField(string Name, AbiFieldKind Kind, AbiType Type, int Offset, string? LengthName, DocComment Doc)
{
    /// <summary>
    /// Gets the type of the items of an <see cref="AbiFieldKind.Array"/> field, or the pointee of a
    /// <see cref="AbiFieldKind.Pointer"/> field.
    /// </summary>
    public AbiType ElementType => ((AbiPointerType)Type).Pointee;
}

/// <summary>
/// A constant declared in an <see cref="AbiStruct"/>.
/// </summary>
/// <param name="Name">The name of the constant.</param>
/// <param name="Type">The C++ type of the constant.</param>
/// <param name="Value">The value, written as a C++ literal.</param>
/// <param name="Doc">The documentation of the constant.</param>
internal sealed record AbiConstant(string Name, AbiPrimitiveType Type, string Value, DocComment Doc);

/// <summary>
/// A function exported with <c>[UnmanagedCallersOnly(EntryPoint = ...)]</c>.
/// </summary>
/// <param name="EntryPoint">The exported name.</param>
/// <param name="CallingConvention">The calling convention: <c>cdecl</c> or <c>stdcall</c>.</param>
/// <param name="ReturnType">The C++ return type.</param>
/// <param name="Parameters">The parameters.</param>
/// <param name="Doc">The documentation of the function.</param>
internal sealed record AbiFunction(string EntryPoint,
                                   string CallingConvention,
                                   AbiType ReturnType,
                                   IReadOnlyList<AbiParameter> Parameters,
                                   DocComment Doc);

/// <summary>
/// A parameter of an <see cref="AbiFunction"/>.
/// </summary>
/// <param name="Name">The name of the parameter.</param>
/// <param name="Type">The C++ type of the parameter.</param>
internal sealed record AbiParameter(string Name, AbiType Type);
