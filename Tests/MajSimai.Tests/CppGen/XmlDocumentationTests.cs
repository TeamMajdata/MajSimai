using System.Xml.Linq;
using MajSimai.CppGen.Abi;
using MajSimai.Unmanaged;
using Xunit;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Tests reading XML documentation into plain text.
/// </summary>
public sealed class XmlDocumentationTests
{
    /// <summary>
    /// Get ID: matches the IDs the compiler writes, with pointers and overloads.
    /// </summary>
    [Fact]
    public void GetId_MatchesTheIdsTheCompilerWrites()
    {
        var parse = typeof(UnmanagedSimaiParser).GetMethod(nameof(UnmanagedSimaiParser.Parse))!;
        var freeHGlobal = typeof(UnmanagedSimaiParser).GetMethod(nameof(UnmanagedSimaiParser.Free), new[] { typeof(nint) })!;

        Assert.Equal("M:MajSimai.Unmanaged.UnmanagedSimaiParser.Parse(System.Byte*,System.Int32,MajSimai.Unmanaged.UnmanagedSimaiParseResult*)", XmlDocumentation.GetId(parse));
        Assert.Equal("M:MajSimai.Unmanaged.UnmanagedSimaiParser.Free(System.IntPtr)", XmlDocumentation.GetId(freeHGlobal));
        Assert.Equal("F:MajSimai.Unmanaged.UnmanagedSimaiNote.type", XmlDocumentation.GetId(typeof(UnmanagedSimaiNote).GetField("type")!));
        Assert.Equal("T:MajSimai.Tests.CppGen.AbiFixtures.ValidHandle", XmlDocumentation.GetId(typeof(AbiFixtures.ValidHandle)));
    }

    /// <summary>
    /// Render text: turns tags into plain text and collapses whitespace.
    /// </summary>
    [Fact]
    public void RenderText_TurnsTagsIntoPlainTextAndCollapsesWhitespace()
    {
        var element = XElement.Parse("""
            <summary>
            The <c>&amp;first</c> offset of <see cref="F:A.B.c"/> and <see cref="P:X.Y.Z"/>,
            not <see cref="T:X.Y"/> or <see cref="M:A.B.Run(System.Int32)"/>; <see langword="null"/> for <paramref name="p"/>.
            <para>Second paragraph.</para>
            </summary>
            """);

        var text = XmlDocumentation.RenderText(element, "A.B");

        Assert.Equal("The &first offset of c and Y.Z, not Y or Run; null for p. Second paragraph.", text);
    }

    /// <summary>
    /// Get: reads the summary, parameters and return value of a member.
    /// </summary>
    [Fact]
    public void Get_ReadsTheSummaryParametersAndReturnValueOfAMember()
    {
        var docs = XmlDocumentation.Parse("""
            <doc><members>
              <member name="M:MajSimai.Unmanaged.UnmanagedSimaiParser.Free(System.IntPtr)">
                <summary>Frees <see cref="F:MajSimai.Unmanaged.UnmanagedSimaiParser.Other"/>.</summary>
                <param name="ptr">The memory.</param>
                <returns><see langword="true"/> when freed.</returns>
              </member>
            </members></doc>
            """);
        var free = typeof(UnmanagedSimaiParser).GetMethod(nameof(UnmanagedSimaiParser.Free), new[] { typeof(nint) })!;

        var doc = docs.Get(free);

        Assert.Equal("Frees Other.", doc.Summary);
        Assert.Equal("The memory.", doc.GetParameter("ptr"));
        Assert.Equal(string.Empty, doc.GetParameter("missing"));
        Assert.Equal("true when freed.", doc.Returns);
        Assert.Same(DocComment.Empty, docs.Get(typeof(UnmanagedSimaiNote)));
    }
}
