using System;
using System.Buffers;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// The first pass over a chart text: reads BPM, beat, speed and signature changes, creates the comma timing points, and
/// collects the note text of every timing point without reading the notes yet.
/// </summary>
/// <remarks>
/// The scanner follows MajSimai 2.x exactly, including its column counting: the column grows by one more than the
/// characters a <c>||</c> comment covers, a declaration such as <c>(120)</c> does not count its closing character,
/// <c>(</c> and <c>{</c> drop the note text read so far, and note text that no comma ends is dropped.
/// </remarks>
internal ref struct ChartScanner
{
    /// <summary>
    /// The chart text.
    /// </summary>
    private readonly ReadOnlySpan<char> _fumen;

    /// <summary>
    /// The note text of the current timing point, with whitespace.
    /// </summary>
    private readonly PooledBuffer<char> _noteText;

    /// <summary>
    /// The text of the speed declaration being read.
    /// </summary>
    private readonly PooledBuffer<char> _declaration;

    /// <summary>
    /// The note text of every timing point, without whitespace.
    /// </summary>
    private readonly PooledBuffer<char> _contentArena;

    /// <summary>
    /// The timing points that have note text.
    /// </summary>
    private readonly PooledBuffer<RawNoteTiming> _noteTimings;

    /// <summary>
    /// The timing point of every comma.
    /// </summary>
    private readonly PooledBuffer<CommaTiming> _commaTimings;

    /// <summary>
    /// The BPM in effect.
    /// </summary>
    private float _bpm;

    /// <summary>
    /// The beat division in effect (<c>{4}</c>).
    /// </summary>
    private float _beats;

    /// <summary>
    /// The note speed multiplier in effect.
    /// </summary>
    private float _hSpeed;

    /// <summary>
    /// The slide velocity multiplier in effect.
    /// </summary>
    private float _sVeloc;

    /// <summary>
    /// The numerator of the time signature in effect.
    /// </summary>
    private int _signatureNumerator;

    /// <summary>
    /// The denominator of the time signature in effect.
    /// </summary>
    private int _signatureDenominator;

    /// <summary>
    /// The time in seconds of the current timing point.
    /// </summary>
    private double _time;

    /// <summary>
    /// Whether note text has started since the last comma.
    /// </summary>
    private bool _hasNote;

    /// <summary>
    /// The current line, starting from 1.
    /// </summary>
    private int _line;

    /// <summary>
    /// The current column, counted the way MajSimai 2.x counted it.
    /// </summary>
    private int _column;

    /// <summary>
    /// Creates a scanner.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="contentArena">Receives the note text of every timing point, without whitespace.</param>
    /// <param name="noteTimings">Receives the timing points that have note text.</param>
    /// <param name="commaTimings">Receives the timing point of every comma and of the end of the text.</param>
    /// <param name="noteText">A buffer for the note text being read.</param>
    /// <param name="declaration">A buffer for the text of a speed declaration.</param>
    public ChartScanner(ReadOnlySpan<char> fumen,
                        PooledBuffer<char> contentArena,
                        PooledBuffer<RawNoteTiming> noteTimings,
                        PooledBuffer<CommaTiming> commaTimings,
                        PooledBuffer<char> noteText,
                        PooledBuffer<char> declaration)
    {
        _fumen = fumen;
        _contentArena = contentArena;
        _noteTimings = noteTimings;
        _commaTimings = commaTimings;
        _noteText = noteText;
        _declaration = declaration;
        _bpm = 0;
        _beats = 4;
        _hSpeed = 1;
        _sVeloc = 1;
        _signatureNumerator = 4;
        _signatureDenominator = 4;
        _time = 0;
        _hasNote = false;
        _line = 1;
        _column = 0;
    }

    /// <summary>
    /// Scans the whole text.
    /// </summary>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when a declaration or comment is malformed.</exception>
    /// <exception cref="Exception">Thrown with the message <c>Error at line,column</c> for any other failure, such as a
    /// <c>||</c> at the very end of the text.</exception>
    public void Scan()
    {
        try
        {
            ScanCore();
        }
        catch (InvalidSimaiMarkupException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new Exception("Error at " + _line + "," + _column + "\n" + e.Message);
        }
    }

    /// <summary>
    /// Reads every character of the chart text.
    /// </summary>
    private void ScanCore()
    {
        var fumen = _fumen;
        for (var i = 0; i < fumen.Length; i++)
        {
            var c = fumen[i];
            if (c == '\n')
            {
                NewLine();
                continue;
            }
            _column++;
            switch (c)
            {
                case '|':
                    ReadComment(ref i);
                    continue;
                case '(':
                    ReadBpm(ref i);
                    continue;
                case '{':
                    ReadBeats(ref i);
                    continue;
                case '<' when !_hasNote:
                    ReadSpeed(ref i);
                    continue;
            }
            if (!_hasNote && IsNoteStart(c))
            {
                _hasNote = true;
                _noteText.Clear();
            }
            if (c == ',')
            {
                EndTimingPoint(i);
            }
            else if (_hasNote)
            {
                _noteText.Add(c);
            }
        }
        AddCommaTiming(fumen.Length);
    }

    /// <summary>
    /// Handles a comma: stores the note text read so far, adds a comma timing point and moves the time forward.
    /// </summary>
    private void EndTimingPoint(int position)
    {
        if (_hasNote)
        {
            AddNoteTimings(position);
            _noteText.Clear();
        }
        AddCommaTiming(position);
        _time += 1d / (_bpm / 60d) * 4d / _beats;
        _hasNote = false;
        _noteText.Clear();
    }

    /// <summary>
    /// Stores the note text of the current timing point. Text with <c>`</c> (fake each) becomes one timing point per
    /// part, each a 128th note after the previous one.
    /// </summary>
    private void AddNoteTimings(int position)
    {
        ReadOnlySpan<char> noteText = _noteText.AsSpan();
        var fakeEachCount = LegacySplit.Count(noteText, '`');
        if (fakeEachCount == 0)
        {
            AddNoteTiming(_time, noteText, position);
            return;
        }
        // 2.x sliced the rented array from index n + 1 instead of taking its first n + 1 items, so the number of parts
        // it can hold depends on the size of the rented array. Keep that capacity.
        var rented = ArrayPool<Range>.Shared.Rent(fakeEachCount + 1);
        try
        {
            var ranges = rented.AsSpan(fakeEachCount + 1);
            var count = LegacySplit.Split(noteText, ranges, '`', true);
            var fakeTime = _time;
            var interval = 1.875 / _bpm;
            for (var i = 0; i < count; i++)
            {
                AddNoteTiming(fakeTime, noteText[ranges[i]], position);
                fakeTime += interval;
            }
        }
        finally
        {
            ArrayPool<Range>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Stores the note text of one timing point without whitespace.
    /// </summary>
    /// <param name="timing">The time in seconds.</param>
    /// <param name="noteText">The note text.</param>
    /// <param name="position">The character offset of the comma.</param>
    private void AddNoteTiming(double timing, ReadOnlySpan<char> noteText, int position)
    {
        var start = _contentArena.Count;
        foreach (var c in noteText)
        {
            if (!char.IsWhiteSpace(c))
            {
                _contentArena.Add(c);
            }
        }
        var timingPoint = new RawNoteTiming(timing,
                                            _bpm,
                                            _hSpeed,
                                            _column,
                                            _line,
                                            position,
                                            start,
                                            _contentArena.Count - start);
        _noteTimings.Add(timingPoint);
    }

    /// <summary>
    /// Stores the values of a comma timing point.
    /// </summary>
    /// <param name="position">The character offset of the comma, or the length of the text for the last one.</param>
    private void AddCommaTiming(int position)
    {
        var timing = new CommaTiming(_time,
                                     _bpm,
                                     _sVeloc,
                                     _column,
                                     _line,
                                     position,
                                     _signatureNumerator,
                                     _signatureDenominator);
        _commaTimings.Add(timing);
    }

    /// <summary>
    /// Reads a <c>||</c> comment, or a <c>||s3/4</c> time signature, up to the end of the line.
    /// </summary>
    private void ReadComment(ref int i)
    {
        var fumen = _fumen;
        if (fumen.Length - i < 2 || fumen[i + 1] != '|')
        {
            throw UnexpectedCharacter(fumen[i]);
        }
        i += 2;
        _column += 2;
        if (i >= fumen.Length)
        {
            // 2.x read past the end of the text here.
            throw new IndexOutOfRangeException();
        }
        if (fumen[i] != 's')
        {
            SkipToLineEnd(ref i);
            return;
        }
        var start = i + 1;
        i++;
        _column++;
        SkipToLineEnd(ref i);
        var signature = fumen.Slice(start, i - start).Trim();
        Span<Range> ranges = stackalloc Range[2];
        if (LegacySplit.Split(signature, ranges, '/', false) < 2)
        {
            return;
        }
        if (!DurationReader.TryParseInt(signature[ranges[0]], out _signatureNumerator))
        {
            _signatureNumerator = 4;
        }
        if (!DurationReader.TryParseInt(signature[ranges[1]], out _signatureDenominator))
        {
            _signatureDenominator = 4;
        }
    }

    /// <summary>
    /// Moves <paramref name="i"/> to the next line break, or to the end of the text.
    /// </summary>
    private void SkipToLineEnd(ref int i)
    {
        var fumen = _fumen;
        for (; i < fumen.Length; i++)
        {
            if (fumen[i] == '\n')
            {
                NewLine();
                break;
            }
            _column++;
        }
    }

    /// <summary>
    /// Reads a <c>(120)</c> BPM declaration.
    /// </summary>
    private void ReadBpm(ref int i)
    {
        ResetNote();
        if (_fumen.Length - i < 3)
        {
            throw UnexpectedCharacter(_fumen[i]);
        }
        var text = ReadDeclaration(ref i, ')');
        var value = text.Trim();
        if (!DurationReader.TryParseFloat(value, out _bpm))
        {
            throw new InvalidSimaiSyntaxException(_line, _column, value.ToString(), "BPM value must be a number");
        }
    }

    /// <summary>
    /// Reads a <c>{4}</c> beat declaration, or a <c>{#0.5}</c> declaration in seconds.
    /// </summary>
    private void ReadBeats(ref int i)
    {
        ResetNote();
        if (_fumen.Length - i < 3)
        {
            throw UnexpectedCharacter(_fumen[i]);
        }
        var text = ReadDeclaration(ref i, '}');
        var value = text.Trim();
        if (value.IsEmpty)
        {
            throw new InvalidSimaiSyntaxException(_line, _column, text.ToString(), "Beats value must be a number");
        }
        if (value[0] == '#')
        {
            if (!DurationReader.TryParseFloat(value.Slice(1), out var interval))
            {
                throw new InvalidSimaiSyntaxException(_line, _column, value.ToString(), "Beats value must be a number");
            }
            _beats = 240f / (_bpm * interval);
        }
        else if (!DurationReader.TryParseFloat(value, out _beats))
        {
            throw new InvalidSimaiSyntaxException(_line, _column, value.ToString(), "Beats value must be a number");
        }
    }

    /// <summary>
    /// Reads the text of a declaration up to <paramref name="end"/> or the end of the chart, and leaves
    /// <paramref name="i"/> on the closing character.
    /// </summary>
    /// <returns>The text between the opening and closing characters, including line breaks.</returns>
    private ReadOnlySpan<char> ReadDeclaration(ref int i, char end)
    {
        var fumen = _fumen;
        var start = i + 1;
        i++;
        _column++;
        for (; i < fumen.Length; i++)
        {
            var c = fumen[i];
            if (c == '\n')
            {
                NewLine();
                continue;
            }
            if (c == end)
            {
                break;
            }
            _column++;
        }

        return fumen.Slice(start, i - start);
    }

    /// <summary>
    /// Reads a <c>&lt;HS*1.5&gt;</c> or <c>&lt;SV*0.5&gt;</c> declaration.
    /// </summary>
    private void ReadSpeed(ref int i)
    {
        ResetNote();
        var fumen = _fumen;
        if (fumen.Length - i < 4)
        {
            throw UnexpectedCharacter(fumen[i]);
        }
        var start = i + 1;
        var declaration = _declaration;
        declaration.Clear();
        var tagIndex = -1;
        i++;
        _column++;
        for (; i < fumen.Length; i++)
        {
            var c = fumen[i];
            if (c == '\n')
            {
                NewLine();
                continue;
            }
            if (c == '*')
            {
                if (tagIndex != -1)
                {
                    var text = fumen.Slice(start - 1, i + 2 - start).ToString();
                    throw new InvalidSimaiSyntaxException(_line, _column, text, "Unexpected HS declaration syntax");
                }
                tagIndex = declaration.Count;
            }
            else if (c == '>')
            {
                break;
            }
            declaration.Add(c);
            _column++;
        }
        ReadOnlySpan<char> content = declaration.AsSpan();
        if (content.Length < 4 || tagIndex == -1)
        {
            throw new InvalidSimaiSyntaxException(_line, _column, content.ToString(), "Unexpected HS / SV declaration syntax");
        }
        var name = content.Slice(0, tagIndex);
        var value = content.Slice(tagIndex + 1);
        if (name.SequenceEqual("HS".AsSpan()))
        {
            if (!DurationReader.TryParseFloat(value, out _hSpeed))
            {
                throw new InvalidSimaiMarkupException(_line, _column, content.ToString(), "HSpeed value must be a number");
            }
        }
        else if (name.SequenceEqual("SV".AsSpan()))
        {
            if (!DurationReader.TryParseFloat(value, out _sVeloc))
            {
                throw new InvalidSimaiMarkupException(_line, _column, content.ToString(), "SVeloc value must be a number");
            }
        }
        else
        {
            var message = $"Unexpected HS / SV declaration syntax \"{name.ToString()}\", is it \"HS\" or \"SV\"?";
            throw new InvalidSimaiSyntaxException(_line, _column, content.ToString(), message);
        }
    }

    /// <summary>
    /// Drops the note text read since the last comma.
    /// </summary>
    private void ResetNote()
    {
        _hasNote = false;
        _noteText.Clear();
    }

    /// <summary>
    /// Moves the position to the start of the next line.
    /// </summary>
    private void NewLine()
    {
        _line++;
        _column = 0;
    }

    /// <summary>
    /// Creates the exception for a character that cannot start a declaration.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns>The exception.</returns>
    private InvalidSimaiMarkupException UnexpectedCharacter(char c)
    {
        var text = c.ToString();

        return new InvalidSimaiMarkupException(_line, _column, text, $"Unexpected character \"{text}\"");
    }

    /// <summary>
    /// Gets whether a character starts note text: a button digit or a touch area letter.
    /// </summary>
    private static bool IsNoteStart(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'E');
    }
}
