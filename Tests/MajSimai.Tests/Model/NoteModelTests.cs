using System.Linq;
using Xunit;
using static MajSimai.Tests.ChartHelper;

namespace MajSimai.Tests.Model;

/// <summary>
/// Tests the note model of issue #12: a slide with a head is a star tap plus slide paths, with each, star and double
/// star flags.
/// </summary>
public sealed class NoteModelTests
{
    /// <summary>
    /// Headed slide: is star tap followed by slide.
    /// </summary>
    [Fact]
    public void HeadedSlide_IsStarTapFollowedBySlide()
    {
        var notes = Notes("1-5[8:1]");

        Assert.Equal(2, notes.Length);
        var head = notes[0];
        var slide = notes[1];
        Assert.Equal(SimaiNoteType.Tap, head.Type);
        Assert.Equal(1, head.StartPosition);
        Assert.Equal("1", head.RawContent);
        Assert.True(head.IsStar);
        Assert.False(head.IsStarDouble);
        Assert.Same(slide, Assert.Single(head.RelatedNotes));
        Assert.Equal(SimaiNoteType.Slide, slide.Type);
        Assert.Equal(1, slide.StartPosition);
        Assert.Equal("1-5[8:1]", slide.RawContent);
        Assert.Empty(slide.RelatedNotes);
        Assert.Equal(0.5 + 0.25, slide.SlideStartTime + slide.SlideTime, 12);
    }

    /// <summary>
    /// Headed slide: splits head and path flags.
    /// </summary>
    [Fact]
    public void HeadedSlide_SplitsHeadAndPathFlags()
    {
        var notes = Notes("1bx$-5b[8:1]");
        var head = notes[0];
        var slide = notes[1];

        Assert.True(head.IsBreak);
        Assert.True(head.IsEx);
        Assert.True(head.IsForceStar);
        Assert.True(slide.IsBreak);
        Assert.False(slide.IsEx);
        Assert.False(slide.IsForceStar);
    }

    /// <summary>
    /// Mine slide: marks only the path.
    /// </summary>
    [Fact]
    public void MineSlide_MarksOnlyThePath()
    {
        var notes = Notes("1-5[8:1]m");

        Assert.False(notes[0].IsMine);
        Assert.True(notes[1].IsMine);
    }

    /// <summary>
    /// Headless slide: is only a slide.
    /// </summary>
    /// <param name="text">A test case value.</param>
    [Theory]
    [InlineData("1?-5[8:1]")]
    [InlineData("1!-5[8:1]")]
    public void HeadlessSlide_IsOnlyASlide(string text)
    {
        var slide = Assert.Single(Notes(text));

        Assert.Equal(SimaiNoteType.Slide, slide.Type);
        Assert.False(slide.IsEach);
    }

    /// <summary>
    /// Tap head slide: head is not a star.
    /// </summary>
    [Fact]
    public void TapHeadSlide_HeadIsNotAStar()
    {
        var head = Notes("1@-5[8:1]")[0];

        Assert.True(head.IsTapHeadSlide);
        Assert.False(head.IsStar);
        Assert.Single(head.RelatedNotes);
    }

    /// <summary>
    /// Force star: tap without slide is a star.
    /// </summary>
    [Fact]
    public void ForceStar_TapWithoutSlideIsAStar()
    {
        var tap = Assert.Single(Notes("1$"));

        Assert.True(tap.IsStar);
        Assert.False(tap.IsStarDouble);
        Assert.Empty(tap.RelatedNotes);
    }

    /// <summary>
    /// Force star: wins over tap head.
    /// </summary>
    [Fact]
    public void ForceStar_WinsOverTapHead()
    {
        Assert.True(Notes("1@$-5[8:1]")[0].IsStar);
    }

    /// <summary>
    /// Force star: on hold is not a star.
    /// </summary>
    [Fact]
    public void ForceStar_OnHoldIsNotAStar()
    {
        var hold = Assert.Single(Notes("1$h[4:1]"));

        Assert.True(hold.IsForceStar);
        Assert.False(hold.IsStar);
    }

