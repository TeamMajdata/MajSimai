using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>MajSimai.Model.g.h</c>: an owned C++ struct for every data struct, with <c>std::string</c> for texts and
/// <c>std::vector</c> for arrays.
/// </summary>
internal static class ModelHeaderEmitter
{
    /// <summary>
    /// The name of the header.
    /// </summary>
    public const string FileName = "MajSimai.Model.g.h";

    /// <summary>
    /// Writes the header.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <returns>The content of the header.</returns>
    public static string Emit(AbiModel model)
    {
        var writer = new CppWriter();
        writer.Banner(model.Version);
        writer.Lines($"""
            #pragma once

            #include "{CoreHeaderEmitter.FileName}"

            #include <cstdint>
            #include <string>
            #include <vector>
            """);
        writer.Line();
        writer.Line($"namespace {CppNames.Namespace}");
        using (writer.Block())
        {
            foreach (var abiStruct in model.DataStructs)
            {
                if (abiStruct != model.DataStructs.First())
                {
                    writer.Line();
                }
                WriteStruct(writer, model, abiStruct);
            }
        }

        return writer.ToString();
    }

    /// <summary>
    /// Writes the owned model of a data struct.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    /// <param name="abiStruct">The data struct.</param>
    private static void WriteStruct(CppWriter writer, AbiModel model, AbiStruct abiStruct)
    {
        writer.Doc($"An owned copy of a {CppNames.Core(abiStruct.Name)}. {abiStruct.Doc.Summary}");
        writer.Line($"struct {CppNames.Model(abiStruct)}");
        using (writer.Block("};"))
        {
            foreach (var field in abiStruct.DataFields)
            {
                writer.Doc(field.Doc.Summary);
                writer.Line($"{GetType(model, field)} {field.Name}{(field.Kind == AbiFieldKind.Value ? "{}" : string.Empty)};");
            }
        }
    }

    /// <summary>
    /// Gets the C++ type of a field in the owned model.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <param name="field">The field.</param>
    /// <returns>The type.</returns>
    /// <exception cref="CppGenException">Thrown for a field kind that data structs cannot have.</exception>
    internal static string GetType(AbiModel model, AbiField field)
    {
        return field.Kind switch
        {
            AbiFieldKind.Value => field.Type.CppName,
            AbiFieldKind.String => "std::string",
            AbiFieldKind.Array => $"std::vector<{GetItemType(model, field)}>",
            _ => throw new CppGenException($"The field {field.Name} of kind {field.Kind} has no owned model."),
        };
    }

    /// <summary>
    /// Gets the C++ type of an item of an array field in the owned model.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <param name="field">The array field.</param>
    /// <returns>The model of the item struct, or the item type itself for numbers and enums.</returns>
    internal static string GetItemType(AbiModel model, AbiField field)
    {
        return field.ElementType is AbiStructType structType
            ? CppNames.Model(model.FindStruct(structType.Name)!)
            : field.ElementType.CppName;
    }
}
