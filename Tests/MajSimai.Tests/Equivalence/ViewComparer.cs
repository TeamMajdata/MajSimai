using System;
using System.Collections.Generic;
using MajSimai.Tests.Legacy;

namespace MajSimai.Tests.Equivalence;

/// <summary>
/// Compares legacy views field by field. Floating point values are compared bit by bit.
/// </summary>
internal static class ViewComparer
{
    /// <summary>
    /// Finds the first difference between two files.
    /// </summary>
    /// <param name="expected">The legacy file.</param>
    /// <param name="actual">The projected 3.x file.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(LegacyFileView expected, LegacyFileView actual)
    {
        return Field("Title", expected.Title, actual.Title)
            ?? Field("Artist", expected.Artist, actual.Artist)
            ?? Field("FinalDesigner", expected.FinalDesigner, actual.FinalDesigner)
            ?? Bits("Offset", expected.Offset, actual.Offset)
            ?? Field("Hash", expected.Hash, actual.Hash)
            ?? Commands(expected.Commands, actual.Commands)
            ?? Items("Charts", expected.Charts, actual.Charts, FindDifference);
    }

    /// <summary>
    /// Finds the first difference between two metadata values.
    /// </summary>
    /// <param name="expected">The legacy metadata.</param>
    /// <param name="actual">The projected 3.x metadata.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(LegacyMetadataView expected, LegacyMetadataView actual)
    {
        return Field("Title", expected.Title, actual.Title)
            ?? Field("Artist", expected.Artist, actual.Artist)
            ?? Field("FinalDesigner", expected.FinalDesigner, actual.FinalDesigner)
            ?? Bits("Offset", expected.Offset, actual.Offset)
            ?? Field("Hash", expected.Hash, actual.Hash)
            ?? Items("Designers", expected.Designers, actual.Designers, (e, a) => Field("Value", e, a))
            ?? Items("Levels", expected.Levels, actual.Levels, (e, a) => Field("Value", e, a))
            ?? Items("Fumens", expected.Fumens, actual.Fumens, (e, a) => Field("Value", e, a))
            ?? Commands(expected.Commands, actual.Commands);
    }

    /// <summary>
    /// Finds the first difference between two charts.
    /// </summary>
    /// <param name="expected">The legacy chart, already normalized.</param>
    /// <param name="actual">The projected 3.x chart.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(LegacyChartView expected, LegacyChartView actual)
    {
        return Field("Level", expected.Level, actual.Level)
            ?? Field("Designer", expected.Designer, actual.Designer)
            ?? Field("Fumen", expected.Fumen, actual.Fumen)
            ?? Field("IsEmpty", expected.IsEmpty, actual.IsEmpty)
            ?? Items("NoteTimings", expected.NoteTimings, actual.NoteTimings, FindDifference)
            ?? Items("CommaTimings", expected.CommaTimings, actual.CommaTimings, FindDifference);
    }

    /// <summary>
    /// Finds the first difference between two timing points.
    /// </summary>
    /// <param name="expected">The legacy timing point.</param>
    /// <param name="actual">The projected 3.x timing point.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(LegacyTimingView expected, LegacyTimingView actual)
    {
        var difference = Bits("Timing", expected.Timing, actual.Timing)
            ?? Bits("Bpm", expected.Bpm, actual.Bpm)
            ?? Bits("HSpeed", expected.HSpeed, actual.HSpeed)
            ?? Bits("SVeloc", expected.SVeloc, actual.SVeloc)
            ?? Field("RawContent", expected.RawContent, actual.RawContent)
            ?? Field("RawTextPositionX", expected.RawTextPositionX, actual.RawTextPositionX)
            ?? Field("RawTextPositionY", expected.RawTextPositionY, actual.RawTextPositionY)
            ?? Field("RawTextPosition", expected.RawTextPosition, actual.RawTextPosition)
            ?? Field("SignatureNumerator", expected.SignatureNumerator, actual.SignatureNumerator)
            ?? Field("SignatureDenominator", expected.SignatureDenominator, actual.SignatureDenominator)
            ?? Field("IsEmpty", expected.IsEmpty, actual.IsEmpty)
            ?? Items("Notes", expected.Notes, actual.Notes, FindDifference);

        return difference is null ? null : $"[\"{expected.RawContent}\" L{expected.RawTextPositionY}] {difference}";
    }

