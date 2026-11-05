using System;
using Xunit;

namespace MajSimai.Tests.Model;

/// <summary>
/// Tests the properties of <see cref="SimaiNote"/>.
/// </summary>
public sealed class SimaiNoteTests
{
    /// <summary>
    /// New note: has defaults.
    /// </summary>
    [Fact]
    public void NewNote_HasDefaults()
    {
        var note = new SimaiNote();

        Assert.Equal(SimaiNoteType.Tap, note.Type);
        Assert.Equal(1, note.StartPosition);
        Assert.Equal(' ', note.TouchArea);
        Assert.Equal(string.Empty, note.RawContent);
        Assert.Empty(note.RelatedNotes);
        Assert.False(note.IsBreak || note.IsEx || note.IsFakeRotate || note.IsForceStar || note.IsHanabi ||
                     note.IsTapHeadSlide || note.IsMine || note.UsingSV || note.IsEach || note.IsStar || note.IsStarDouble);
    }

    /// <summary>
    /// Flags: are independent.
    /// </summary>
    [Fact]
    public void Flags_AreIndependent()
    {
        var setters = new Action<SimaiNote, bool>[]
        {
            (n, v) => n.IsBreak = v, (n, v) => n.IsEx = v, (n, v) => n.IsFakeRotate = v, (n, v) => n.IsForceStar = v,
            (n, v) => n.IsHanabi = v, (n, v) => n.IsTapHeadSlide = v, (n, v) => n.IsMine = v, (n, v) => n.UsingSV = v,
            (n, v) => n.IsEach = v, (n, v) => n.IsStar = v, (n, v) => n.IsStarDouble = v,
        };
        var getters = new Func<SimaiNote, bool>[]
        {
            n => n.IsBreak, n => n.IsEx, n => n.IsFakeRotate, n => n.IsForceStar, n => n.IsHanabi, n => n.IsTapHeadSlide,
            n => n.IsMine, n => n.UsingSV, n => n.IsEach, n => n.IsStar, n => n.IsStarDouble,
        };
        for (var i = 0; i < setters.Length; i++)
        {
            var note = new SimaiNote();
            setters[i](note, true);
            for (var j = 0; j < getters.Length; j++)
            {
                Assert.Equal(i == j, getters[j](note));
            }
            setters[i](note, false);
            Assert.False(getters[i](note));
        }
    }

    /// <summary>
    /// Values: round trip.
    /// </summary>
    [Fact]
    public void Values_RoundTrip()
    {
        var related = new SimaiNote();
        var note = new SimaiNote
        {
            Type = SimaiNoteType.TouchHold,
            StartPosition = 5,
            TouchArea = 'E',
            HoldTime = 1.5,
            SlideStartTime = 2.5,
            SlideTime = 3.5,
            RawContent = "E5h",
            RelatedNotes = new[] { related },
        };

        Assert.Equal(SimaiNoteType.TouchHold, note.Type);
        Assert.Equal(5, note.StartPosition);
        Assert.Equal('E', note.TouchArea);
        Assert.Equal(1.5, note.HoldTime);
        Assert.Equal(2.5, note.SlideStartTime);
        Assert.Equal(3.5, note.SlideTime);
        Assert.Equal("E5h", note.RawContent);
        Assert.Same(related, Assert.Single(note.RelatedNotes));
    }

    /// <summary>
    /// Null values: are rejected.
    /// </summary>
    [Fact]
    public void NullValues_AreRejected()
    {
        var note = new SimaiNote();

        Assert.Throws<ArgumentNullException>(() => note.RawContent = null!);
        Assert.Throws<ArgumentNullException>(() => note.RelatedNotes = null!);
    }
}
