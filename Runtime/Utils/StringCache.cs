using System;

namespace MajSimai.Utils;

/// <summary>
/// A small per-thread cache that returns the same <see cref="string"/> instance for repeated short texts.
/// </summary>
/// <remarks>
/// Charts repeat the same note texts (such as <c>1</c> or <c>1-5[8:1]</c>) many times. The cache is direct mapped: each
/// text has one slot chosen by its hash, and a new text simply replaces the old one. Strings are immutable, so sharing
/// them between results is safe. Memory stays bounded because the cache has a fixed number of slots and only keeps short
/// texts.
/// </remarks>
internal static class StringCache
{
    /// <summary>
    /// The longest text that is cached; longer texts are rarely repeated.
    /// </summary>
    internal const int MaxCachedLength = 48;

    /// <summary>
    /// The number of slots of a cache. Must be a power of two.
    /// </summary>
    internal const int SlotCount = 2048;

    /// <summary>
    /// The cache slots of the current thread.
    /// </summary>
    [ThreadStatic]
    private static string?[]? t_slots;

    /// <summary>
    /// Gets a string with the content of <paramref name="value"/>, reusing a cached instance when possible.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>A string equal to <paramref name="value"/>.</returns>
    public static string Get(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }
        if (value.Length > MaxCachedLength)
        {
            return new string(value);
        }
        var slots = t_slots ??= new string?[SlotCount];
        var index = GetHash(value) & (SlotCount - 1);
        var cached = slots[index];
        if (cached is not null && value.SequenceEqual(cached.AsSpan()))
        {
            return cached;
        }
        var created = new string(value);
        slots[index] = created;

        return created;
    }

    /// <summary>
    /// Computes the FNV-1a hash of a text.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The hash, as a non-negative number.</returns>
    private static int GetHash(ReadOnlySpan<char> value)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;
        var hash = OffsetBasis;
        foreach (var c in value)
        {
            hash = (hash ^ c) * Prime;
        }

        return (int)(hash & 0x7FFFFFFF);
    }
}
