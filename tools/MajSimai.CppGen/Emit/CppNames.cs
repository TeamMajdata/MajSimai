using System;
using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// The naming rules of the generated C++ wrapper, which follow MajSimaiCpp.
/// </summary>
internal static class CppNames
{
    /// <summary>
    /// The namespace of the wrapper.
    /// </summary>
    public const string Namespace = "MajSimai";

    /// <summary>
    /// The namespace, inside <see cref="Namespace"/>, of the native structs and functions.
    /// </summary>
    public const string CoreNamespace = "Core";

    /// <summary>
    /// Gets the name of the owned model of a data struct: <c>UnmanagedSimaiNote</c> becomes <c>SimaiNote</c>.
    /// </summary>
    /// <param name="abiStruct">The data struct.</param>
    /// <returns>The model name.</returns>
    public static string Model(AbiStruct abiStruct)
    {
        return abiStruct.Name.Substring(AbiReader.DataStructPrefix.Length);
    }

    /// <summary>
    /// Gets the name of the view of a data struct: <c>UnmanagedSimaiNote</c> becomes <c>SimaiNoteView</c>.
    /// </summary>
    /// <param name="abiStruct">The data struct.</param>
    /// <returns>The view name.</returns>
    public static string View(AbiStruct abiStruct)
    {
        return Model(abiStruct) + "View";
    }

    /// <summary>
    /// Gets the qualified name of a native struct as written outside the core namespace.
    /// </summary>
    /// <param name="name">The name of the struct.</param>
    /// <returns>The name, such as <c>Core::UnmanagedSimaiNote</c>.</returns>
    public static string Core(string name)
    {
        return CoreNamespace + "::" + name;
    }

    /// <summary>
    /// Gets the name of the view getter of a field: <c>IsBreak</c> for the flag <c>isBreak</c>, <c>IsUsingSV</c> for
    /// the flag <c>usingSV</c>, and <c>GetHoldTime</c> for <c>holdTime</c>.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The getter name.</returns>
    public static string Getter(AbiField field)
    {
        var name = Pascal(field.Name);
        if (field.Type != AbiPrimitiveType.Bool)
        {
            return "Get" + name;
        }
        var hasIsPrefix = field.Name.Length > 2 && field.Name.StartsWith("is", StringComparison.Ordinal) && char.IsUpper(field.Name[2]);

        return hasIsPrefix ? name : "Is" + name;
    }

    /// <summary>
    /// Turns a constant name such as <c>CODE_INVALID_CONTENT</c> into an enum member name such as
    /// <c>InvalidContent</c>, after removing <paramref name="prefix"/>.
    /// </summary>
    /// <param name="constantName">The constant name.</param>
    /// <param name="prefix">The prefix to remove, such as <c>CODE_</c>.</param>
    /// <returns>The member name.</returns>
    public static string EnumMember(string constantName, string prefix)
    {
        var words = constantName.Substring(prefix.Length).Split('_', StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(words.Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }

    /// <summary>
    /// Makes the first letter of a name upper case.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The name in Pascal case.</returns>
    public static string Pascal(string name)
    {
        return name.Length == 0 ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
