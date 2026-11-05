using System;
using System.Linq;
using MajSimai.CppGen;
using MajSimai.CppGen.Abi;
using MajSimai.CppGen.Emit;
using Xunit;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Tests the C++ naming rules, the writer and the emitters.
/// </summary>
public sealed class CppEmitterTests
{
    /// <summary>
    /// The ABI of MajSimai, read once.
    /// </summary>
    private static readonly Lazy<AbiModel> s_majSimai = new(CppBindingGenerator.ReadMajSimai);

    /// <summary>
    /// Getter: uses Is for flags and Get for other fields.
    /// </summary>
    /// <param name="name">The field name.</param>
    /// <param name="isBool">Whether the field is a bool.</param>
    /// <param name="expected">The expected getter name.</param>
    [Theory]
    [InlineData("isBreak", true, "IsBreak")]
    [InlineData("usingSV", true, "IsUsingSV")]
    [InlineData("island", true, "IsIsland")]
    [InlineData("holdTime", false, "GetHoldTime")]
    [InlineData("isEmptyCount", false, "GetIsEmptyCount")]
    public void Getter_UsesIsForFlagsAndGetForOtherFields(string name, bool isBool, string expected)
    {
        var field = new AbiField(name, AbiFieldKind.Value, isBool ? AbiPrimitiveType.Bool : AbiPrimitiveType.Int32, 0, null, DocComment.Empty);

        Assert.Equal(expected, CppNames.Getter(field));
    }

    /// <summary>
    /// Names: views and models drop the Unmanaged prefix, and codes become Pascal case.
    /// </summary>
    [Fact]
    public void Names_ViewsAndModelsDropTheUnmanagedPrefix()
    {
        var note = s_majSimai.Value.FindStruct("UnmanagedSimaiNote")!;

        Assert.Equal("SimaiNote", CppNames.Model(note));
        Assert.Equal("SimaiNoteView", CppNames.View(note));
        Assert.Equal("Core::UnmanagedSimaiNote", CppNames.Core(note.Name));
        Assert.Equal("InvalidSimaiMarkup", CppNames.EnumMember("CODE_INVALID_SIMAI_MARKUP", "CODE_"));
    }

    /// <summary>
    /// Doc: wraps long text at 120 columns and writes nothing for empty text.
    /// </summary>
    [Fact]
    public void Doc_WrapsLongTextAndWritesNothingForEmptyText()
    {
        var writer = new CppWriter();
        writer.Doc(string.Empty);
        writer.Doc(string.Join(" ", Enumerable.Repeat("word", 40)));

        var lines = writer.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.All(lines, l => Assert.StartsWith("/// word", l));
        Assert.All(lines, l => Assert.True(l.Length <= 120));
    }

