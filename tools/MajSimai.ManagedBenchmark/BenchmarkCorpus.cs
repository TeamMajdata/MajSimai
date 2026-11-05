using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MajSimai.ManagedBenchmark;

/// <summary>
/// The sample files of a benchmark, kept in one buffer. A corpus is read from a sample folder, or from a corpus file
/// written by tools/benchmark-native.py so the C# benchmarks parse exactly the files the native benchmarks parse. The
/// corpus file format is the one tools/NativeBenchmark/BenchmarkCommon.h reads: a little-endian <see cref="uint"/> file
/// count, then for every file a little-endian <see cref="uint"/> length followed by the bytes of the file.
/// </summary>
internal sealed class BenchmarkCorpus
{
    /// <summary>
    /// The environment variable that names a corpus file written by tools/benchmark-native.py. It wins over
    /// <see cref="SamplesDirectoryVariable"/>.
    /// </summary>
    public const string CorpusFileVariable = "MAJSIMAI_BENCHMARK_CORPUS";

    /// <summary>
    /// The environment variable that names a folder searched for <c>maidata.txt</c>, as in the sample tests.
    /// </summary>
    public const string SamplesDirectoryVariable = "MAJSIMAI_SAMPLES_DIR";

    /// <summary>
    /// The name of the sample files in a sample folder.
    /// </summary>
    private const string SampleFileName = "maidata.txt";

    /// <summary>
    /// The size of a count or a length in a corpus file, in bytes.
    /// </summary>
    private const int LengthSize = sizeof(uint);

    /// <summary>
    /// The bytes of every file, one after the other.
    /// </summary>
    private readonly byte[] _buffer;

    /// <summary>
    /// The offset and length of every file in <see cref="_buffer"/>.
    /// </summary>
    private readonly (int Offset, int Length)[] _files;

    /// <summary>
    /// Creates a corpus over a buffer that has already been checked.
    /// </summary>
    /// <param name="buffer">The bytes of the files.</param>
    /// <param name="files">The offset and length of every file in <paramref name="buffer"/>.</param>
    private BenchmarkCorpus(byte[] buffer, (int Offset, int Length)[] files)
    {
        _buffer = buffer;
        _files = files;
        foreach (var file in files)
        {
            TotalBytes += file.Length;
        }
    }

    /// <summary>
    /// Gets the number of files.
    /// </summary>
    public int Count => _files.Length;

    /// <summary>
    /// Gets the size of all files together, in bytes.
    /// </summary>
    public long TotalBytes { get; }

    /// <summary>
    /// Gets the bytes of a file.
    /// </summary>
    /// <param name="index">The index of the file, less than <see cref="Count"/>.</param>
    /// <returns>The bytes of the file, which belong to the corpus.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when <paramref name="index"/> is out of range.</exception>
    public ArraySegment<byte> GetFile(int index)
    {
        var (offset, length) = _files[index];

        return new ArraySegment<byte>(_buffer, offset, length);
    }

    /// <summary>
    /// Creates a corpus from files in memory.
    /// </summary>
    /// <param name="files">The bytes of every file; the corpus copies them.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="files"/> or one of its items is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="files"/> is empty.</exception>
    public static BenchmarkCorpus FromFiles(IReadOnlyList<byte[]> files)
    {
        if (files is null)
        {
            throw new ArgumentNullException(nameof(files));
        }
        if (files.Count == 0)
        {
            throw new ArgumentException("A corpus needs at least one file.", nameof(files));
        }

        var buffer = new byte[files.Sum(file => (long)(file ?? throw new ArgumentNullException(nameof(files))).Length)];
        var positions = new (int Offset, int Length)[files.Count];
        var offset = 0;
        for (var index = 0; index < files.Count; index++)
        {
            files[index].CopyTo(buffer, offset);
            positions[index] = (offset, files[index].Length);
            offset += files[index].Length;
        }

        return new BenchmarkCorpus(buffer, positions);
    }

