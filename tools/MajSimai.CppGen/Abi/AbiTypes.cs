namespace MajSimai.CppGen.Abi;

/// <summary>
/// A type of the native ABI.
/// </summary>
internal abstract record AbiType
{
    /// <summary>
    /// Gets how the type is written in C++.
    /// </summary>
    public abstract string CppName { get; }
}

/// <summary>
/// A built-in C++ type such as <c>int32_t</c> or <c>bool</c>.
/// </summary>
/// <param name="Name">How the type is written in C++.</param>
/// <param name="Size">The size of the type in bytes; 0 for <c>void</c>.</param>
internal sealed record AbiPrimitiveType(string Name, int Size) : AbiType
{
    /// <summary>
    /// Gets the C++ <c>void</c> type.
    /// </summary>
    public static AbiPrimitiveType Void { get; } = new("void", 0);

    /// <summary>
    /// Gets the C++ <c>bool</c> type, which matches a C# <see cref="bool"/> when runtime marshalling is disabled.
    /// </summary>
    public static AbiPrimitiveType Bool { get; } = new("bool", 1);

    /// <summary>
    /// Gets the C++ <c>char16_t</c> type, which matches a C# <see cref="char"/>.
    /// </summary>
    public static AbiPrimitiveType Char16 { get; } = new("char16_t", 2);

    /// <summary>
    /// Gets the C++ <c>char</c> type, used for UTF-8 text.
    /// </summary>
    public static AbiPrimitiveType Utf8Char { get; } = new("char", 1);

    /// <summary>
    /// Gets the C++ <c>int32_t</c> type.
    /// </summary>
    public static AbiPrimitiveType Int32 { get; } = new("int32_t", 4);

    /// <inheritdoc/>
    public override string CppName => Name;
}

/// <summary>
/// A C++ enum generated from a C# enum.
/// </summary>
/// <param name="Enum">The enum.</param>
internal sealed record AbiEnumType(AbiEnum Enum) : AbiType
{
    /// <inheritdoc/>
    public override string CppName => Enum.Name;
}

/// <summary>
/// A C++ struct generated from a C# struct of the native ABI.
/// </summary>
/// <param name="Name">The name of the struct.</param>
internal sealed record AbiStructType(string Name) : AbiType
{
    /// <inheritdoc/>
    public override string CppName => Name;
}

/// <summary>
/// A pointer.
/// </summary>
/// <param name="Pointee">The type the pointer points to.</param>
/// <param name="IsConst">Whether the pointee is <c>const</c>.</param>
internal sealed record AbiPointerType(AbiType Pointee, bool IsConst = false) : AbiType
{
    /// <inheritdoc/>
    public override string CppName => (IsConst ? "const " : string.Empty) + Pointee.CppName + "*";
}
