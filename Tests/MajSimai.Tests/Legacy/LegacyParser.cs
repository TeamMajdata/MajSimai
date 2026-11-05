using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Loader;

namespace MajSimai.Tests.Legacy;

/// <summary>
/// Runs the published MajSimai 2.2.3 parser, loaded into its own <see cref="AssemblyLoadContext"/>, and converts its
/// results into views that can be compared with the refactored parser.
/// </summary>
internal sealed class LegacyParser
{
    /// <summary>
    /// The delegate shape of <c>SimaiParser.ParseChart(string, string, ReadOnlySpan&lt;char&gt;)</c>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The legacy <c>SimaiChart</c> instance.</returns>
    private delegate object ParseChartDelegate(string level, string designer, ReadOnlySpan<char> fumen);

    /// <summary>
    /// The shared parser, loaded on first use.
    /// </summary>
    private static readonly Lazy<LegacyParser> s_instance = new(() => new LegacyParser(DefaultAssemblyPath));

    /// <summary>
    /// Calls <c>SimaiParser.Parse(Stream)</c>.
    /// </summary>
    private readonly Func<Stream, object> _parseFile;

    /// <summary>
    /// Calls <c>SimaiParser.ParseChart(string, string, ReadOnlySpan&lt;char&gt;)</c>.
    /// </summary>
    private readonly ParseChartDelegate _parseChart;

    /// <summary>
    /// <c>SimaiParser.ParseMetadata(Stream)</c>, which returns a struct.
    /// </summary>
    private readonly MethodInfo _parseMetadata;

    /// <summary>
    /// <c>SimaiParser.Deparse(SimaiFile)</c>.
    /// </summary>
    private readonly MethodInfo _deparse;

    /// <summary>
    /// Reads <c>fileTitle</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _fileTitle;

    /// <summary>
    /// Reads <c>fileArtist</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _fileArtist;

    /// <summary>
    /// Reads <c>fileFinalDesigner</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _fileFinalDesigner;

    /// <summary>
    /// Reads <c>fileOffset</c> from a legacy object.
    /// </summary>
    private readonly Func<object, float> _fileOffset;

    /// <summary>
    /// Reads <c>fileHash</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _fileHash;

    /// <summary>
    /// Reads <c>fileCharts</c> from a legacy object.
    /// </summary>
    private readonly Func<object, Array> _fileCharts;

    /// <summary>
    /// Reads <c>fileCommands</c> from a legacy object.
    /// </summary>
    private readonly Func<object, IEnumerable> _fileCommands;

    /// <summary>
    /// Reads <c>commandPrefix</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _commandPrefix;

    /// <summary>
    /// Reads <c>commandValue</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _commandValue;

    /// <summary>
    /// Reads <c>chartLevel</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _chartLevel;

    /// <summary>
    /// Reads <c>chartDesigner</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _chartDesigner;

    /// <summary>
    /// Reads <c>chartFumen</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _chartFumen;

    /// <summary>
    /// Reads <c>chartIsEmpty</c> from a legacy object.
    /// </summary>
    private readonly Func<object, bool> _chartIsEmpty;

    /// <summary>
    /// Reads <c>chartNoteTimings</c> from a legacy object.
    /// </summary>
    private readonly Func<object, Array> _chartNoteTimings;

    /// <summary>
    /// Reads <c>chartCommaTimings</c> from a legacy object.
    /// </summary>
    private readonly Func<object, Array> _chartCommaTimings;

    /// <summary>
    /// Reads <c>timingTiming</c> from a legacy object.
    /// </summary>
    private readonly Func<object, double> _timingTiming;

    /// <summary>
    /// Reads <c>timingBpm</c> from a legacy object.
    /// </summary>
    private readonly Func<object, float> _timingBpm;

    /// <summary>
    /// Reads <c>timingHSpeed</c> from a legacy object.
    /// </summary>
    private readonly Func<object, float> _timingHSpeed;

    /// <summary>
    /// Reads <c>timingSVeloc</c> from a legacy object.
    /// </summary>
    private readonly Func<object, float> _timingSVeloc;