    /// <summary>
    /// Reads every <c>maidata.txt</c> under a folder, in ordinal order of their paths.
    /// </summary>
    /// <param name="directory">The sample folder.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when <paramref name="directory"/> does not exist.</exception>
    /// <exception cref="InvalidDataException">Thrown when the folder has no <c>maidata.txt</c>.</exception>
    public static BenchmarkCorpus FromDirectory(string directory)
    {
        if (directory is null)
        {
            throw new ArgumentNullException(nameof(directory));
        }
        var paths = Directory.GetFiles(directory, SampleFileName, SearchOption.AllDirectories);
        if (paths.Length == 0)
        {
            throw new InvalidDataException($"'{directory}' has no {SampleFileName}.");
        }
        Array.Sort(paths, StringComparer.Ordinal);

        return FromFiles(paths.Select(File.ReadAllBytes).ToArray());
    }

    /// <summary>
    /// Reads a corpus file to the end of a stream.
    /// </summary>
    /// <param name="stream">The stream holding the corpus file.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">Thrown when the corpus is empty, has no file or ends inside a file.</exception>
    public static BenchmarkCorpus Read(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var buffer = memory.ToArray();

        if (buffer.Length < LengthSize)
        {
            throw new InvalidDataException("The corpus is empty.");
        }
        var count = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        if (count == 0)
        {
            throw new InvalidDataException("The corpus has no file.");
        }
        // Every file takes at least its length, so a count larger than that is malformed rather than a reason to allocate.
        if (count > (uint)(buffer.Length - LengthSize) / LengthSize)
        {
            throw new InvalidDataException($"The corpus names {count} files but is too short for them.");
        }

        var files = new (int Offset, int Length)[count];
        var offset = LengthSize;
        for (var index = 0; index < files.Length; index++)
        {
            if (buffer.Length - offset < LengthSize)
            {
                throw new InvalidDataException($"The corpus ends before the length of file {index}.");
            }
            var length = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(offset));
            offset += LengthSize;
            if (length > (uint)(buffer.Length - offset))
            {
                throw new InvalidDataException($"The corpus ends inside file {index}.");
            }
            files[index] = (offset, (int)length);
            offset += (int)length;
        }

        return new BenchmarkCorpus(buffer, files);
    }

    /// <summary>
    /// Tells why the environment does not name the sample files, if it does not.
    /// </summary>
    /// <param name="getVariable">Reads an environment variable, such as <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <returns>The problem, or <see langword="null"/> when <see cref="FromEnvironment"/> can find the files.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getVariable"/> is <see langword="null"/>.</exception>
    public static string? GetEnvironmentProblem(Func<string, string?> getVariable)
    {
        if (getVariable is null)
        {
            throw new ArgumentNullException(nameof(getVariable));
        }
        var corpusFile = getVariable(CorpusFileVariable);
        if (!string.IsNullOrEmpty(corpusFile))
        {
            return File.Exists(corpusFile) ? null : $"{CorpusFileVariable} names '{corpusFile}', which does not exist.";
        }
        var samples = getVariable(SamplesDirectoryVariable);
        if (!string.IsNullOrEmpty(samples))
        {
            return Directory.Exists(samples) ? null : $"{SamplesDirectoryVariable} names '{samples}', which does not exist.";
        }
        return $"Set {SamplesDirectoryVariable} to the sample folder, or {CorpusFileVariable} to a corpus file written by tools/benchmark-native.py.";
    }

    /// <summary>
    /// Reads the sample files the environment names: the corpus file of <see cref="CorpusFileVariable"/>, or else the
    /// folder of <see cref="SamplesDirectoryVariable"/>.
    /// </summary>
    /// <param name="getVariable">Reads an environment variable, such as <see cref="Environment.GetEnvironmentVariable(string)"/>.</param>
    /// <returns>The corpus.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="getVariable"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the environment names no existing file or folder.</exception>
    /// <exception cref="InvalidDataException">Thrown when the corpus file is malformed or the folder has no sample.</exception>
    public static BenchmarkCorpus FromEnvironment(Func<string, string?> getVariable)
    {
        var problem = GetEnvironmentProblem(getVariable);
        if (problem is not null)
        {
            throw new InvalidOperationException(problem);
        }
        var corpusFile = getVariable(CorpusFileVariable);
        if (!string.IsNullOrEmpty(corpusFile))
        {
            using var stream = File.OpenRead(corpusFile);
            return Read(stream);
        }
        return FromDirectory(getVariable(SamplesDirectoryVariable)!);
    }
}