    /// <summary>
    /// Finds the first difference between two notes.
    /// </summary>
    /// <param name="expected">The legacy note, already normalized.</param>
    /// <param name="actual">The projected 3.x note.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(LegacyNoteView expected, LegacyNoteView actual)
    {
        var difference = Field("Type", expected.Type, actual.Type)
            ?? Field("StartPosition", expected.StartPosition, actual.StartPosition)
            ?? Bits("HoldTime", expected.HoldTime, actual.HoldTime)
            ?? Field("IsBreak", expected.IsBreak, actual.IsBreak)
            ?? Field("IsEx", expected.IsEx, actual.IsEx)
            ?? Field("IsFakeRotate", expected.IsFakeRotate, actual.IsFakeRotate)
            ?? Field("IsForceStar", expected.IsForceStar, actual.IsForceStar)
            ?? Field("IsHanabi", expected.IsHanabi, actual.IsHanabi)
            ?? Field("IsSlideBreak", expected.IsSlideBreak, actual.IsSlideBreak)
            ?? Field("IsSlideNoHead", expected.IsSlideNoHead, actual.IsSlideNoHead)
            ?? Field("IsTapHeadSlide", expected.IsTapHeadSlide, actual.IsTapHeadSlide)
            ?? Field("IsMine", expected.IsMine, actual.IsMine)
            ?? Field("IsMineSlide", expected.IsMineSlide, actual.IsMineSlide)
            ?? Field("UsingSV", expected.UsingSV, actual.UsingSV)
            ?? Field("RawContent", expected.RawContent, actual.RawContent)
            ?? Bits("SlideStartTime", expected.SlideStartTime, actual.SlideStartTime)
            ?? Bits("SlideTime", expected.SlideTime, actual.SlideTime)
            ?? Field("TouchArea", expected.TouchArea, actual.TouchArea);

        return difference is null ? null : $"note \"{expected.RawContent}\": {difference}";
    }

    /// <summary>
    /// Compares two lists of commands.
    /// </summary>
    /// <param name="expected">The expected commands.</param>
    /// <param name="actual">The actual commands.</param>
    /// <returns>The first difference, or <see langword="null"/>.</returns>
    private static string? Commands(KeyValuePair<string, string>[] expected, KeyValuePair<string, string>[] actual)
    {
        return Items("Commands", expected, actual, (e, a) => Field("Prefix", e.Key, a.Key) ?? Field("Value", e.Value, a.Value));
    }

    /// <summary>
    /// Compares two lists item by item.
    /// </summary>
    /// <param name="name">The name of the list.</param>
    /// <param name="expected">The expected items.</param>
    /// <param name="actual">The actual items.</param>
    /// <param name="compare">Compares two items.</param>
    /// <returns>The first difference, or <see langword="null"/>.</returns>
    private static string? Items<T>(string name, IReadOnlyList<T> expected, IReadOnlyList<T> actual, Func<T, T, string?> compare)
    {
        if (expected.Count != actual.Count)
        {
            return $"{name}.Count: expected {expected.Count}, actual {actual.Count}";
        }
        for (var i = 0; i < expected.Count; i++)
        {
            var difference = compare(expected[i], actual[i]);
            if (difference is not null)
            {
                return $"{name}[{i}].{difference}";
            }
        }

        return null;
    }

    /// <summary>
    /// Compares two values.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? Field<T>(string name, T expected, T actual)
    {
        return EqualityComparer<T>.Default.Equals(expected, actual) ? null : $"{name}: expected <{expected}>, actual <{actual}>";
    }

    /// <summary>
    /// Compares two numbers bit by bit.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? Bits(string name, double expected, double actual)
    {
        return BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual)
            ? null
            : $"{name}: expected <{expected:R}>, actual <{actual:R}>";
    }

    /// <summary>
    /// Compares two numbers bit by bit.
    /// </summary>
    /// <param name="name">The name of the value.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? Bits(string name, float expected, float actual)
    {
        return BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(actual)
            ? null
            : $"{name}: expected <{expected:R}>, actual <{actual:R}>";
    }
}
