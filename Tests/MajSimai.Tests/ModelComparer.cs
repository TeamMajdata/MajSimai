using System;
using System.Collections.Generic;

namespace MajSimai.Tests;

/// <summary>
/// Compares parsed 3.x results deeply, including <see cref="SimaiNote.RelatedNotes"/> as positions in the timing point.
/// </summary>
internal static class ModelComparer
{
    /// <summary>
    /// Finds the first difference between two charts.
    /// </summary>
    /// <param name="expected">The expected chart.</param>
    /// <param name="actual">The actual chart.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(SimaiChart expected, SimaiChart actual)
    {
        return Field("Level", expected.Level, actual.Level)
            ?? Field("Designer", expected.Designer, actual.Designer)
            ?? Field("Fumen", expected.Fumen, actual.Fumen)
            ?? Prefix("NoteTimings", FindDifference(expected.NoteTimings.ToArray(), actual.NoteTimings.ToArray()))
            ?? Prefix("CommaTimings", FindDifference(expected.CommaTimings.ToArray(), actual.CommaTimings.ToArray()));
    }

    /// <summary>
    /// Finds the first difference between two lists of timing points.
    /// </summary>
    /// <param name="expected">The expected timing points.</param>
    /// <param name="actual">The actual timing points.</param>
    /// <returns>A description of the first difference, or <see langword="null"/> when they are equal.</returns>
    public static string? FindDifference(SimaiTimingPoint[] expected, SimaiTimingPoint[] actual)
    {
        if (expected.Length != actual.Length)
        {
            return $"Length: expected {expected.Length}, actual {actual.Length}";
        }
        for (var i = 0; i < expected.Length; i++)
        {
            var difference = FindDifference(expected[i], actual[i]);
            if (difference is not null)
            {
                return $"[{i}] {difference}";
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first difference between two timing points.
    /// </summary>
    /// <param name="expected">The expected timing point.</param>
    /// <param name="actual">The actual timing point.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? FindDifference(SimaiTimingPoint expected, SimaiTimingPoint actual)
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
            ?? Field("Notes.Length", expected.Notes.Length, actual.Notes.Length);
        if (difference is not null)
        {
            return difference;
        }
        for (var i = 0; i < expected.Notes.Length; i++)
        {
            difference = FindDifference(expected.Notes[i], expected.Notes, actual.Notes[i], actual.Notes);
            if (difference is not null)
            {
                return $"Notes[{i}].{difference}";
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first difference between two notes.
    /// </summary>
    /// <param name="expected">The expected note.</param>
    /// <param name="expectedSiblings">The notes of the expected timing point.</param>
    /// <param name="actual">The actual note.</param>
    /// <param name="actualSiblings">The notes of the actual timing point.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    private static string? FindDifference(SimaiNote expected, SimaiNote[] expectedSiblings, SimaiNote actual, SimaiNote[] actualSiblings)
    {
        return Field("Type", expected.Type, actual.Type)
            ?? Field("StartPosition", expected.StartPosition, actual.StartPosition)
            ?? Field("TouchArea", expected.TouchArea, actual.TouchArea)
            ?? Bits("HoldTime", expected.HoldTime, actual.HoldTime)
            ?? Bits("SlideStartTime", expected.SlideStartTime, actual.SlideStartTime)
            ?? Bits("SlideTime", expected.SlideTime, actual.SlideTime)
            ?? Field("RawContent", expected.RawContent, actual.RawContent)
            ?? Field("IsBreak", expected.IsBreak, actual.IsBreak)
            ?? Field("IsEx", expected.IsEx, actual.IsEx)
            ?? Field("IsFakeRotate", expected.IsFakeRotate, actual.IsFakeRotate)
            ?? Field("IsForceStar", expected.IsForceStar, actual.IsForceStar)
            ?? Field("IsHanabi", expected.IsHanabi, actual.IsHanabi)
            ?? Field("IsTapHeadSlide", expected.IsTapHeadSlide, actual.IsTapHeadSlide)
            ?? Field("IsMine", expected.IsMine, actual.IsMine)
            ?? Field("UsingSV", expected.UsingSV, actual.UsingSV)
            ?? Field("IsEach", expected.IsEach, actual.IsEach)
            ?? Field("IsStar", expected.IsStar, actual.IsStar)
            ?? Field("IsStarDouble", expected.IsStarDouble, actual.IsStarDouble)
            ?? Field("RelatedNotes",
                     string.Join(",", Array.ConvertAll(expected.RelatedNotes, n => Array.IndexOf(expectedSiblings, n))),
                     string.Join(",", Array.ConvertAll(actual.RelatedNotes, n => Array.IndexOf(actualSiblings, n))));
    }

    /// <summary>
    /// Prefixes a difference with a name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="difference">The difference, or null.</param>
    /// <returns>The prefixed difference, or <see langword="null"/>.</returns>
    private static string? Prefix(string name, string? difference)
    {
        return difference is null ? null : $"{name}{difference}";
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
        return BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual) ? null : $"{name}: expected <{expected:R}>, actual <{actual:R}>";
    }
}
