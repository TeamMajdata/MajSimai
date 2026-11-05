using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace MajSimai.Utils;

/// <summary>
/// The MD5 and SHA-256 hashes of some content, as lowercase hex strings.
/// </summary>
internal sealed class ContentHashes
{
    /// <summary>
    /// The number of bytes of an MD5 hash.
    /// </summary>
    private const int Md5Length = 16;

    /// <summary>
    /// The number of bytes of a SHA-256 hash.
    /// </summary>
    private const int Sha256Length = 32;

    /// <summary>
    /// The value used when there is no content to hash: every hash is an empty string.
    /// </summary>
    public static readonly ContentHashes None = new(string.Empty, string.Empty, string.Empty);

    /// <summary>
    /// Creates the hashes.
    /// </summary>
    /// <param name="md5">The lowercase hex MD5 hash.</param>
    /// <param name="md5Base64">The Base64 MD5 hash.</param>
    /// <param name="sha256">The lowercase hex SHA-256 hash.</param>
    private ContentHashes(string md5, string md5Base64, string sha256)
    {
        Md5 = md5;
        Md5Base64 = md5Base64;
        Sha256 = sha256;
    }

    /// <summary>
    /// Gets the MD5 hash as a lowercase hex string of 32 characters.
    /// </summary>
    public string Md5 { get; }

    /// <summary>
    /// Gets the same MD5 hash as a Base64 string, the form <see cref="SimaiFile.Hash"/> has always used.
    /// </summary>
    public string Md5Base64 { get; }

    /// <summary>
    /// Gets the SHA-256 hash as a lowercase hex string of 64 characters.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Hashes bytes.
    /// </summary>
    /// <param name="bytes">The bytes to hash.</param>
    /// <returns>The hashes of <paramref name="bytes"/>.</returns>
    public static ContentHashes FromBytes(ReadOnlySpan<byte> bytes)
    {
        Span<byte> md5 = stackalloc byte[Md5Length];
        Span<byte> sha256 = stackalloc byte[Sha256Length];
        using (var algorithm = MD5.Create())
        {
            algorithm.TryComputeHash(bytes, md5, out _);
        }
        using (var algorithm = SHA256.Create())
        {
            algorithm.TryComputeHash(bytes, sha256, out _);
        }

        return new ContentHashes(ToHex(md5), Convert.ToBase64String(md5), ToHex(sha256));
    }

    /// <summary>
    /// Hashes the UTF-8 bytes of a text.
    /// </summary>
    /// <param name="text">The text to hash.</param>
    /// <returns>The hashes of the UTF-8 bytes of <paramref name="text"/>.</returns>
    public static ContentHashes FromText(ReadOnlySpan<char> text)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(Math.Max(Encoding.UTF8.GetMaxByteCount(text.Length), 1));
        try
        {
            var length = Encoding.UTF8.GetBytes(text, bytes);

            return FromBytes(bytes.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    /// <summary>
    /// Hashes a chart text as one line: every white-space character (<see cref="char.IsWhiteSpace(char)"/>), line breaks
    /// included, is removed, and the UTF-8 bytes of what is left are hashed.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The hashes of the chart text without white space.</returns>
    public static ContentHashes FromFumen(ReadOnlySpan<char> fumen)
    {
        var chars = ArrayPool<char>.Shared.Rent(Math.Max(fumen.Length, 1));
        try
        {
            var length = 0;
            foreach (var c in fumen)
            {
                if (!char.IsWhiteSpace(c))
                {
                    chars[length++] = c;
                }
            }

            return FromText(chars.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }

    /// <summary>
    /// Writes bytes as a lowercase hex string.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>Two lowercase hex digits per byte.</returns>
    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        const string digits = "0123456789abcdef";
        Span<char> chars = stackalloc char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = digits[bytes[i] >> 4];
            chars[(i * 2) + 1] = digits[bytes[i] & 0xF];
        }

        return chars.ToString();
    }
}
