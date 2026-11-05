using System;
using System.Linq;
using System.Reflection;
using MajSimai.CppGen;
using MajSimai.CppGen.Abi;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Tests reading the native ABI with reflection.
/// </summary>
public sealed unsafe class AbiReaderTests
{
    /// <summary>
    /// The ABI of MajSimai, read once.
    /// </summary>
    private static readonly Lazy<AbiModel> s_majSimai = new(CppBindingGenerator.ReadMajSimai);

    /// <summary>
    /// MajSimai: exports the parse and free functions with C types.
    /// </summary>
    [Fact]
    public void MajSimai_ExportsTheParseAndFreeFunctionsWithCTypes()
    {
        var model = s_majSimai.Value;

        Assert.Equal(new[] { "MajSimai_Parse", "MajSimai_Free", "MajSimai_FreeHGlobal" }, model.Functions.Select(f => f.EntryPoint));
        Assert.All(model.Functions, f => Assert.Equal("cdecl", f.CallingConvention));
        var parse = model.FindFunction("MajSimai_Parse")!;
        Assert.Equal(new[] { "const char*", "int32_t", "UnmanagedSimaiParseResult*" }, parse.Parameters.Select(p => p.Type.CppName));
        Assert.Equal(new[] { "content", "contentLength", "result" }, parse.Parameters.Select(p => p.Name));
        Assert.Equal("void", parse.ReturnType.CppName);
        Assert.StartsWith("Parses a simai file in lenient mode.", parse.Doc.Summary);
        Assert.Equal("The length of the file in bytes.", parse.Doc.GetParameter("contentLength"));
        Assert.Equal("bool", model.FindFunction("MajSimai_Free")!.ReturnType.CppName);
        Assert.Equal("intptr_t", model.FindFunction("MajSimai_FreeHGlobal")!.Parameters[0].Type.CppName);
    }

    /// <summary>
    /// MajSimai: lists structs after the structs they point to, and only the parse result is a handle.
    /// </summary>
    [Fact]
    public void MajSimai_ListsStructsAfterTheStructsTheyPointTo()
    {
        var model = s_majSimai.Value;

        Assert.Equal(new[] { "UnmanagedSimaiNote", "UnmanagedSimaiTimingPoint", "UnmanagedSimaiChart", "UnmanagedSimaiCommand", "UnmanagedSimaiFile", "UnmanagedSimaiParseResult" },
                     model.Structs.Select(s => s.Name));
        Assert.Equal(new[] { "UnmanagedSimaiParseResult" }, model.Structs.Where(s => s.IsHandle).Select(s => s.Name));
        Assert.Equal(new[] { "SimaiNoteType" }, model.Enums.Select(e => e.Name));
        Assert.Equal(new[] { "Tap", "Slide", "Hold", "Touch", "TouchHold" }, model.Enums[0].Members.Select(m => m.Name));
        Assert.Equal(new[] { "0", "1", "2", "3", "4" }, model.Enums[0].Members.Select(m => m.Value));
        Assert.Equal("int32_t", model.Enums[0].UnderlyingType.CppName);
    }

    /// <summary>
    /// MajSimai: note fields have the kinds given by their attributes.
    /// </summary>
    [Fact]
    public void MajSimai_NoteFieldsHaveTheKindsGivenByTheirAttributes()
    {
        var note = s_majSimai.Value.FindStruct("UnmanagedSimaiNote")!;

        Assert.Equal(AbiFieldKind.String, note.FindField("rawContent")!.Kind);
        Assert.Equal("rawContentLen", note.FindField("rawContent")!.LengthName);
        Assert.Equal(AbiFieldKind.Length, note.FindField("rawContentLen")!.Kind);
        Assert.Equal(AbiFieldKind.Array, note.FindField("relatedNotes")!.Kind);
        Assert.Equal("int32_t", note.FindField("relatedNotes")!.ElementType.CppName);
        Assert.Equal("char16_t", note.FindField("touchArea")!.Type.CppName);
        Assert.Equal("SimaiNoteType", note.FindField("type")!.Type.CppName);
        Assert.Equal("char*", note.FindField("rawContent")!.Type.CppName);
        Assert.DoesNotContain(note.DataFields, f => f.Name.EndsWith("Len", StringComparison.Ordinal));
        Assert.Equal("The touch area letter, or a space.", note.FindField("touchArea")!.Doc.Summary);
    }

    /// <summary>
    /// MajSimai: offsets and sizes match the struct layout seen from C#.
    /// </summary>
    [Fact]
    public void MajSimai_OffsetsAndSizesMatchTheStructLayoutSeenFromCSharp()
    {
        var model = s_majSimai.Value;
        var note = default(UnmanagedSimaiNote);
        var result = new UnmanagedSimaiParseResult { code = 0 };

        Assert.Equal(sizeof(UnmanagedSimaiNote), model.FindStruct("UnmanagedSimaiNote")!.Size);
        Assert.Equal(sizeof(UnmanagedSimaiTimingPoint), model.FindStruct("UnmanagedSimaiTimingPoint")!.Size);
        Assert.Equal(sizeof(UnmanagedSimaiChart), model.FindStruct("UnmanagedSimaiChart")!.Size);
        Assert.Equal(sizeof(UnmanagedSimaiCommand), model.FindStruct("UnmanagedSimaiCommand")!.Size);
        Assert.Equal(sizeof(UnmanagedSimaiFile), model.FindStruct("UnmanagedSimaiFile")!.Size);
        Assert.Equal(sizeof(UnmanagedSimaiParseResult), model.FindStruct("UnmanagedSimaiParseResult")!.Size);
        Assert.Equal((int)((byte*)&note.touchArea - (byte*)&note), model.FindStruct("UnmanagedSimaiNote")!.FindField("touchArea")!.Offset);
        Assert.Equal((int)((byte*)&note.isStarDouble - (byte*)&note), model.FindStruct("UnmanagedSimaiNote")!.FindField("isStarDouble")!.Offset);
        Assert.Equal((int)((byte*)&note.relatedNotes - (byte*)&note), model.FindStruct("UnmanagedSimaiNote")!.FindField("relatedNotes")!.Offset);
        Assert.Equal((int)((byte*)&result.simaiFile - (byte*)&result), model.FindStruct("UnmanagedSimaiParseResult")!.FindField("simaiFile")!.Offset);
    }

