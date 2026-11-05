using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace MajSimai.Utils;

/// <summary>
/// A growable list whose storage is rented from <see cref="ArrayPool{T}.Shared"/>.
/// </summary>
/// <remarks>
/// The buffer is meant to be reused: <see cref="Clear"/> keeps the rented array, and <see cref="Dispose"/> gives it back
/// to the pool. An instance is not thread safe.
/// </remarks>
/// <typeparam name="T">The type of the items.</typeparam>
internal sealed class PooledBuffer<T> : IDisposable
{
    /// <summary>
    /// The smallest capacity that is rented.
    /// </summary>
    private const int DefaultCapacity = 16;

    /// <summary>
    /// The rented storage.
    /// </summary>
    private T[] _items;

    /// <summary>
    /// The number of items in use.
    /// </summary>
    private int _count;

    /// <summary>
    /// Creates an empty buffer.
    /// </summary>
    /// <param name="initialCapacity">The number of items the buffer can hold before it has to grow.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="initialCapacity"/> is negative.</exception>
    public PooledBuffer(int initialCapacity = DefaultCapacity)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }
        _items = ArrayPool<T>.Shared.Rent(Math.Max(initialCapacity, DefaultCapacity));
    }

    /// <summary>
    /// Gets the number of items in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets a reference to the item at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the item.</param>
    /// <returns>A reference to the item.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the buffer.</exception>
    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref _items[index];
        }
    }

    /// <summary>
    /// Appends an item.
    /// </summary>
    /// <param name="item">The item to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T item)
    {
        if (_count == _items.Length)
        {
            Grow(_count + 1);
        }
        _items[_count++] = item;
    }

    /// <summary>
    /// Appends every item of <paramref name="items"/>.
    /// </summary>
    /// <param name="items">The items to append.</param>
    public void AddRange(ReadOnlySpan<T> items)
    {
        if (_count + items.Length > _items.Length)
        {
            Grow(_count + items.Length);
        }
        items.CopyTo(_items.AsSpan(_count));
        _count += items.Length;
    }

    /// <summary>
    /// Gets the items as a span. The span is only valid until the buffer changes.
    /// </summary>
    /// <returns>The items of the buffer.</returns>
    public Span<T> AsSpan()
    {
        return _items.AsSpan(0, _count);
    }

    /// <summary>
    /// Gets a part of the items as a span. The span is only valid until the buffer changes.
    /// </summary>
    /// <param name="start">The index of the first item.</param>
    /// <param name="length">The number of items.</param>
    /// <returns>The requested items.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is outside the buffer.</exception>
    public Span<T> AsSpan(int start, int length)
    {
        return AsSpan().Slice(start, length);
    }

    /// <summary>
    /// Copies the items into a new array of exactly <see cref="Count"/> items.
    /// </summary>
    /// <returns>The new array, or an empty array when the buffer is empty.</returns>
    public T[] ToArray()
    {
        if (_count == 0)
        {
            return Array.Empty<T>();
        }

        return AsSpan().ToArray();
    }

    /// <summary>
    /// Removes every item but keeps the rented storage. Clearing an empty buffer costs nothing, so callers can clear
    /// before and after use.
    /// </summary>
    public void Clear()
    {
        if (_count != 0 && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        {
            Array.Clear(_items, 0, _count);
        }
        _count = 0;
    }

    /// <summary>
    /// Removes every item and returns the storage to the pool. The buffer stays usable and rents again when needed.
    /// </summary>
    public void Dispose()
    {
        Clear();
        var items = _items;
        _items = Array.Empty<T>();
        if (items.Length != 0)
        {
            ArrayPool<T>.Shared.Return(items);
        }
    }

    /// <summary>
    /// Rents a larger array and moves the items into it.
    /// </summary>
    /// <param name="minimumCapacity">The number of items the buffer must hold.</param>
    private void Grow(int minimumCapacity)
    {
        var newCapacity = Math.Max(Math.Max(_items.Length * 2, DefaultCapacity), minimumCapacity);
        var newItems = ArrayPool<T>.Shared.Rent(newCapacity);
        _items.AsSpan(0, _count).CopyTo(newItems);
        var oldItems = _items;
        _items = newItems;
        if (oldItems.Length != 0)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Array.Clear(oldItems, 0, _count);
            }
            ArrayPool<T>.Shared.Return(oldItems);
        }
    }
}
