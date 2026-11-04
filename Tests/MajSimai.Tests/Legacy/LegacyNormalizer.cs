using System.Threading;

namespace MajSimai.Tests.Legacy;

/// <summary>
/// Removes the parts of a MajSimai 2.x result that carry no meaning, so the result can be compared with the 3.x note model.
/// </summary>
/// <remarks>
/// In 2.x every flag was parsed for every note, including flags that cannot apply to it. The 3.x model only keeps
/// flags where they mean something:
/// <list type="bullet">
/// <item>A slide without a star head has no head, so its head flags (break, EX, mine, force star, fake rotation, tap
/// head, hanabi) are dropped.</item>
/// <item><c>IsSlideNoHead</c> only describes slides; 2.x could set it on a tap produced by an empty same-head part
/// such as <c>1-3[8:1]*</c>.</item>
/// </list>
/// The counters record how often each rule changed something, so a test can report it.
/// </remarks>
internal sealed class LegacyNormalizer
{
    private long _headlessSlideHeadFlags;
    private long _nonSlideNoHeadFlags;

    /// <summary>
    /// Gets how many headless slides had head flags that were dropped.
    /// </summary>
    public long HeadlessSlideHeadFlags => Interlocked.Read(ref _headlessSlideHeadFlags);

    /// <summary>
    /// Gets how many non-slide notes had <c>IsSlideNoHead</c> set.
    /// </summary>
    public long NonSlideNoHeadFlags => Interlocked.Read(ref _nonSlideNoHeadFlags);

    /// <summary>
    /// Normalizes every note of a chart.
    /// </summary>
    /// <param name="chart">The legacy chart.</param>
    /// <returns>A chart whose notes only carry meaningful flags.</returns>
    public LegacyChartView Normalize(LegacyChartView chart)
    {
        var timings = new LegacyTimingView[chart.NoteTimings.Length];
        for (var i = 0; i < timings.Length; i++)
        {
            var timing = chart.NoteTimings[i];
            var notes = new LegacyNoteView[timing.Notes.Length];
            for (var j = 0; j < notes.Length; j++)
            {
                notes[j] = Normalize(timing.Notes[j]);
            }
            timings[i] = timing with { Notes = notes };
        }

        return chart with { NoteTimings = timings };
    }

    /// <summary>
    /// Normalizes one note.
    /// </summary>
    /// <param name="note">The legacy note.</param>
    /// <returns>The note with meaningless flags cleared.</returns>
    public LegacyNoteView Normalize(LegacyNoteView note)
    {
        if (note.Type == LegacyNoteView.SlideType)
        {
            if (!note.IsSlideNoHead)
            {
                return note;
            }
            var hasHeadFlags = note.IsBreak || note.IsEx || note.IsMine || note.IsForceStar ||
                               note.IsFakeRotate || note.IsTapHeadSlide || note.IsHanabi;
            if (!hasHeadFlags)
            {
                return note;
            }
            Interlocked.Increment(ref _headlessSlideHeadFlags);

            return note with
            {
                IsBreak = false,
                IsEx = false,
                IsMine = false,
                IsForceStar = false,
                IsFakeRotate = false,
                IsTapHeadSlide = false,
                IsHanabi = false,
            };
        }
        if (note.IsSlideNoHead)
        {
            Interlocked.Increment(ref _nonSlideNoHeadFlags);

            return note with { IsSlideNoHead = false };
        }

        return note;
    }
}
