using System;

namespace MajSimai.Tests;

/// <summary>
/// Shortcuts for parsing small charts in tests.
/// </summary>
internal static class ChartHelper
{
    /// <summary>
    /// The prefix used by <see cref="Notes"/>: 120 BPM, quarter notes.
    /// </summary>
    public const string Prefix = "(120){4}";

    /// <summary>
    /// Parses one timing point and returns its notes.
    /// </summary>
    /// <param name="timingPoint">The note text of the timing point, without the ending comma.</param>
    /// <returns>The notes of the timing point.</returns>
    public static SimaiNote[] Notes(string timingPoint)
    {
        var chart = SimaiParser.ParseChart(Prefix + timingPoint + ",");
        if (chart.NoteTimings.Length != 1)
        {
            throw new InvalidOperationException($"Expected one note timing point, found {chart.NoteTimings.Length}.");
        }

        return chart.NoteTimings[0].Notes;
    }

    /// <summary>
    /// Parses a chart with <see cref="Prefix"/> in front.
    /// </summary>
    /// <param name="body">The chart text after the prefix.</param>
    /// <returns>The parsed chart.</returns>
    public static SimaiChart Chart(string body)
    {
        return SimaiParser.ParseChart(Prefix + body);
    }
}
