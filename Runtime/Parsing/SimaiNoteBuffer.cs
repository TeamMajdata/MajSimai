using System;
using System.Runtime.CompilerServices;

namespace MajSimai.Parsing;

/// <summary>
/// A growable list of <see cref="SimaiNote"/> objects for building the notes of one timing point.
/// </summary>
/// <remarks>
/// It does the job of a <c>PooledBuffer&lt;SimaiNote&gt;</c> without generics: generic code over a reference type is
/// shared between all reference types, so every add and copy would go through type checks and lookups, while
/// <see cref="SimaiNote"/> is sealed and needs none. Notes are added for every timing point of every chart, which makes
/// the difference measurable. An instance is not thread safe.
/// </remarks>
internal sealed class SimaiNoteBuffer
{
    /// <summary>
    /// The capacity of a new buffer; a timing point rarely has more notes.
    /// </summary>
    private const int InitialCapacity = 16;

    /// <summary>
    /// The storage; the items after <see cref="_count"/> are null.
    /// </summary>
    private SimaiNote[] _items = new SimaiNote[InitialCapacity];

    /// <summary>
    /// The number of items in use.
    /// </summary>
    private int _count;

    /// <summary>
    /// Gets the number of notes in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Appends a note.
    /// </summary>
    /// <param name="note">The note.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(SimaiNote note)
    {
        if (_count == _items.Length)
        {
            Array.Resize(ref _items, _items.Length * 2);
        }
        _items[_count++] = note;
    }

    /// <summary>
    /// Copies the notes into a new array of exactly <see cref="Count"/> items.
    /// </summary>
    /// <returns>The new array, or an empty array when the buffer is empty.</returns>
    public SimaiNote[] ToArray()
    {
        if (_count == 0)
        {
            return Array.Empty<SimaiNote>();
        }
        var result = new SimaiNote[_count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = _items[i];
        }

        return result;
    }

    /// <summary>
    /// Removes every note, so the buffer does not keep them alive.
    /// </summary>
    public void Clear()
    {
        for (var i = 0; i < _count; i++)
        {
            _items[i] = null!;
        }
        _count = 0;
    }
}
