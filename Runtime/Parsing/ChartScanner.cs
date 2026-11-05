using System;
using MajSimai.Utils;

namespace MajSimai.Parsing;

/// <summary>
/// The first pass over a chart text: reads BPM, beat, speed and signature changes, creates the comma timing points, and
/// collects the note text of every timing point without reading the notes yet.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Lines and columns start from 1; the column of a timing point is the column of its comma.</item>
/// <item><c>||</c> starts a comment that runs to the end of its line. <c>||s3/4</c> also sets the time signature.</item>
/// <item><c>(bpm)</c>, <c>{beats}</c> and <c>&lt;HS*x&gt;</c> / <c>&lt;SV*x&gt;</c> declarations must be closed; a declaration
/// that is not closed throws in every mode.</item>
/// <item>An <c>E</c> at the start of note text ends the chart unless the next character that is not whitespace is a
/// digit: whitespace and line breaks are skipped as everywhere in simai, so <c>E\n3</c> is the sensor <c>E3</c>.</item>
/// <item>In lenient mode, note text cut off by <c>(</c> or <c>{</c>, note text without a comma at the end, and stray
/// characters are dropped; strict mode throws for them.</item>
/// </list>
/// </remarks>
internal ref struct ChartScanner
{
    /// <summary>
    /// The chart text.
    /// </summary>
    private readonly ReadOnlySpan<char> _fumen;

    /// <summary>
    /// Decides what happens when the text breaks the grammar.
    /// </summary>
    private readonly SyntaxReporter _reporter;

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
    /// The time in seconds from one comma to the next, for the BPM and beats in effect. It is computed once per change
    /// instead of at every comma, with the same expression, so the times are the same to the last bit.
    /// </summary>
    private double _commaInterval;

    /// <summary>
    /// Whether note text has started since the last comma.
    /// </summary>
    private bool _hasNote;

    /// <summary>
    /// The current line, starting from 1.
    /// </summary>
    private int _line;

    /// <summary>
    /// The index of the first character of the current line.
    /// </summary>
    private int _lineStart;

    /// <summary>
    /// The line where the current note text starts.
    /// </summary>
    private int _noteLine;

    /// <summary>
    /// The column where the current note text starts.
    /// </summary>
    private int _noteColumn;

    /// <summary>
    /// Creates a scanner.
    /// </summary>
    /// <param name="fumen">The chart text.</param>
    /// <param name="reporter">Decides what happens when the text breaks the grammar.</param>
    /// <param name="contentArena">Receives the note text of every timing point, without whitespace.</param>
    /// <param name="noteTimings">Receives the timing points that have note text.</param>
    /// <param name="commaTimings">Receives the timing point of every comma and of the end of the chart.</param>
    /// <param name="noteText">A buffer for the note text being read.</param>
    /// <param name="declaration">A buffer for the text of a speed declaration.</param>
    public ChartScanner(ReadOnlySpan<char> fumen,
                        SyntaxReporter reporter,
                        PooledBuffer<char> contentArena,
                        PooledBuffer<RawNoteTiming> noteTimings,
                        PooledBuffer<CommaTiming> commaTimings,
                        PooledBuffer<char> noteText,
                        PooledBuffer<char> declaration)
    {
        _fumen = fumen;
        _reporter = reporter;
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
        UpdateCommaInterval();
        _hasNote = false;
        _line = 1;
        _lineStart = 0;
        _noteLine = 1;
        _noteColumn = 1;
    }

    /// <summary>
    /// Scans the whole text.
    /// </summary>
    /// <exception cref="InvalidSimaiMarkupException">Thrown when the text breaks the grammar in a way the mode does not accept.</exception>
    public void Scan()
    {
        var fumen = _fumen;
        // The readers of comments and declarations move the position through a copy of the loop index: passing the
        // index itself by reference would keep it in memory instead of a register for every character of the chart.
        for (var i = 0; i < fumen.Length; i++)
        {
            var c = fumen[i];
            switch (c)
            {
                case '\n':
                    NewLine(i);
                    continue;
                case '|':
                {
                    var end = i;
                    ReadComment(ref end);
                    i = end;
                    continue;
                }
                case '(':
                {
                    var end = i;
                    var isClosed = ReadBpm(ref end);
                    i = end;
                    if (!isClosed)
                    {
                        // The declaration is not closed. Parsing threw; the syntax checker keeps what it read so far.
                        AddCommaTiming(i);
                        return;
                    }
                    continue;
                }
                case '{':
                {
                    var end = i;
                    var isClosed = ReadBeats(ref end);
                    i = end;
                    if (!isClosed)
                    {
                        // The declaration is not closed. Parsing threw; the syntax checker keeps what it read so far.
                        AddCommaTiming(i);
                        return;
                    }
                    continue;
                }
                case '<' when !_hasNote:
                {
                    var end = i;
                    var isClosed = ReadSpeed(ref end);
                    i = end;
                    if (!isClosed)
                    {
                        // The declaration is not closed. Parsing threw; the syntax checker keeps what it read so far.
                        AddCommaTiming(i);
                        return;
                    }
                    continue;
                }
                case ',':
                    EndTimingPoint(i);
                    continue;
            }
            if (_hasNote)
            {
                _noteText.Add(c);
                continue;
            }
            if (IsNoteStart(c))
            {
                if (c == 'E' && IsEndMarker(i))
                {
                    EndChart(i);
                    return;
                }
                _hasNote = true;
                _noteText.Clear();
                _noteLine = _line;
                _noteColumn = Column(i);
                _noteText.Add(c);
                continue;
            }
            if (!char.IsWhiteSpace(c) && _reporter.IsReported(SyntaxRules.StrayCharacter))
            {
                var text = c.ToString();
                _reporter.Report(SyntaxRules.StrayCharacter, _line, Column(i), text, $"The character \"{text}\" is not part of a note or declaration and is ignored.");
            }
        }
        DropUnterminatedNote();
        AddCommaTiming(fumen.Length);
    }

    /// <summary>
    /// Computes <see cref="_commaInterval"/> for the BPM and beats in effect; called whenever one of them changes.
    /// </summary>
    private void UpdateCommaInterval()
    {
        _commaInterval = 1d / (_bpm / 60d) * 4d / _beats;
    }

    /// <summary>
    /// Gets the column of a character on the current line.
    /// </summary>
    /// <param name="index">The index of the character.</param>
    /// <returns>The column, starting from 1.</returns>
    private readonly int Column(int index)
    {
        return index - _lineStart + 1;
    }

    /// <summary>
    /// Moves the position to the line after the line break at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The index of the line break.</param>
    private void NewLine(int index)
    {
        _line++;
        _lineStart = index + 1;
    }

    /// <summary>
    /// Handles a comma: stores the note text read so far, adds a comma timing point and moves the time forward.
    /// </summary>
    /// <param name="position">The index of the comma.</param>
    private void EndTimingPoint(int position)
    {
        if (_hasNote)
        {
            AddNoteTimings(position);
        }
        AddCommaTiming(position);
        _time += _commaInterval;
        _hasNote = false;
        _noteText.Clear();
    }

    /// <summary>
    /// Stores the note text of the current timing point. Text with <c>`</c> (fake each) becomes one timing point per
    /// part, each a 128th note after the previous one.
    /// </summary>
    /// <param name="position">The index of the comma.</param>
    private void AddNoteTimings(int position)
    {
        ReadOnlySpan<char> noteText = _noteText.AsSpan();
        if (noteText.IndexOf('`') == -1)
        {
            AddNoteTiming(_time, noteText, position);
            return;
        }
        var fakeTime = _time;
        var interval = 1.875 / _bpm;
        var rest = noteText;
        while (true)
        {
            var separator = rest.IndexOf('`');
            var part = separator == -1 ? rest : rest.Slice(0, separator);
            if (part.IsEmpty || (_reporter.ChecksStrictly && IsWhiteSpace(part)))
            {
                if (_reporter.IsReported(SyntaxRules.EmptyFakeEachPart))
                {
                    var text = noteText.ToString();
                    _reporter.Report(SyntaxRules.EmptyFakeEachPart, _noteLine, _noteColumn, text, $"The fake each \"{text}\" has an empty part between '`'.");
                }
            }
            else
            {
                AddNoteTiming(fakeTime, part, position);
                fakeTime += interval;
            }
            if (separator == -1)
            {
                return;
            }
            rest = rest.Slice(separator + 1);
        }
    }

    /// <summary>
    /// Stores the note text of one timing point without whitespace.
    /// </summary>
    /// <param name="timing">The time in seconds.</param>
    /// <param name="noteText">The note text.</param>
    /// <param name="position">The index of the comma.</param>
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
                                            _sVeloc,
                                            _signatureNumerator,
                                            _signatureDenominator,
                                            Column(position),
                                            _line,
                                            position,
                                            start,
                                            _contentArena.Count - start,
                                            _noteLine,
                                            _noteColumn);
        _noteTimings.Add(timingPoint);
    }

    /// <summary>
    /// Stores the values of a comma timing point.
    /// </summary>
    /// <param name="position">The index of the comma, or of the end of the chart for the last one.</param>
    private void AddCommaTiming(int position)
    {
        var timing = new CommaTiming(_time,
                                     _bpm,
                                     _hSpeed,
                                     _sVeloc,
                                     Column(position),
                                     _line,
                                     position,
                                     _signatureNumerator,
                                     _signatureDenominator);
        _commaTimings.Add(timing);
    }

    /// <summary>
    /// Gets whether an <c>E</c> at the start of note text is the end marker: it is not followed by a sensor number.
    /// </summary>
    /// <param name="index">The index of the <c>E</c>.</param>
    /// <returns><see langword="true"/> when the next character that is not whitespace is not a digit.</returns>
    private readonly bool IsEndMarker(int index)
    {
        var fumen = _fumen;
        for (var i = index + 1; i < fumen.Length; i++)
        {
            if (!char.IsWhiteSpace(fumen[i]))
            {
                return fumen[i] < '0' || fumen[i] > '9';
            }
        }

        return true;
    }

    /// <summary>
    /// Ends the chart at the end marker <c>E</c>. Text after it, other than whitespace and comments, is ignored.
    /// </summary>
    /// <param name="index">The index of the <c>E</c>.</param>
    private void EndChart(int index)
    {
        var fumen = _fumen;
        var endLine = _line;
        var endColumn = Column(index);
        for (var i = index + 1; i < fumen.Length; i++)
        {
            var c = fumen[i];
            if (c == '|' && i + 1 < fumen.Length && fumen[i + 1] == '|')
            {
                var lineEnd = fumen.Slice(i).IndexOf('\n');
                if (lineEnd == -1)
                {
                    break;
                }
                i += lineEnd - 1;
                continue;
            }
            if (!char.IsWhiteSpace(c))
            {
                var text = fumen.Slice(i).Trim().ToString();
                _reporter.Report(SyntaxRules.TextAfterEnd, endLine, endColumn, text, $"The text after the end marker 'E' is ignored: \"{Shorten(text)}\".");
                break;
            }
        }
        AddCommaTiming(index);
    }

    /// <summary>
    /// Handles note text that no comma ended at the end of the chart: it is dropped.
    /// </summary>
    private void DropUnterminatedNote()
    {
        if (!_hasNote)
        {
            return;
        }
        ReadOnlySpan<char> noteText = _noteText.AsSpan();
        if (IsWhiteSpace(noteText))
        {
            return;
        }
        var text = noteText.Trim().ToString();
        _reporter.Report(SyntaxRules.UnterminatedNote, _noteLine, _noteColumn, text, $"The note text \"{text}\" has no comma after it and is ignored.");
    }

    /// <summary>
    /// Handles a declaration that starts while note text is being read: the note text is dropped.
    /// </summary>
    /// <param name="index">The index of the declaration.</param>
    private void CutNote(int index)
    {
        if (_hasNote)
        {
            ReadOnlySpan<char> noteText = _noteText.AsSpan();
            if (!IsWhiteSpace(noteText))
            {
                var text = noteText.Trim().ToString();
                _reporter.Report(SyntaxRules.NoteCutByDeclaration,
                                 _line,
                                 Column(index),
                                 text,
                                 $"The note text \"{text}\" is cut off by a declaration and is ignored; end it with a comma first.");
            }
        }
        _hasNote = false;
        _noteText.Clear();
    }

    /// <summary>
    /// Reads a <c>||</c> comment, or a <c>||s3/4</c> time signature, up to the end of the line.
    /// </summary>
    /// <param name="i">The index of the first <c>|</c>; receives the index of the last character of the comment.</param>
    private void ReadComment(ref int i)
    {
        var fumen = _fumen;
        if (i + 1 >= fumen.Length || fumen[i + 1] != '|')
        {
            _reporter.Report(SyntaxRules.UnexpectedCharacter, _line, Column(i), "|", "Unexpected character \"|\"");
            return;
        }
        var lineEnd = fumen.Slice(i).IndexOf('\n');
        var end = lineEnd == -1 ? fumen.Length : i + lineEnd;
        if (i + 2 < end && fumen[i + 2] == 's')
        {
            ReadSignature(fumen.Slice(i + 3, end - i - 3), i);
        }
        i = end - 1;
    }

    /// <summary>
    /// Reads the time signature of a <c>||s</c> comment. Values that cannot be read become 4.
    /// </summary>
    /// <param name="text">The text after <c>||s</c>.</param>
    /// <param name="index">The index of the comment.</param>
    private void ReadSignature(ReadOnlySpan<char> text, int index)
    {
        var signature = text.Trim();
        Span<Range> ranges = stackalloc Range[2];
        if (LegacySplit.Split(signature, ranges, '/', false) < 2)
        {
            return;
        }
        var isNumeratorValid = DurationReader.TryParseInt(signature[ranges[0]], out _signatureNumerator);
        var isDenominatorValid = DurationReader.TryParseInt(signature[ranges[1]], out _signatureDenominator);
        if (!isNumeratorValid)
        {
            _signatureNumerator = 4;
        }
        if (!isDenominatorValid)
        {
            _signatureDenominator = 4;
        }
        var isExact = ranges[1].End.Value == signature.Length;
        if (!isNumeratorValid || !isDenominatorValid || !isExact)
        {
            var content = signature.ToString();
            _reporter.Report(SyntaxRules.InvalidSignature, _line, Column(index), content, $"The time signature \"{content}\" must be two integers such as \"3/4\".");
        }
    }

    /// <summary>
    /// Reads a <c>(120)</c> BPM declaration.
    /// </summary>
    /// <param name="i">The index of <c>(</c>; receives the index of <c>)</c>.</param>
    /// <returns><see langword="false"/> when the chart cannot be read further.</returns>
    private bool ReadBpm(ref int i)
    {
        CutNote(i);
        var line = _line;
        var column = Column(i);
        if (!TryReadDeclaration(ref i, ')', "BPM", out var text))
        {
            return false;
        }
        var value = text.Trim();
        if (!DurationReader.TryParseFloat(value, out var bpm))
        {
            _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, value.ToString(), "BPM value must be a number");
            return true;
        }
        _bpm = bpm;
        UpdateCommaInterval();

        return true;
    }

    /// <summary>
    /// Reads a <c>{4}</c> beat declaration, or a <c>{#0.5}</c> declaration in seconds.
    /// </summary>
    /// <param name="i">The index of <c>{</c>; receives the index of <c>}</c>.</param>
    /// <returns><see langword="false"/> when the chart cannot be read further.</returns>
    private bool ReadBeats(ref int i)
    {
        CutNote(i);
        var line = _line;
        var column = Column(i);
        if (!TryReadDeclaration(ref i, '}', "beat", out var text))
        {
            return false;
        }
        var value = text.Trim();
        if (value.IsEmpty)
        {
            _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, text.ToString(), "Beats value must be a number");
            return true;
        }
        if (value[0] == '#')
        {
            if (!DurationReader.TryParseFloat(value.Slice(1), out var interval))
            {
                _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, value.ToString(), "Beats value must be a number");
                return true;
            }
            _beats = 240f / (_bpm * interval);
            UpdateCommaInterval();
        }
        else if (DurationReader.TryParseFloat(value, out var beats))
        {
            _beats = beats;
            UpdateCommaInterval();
        }
        else
        {
            _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, value.ToString(), "Beats value must be a number");
        }

        return true;
    }

    /// <summary>
    /// Reads the text of a declaration up to its closing character, and leaves <paramref name="i"/> on it. Line breaks
    /// inside the declaration are allowed.
    /// </summary>
    /// <param name="i">The index of the opening character; receives the index of the closing character.</param>
    /// <param name="end">The closing character.</param>
    /// <param name="kind">The name of the declaration, for the error message.</param>
    /// <param name="text">Receives the text between the opening and closing characters.</param>
    /// <returns><see langword="false"/> when the declaration is not closed.</returns>
    private bool TryReadDeclaration(ref int i, char end, string kind, out ReadOnlySpan<char> text)
    {
        var fumen = _fumen;
        var start = i;
        var line = _line;
        var column = Column(i);
        var close = fumen.Slice(start + 1).IndexOf(end);
        if (close == -1)
        {
            text = default;
            var content = Shorten(fumen.Slice(start).ToString());
            _reporter.Report(SyntaxRules.UnclosedDeclaration, line, column, content, $"The {kind} declaration \"{content}\" is not closed by '{end}'.");
            return false;
        }
        close += start + 1;
        for (var j = start + 1; j < close; j++)
        {
            if (fumen[j] == '\n')
            {
                NewLine(j);
            }
        }
        text = fumen.Slice(start + 1, close - start - 1);
        i = close;

        return true;
    }

    /// <summary>
    /// Reads a <c>&lt;HS*1.5&gt;</c> or <c>&lt;SV*0.5&gt;</c> declaration.
    /// </summary>
    /// <param name="i">The index of <c>&lt;</c>; receives the index of <c>&gt;</c>.</param>
    /// <returns><see langword="false"/> when the chart cannot be read further.</returns>
    private bool ReadSpeed(ref int i)
    {
        var line = _line;
        var column = Column(i);
        var start = i;
        if (!TryReadDeclaration(ref i, '>', "HS / SV", out var text))
        {
            return false;
        }
        var declaration = _declaration;
        declaration.Clear();
        var tagIndex = -1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                continue;
            }
            if (c == '*')
            {
                if (tagIndex != -1)
                {
                    var content = _fumen.Slice(start, i - start + 1).ToString();
                    _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, content, "Unexpected HS declaration syntax");
                    return true;
                }
                tagIndex = declaration.Count;
            }
            declaration.Add(c);
        }
        ReadOnlySpan<char> body = declaration.AsSpan();
        if (body.Length < 4 || tagIndex == -1)
        {
            _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, body.ToString(), "Unexpected HS / SV declaration syntax");
            return true;
        }
        var name = body.Slice(0, tagIndex);
        var value = body.Slice(tagIndex + 1);
        if (name.SequenceEqual("HS".AsSpan()))
        {
            if (DurationReader.TryParseFloat(value, out var hSpeed))
            {
                _hSpeed = hSpeed;
            }
            else
            {
                _reporter.Report(SyntaxRules.InvalidSpeedValue, line, column, body.ToString(), "HSpeed value must be a number");
            }
        }
        else if (name.SequenceEqual("SV".AsSpan()))
        {
            if (DurationReader.TryParseFloat(value, out var sVeloc))
            {
                _sVeloc = sVeloc;
            }
            else
            {
                _reporter.Report(SyntaxRules.InvalidSpeedValue, line, column, body.ToString(), "SVeloc value must be a number");
            }
        }
        else
        {
            var message = $"Unexpected HS / SV declaration syntax \"{name.ToString()}\", is it \"HS\" or \"SV\"?";
            _reporter.Report(SyntaxRules.InvalidDeclaration, line, column, body.ToString(), message);
        }

        return true;
    }

    /// <summary>
    /// Gets whether a text has only whitespace.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when every character is whitespace.</returns>
    private static bool IsWhiteSpace(ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Shortens a text for an error message to its first line and at most 40 characters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The shortened text.</returns>
    private static string Shorten(string text)
    {
        var lineEnd = text.IndexOf('\n');
        if (lineEnd != -1)
        {
            text = text.Substring(0, lineEnd).TrimEnd();
        }

        return text.Length <= 40 ? text : text.Substring(0, 40) + "...";
    }

    /// <summary>
    /// Gets whether a character starts note text: a button digit or a touch area letter.
    /// </summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for 0 to 9 and A to E.</returns>
    private static bool IsNoteStart(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'E');
    }
}