    /// <summary>
    /// Same head group: all slides belong to the head.
    /// </summary>
    [Fact]
    public void SameHeadGroup_AllSlidesBelongToTheHead()
    {
        var notes = Notes("1-3[8:1]*-5[8:1]*-7[8:1]");

        Assert.Equal(4, notes.Length);
        var head = notes[0];
        Assert.Equal(new[] { notes[1], notes[2], notes[3] }, head.RelatedNotes);
        Assert.Equal(new[] { "1-3[8:1]", "1-5[8:1]", "1-7[8:1]" }, head.RelatedNotes.Select(n => n.RawContent));
        Assert.True(head.IsStar);
        Assert.True(head.IsStarDouble);
        Assert.All(head.RelatedNotes, slide => Assert.True(slide.IsEach));
        Assert.False(head.IsEach);
    }

    /// <summary>
    /// Same head group: with headless first part has no head.
    /// </summary>
    [Fact]
    public void SameHeadGroup_WithHeadlessFirstPart_HasNoHead()
    {
        var notes = Notes("1?-3[8:1]*-5[8:1]");

        Assert.Equal(2, notes.Length);
        Assert.All(notes, n => Assert.Equal(SimaiNoteType.Slide, n.Type));
    }

    /// <summary>
    /// Same head group: with tap first part does not relate.
    /// </summary>
    [Fact]
    public void SameHeadGroup_WithTapFirstPart_DoesNotRelate()
    {
        var notes = Notes("1*-5[8:1]");

        Assert.Equal(SimaiNoteType.Tap, notes[0].Type);
        Assert.Empty(notes[0].RelatedNotes);
        Assert.False(notes[0].IsStar);
        Assert.Equal(SimaiNoteType.Slide, notes[1].Type);
    }

    /// <summary>
    /// Same head group: non slide part does not end the group.
    /// </summary>
    [Fact]
    public void SameHeadGroup_NonSlidePartDoesNotEndTheGroup()
    {
        var notes = Notes("1-3[8:1]*h*-5[8:1]");

        Assert.Equal(new[] { SimaiNoteType.Tap, SimaiNoteType.Slide, SimaiNoteType.Hold, SimaiNoteType.Slide }, notes.Select(n => n.Type));
        Assert.Equal(new[] { notes[1], notes[3] }, notes[0].RelatedNotes);
    }

    /// <summary>
    /// Separate slides from same button: are double stars.
    /// </summary>
    [Fact]
    public void SeparateSlidesFromSameButton_AreDoubleStars()
    {
        var notes = Notes("1-3[8:1]/1-5[8:1]");

        Assert.Equal(4, notes.Length);
        Assert.True(notes[0].IsStarDouble);
        Assert.True(notes[2].IsStarDouble);
        Assert.Single(notes[0].RelatedNotes);
        Assert.Single(notes[2].RelatedNotes);
    }

    /// <summary>
    /// Mine slides: do not make double stars.
    /// </summary>
    [Fact]
    public void MineSlides_DoNotMakeDoubleStars()
    {
        var notes = Notes("1-3[8:1]*-5[8:1]m");

        Assert.True(notes[0].IsStar);
        Assert.False(notes[0].IsStarDouble);
    }

    /// <summary>
    /// Slides from other buttons: are not double stars.
    /// </summary>
    [Fact]
    public void SlidesFromOtherButtons_AreNotDoubleStars()
    {
        var notes = Notes("1-3[8:1]/2-5[8:1]");

        Assert.False(notes[0].IsStarDouble);
        Assert.False(notes[2].IsStarDouble);
        Assert.True(notes[0].IsEach);
        Assert.True(notes[1].IsEach);
    }

    /// <summary>
    /// Each: counts non slide notes and slides separately.
    /// </summary>
    [Fact]
    public void Each_CountsNonSlideNotesAndSlidesSeparately()
    {
        var notes = Notes("1-5[8:1]/2h[4:1]");

        // The star head and the hold make an each; the single slide does not.
        Assert.True(notes[0].IsEach);
        Assert.False(notes[1].IsEach);
        Assert.True(notes[2].IsEach);
    }

    /// <summary>
    /// Each: includes mines.
    /// </summary>
    [Fact]
    public void Each_IncludesMines()
    {
        var notes = Notes("1m/2");

        Assert.All(notes, n => Assert.True(n.IsEach));
    }