    /// <summary>
    /// MajSimai: the parse result points to the file and declares its codes.
    /// </summary>
    [Fact]
    public void MajSimai_TheParseResultPointsToTheFileAndDeclaresItsCodes()
    {
        var result = s_majSimai.Value.FindStruct("UnmanagedSimaiParseResult")!;
        var file = result.FindField("simaiFile")!;

        Assert.Equal(AbiFieldKind.Pointer, file.Kind);
        Assert.Equal("UnmanagedSimaiFile*", file.Type.CppName);
        Assert.Equal(new[] { "CODE_NO_ERROR", "CODE_INVALID_CONTENT", "CODE_INTERNAL_ERROR", "CODE_INVALID_SIMAI_MARKUP", "CODE_INVALID_SIMAI_SYNTAX" },
                     result.Constants.Select(c => c.Name));
        Assert.Equal("4", result.Constants[^1].Value);
    }

    /// <summary>
    /// Valid fixture: reads enums, characters, constants, arrays and pointees.
    /// </summary>
    [Fact]
    public void ValidFixture_ReadsEnumsCharactersConstantsArraysAndPointees()
    {
        var model = Read(nameof(AbiFixtures.Valid));
        var handle = model.FindStruct("ValidHandle")!;

        Assert.True(handle.IsHandle);
        Assert.False(model.FindStruct("UnmanagedItem")!.IsHandle);
        Assert.Equal("5000000000ll", handle.Constants.Single().Value);
        Assert.Equal("int64_t", model.Enums.Single().UnderlyingType.CppName);
        Assert.Equal(new[] { "1ll", "5000000000ll" }, model.Enums.Single().Members.Select(m => m.Value));
        Assert.Equal(new[] { AbiFieldKind.Value, AbiFieldKind.Value, AbiFieldKind.Length, AbiFieldKind.Array, AbiFieldKind.Pointer },
                     handle.Fields.Select(f => f.Kind));
        Assert.Equal("UnmanagedItem*", handle.FindField("first")!.Type.CppName);
        Assert.Equal(new[] { 0, 8, 12, 16, 24 }, handle.Fields.Select(f => f.Offset));
        Assert.Equal(32, handle.Size);
    }

    /// <summary>
    /// Broken fixtures: fail with a message that names the problem.
    /// </summary>
    /// <param name="export">The name of the fixture function.</param>
    /// <param name="expected">A part of the expected message.</param>
    [Theory]
    [InlineData(nameof(AbiFixtures.Unannotated), "UnmanagedUnannotated.values is a pointer; mark it with [NativeString], [NativeArray] or [NativePointee]")]
    [InlineData(nameof(AbiFixtures.WrongStringType), "[NativeString] on WrongStringTypeHandle.text requires the type byte*")]
    [InlineData(nameof(AbiFixtures.MissingLength), "MissingLengthHandle.values names the length 'valuesCount', which does not exist")]
    [InlineData(nameof(AbiFixtures.WideLength), "WideLengthHandle.values names the length 'valuesLen', which must be an int")]
    [InlineData(nameof(AbiFixtures.InlineStruct), "holds the struct UnmanagedItem by value")]
    [InlineData(nameof(AbiFixtures.Packed), "PackedHandle.large is at offset 1 in .NET but would be at offset 4 in C++")]
    [InlineData(nameof(AbiFixtures.BadDataName), "The data struct PackedHandle must be named 'Unmanaged<Name>'")]
    [InlineData(nameof(AbiFixtures.DataPointee), "only handle structs")]
    [InlineData(nameof(AbiFixtures.NoCallingConvention), "must set CallConvs")]
    public void BrokenFixtures_FailWithAMessageThatNamesTheProblem(string export, string expected)
    {
        var error = Assert.Throws<CppGenException>(() => Read(export));

        Assert.Contains(expected, error.Message);
    }

    /// <summary>
    /// Find exports: finds only the functions with an entry point, in declaration order.
    /// </summary>
    [Fact]
    public void FindExports_FindsOnlyTheFunctionsWithAnEntryPoint()
    {
        var exports = AbiReader.FindExports(typeof(UnmanagedSimaiParser).Assembly);

        Assert.Equal(new[] { "Parse", "Free", "Free" }, exports.Select(m => m.Name));
    }

    /// <summary>
    /// Reads the ABI of one fixture function.
    /// </summary>
    /// <param name="export">The name of the function in <see cref="AbiFixtures"/>.</param>
    /// <returns>The ABI.</returns>
    private static AbiModel Read(string export)
    {
        var method = typeof(AbiFixtures).GetMethod(export, BindingFlags.Public | BindingFlags.Static)!;

        return new AbiReader(XmlDocumentation.Parse("<doc/>")).Read(new[] { method }, "1.2.3");
    }
}
