using System;
using System.Collections.Generic;
using System.Linq;
using MajSimai.Tests.Legacy;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Converts results of the 3.x parser back to the MajSimai 2.x shape, so they can be compared with the legacy parser.
/// </summary>
/// <remarks>
/// A star head (a <see cref="SimaiNoteType.Tap"/> with related notes) and its first related slide, which must follow
/// it, become one slide that carries the head flags. Every other slide becomes a slide without a head.
/// </remarks>
internal static class LegacyProjection
{
    /// <summary>
    /// Projects a parsed file.
    /// </summary>
    /// <param name="file">The 3.x file.</param>
    /// <returns>The file in the 2.x shape.</returns>
    public static LegacyFileView Project(SimaiFile file)
    {
        return new LegacyFileView(file.Title,
                                  file.Artist,
                                  file.FinalDesigner,
                                  file.Offset,
                                  file.Hash,
                                  file.Charts.Select(Project).ToArray(),
                                  file.Commands.Select(c => new KeyValuePair<string, string>(c.Prefix, c.Value)).ToArray());
    }

    /// <summary>
    /// Projects metadata.
    /// </summary>
    /// <param name="metadata">The 3.x metadata.</param>
    /// <returns>The metadata in the 2.x shape.</returns>
    public static LegacyMetadataView Project(SimaiMetadata metadata)
    {
        var commands = new List<KeyValuePair<string, string>>();
        foreach (var command in metadata.Commands)
        {
            commands.Add(new KeyValuePair<string, string>(command.Prefix, command.Value));
        }

        return new LegacyMetadataView(metadata.Title,
                                      metadata.Artist,
                                      metadata.FinalDesigner,
                                      metadata.Offset,
                                      metadata.Hash,
                                      metadata.Designers.ToArray(),
                                      metadata.Levels.ToArray(),
                                      metadata.Fumens.ToArray(),
                                      commands.ToArray());
    }

    /// <summary>
    /// Projects a chart.
    /// </summary>
    /// <param name="chart">The 3.x chart.</param>
    /// <returns>The chart in the 2.x shape.</returns>
    public static LegacyChartView Project(SimaiChart chart)
    {
        return new LegacyChartView(chart.Level,
                                   chart.Designer,
                                   chart.Fumen,
                                   chart.IsEmpty,
                                   chart.NoteTimings.ToArray().Select(Project).ToArray(),
                                   chart.CommaTimings.ToArray().Select(Project).ToArray());
    }

    /// <summary>
    /// Projects a timing point.
    /// </summary>
    /// <param name="timing">The 3.x timing point.</param>
    /// <returns>The timing point in the 2.x shape.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a star head is not followed by its first slide.</exception>
    public static LegacyTimingView Project(SimaiTimingPoint timing)
    {
        return new LegacyTimingView(timing.Timing,
                                    timing.Bpm,
                                    timing.HSpeed,
                                    timing.SVeloc,
                                    timing.RawContent,
                                    timing.RawTextPositionX,
                                    timing.RawTextPositionY,
                                    timing.RawTextPosition,
                                    timing.SignatureNumerator,
                                    timing.SignatureDenominator,
                                    timing.IsEmpty,
                                    ProjectNotes(timing.Notes));
    }

    /// <summary>
    /// Projects the notes of a timing point.
    /// </summary>
    /// <param name="notes">The 3.x notes.</param>
    /// <returns>The notes in the 2.x shape.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a star head is not followed by its first slide.</exception>
    public static LegacyNoteView[] ProjectNotes(SimaiNote[] notes)
    {
        var result = new List<LegacyNoteView>(notes.Length);
        for (var i = 0; i < notes.Length; i++)
        {
            var note = notes[i];
            if (note.Type == SimaiNoteType.Tap && note.RelatedNotes.Length != 0)
            {
                var slide = note.RelatedNotes[0];
                if (i + 1 >= notes.Length || !ReferenceEquals(notes[i + 1], slide))
                {
                    throw new InvalidOperationException("A star head must be followed by its first slide.");
                }
                result.Add(ProjectHeadedSlide(note, slide));
                i++;
                continue;
            }
            result.Add(note.Type == SimaiNoteType.Slide ? ProjectHeadlessSlide(note) : ProjectNote(note));
        }

        return result.ToArray();
    }

    /// <summary>
    /// Merges a star head and its first slide into one 2.x slide.
    /// </summary>
    /// <param name="head">The star head.</param>
    /// <param name="slide">The first slide of the head.</param>
    /// <returns>The 2.x slide.</returns>
    private static LegacyNoteView ProjectHeadedSlide(SimaiNote head, SimaiNote slide)
    {
        return new LegacyNoteView((int)SimaiNoteType.Slide,
                                  slide.StartPosition,
                                  slide.HoldTime,
                                  head.IsBreak,
                                  head.IsEx,
                                  head.IsFakeRotate,
                                  head.IsForceStar,
                                  head.IsHanabi,
                                  slide.IsBreak,
                                  false,
                                  head.IsTapHeadSlide,
                                  head.IsMine,
                                  slide.IsMine,
                                  slide.UsingSV,
                                  slide.RawContent,
                                  slide.SlideStartTime,
                                  slide.SlideTime,
                                  slide.TouchArea);
    }

    /// <summary>
    /// Converts a slide that is not the first slide of a head into a 2.x slide without a head.
    /// </summary>
    /// <param name="slide">The slide.</param>
    /// <returns>The 2.x slide.</returns>
    private static LegacyNoteView ProjectHeadlessSlide(SimaiNote slide)
    {
        return new LegacyNoteView((int)SimaiNoteType.Slide,
                                  slide.StartPosition,
                                  slide.HoldTime,
                                  false,
                                  false,
                                  false,
                                  false,
                                  false,
                                  slide.IsBreak,
                                  true,
                                  false,
                                  false,
                                  slide.IsMine,
                                  slide.UsingSV,
                                  slide.RawContent,
                                  slide.SlideStartTime,
                                  slide.SlideTime,
                                  slide.TouchArea);
    }

    /// <summary>
    /// Converts a note that is not a slide.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <returns>The 2.x note.</returns>
    private static LegacyNoteView ProjectNote(SimaiNote note)
    {
        return new LegacyNoteView((int)note.Type,
                                  note.StartPosition,
                                  note.HoldTime,
                                  note.IsBreak,
                                  note.IsEx,
                                  note.IsFakeRotate,
                                  note.IsForceStar,
                                  note.IsHanabi,
                                  false,
                                  false,
                                  note.IsTapHeadSlide,
                                  note.IsMine,
                                  false,
                                  note.UsingSV,
                                  note.RawContent,
                                  note.SlideStartTime,
                                  note.SlideTime,
                                  note.TouchArea);
    }
}
