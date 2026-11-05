#if NET7_0_OR_GREATER
using System;
using System.Text;

namespace MajSimai.Unmanaged;

/// <summary>
/// Hands out the parts of one block of native memory in order, so that a whole parse result lives in one allocation that
/// one call frees. Every part starts at a multiple of <see cref="Alignment"/>. The block is sized beforehand with
/// <see cref="SizeOfArray{T}"/> and <see cref="SizeOfUtf8"/>, which round the same way the writer does; asking for more
/// than the block holds throws instead of writing past its end.
/// </summary>
internal unsafe ref struct NativeBlockWriter
{
    /// <summary>
    /// The alignment of every part, enough for the doubles and pointers of the native structs.
    /// </summary>
    public const int Alignment = 8;

    /// <summary>
    /// The start of the block.
    /// </summary>
    private readonly byte* _start;

    /// <summary>
    /// The size of the block in bytes.
    /// </summary>
    private readonly long _capacity;

    /// <summary>
    /// The number of bytes handed out so far.
    /// </summary>
    private long _used;

    /// <summary>
    /// Creates a writer over a block of native memory.
    /// </summary>
    /// <param name="start">The start of the block, aligned to <see cref="Alignment"/>.</param>
    /// <param name="capacity">The size of the block in bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is negative.</exception>
    public NativeBlockWriter(byte* start, long capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The capacity cannot be negative.");
        }
        _start = start;
        _capacity = capacity;
        _used = 0;
    }

    /// <summary>
    /// Gets the number of bytes handed out so far, padding included.
    /// </summary>
    public readonly long Used => _used;

    /// <summary>
    /// Gets the size an array takes in the block.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="count">The number of items, 0 or more.</param>
    /// <returns>The size in bytes, rounded up to <see cref="Alignment"/>; 0 for no items, which take no space.</returns>
    public static long SizeOfArray<T>(int count) where T : unmanaged
    {
        return count == 0 ? 0 : Align(checked((long)sizeof(T) * count));
    }

    /// <summary>
    /// Gets the size a text takes in the block as zero-terminated UTF-8.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The size in bytes, rounded up to <see cref="Alignment"/>; 0 for a null or empty text, which takes no space.</returns>
    public static long SizeOfUtf8(string? value)
    {
        return string.IsNullOrEmpty(value) ? 0 : Align(Encoding.UTF8.GetByteCount(value) + 1L);
    }

    /// <summary>
    /// Takes the space of an array from the block. The items are not cleared, so the caller writes every one.
    /// </summary>
    /// <typeparam name="T">The type of the items.</typeparam>
    /// <param name="count">The number of items, 0 or more.</param>
    /// <returns>The first item, or <see langword="null"/> for no items.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the block has no room left for the array.</exception>
    public T* Allocate<T>(int count) where T : unmanaged
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "The number of items cannot be negative.");
        }
        return count == 0 ? null : (T*)Take(SizeOfArray<T>(count));
    }

    /// <summary>
    /// Copies a text into the block as UTF-8 followed by a zero byte.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="length">Receives the length of the UTF-8 text in bytes, without the zero byte.</param>
    /// <returns>The text in the block, or <see langword="null"/> when <paramref name="value"/> is null or empty.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the block has no room left for the text.</exception>
    public byte* WriteUtf8(string? value, out int length)
    {
        length = 0;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var text = Take(Align(byteCount + 1L));
        Encoding.UTF8.GetBytes(value, new Span<byte>(text, byteCount));
        text[byteCount] = 0;
        length = byteCount;

        return text;
    }

    /// <summary>
    /// Rounds a size up to <see cref="Alignment"/>.
    /// </summary>
    /// <param name="size">The size in bytes.</param>
    /// <returns>The rounded size.</returns>
    private static long Align(long size)
    {
        return checked(size + (Alignment - 1)) & ~(long)(Alignment - 1);
    }

    /// <summary>
    /// Takes space from the block.
    /// </summary>
    /// <param name="size">The size in bytes, already aligned.</param>
    /// <returns>The start of the space.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the block has no room left.</exception>
    private byte* Take(long size)
    {
        if (size > _capacity - _used)
        {
            throw new InvalidOperationException($"The native block of {_capacity} bytes is too small: {size} more bytes are needed after {_used}.");
        }
        var part = _start + _used;
        _used += size;

        return part;
    }
}
#endif
