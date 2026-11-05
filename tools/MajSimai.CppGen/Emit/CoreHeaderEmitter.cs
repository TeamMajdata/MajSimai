using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>MajSimai.Core.g.h</c>: the enums, the native structs exactly as MajSimai lays them out, the exported
/// functions, and compile-time checks of the layouts.
/// </summary>
internal static class CoreHeaderEmitter
{
    /// <summary>
    /// The name of the header.
    /// </summary>
    public const string FileName = "MajSimai.Core.g.h";

    /// <summary>
    /// Writes the header.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <returns>The content of the header.</returns>
    public static string Emit(AbiModel model)
    {
        var writer = new CppWriter();
        writer.Banner(model.Version);
        writer.Lines("""
            #pragma once

            #include <cstddef>
            #include <cstdint>
            #include <string_view>

            #ifndef MAJSIMAI_API
            // Put in front of every MajSimai function; define it as __declspec(dllimport) to import from MajSimai.dll.
            #define MAJSIMAI_API
            #endif
            """);
        WriteCallingConventions(writer, model);
        writer.Line();
        writer.Line($"namespace {CppNames.Namespace}");
        using (writer.Block())
        {
            foreach (var abiEnum in model.Enums)
            {
                WriteEnum(writer, abiEnum);
                writer.Line();
            }
            writer.Line($"namespace {CppNames.CoreNamespace}");
            using (writer.Block())
            {
                foreach (var abiStruct in model.Structs)
                {
                    writer.Line($"struct {abiStruct.Name};");
                }
                foreach (var abiStruct in model.Structs)
                {
                    writer.Line();
                    WriteStruct(writer, abiStruct);
                }
                writer.Line();
                writer.Line("extern \"C\"");
                using (writer.Block())
                {
                    foreach (var function in model.Functions)
                    {
                        if (function != model.Functions[0])
                        {
                            writer.Line();
                        }
                        WriteFunction(writer, function);
                    }
                }
                writer.Line();
                writer.Lines("""
                    /// Gets native UTF-8 text as a view; null text or a length that is not positive gives an empty view.
                    /// @param text The text, which is not necessarily followed by a zero byte.
                    /// @param length The length of the text in bytes.
                    /// @return The view of the text.
                    inline std::string_view MakeView(const char* text, int32_t length) noexcept
                    {
                        if (text == nullptr || length <= 0)
                        {
                            return {};
                        }

                        return std::string_view(text, static_cast<std::size_t>(length));
                    }
                    """);
            }
            writer.Line();
            WriteLayoutChecks(writer, model);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Writes the macros of the calling conventions the functions use.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    private static void WriteCallingConventions(CppWriter writer, AbiModel model)
    {
        foreach (var convention in model.Functions.Select(f => f.CallingConvention).Distinct().OrderBy(c => c))
        {
            var macro = CallingConventionMacro(convention);
            writer.Line();
            writer.Line($"#ifndef {macro}");
            writer.Line("#if defined(_WIN32)");
            writer.Line($"#define {macro} __{convention}");
            writer.Line("#else");
            writer.Line($"#define {macro}");
            writer.Line("#endif");
            writer.Line("#endif");
        }
    }

    /// <summary>
    /// Gets the macro that expands to a calling convention.
    /// </summary>
    /// <param name="convention">The calling convention: <c>cdecl</c> or <c>stdcall</c>.</param>
    /// <returns>The macro name, such as <c>MAJSIMAI_CDECL</c>.</returns>
    private static string CallingConventionMacro(string convention)
    {
        return "MAJSIMAI_" + convention.ToUpperInvariant();
    }

    /// <summary>
    /// Writes an enum.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="abiEnum">The enum.</param>
    private static void WriteEnum(CppWriter writer, AbiEnum abiEnum)
    {
        writer.Doc(abiEnum.Doc.Summary);
        writer.Line($"enum class {abiEnum.Name} : {abiEnum.UnderlyingType.CppName}");
        using (writer.Block("};"))
        {
            foreach (var member in abiEnum.Members)
            {
                writer.Doc(member.Doc.Summary);
                writer.Line($"{member.Name} = {member.Value},");
            }
        }
    }

    /// <summary>
    /// Writes a native struct with its constants and fields.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="abiStruct">The struct.</param>
    private static void WriteStruct(CppWriter writer, AbiStruct abiStruct)
    {
        writer.Doc(abiStruct.Doc.Summary);
        writer.Line($"struct {abiStruct.Name}");
        using (writer.Block("};"))
        {
            foreach (var constant in abiStruct.Constants)
            {
                writer.Doc(constant.Doc.Summary);
                writer.Line($"static constexpr {constant.Type.CppName} {constant.Name} = {constant.Value};");
            }
            if (abiStruct.Constants.Count != 0)
            {
                writer.Line();
            }
            foreach (var field in abiStruct.Fields)
            {
                writer.Doc(field.Doc.Summary);
                writer.Line($"{field.Type.CppName} {field.Name};");
            }
        }
    }

    /// <summary>
    /// Writes the declaration of an exported function.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="function">The function.</param>
    private static void WriteFunction(CppWriter writer, AbiFunction function)
    {
        writer.Doc(function.Doc, function.Parameters.Select(p => p.Name));
        var parameters = string.Join(", ", function.Parameters.Select(p => $"{p.Type.CppName} {p.Name}"));
        var macro = CallingConventionMacro(function.CallingConvention);
        writer.Line($"MAJSIMAI_API {function.ReturnType.CppName} {macro} {function.EntryPoint}({parameters});");
    }

    /// <summary>
    /// Writes static assertions that the C++ compiler lays out every struct exactly like MajSimai.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="model">The native ABI.</param>
    private static void WriteLayoutChecks(CppWriter writer, AbiModel model)
    {
        writer.Lines("""
            static_assert(sizeof(bool) == 1, "MajSimai passes bool as 1 byte.");
            static_assert(sizeof(char16_t) == 2, "MajSimai passes char as 2 bytes.");
            static_assert(sizeof(float) == 4 && sizeof(double) == 8, "MajSimai passes float as 4 bytes and double as 8 bytes.");

            // The offsets and sizes MajSimai uses on 64-bit platforms. 32-bit layouts are not checked.
            #if INTPTR_MAX == INT64_MAX
            """);
        foreach (var abiStruct in model.Structs)
        {
            var name = CppNames.Core(abiStruct.Name);
            writer.Line($"static_assert(sizeof({name}) == {abiStruct.Size}, \"{name} does not have the size MajSimai uses.\");");
            foreach (var field in abiStruct.Fields)
            {
                writer.Line($"static_assert(offsetof({name}, {field.Name}) == {field.Offset}, \"{name}::{field.Name} is not at the offset MajSimai uses.\");");
            }
        }
        writer.Line("#endif");
    }
}