    /// <summary>
    /// Reads <c>timingRawContent</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _timingRawContent;

    /// <summary>
    /// Reads <c>timingPositionX</c> from a legacy object.
    /// </summary>
    private readonly Func<object, int> _timingPositionX;

    /// <summary>
    /// Reads <c>timingPositionY</c> from a legacy object.
    /// </summary>
    private readonly Func<object, int> _timingPositionY;

    /// <summary>
    /// Reads <c>timingPosition</c> from a legacy object.
    /// </summary>
    private readonly Func<object, int> _timingPosition;

    /// <summary>
    /// Reads <c>timingSignatureNumerator</c> from a legacy object.
    /// </summary>
    private readonly Func<object, int> _timingSignatureNumerator;

    /// <summary>
    /// Reads <c>timingSignatureDenominator</c> from a legacy object.
    /// </summary>
    private readonly Func<object, int> _timingSignatureDenominator;

    /// <summary>
    /// Reads <c>timingIsEmpty</c> from a legacy object.
    /// </summary>
    private readonly Func<object, bool> _timingIsEmpty;

    /// <summary>
    /// Reads <c>timingNotes</c> from a legacy object.
    /// </summary>
    private readonly Func<object, Array> _timingNotes;

    /// <summary>
    /// Reads <c>readNote</c> from a legacy object.
    /// </summary>
    private readonly Func<object, LegacyNoteView> _readNote;

    /// <summary>
    /// Reads <c>metadataTitle</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _metadataTitle;

    /// <summary>
    /// Reads <c>metadataArtist</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _metadataArtist;

    /// <summary>
    /// Reads <c>metadataFinalDesigner</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _metadataFinalDesigner;

    /// <summary>
    /// Reads <c>metadataOffset</c> from a legacy object.
    /// </summary>
    private readonly Func<object, float> _metadataOffset;

    /// <summary>
    /// Reads <c>metadataHash</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string> _metadataHash;

    /// <summary>
    /// Reads <c>metadataDesigners</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string[]> _metadataDesigners;

    /// <summary>
    /// Reads <c>metadataLevels</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string[]> _metadataLevels;

    /// <summary>
    /// Reads <c>metadataFumens</c> from a legacy object.
    /// </summary>
    private readonly Func<object, string[]> _metadataFumens;

    /// <summary>
    /// Reads <c>metadataCommands</c> from a legacy object.
    /// </summary>
    private readonly Func<object, Array> _metadataCommands;

    /// <summary>
    /// Loads the legacy parser from an assembly file.
    /// </summary>
    /// <param name="assemblyPath">The path of the MajSimai 2.x assembly.</param>
    /// <exception cref="FileNotFoundException">Thrown when <paramref name="assemblyPath"/> does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the assembly does not have the expected API.</exception>
    public LegacyParser(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new FileNotFoundException("The legacy MajSimai assembly was not found.", assemblyPath);
        }
        var context = new AssemblyLoadContext("MajSimai.Legacy");
        Assembly = context.LoadFromAssemblyPath(assemblyPath);

        var parserType = GetLegacyType("MajSimai.SimaiParser");
        var fileType = GetLegacyType("MajSimai.SimaiFile");
        var chartType = GetLegacyType("MajSimai.SimaiChart");
        var timingType = GetLegacyType("MajSimai.SimaiTimingPoint");
        var noteType = GetLegacyType("MajSimai.SimaiNote");
        var commandType = GetLegacyType("MajSimai.SimaiCommand");
        var metadataType = GetLegacyType("MajSimai.SimaiMetadata");

        _parseFile = (Func<Stream, object>)GetMethod(parserType, "Parse", typeof(Stream)).CreateDelegate(typeof(Func<Stream, object>));
        _parseChart = (ParseChartDelegate)GetMethod(parserType, "ParseChart", typeof(string), typeof(string), typeof(ReadOnlySpan<char>))
            .CreateDelegate(typeof(ParseChartDelegate));
        _parseMetadata = GetMethod(parserType, "ParseMetadata", typeof(Stream));
        _deparse = GetMethod(parserType, "Deparse", fileType);

