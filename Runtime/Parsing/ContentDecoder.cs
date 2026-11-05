using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// Reads a simai file from a stream: computes the MD5 and SHA-256 hashes of its bytes and decodes them into rented
/// characters.
/// </summary>
internal static class ContentDecoder
{
    /// <summary>
    /// Reads and decodes a stream to its end.
    /// </summary>
    /// <param name="stream">The stream holding the file.</param>
    /// <param name="encoding">The encoding to use when the bytes have no byte order mark.</param>
    /// <returns>The decoded text and the hashes of the bytes. Dispose the result to return its buffer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static DecodedContent Decode(Stream stream, Encoding encoding)
    {
        ValidateArguments(stream, encoding);
        var bytes = ReadAllBytes(stream, out var length);
        try
        {
            return DecodeBytes(bytes, length, encoding);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    /// <summary>
    /// Reads and decodes a stream to its end without blocking.
    /// </summary>
    /// <param name="stream">The stream holding the file.</param>
    /// <param name="encoding">The encoding to use when the bytes have no byte order mark.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decoded text and the hashes of the bytes. Dispose the result to return its buffer.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public static async Task<DecodedContent> DecodeAsync(Stream stream, Encoding encoding, CancellationToken cancellationToken = default)
    {
        ValidateArguments(stream, encoding);
        var bytes = ArrayPool<byte>.Shared.Rent(GetInitialCapacity(stream));
        var length = 0;
        try
        {
            while (true)
            {
                if (length == bytes.Length)
                {
                    bytes = GrowBytes(bytes, length);
                }
                var read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                length += read;
            }

            return DecodeBytes(bytes, length, encoding);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bytes);
        }
    }

    /// <summary>
    /// Checks the arguments of the decode methods.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="encoding">The encoding.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    private static void ValidateArguments(Stream stream, Encoding encoding)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        if (encoding is null)
        {
            throw new ArgumentNullException(nameof(encoding));
        }
    }

    /// <summary>
    /// Reads a stream to its end into a rented array.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="length">Receives the number of bytes read.</param>
    /// <returns>The rented array; the caller returns it to the pool.</returns>
    private static byte[] ReadAllBytes(Stream stream, out int length)
    {
        var bytes = ArrayPool<byte>.Shared.Rent(GetInitialCapacity(stream));
        length = 0;
        try
        {
            while (true)
            {
                if (length == bytes.Length)
                {
                    bytes = GrowBytes(bytes, length);
                }
                var read = stream.Read(bytes, length, bytes.Length - length);
                if (read == 0)
                {
                    return bytes;
                }
                length += read;
            }
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(bytes);
            throw;
        }
    }

    /// <summary>
    /// Guesses the number of bytes a stream holds.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The remaining length of a seekable stream plus one, or 4096.</returns>
    private static int GetInitialCapacity(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return 4096;
        }
        var remaining = stream.Length - stream.Position;

        return (int)Math.Clamp(remaining + 1, 1, int.MaxValue / 2);
    }

    /// <summary>
    /// Rents a larger array, copies the bytes read so far and returns the old array.
    /// </summary>
    /// <param name="bytes">The full array.</param>
    /// <param name="length">The number of bytes read.</param>
    /// <returns>The larger array.</returns>
    private static byte[] GrowBytes(byte[] bytes, int length)
    {
        var grown = ArrayPool<byte>.Shared.Rent(Math.Max(bytes.Length * 2, 4096));
        bytes.AsSpan(0, length).CopyTo(grown);
        ArrayPool<byte>.Shared.Return(bytes);

        return grown;
    }

    /// <summary>
    /// Hashes and decodes bytes. A byte order mark selects the encoding and is not part of the text.
    /// </summary>
    private static DecodedContent DecodeBytes(byte[] bytes, int length, Encoding encoding)
    {
        var hashes = ContentHashes.FromBytes(bytes.AsSpan(0, length));
        using var memory = new MemoryStream(bytes, 0, length, false);
        using var reader = new StreamReader(memory, encoding, true);
        var chars = ArrayPool<char>.Shared.Rent(Math.Max(length, 16));
        var charCount = 0;
        try
        {
            while (true)
            {
                if (charCount == chars.Length)
                {
                    var grown = ArrayPool<char>.Shared.Rent(chars.Length * 2);
                    chars.AsSpan(0, charCount).CopyTo(grown);
                    ArrayPool<char>.Shared.Return(chars);
                    chars = grown;
                }
                var read = reader.Read(chars, charCount, chars.Length - charCount);
                if (read == 0)
                {
                    break;
                }
                charCount += read;
            }
        }
        catch
        {
            ArrayPool<char>.Shared.Return(chars);
            throw;
        }

        return new DecodedContent(chars, charCount, hashes);
    }
}

/// <summary>
/// Text decoded from a stream, held in a rented array, with the hashes of the original bytes.
/// </summary>
internal readonly struct DecodedContent : IDisposable
{
    /// <summary>
    /// The rented array holding the text.
    /// </summary>
    private readonly char[] _buffer;

    /// <summary>
    /// Creates decoded content.
    /// </summary>
    /// <param name="buffer">The rented array holding the text.</param>
    /// <param name="length">The length of the text.</param>
    /// <param name="hashes">The hashes of the bytes.</param>
    public DecodedContent(char[] buffer, int length, ContentHashes hashes)
    {
        _buffer = buffer;
        Length = length;
        Hashes = hashes;
    }

    /// <summary>Gets the length of the text.</summary>
    public int Length { get; }

    /// <summary>Gets the Base64 MD5 hash of the bytes.</summary>
    public string Hash => Hashes.Md5Base64;

    /// <summary>Gets the MD5 and SHA-256 hashes of the bytes.</summary>
    public ContentHashes Hashes { get; }

    /// <summary>Gets the text as a span.</summary>
    public ReadOnlySpan<char> Span => _buffer.AsSpan(0, Length);

    /// <summary>Gets the text as memory.</summary>
    public ReadOnlyMemory<char> Memory => _buffer.AsMemory(0, Length);

    /// <summary>
    /// Returns the buffer to the pool. The text must not be used afterwards.
    /// </summary>
    public void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<char>.Shared.Return(_buffer);
        }
    }
}
