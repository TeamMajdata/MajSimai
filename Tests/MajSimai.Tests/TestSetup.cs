using System.Runtime.CompilerServices;
using MajSimai.Logging;

namespace MajSimai.Tests;

/// <summary>
/// Settings applied once when the test assembly loads.
/// </summary>
internal static class TestSetup
{
    /// <summary>
    /// Silences the MajSimai logger, so expected chart failures do not flood the test output. Logging tests install
    /// their own logger.
    /// </summary>
    [ModuleInitializer]
    internal static void Initialize()
    {
        SimaiLogger.Current = QuietLogger;
    }

    /// <summary>
    /// Gets the logger used by default in tests: it writes nothing.
    /// </summary>
    public static ISimaiLogger QuietLogger { get; } = new ConsoleSimaiLogger(SimaiLogLevel.None);
}