    /// <summary>
    /// Single note: is not each.
    /// </summary>
    [Fact]
    public void SingleNote_IsNotEach()
    {
        Assert.False(Assert.Single(Notes("B1")).IsEach);
    }

    /// <summary>
    /// Fake each: parts are separate timing points.
    /// </summary>
    [Fact]
    public void FakeEach_PartsAreSeparateTimingPoints()
    {
        var chart = Chart("1`2,");

        Assert.Equal(2, chart.NoteTimings.Length);
        Assert.All(chart.NoteTimings.ToArray(), t => Assert.False(Assert.Single(t.Notes).IsEach));
        Assert.Equal(1.875 / 120, chart.NoteTimings[1].Timing - chart.NoteTimings[0].Timing, 12);
    }

    /// <summary>
    /// Connected slide: adds durations.
    /// </summary>
    [Fact]
    public void ConnectedSlide_AddsDurations()
    {
        var slide = Notes("1-3[8:1]-5[8:1]")[1];

        Assert.Equal(0.25 + 0.25, slide.SlideTime, 12);
    }

    /// <summary>
    /// Touch notes: keep area and flags.
    /// </summary>
    [Fact]
    public void TouchNotes_KeepAreaAndFlags()
    {
        var notes = Notes("B1f/Ch[4:1]/E8bx");

        Assert.Equal(SimaiNoteType.Touch, notes[0].Type);
        Assert.Equal('B', notes[0].TouchArea);
        Assert.True(notes[0].IsHanabi);
        Assert.Equal(SimaiNoteType.TouchHold, notes[1].Type);
        Assert.Equal(8, notes[1].StartPosition);
        Assert.Equal(0.5, notes[1].HoldTime, 12);
        Assert.True(notes[2].IsBreak);
        Assert.True(notes[2].IsEx);
    }

    /// <summary>
    /// Hold: reads duration.
    /// </summary>
    /// <param name="text">A test case value.</param>
    /// <param name="expected">A test case value.</param>
    [Theory]
    [InlineData("1h[8:3]", 0.75)]
    [InlineData("1h[#1.5]", 1.5)]
    [InlineData("1h[60#4:1]", 1.0)]
    [InlineData("1h", 0)]
    public void Hold_ReadsDuration(string text, double expected)
    {
        var hold = Assert.Single(Notes(text));

        Assert.Equal(SimaiNoteType.Hold, hold.Type);
        Assert.Equal(expected, hold.HoldTime, 12);
    }

    /// <summary>
    /// Slide: reads wait and duration.
    /// </summary>
    /// <param name="text">A test case value.</param>
    /// <param name="wait">A test case value.</param>
    /// <param name="length">A test case value.</param>
    [Theory]
    [InlineData("1-5[160#8:3]", 60d / 160, 0.5625)]
    [InlineData("1-5[60#2]", 1, 2)]
    [InlineData("1-5[3##1.5]", 3, 1.5)]
    [InlineData("1-5[3##8:1]", 3, 0.25)]
    [InlineData("1-5[3##60#4:1]", 3, 1)]
    public void Slide_ReadsWaitAndDuration(string text, double wait, double length)
    {
        var slide = Notes(text)[1];

        Assert.Equal(wait, slide.SlideStartTime, 12);
        Assert.Equal(length, slide.SlideTime, 12);
    }

    /// <summary>
    /// Using sv: is off with c.
    /// </summary>
    [Fact]
    public void UsingSV_IsOffWithC()
    {
        Assert.True(Assert.Single(Notes("1")).UsingSV);
        Assert.False(Assert.Single(Notes("1c")).UsingSV);
        Assert.All(Notes("1c-5[8:1]"), n => Assert.False(n.UsingSV));
    }

    /// <summary>
    /// Fake rotate: needs two dollars.
    /// </summary>
    [Fact]
    public void FakeRotate_NeedsTwoDollars()
    {
        var tap = Assert.Single(Notes("1$$"));

        Assert.True(tap.IsForceStar);
        Assert.True(tap.IsFakeRotate);
    }
}
