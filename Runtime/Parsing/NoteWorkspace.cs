using System;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// The reusable buffers needed to read the notes of timing points. One workspace is kept per thread and rented with
/// <see cref="Rent"/>, so reading notes does not allocate buffers.
/// </summary>
internal sealed class NoteWorkspace
{
    /// <summary>
    /// The workspace of the current thread, or null while it is rented.
    /// </summary>
    [ThreadStatic]
    private static NoteWorkspace? t_cached;

    /// <summary>
    /// Gets the buffer for the notes read from the text of one timing point.
    /// </summary>
    public PooledBuffer<ParsedNote> ParsedNotes { get; } = new();

    /// <summary>
    /// Gets the buffer for the notes being built for one timing point.
    /// </summary>
    public SimaiNoteBuffer Notes { get; } = new();

    /// <summary>
    /// Gets the buffer for the slides of the star head being built.
    /// </summary>
    public SimaiNoteBuffer RelatedSlides { get; } = new();

    /// <summary>
    /// Takes the workspace of the current thread, or creates one when it is in use or does not exist yet.
    /// </summary>
    /// <returns>A workspace that the caller owns until it calls <see cref="Return"/>.</returns>
    public static NoteWorkspace Rent()
    {
        var workspace = t_cached;
        if (workspace is null)
        {
            return new NoteWorkspace();
        }
        t_cached = null;

        return workspace;
    }

    /// <summary>
    /// Gives a workspace back so the current thread can reuse it.
    /// </summary>
    /// <param name="workspace">The workspace returned by <see cref="Rent"/>.</param>
    public static void Return(NoteWorkspace workspace)
    {
        workspace.ParsedNotes.Clear();
        workspace.Notes.Clear();
        workspace.RelatedSlides.Clear();
        t_cached = workspace;
    }
}