    /// <summary>
    /// Core header: declares the exports and checks every offset.
    /// </summary>
    [Fact]
    public void CoreHeader_DeclaresTheExportsAndChecksEveryOffset()
    {
        var model = s_majSimai.Value;

        var header = CoreHeaderEmitter.Emit(model);

        Assert.Contains("MAJSIMAI_API void MAJSIMAI_CDECL MajSimai_Parse(const char* content, int32_t contentLength, UnmanagedSimaiParseResult* result);", header);
        Assert.Contains("MAJSIMAI_API void MAJSIMAI_CDECL MajSimai_ParseWithTolerance(const char* content, int32_t contentLength, SimaiSyntaxTolerance tolerance, UnmanagedSimaiParseResult* result);", header);
        Assert.Contains("MAJSIMAI_API int32_t MAJSIMAI_CDECL MajSimai_Free(UnmanagedSimaiParseResult* ptr);", header);
        Assert.Contains("static constexpr int32_t CODE_INVALID_SIMAI_SYNTAX = 4;", header);
        Assert.Contains("const UnmanagedSimaiFile* simaiFile;", header);
        Assert.Contains("enum class SimaiSyntaxTolerance : int32_t", header);
        Assert.DoesNotContain("bool MAJSIMAI_CDECL", header);
        Assert.Contains("#define MAJSIMAI_CDECL __cdecl", header);
        var fieldCount = model.Structs.Sum(s => s.Fields.Count);
        Assert.Equal(fieldCount, header.Split('\n').Count(l => l.Contains("static_assert(offsetof(", StringComparison.Ordinal)));
        Assert.Equal(model.Structs.Count, header.Split('\n').Count(l => l.Contains("static_assert(sizeof(Core::", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Views and models: skip length fields and use string views, array views, strings and vectors.
    /// </summary>
    [Fact]
    public void ViewsAndModels_SkipLengthFieldsAndUseStandardTypes()
    {
        var model = s_majSimai.Value;

        var views = ViewsHeaderEmitter.Emit(model);
        var models = ModelHeaderEmitter.Emit(model);

        Assert.Contains("ArrayView<SimaiNoteView, Core::UnmanagedSimaiNote> GetNotes() const noexcept", views);
        Assert.Contains("ArrayView<int32_t, int32_t> GetRelatedNotes() const noexcept", views);
        Assert.Contains("std::string_view GetTitle() const noexcept", views);
        Assert.Contains("bool IsUsingSV() const noexcept", views);
        Assert.Contains("model.notes.push_back(item.ToModel());", views);
        Assert.DoesNotContain("Len()", views);
        Assert.DoesNotContain("class SimaiParseResultView", views);
        Assert.Contains("std::vector<SimaiTimingPoint> noteTimings;", models);
        Assert.Contains("std::vector<int32_t> relatedNotes;", models);
        Assert.Contains("char16_t touchArea{};", models);
        Assert.DoesNotContain("rawContentLen", models);
    }

    /// <summary>
    /// Parser header: turns the result codes into an enum and takes the syntax tolerance.
    /// </summary>
    [Fact]
    public void ParserHeader_TurnsTheResultCodesIntoAnEnumAndTakesTheTolerance()
    {
        var header = ParserHeaderEmitter.Emit(s_majSimai.Value);

        Assert.Contains("InvalidSimaiSyntax = Core::UnmanagedSimaiParseResult::CODE_INVALID_SIMAI_SYNTAX,", header);
        Assert.Contains("InvalidArgument = Core::UnmanagedSimaiParseResult::CODE_INVALID_ARGUMENT,", header);
        Assert.Contains("static SimaiFile Parse(std::string_view content, SimaiSyntaxTolerance tolerance = SimaiSyntaxTolerance::Lenient)", header);
        Assert.Contains("Core::MajSimai_ParseWithTolerance(content.data(), static_cast<int32_t>(content.size()), tolerance, &result);", header);
    }

    /// <summary>
    /// Parser header: has a single owner that is not a view, cannot be copied, and only gives views from an lvalue.
    /// </summary>
    [Fact]
    public void ParserHeader_HasASingleOwnerThatOnlyGivesViewsFromAnLvalue()
    {
        var header = ParserHeaderEmitter.Emit(s_majSimai.Value);

        Assert.Contains("class ParsedSimaiFile\n", header);
        Assert.DoesNotContain("class ParsedSimaiFile : public", header);
        Assert.Contains("ParsedSimaiFile(const ParsedSimaiFile&) = delete;", header);
        Assert.Contains("SimaiFileView GetFile() const&\n", header);
        Assert.Contains("SimaiFileView GetFile() const&& = delete;", header);
        Assert.Contains("        explicit ParsedSimaiFile(Core::UnmanagedSimaiParseResult& result) noexcept", header);
        Assert.True(header.IndexOf("ParsedSimaiFile parsed(result);", StringComparison.Ordinal) < header.IndexOf("throw parsed.CreateException();", StringComparison.Ordinal));
        var privateSection = header.IndexOf("friend class SimaiParser;", StringComparison.Ordinal);
        Assert.True(privateSection > 0 && privateSection < header.IndexOf("explicit ParsedSimaiFile(Core::UnmanagedSimaiParseResult& result)", StringComparison.Ordinal));
    }

    /// <summary>
    /// Check contract: lists every part of the ABI the facade misses.
    /// </summary>
    [Fact]
    public void CheckContract_ListsEveryPartOfTheAbiTheFacadeMisses()
    {
        var model = s_majSimai.Value;
        var result = model.FindStruct(ParserHeaderEmitter.ResultStruct)!;
        var brokenResult = result with { Fields = result.Fields.Where(f => f.Name != "errorMsg").ToArray() };
        var broken = model with
        {
            Structs = model.Structs.Select(s => s == result ? brokenResult : s).ToArray(),
            Functions = model.Functions.Where(f => f.EntryPoint != ParserHeaderEmitter.FreeFunction).ToArray(),
        };

        var error = Assert.Throws<CppGenException>(() => ParserHeaderEmitter.Emit(broken));

        Assert.Contains("MajSimai_Free(UnmanagedSimaiParseResult*)", error.Message);
        Assert.DoesNotContain("MajSimai_ParseWithTolerance", error.Message);
        Assert.Contains("UnmanagedSimaiParseResult::errorMsg (String)", error.Message);
        Assert.DoesNotContain("errorContent", error.Message);
    }
}