        _fileTitle = BuildGetter<string>(fileType, "Title");
        _fileArtist = BuildGetter<string>(fileType, "Artist");
        _fileFinalDesigner = BuildGetter<string>(fileType, "FinalDesigner");
        _fileOffset = BuildGetter<float>(fileType, "Offset");
        _fileHash = BuildGetter<string>(fileType, "Hash");
        _fileCharts = BuildGetter<Array>(fileType, "Charts");
        _fileCommands = BuildGetter<IEnumerable>(fileType, "Commands");

        _commandPrefix = BuildGetter<string>(commandType, "Prefix");
        _commandValue = BuildGetter<string>(commandType, "Value");

        _chartLevel = BuildGetter<string>(chartType, "Level");
        _chartDesigner = BuildGetter<string>(chartType, "Designer");
        _chartFumen = BuildGetter<string>(chartType, "Fumen");
        _chartIsEmpty = BuildGetter<bool>(chartType, "IsEmpty");
        _chartNoteTimings = BuildGetter<Array>(chartType, "_noteTimings");
        _chartCommaTimings = BuildGetter<Array>(chartType, "_commaTimings");

        _timingTiming = BuildGetter<double>(timingType, "Timing");
        _timingBpm = BuildGetter<float>(timingType, "Bpm");
        _timingHSpeed = BuildGetter<float>(timingType, "HSpeed");
        _timingSVeloc = BuildGetter<float>(timingType, "SVeloc");
        _timingRawContent = BuildGetter<string>(timingType, "RawContent");
        _timingPositionX = BuildGetter<int>(timingType, "RawTextPositionX");
        _timingPositionY = BuildGetter<int>(timingType, "RawTextPositionY");
        _timingPosition = BuildGetter<int>(timingType, "RawTextPosition");
        _timingSignatureNumerator = BuildGetter<int>(timingType, "SignatureNumerator");
        _timingSignatureDenominator = BuildGetter<int>(timingType, "SignatureDenominator");
        _timingIsEmpty = BuildGetter<bool>(timingType, "IsEmpty");
        _timingNotes = BuildGetter<Array>(timingType, "Notes");

        _readNote = BuildNoteReader(noteType);

