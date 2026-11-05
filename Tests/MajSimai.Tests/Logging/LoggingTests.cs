using System;
using System.IO;
using System.Linq;
using MajSimai.Logging;
using Xunit;

namespace MajSimai.Tests.Logging;

/// <summary>
/// Tests <see cref="SimaiLogger"/>, <see cref="ConsoleSimaiLogger"/> and the messages the parser writes.
/// </summary>
/// <remarks>
/// The tests replace the global logger, so they do not run in parallel with each other.
/// </remarks>
[Collection(nameof(LoggingTests))]
[CollectionDefinition(nameof(LoggingTests), DisableParallelization = true)]
public sealed class LoggingTests
{
    /// <summary>
    /// Current: rejects null.
    /// </summary>
    [Fact]
    public void Current_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => SimaiLogger.Current = null!);
    }

    /// <summary>
    /// Helpers: write at their levels.
    /// </summary>
    [Fact]
    public void Helpers_WriteAtTheirLevels()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Debug);
        Use(logger, () =>
        {
            var error = new InvalidOperationException();
            SimaiLogger.Debug("d");
            SimaiLogger.Info("i");
            SimaiLogger.Warning("w");
            SimaiLogger.Error("e", error);

            Assert.True(SimaiLogger.IsEnabled(SimaiLogLevel.Debug));
            var entries = logger.Entries.ToArray();
            Assert.Equal(new[] { SimaiLogLevel.Debug, SimaiLogLevel.Info, SimaiLogLevel.Warning, SimaiLogLevel.Error }, entries.Select(e => e.Level));
            Assert.Equal(new[] { "d", "i", "w", "e" }, entries.Select(e => e.Message));
            Assert.Same(error, entries[3].Exception);
        });
    }

    /// <summary>
    /// Helpers: skip disabled levels.
    /// </summary>
    [Fact]
    public void Helpers_SkipDisabledLevels()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Error);
        Use(logger, () =>
        {
            SimaiLogger.Warning("w");
            SimaiLogger.Error("e");

            Assert.Equal("e", Assert.Single(logger.Entries).Message);
            Assert.False(SimaiLogger.IsEnabled(SimaiLogLevel.Warning));
        });
    }

    /// <summary>
    /// Failed chart: is logged as error.
    /// </summary>
    [Fact]
    public void FailedChart_IsLoggedAsError()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Debug);
        Use(logger, () =>
        {
            SimaiParser.Parse("&title=Song\n&inote_3=(x)1,".AsSpan(), "h");

            var entry = Assert.Single(logger.Entries, e => e.Level == SimaiLogLevel.Error);
            Assert.Contains("chart 3", entry.Message);
            Assert.Contains("Song", entry.Message);
            Assert.IsType<InvalidSimaiSyntaxException>(entry.Exception);
            Assert.Contains(logger.Entries, e => e.Level == SimaiLogLevel.Info && e.Message.Contains("Song"));
        });
    }

    /// <summary>
    /// Unreadable notes: are logged as debug.
    /// </summary>
    [Fact]
    public void UnreadableNotes_AreLoggedAsDebug()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Debug);
        Use(logger, () =>
        {
            SimaiParser.ParseChart("(120)1-5/2,1-b5[8:1],9*-x,");

            var messages = logger.Entries.Where(e => e.Level == SimaiLogLevel.Debug).Select(e => e.Message).ToArray();
            Assert.Contains(messages, m => m.Contains("Cannot read the note \"1-5\""));
            Assert.Contains(messages, m => m.Contains("has no effect") && m.Contains("SM207"));
            Assert.Contains(messages, m => m.Contains("Cannot read the slide \"9-x\""));
        });
    }

    /// <summary>
    /// Dropped text: is logged as a warning.
    /// </summary>
    [Fact]
    public void DroppedText_IsLoggedAsAWarning()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Warning);
        Use(logger, () =>
        {
            SimaiParser.ParseChart("(120)1(120)2,3,E,4,5");
            SimaiParser.ParseMetadata("&broken".AsSpan(), string.Empty);

            var messages = logger.Entries.Select(e => e.Message).ToArray();
            Assert.Contains(messages, m => m.Contains("is cut off by a declaration"));
            Assert.Contains(messages, m => m.Contains("after the end marker"));
            Assert.Contains(messages, m => m.Contains("has no '='"));
            Assert.All(logger.Entries, e => Assert.Equal(SimaiLogLevel.Warning, e.Level));
        });
    }

    /// <summary>
    /// Skipped notes: are logged as warnings with their codes.
    /// </summary>
    [Fact]
    public void SkippedNotes_AreLoggedAsWarningsWithTheirCodes()
    {
        var logger = new CapturingLogger(SimaiLogLevel.Warning);
        Use(logger, () =>
        {
            var chart = SimaiParser.ParseChart("(120)1/b,12E,3-5[8:1]*,1h[8:1:2],");

            Assert.Equal(4, chart.NoteTimings.Length);
            var messages = logger.Entries.Select(e => e.Message).ToArray();
            Assert.Contains(messages, m => m.Contains("has no button or sensor") && m.Contains("SM201"));
            Assert.Contains(messages, m => m.Contains("touch area letter") && m.Contains("SM202"));
            Assert.Contains(messages, m => m.Contains("has an empty part") && m.Contains("SM203"));
            Assert.Contains(messages, m => m.Contains("too many ':' or '#'") && m.Contains("SM204"));
            Assert.All(logger.Entries, e => Assert.Equal(SimaiLogLevel.Warning, e.Level));
        });
    }

    /// <summary>
    /// Console logger: defaults to warning.
    /// </summary>
    [Fact]
    public void ConsoleLogger_DefaultsToWarning()
    {
        var logger = new ConsoleSimaiLogger();

        Assert.Equal(SimaiLogLevel.Warning, logger.MinimumLevel);
        Assert.False(logger.IsEnabled(SimaiLogLevel.Info));
        Assert.True(logger.IsEnabled(SimaiLogLevel.Warning));
        Assert.True(logger.IsEnabled(SimaiLogLevel.Error));
        Assert.False(logger.IsEnabled(SimaiLogLevel.None));
        Assert.False(new ConsoleSimaiLogger(SimaiLogLevel.None).IsEnabled(SimaiLogLevel.Error));
    }

    /// <summary>
    /// Console logger: writes enabled messages.
    /// </summary>
    [Fact]
    public void ConsoleLogger_WritesEnabledMessages()
    {
        var previous = Console.Out;
        var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            var logger = new ConsoleSimaiLogger(SimaiLogLevel.Info);
            logger.Log(SimaiLogLevel.Debug, "hidden", null);
            logger.Log(SimaiLogLevel.Info, "shown", null);
            logger.Log(SimaiLogLevel.Error, "failed", new InvalidOperationException("boom"));
        }
        finally
        {
            Console.SetOut(previous);
        }
        var text = writer.ToString();

        Assert.DoesNotContain("hidden", text);
        Assert.Contains("[MajSimai] [Info] shown", text);
        Assert.Contains("[MajSimai] [Error] failed", text);
        Assert.Contains("boom", text);
    }

    /// <summary>
    /// Runs an action with a logger installed, then restores the previous logger.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="action">The action.</param>
    private static void Use(ISimaiLogger logger, Action action)
    {
        var previous = SimaiLogger.Current;
        SimaiLogger.Current = logger;
        try
        {
            action();
        }
        finally
        {
            SimaiLogger.Current = previous;
        }
    }
}
