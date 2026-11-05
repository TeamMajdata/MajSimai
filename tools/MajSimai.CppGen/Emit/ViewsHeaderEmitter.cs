using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>MajSimai.Views.g.h</c>: a view class for every data struct that reads the native memory without copying
/// it, and copies it into the owned model on request.
/// </summary>
internal static class ViewsHeaderEmitter
{
    /// <summary>
    /// The name of the header.
    /// </summary>
    public const string FileName = "MajSimai.Views.g.h";

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

            #include "{ArrayViewHeaderEmitter.FileName}"
            #include "{CoreHeaderEmitter.FileName}"
            #include "{ModelHeaderEmitter.FileName}"

            #include <cstdint>
            #include <string>
            #include <string_view>
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
                WriteView(writer, model, abiStruct);
            }
        }

        return writer.ToString();
    }

    /// <summary>
    /// Writes the view class of a data struct.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    /// <param name="abiStruct">The data struct.</param>
    private static void WriteView(CppWriter writer, AbiModel model, AbiStruct abiStruct)
    {
        var view = CppNames.View(abiStruct);
        var native = CppNames.Core(abiStruct.Name);
        writer.Doc($"A view of a {native} that reads the native memory without copying it. {abiStruct.Doc.Summary} The view stays valid while the ParsedSimaiFile it came from lives.");
        writer.Line($"class {view}");
        using (writer.Block("};"))
        {
            writer.Label("public:");
            writer.Lines($$"""
                /// Creates a view of native data.
                /// @param native The native data; it must not be null when a getter is called.
                explicit {{view}}(const {{native}}* native) noexcept
                    : m_native(native)
                {
                }

                /// Gets the native data the view reads.
                /// @return The native data.
                const {{native}}* GetNative() const noexcept
                {
                    return m_native;
                }
                """);
            foreach (var field in abiStruct.DataFields)
            {
                writer.Line();
                WriteGetter(writer, model, field);
            }
            writer.Line();
            WriteToModel(writer, model, abiStruct);
            writer.Line();
            writer.Label("private:");
            writer.Line($"const {native}* m_native;");
        }
    }

    /// <summary>
    /// Writes the getter of a field.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    /// <param name="field">The field.</param>
    private static void WriteGetter(CppWriter writer, AbiModel model, AbiField field)
    {
        var (type, expression) = field.Kind switch
        {
            AbiFieldKind.Value => (field.Type.CppName, $"m_native->{field.Name}"),
            AbiFieldKind.String => ("std::string_view", $"{CppNames.CoreNamespace}::MakeView(m_native->{field.Name}, m_native->{field.LengthName})"),
            AbiFieldKind.Array => (GetArrayViewType(model, field), $"{{ m_native->{field.Name}, m_native->{field.LengthName} }}"),
            _ => throw new CppGenException($"The field {field.Name} of kind {field.Kind} has no view getter."),
        };
        writer.Doc(field.Doc.Summary);
        writer.Line($"{type} {CppNames.Getter(field)}() const noexcept");
        using (writer.Block())
        {
            writer.Line($"return {expression};");
        }
    }

    /// <summary>
    /// Writes the method that copies the view into the owned model.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    /// <param name="abiStruct">The data struct.</param>
    private static void WriteToModel(CppWriter writer, AbiModel model, AbiStruct abiStruct)
    {
        var modelName = CppNames.Model(abiStruct);
        writer.Doc($"Copies the data into an owned {modelName}.");
        writer.Doc("@return The copy.");
        writer.Line($"{modelName} ToModel() const");
        using (writer.Block())
        {
            writer.Line($"{modelName} model;");
            foreach (var field in abiStruct.DataFields)
            {
                var getter = CppNames.Getter(field);
                switch (field.Kind)
                {
                    case AbiFieldKind.String:
                        writer.Line($"model.{field.Name} = std::string({getter}());");
                        break;
                    case AbiFieldKind.Array:
                        var item = field.ElementType is AbiStructType ? "item.ToModel()" : "item";
                        writer.Line($"model.{field.Name}.reserve({getter}().Size());");
                        writer.Line($"for (const auto item : {getter}())");
                        using (writer.Block())
                        {
                            writer.Line($"model.{field.Name}.push_back({item});");
                        }
                        break;
                    default:
                        writer.Line($"model.{field.Name} = {getter}();");
                        break;
                }
            }
            writer.Line();
            writer.Line("return model;");
        }
    }

    /// <summary>
    /// Gets the <c>ArrayView</c> type of an array field.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <param name="field">The array field.</param>
    /// <returns>The type, such as <c>ArrayView&lt;SimaiNoteView, Core::UnmanagedSimaiNote&gt;</c>.</returns>
    private static string GetArrayViewType(AbiModel model, AbiField field)
    {
        if (field.ElementType is AbiStructType structType)
        {
            var element = model.FindStruct(structType.Name)!;

            return $"ArrayView<{CppNames.View(element)}, {CppNames.Core(element.Name)}>";
        }

        return $"ArrayView<{field.ElementType.CppName}, {field.ElementType.CppName}>";
    }
}
