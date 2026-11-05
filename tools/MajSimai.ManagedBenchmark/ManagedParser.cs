using System;
using System.IO;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// Parses a file with the C# parser the way the native entry point MajSimai_ParseWithTolerance does in lenient mode, so
/// the C# and C++ numbers measure the same parse: empty content is rejected, the bytes are read through a stream as
/// UTF-8 (or the encoding of their byte order mark), and any exception rejects the file.
/// </summary>
internal static class ManagedParser
{
    /// <summary>
    /// Parses a file and counts the notes of its charts.
    /// </summary>
    /// <param name="file">The bytes of the file.</param>
    /// <param name="notes">Receives the number of notes, or 0 when the file was rejected.</param>
    /// <returns><see langword="true"/> when the file was parsed, <see langword="false"/> when it was rejected.</returns>
    public static bool Parse(ArraySegment<byte> file, out long notes)
    {
        notes = 0;
        if (file.Array is null || file.Count == 0)
        {
            return false;
        }
        SimaiFile parsed;
        try
        {
            using var stream = new MemoryStream(file.Array, file.Offset, file.Count, writable: false);
            parsed = SimaiParser.Parse(stream, SimaiParserOptions.Lenient);
        }
        catch (Exception)
        {
            // The native entry point turns every exception into an error result; count them all as rejected files.
            return false;
        }

        foreach (var chart in parsed.Charts)
        {
            foreach (var timing in chart.NoteTimings)
            {
                notes += timing.Notes.Length;
            }
        }
        return true;
    }
}