        _metadataTitle = BuildGetter<string>(metadataType, "Title");
        _metadataArtist = BuildGetter<string>(metadataType, "Artist");
        _metadataFinalDesigner = BuildGetter<string>(metadataType, "FinalDesigner");
        _metadataOffset = BuildGetter<float>(metadataType, "Offset");
        _metadataHash = BuildGetter<string>(metadataType, "Hash");
        _metadataDesigners = BuildGetter<string[]>(metadataType, "_designers");
        _metadataLevels = BuildGetter<string[]>(metadataType, "_levels");
        _metadataFumens = BuildGetter<string[]>(metadataType, "_fumens");
        _metadataCommands = BuildGetter<Array>(metadataType, "_commands");
    }

    /// <summary>
    /// Gets the shared legacy parser loaded from <see cref="DefaultAssemblyPath"/>.
    /// </summary>
    public static LegacyParser Instance => s_instance.Value;

    /// <summary>
    /// Gets the path the test project copies the MajSimai 2.2.3 assembly to.
    /// </summary>
    public static string DefaultAssemblyPath => Path.Combine(AppContext.BaseDirectory, "Legacy", "MajSimai.dll");

    /// <summary>
    /// Gets the loaded legacy assembly.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Parses a whole simai file with <c>SimaiParser.Parse(Stream)</c>.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <param name="legacyFile">Receives the legacy <c>SimaiFile</c> instance, which <see cref="Deparse"/> accepts.</param>
    /// <returns>The parsed file.</returns>
    public LegacyFileView ParseFile(byte[] content, out object legacyFile)
    {
        using var stream = new MemoryStream(content, false);
        legacyFile = Unwrap(() => _parseFile(stream));

        return ReadFile(legacyFile);
    }

    /// <summary>
    /// Parses a whole simai file with <c>SimaiParser.Parse(Stream)</c> without reading the result, for benchmarks.
    /// Markup errors in the metadata are ignored.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    public void ParseRaw(byte[] content)
    {
        using var stream = new MemoryStream(content, false);
        try
        {
            _parseFile(stream);
        }
        catch (FormatException)
        {
        }
    }

    /// <summary>
    /// Parses only the metadata of a simai file with <c>SimaiParser.ParseMetadata(Stream)</c>.
    /// </summary>
    /// <param name="content">The raw bytes of the file.</param>
    /// <returns>The parsed metadata.</returns>
    public LegacyMetadataView ParseMetadata(byte[] content)
    {
        using var stream = new MemoryStream(content, false);
        var metadata = Unwrap(() => _parseMetadata.Invoke(null, new object[] { stream })!);

        return ReadMetadata(metadata);
    }

    /// <summary>
    /// Parses a chart with <c>SimaiParser.ParseChart(string, string, ReadOnlySpan&lt;char&gt;)</c>.
    /// </summary>
    /// <param name="level">The level text of the chart.</param>
    /// <param name="designer">The designer of the chart.</param>
    /// <param name="fumen">The chart text.</param>
    /// <returns>The parsed chart.</returns>
    public LegacyChartView ParseChart(string level, string designer, string fumen)
    {
        return ReadChart(_parseChart(level, designer, fumen));
    }

    /// <summary>
    /// Converts a legacy <c>SimaiFile</c> back to text with <c>SimaiParser.Deparse(SimaiFile)</c>.
    /// </summary>
    /// <param name="legacyFile">A file returned through <see cref="ParseFile"/>.</param>
    /// <returns>The simai text.</returns>
    public string Deparse(object legacyFile)
    {
        return (string)Unwrap(() => _deparse.Invoke(null, new[] { legacyFile })!);
    }

    /// <summary>
    /// Runs a reflection call and rethrows the exception of the called method.
    /// </summary>
    /// <param name="action">The call.</param>
    /// <returns>The result of the call.</returns>
    private static T Unwrap<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Reads a legacy file.
    /// </summary>
    /// <param name="file">The legacy <c>SimaiFile</c>.</param>
    /// <returns>The view.</returns>
    private LegacyFileView ReadFile(object file)
    {
        var charts = _fileCharts(file);
        var chartViews = new LegacyChartView[charts.Length];
        for (var i = 0; i < charts.Length; i++)
        {
            chartViews[i] = ReadChart(charts.GetValue(i)!);
        }
        var commands = new List<KeyValuePair<string, string>>();
        foreach (var command in _fileCommands(file))
        {
            commands.Add(new KeyValuePair<string, string>(_commandPrefix(command!), _commandValue(command!)));
        }

        return new LegacyFileView(_fileTitle(file),
                                  _fileArtist(file),
                                  _fileFinalDesigner(file),
                                  _fileOffset(file),
                                  _fileHash(file),
                                  chartViews,
                                  commands.ToArray());
    }

    /// <summary>
    /// Reads legacy metadata.
    /// </summary>
    /// <param name="metadata">The legacy <c>SimaiMetadata</c>.</param>
    /// <returns>The view.</returns>
    private LegacyMetadataView ReadMetadata(object metadata)
    {
        var commands = _metadataCommands(metadata);
        var commandViews = new KeyValuePair<string, string>[commands.Length];
        for (var i = 0; i < commands.Length; i++)
        {
            var command = commands.GetValue(i)!;
            commandViews[i] = new KeyValuePair<string, string>(_commandPrefix(command), _commandValue(command));
        }

        return new LegacyMetadataView(_metadataTitle(metadata),
                                      _metadataArtist(metadata),
                                      _metadataFinalDesigner(metadata),
                                      _metadataOffset(metadata),
                                      _metadataHash(metadata),
                                      (string[])_metadataDesigners(metadata).Clone(),
                                      (string[])_metadataLevels(metadata).Clone(),
                                      (string[])_metadataFumens(metadata).Clone(),
                                      commandViews);
    }

    /// <summary>
    /// Reads a legacy chart.
    /// </summary>
    /// <param name="chart">The legacy <c>SimaiChart</c>.</param>
    /// <returns>The view.</returns>
    private LegacyChartView ReadChart(object chart)
    {
        return new LegacyChartView(_chartLevel(chart),
                                   _chartDesigner(chart),
                                   _chartFumen(chart),
                                   _chartIsEmpty(chart),
                                   ReadTimings(_chartNoteTimings(chart)),
                                   ReadTimings(_chartCommaTimings(chart)));
    }

    /// <summary>
    /// Reads legacy timing points.
    /// </summary>
    /// <param name="timings">The legacy <c>SimaiTimingPoint</c> array.</param>
    /// <returns>The views.</returns>
    private LegacyTimingView[] ReadTimings(Array timings)
    {
        var views = new LegacyTimingView[timings.Length];
        for (var i = 0; i < timings.Length; i++)
        {
            var timing = timings.GetValue(i)!;
            var notes = _timingNotes(timing);
            var noteViews = new LegacyNoteView[notes.Length];
            for (var j = 0; j < notes.Length; j++)
            {
                noteViews[j] = _readNote(notes.GetValue(j)!);
            }
            views[i] = new LegacyTimingView(_timingTiming(timing),
                                            _timingBpm(timing),
                                            _timingHSpeed(timing),
                                            _timingSVeloc(timing),
                                            _timingRawContent(timing),
                                            _timingPositionX(timing),
                                            _timingPositionY(timing),
                                            _timingPosition(timing),
                                            _timingSignatureNumerator(timing),
                                            _timingSignatureDenominator(timing),
                                            _timingIsEmpty(timing),
                                            noteViews);
        }

        return views;
    }

    /// <summary>
    /// Gets a type of the legacy assembly.
    /// </summary>
    /// <param name="fullName">The full type name.</param>
    /// <returns>The type.</returns>
    private Type GetLegacyType(string fullName)
    {
        return Assembly.GetType(fullName, true)!;
    }

    /// <summary>
    /// Gets a public static method.
    /// </summary>
    /// <param name="type">The declaring type.</param>
    /// <param name="name">The method name.</param>
    /// <param name="parameterTypes">The parameter types.</param>
    /// <returns>The method.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the method does not exist.</exception>
    private static MethodInfo GetMethod(Type type, string name, params Type[] parameterTypes)
    {
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null)
            ?? throw new InvalidOperationException($"The legacy assembly has no method {type.Name}.{name}.");
    }

    /// <summary>
    /// Compiles a fast getter for a public property or a private instance field of a legacy type.
    /// </summary>
    private static Func<object, T> BuildGetter<T>(Type type, string memberName)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var typed = Expression.Convert(instance, type);
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Expression member = type.GetProperty(memberName, Flags) is { } property
            ? Expression.Property(typed, property)
            : Expression.Field(typed, type.GetField(memberName, Flags)
                ?? throw new InvalidOperationException($"The legacy type {type.Name} has no member {memberName}."));

        return Expression.Lambda<Func<object, T>>(Expression.Convert(member, typeof(T)), instance).Compile();
    }

    /// <summary>
    /// Compiles a function that copies every property of a legacy <c>SimaiNote</c> into a <see cref="LegacyNoteView"/>.
    /// </summary>
    private static Func<object, LegacyNoteView> BuildNoteReader(Type noteType)
    {
        var instance = Expression.Parameter(typeof(object), "instance");
        var note = Expression.Variable(noteType, "note");
        var constructor = typeof(LegacyNoteView).GetConstructors()[0];
        var arguments = new List<Expression>();
        foreach (var parameter in constructor.GetParameters())
        {
            var property = noteType.GetProperty(parameter.Name!)
                ?? throw new InvalidOperationException($"The legacy SimaiNote has no property {parameter.Name}.");
            arguments.Add(Expression.Convert(Expression.Property(note, property), parameter.ParameterType));
        }
        var body = Expression.Block(new[] { note },
                                    Expression.Assign(note, Expression.Convert(instance, noteType)),
                                    Expression.New(constructor, arguments));

        return Expression.Lambda<Func<object, LegacyNoteView>>(body, instance).Compile();
    }
}
