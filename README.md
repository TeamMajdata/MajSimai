# MajSimai

MajSimai is an interpreter for [Simai](https://w.atwiki.jp/simai/), written in [C#](https://learn.microsoft.com/en-us/dotnet/csharp/).

## Features

### Metadata

- [x] Basic metadata fields
- [x] Custom simai commands with '=' as separator `&commandPrefix=value`

### Chart

- [x] Comments `||Hello world!` (to the end of the line, only inside `&inote_N` chart text)
- [x] Time signature `||s3/4`
- [x] BPM declaration `(float)`
- [x] Beats declaration
  - [x] Common `{int}`
  - [x] Absolute time `{#float}`
- [x] Tap `1`
  - [x] Break flag `1b`
  - [x] EX flag `1x`
  - [x] Mine flag `1m`
  - [x] Force star flag `1$`
    - [x] Fake rotation `1$$`
- [x] Hold `1h[duration]`
  - [x] Short form `1h`
  - [x] Break flag `1hb`
  - [x] Mine flag `1bm`
  - Duration format:
    - [x] Common `[division:beats]`
    - [x] With absolute time `[#time]`
    - [x] With custom BPM `[bpm#division:beats]`
- [x] Slide `x-y[duration]`
  - [x] Same head `1-3[duration]*-7[duration]`
  - [x] Break flag
    - `1-3[duration]b`
    - `1-3b[duration]`
  - [x] Mine flag
    - `1-3[duration]m`
    - `1-3m[duration]`
  - [x] Conn slide
    - `1-3-5-7[duration]`
    - `1-3[duration]-5[duration]-7[duration]`
  - [x] No head slide:
    - [x] `1?-3[duration]`
    - [x] `1!-3[duration]`
  - [x] Tap head slide: `1@-3[duration]`
  - Duration format:
    - [x] Common `[division:beats]`
    - [x] With _custom BPM_ `[bpm#division:beats]`
    - [x] With _custom BPM_ and **absolute slide length** `[bpm#time]`
    - [x] With **absolute slide start time** and common slide length `[time##division:beats]`
    - [x] With **absolute slide start time** and **absolute slide length** `[time##time]`
    - [x] With **absolute slide start time** and **custom BPM length** `[time##bpm#division:beats]`
- [x] Touch `B1`
  - [x] Hanabi flag `B1f`
  - [x] Break flag `B1b`
  - [x] Mine flag `B1m`
  - [x] Ex flag `B1x`
- [x] Touch hold `B1h[duration]`
  - [x] Short form `B1h`
  - [x] Hanabi flag `B1hf[duration]`
  - [x] Break flag `B1hb[duration]`
  - [x] Mine flag `B1hm[duration]`
  - [x] Ex flag `B1hx[duration]`
  - Duration format:
    - [x] Common `[division:beats]`
    - [x] With absolute time `[#time]`
    - [x] With custom BPM `[bpm#division:beats]`
- [x] Each note `note/note`
  - [x] Fake each ``1`2`3`4,``
- [x] EOF flag `E` (an `E` where a note starts that is not a sensor such as `E1`)
- [x] Slide Code `1A357K1[2:1]` ([MajSimai docs](https://docs.majdata.net/majsimai/index.html))

## Getting Started

To use MajSimai in your own project, you will need to add a reference to the MajSimai library in your solution.

```C#
// Specify the chart file
var filePath = "/path/to/your/chart.txt";
using var fileStream = File.OpenRead(filePath);

// Parse into SimaiFile
var simaiFile = await SimaiParser.ParseAsync(fileStream);
// With special encoding
simaiFile = await SimaiParser.ParseAsync(fileStream, Encoding.ASCII);

// Or parse only metadata
var simaiMetadata = await SimaiParser.ParseMetadataAsync(fileStream);
// With special encoding
simaiMetadata = await SimaiParser.ParseMetadataAsync(fileStream, Encoding.ASCII);


// Or parse from the chart text you provide
var fumen = "(197){8}1,1,1,1,1,1,1,1,2b,3h,4-1[8:1],";
var simaiChart = await SimaiParser.ParseChartAsync(fumen);

```

## Note model

Since 3.0, a slide is read as its star head plus its slide paths (issue #12):

- A slide with a head, such as `1-5[8:1]`, becomes a `Tap` note followed by a `Slide` note. The `Tap` is the star
  head: its `RawContent` is the text before the first slide mark (`1`), and `RelatedNotes` lists its slides.
- The later parts of a same-head group (`1-3[8:1]*-5[8:1]`) are also listed in the `RelatedNotes` of the head.
- A slide without a head (`1?-5[8:1]`, `1!-5[8:1]`) is a single `Slide` note.
- On a `Slide` note, `IsBreak` and `IsMine` describe the slide path (they were `IsSlideBreak` and `IsMineSlide`
  before 3.0). `IsSlideNoHead` was removed.

Every note also has these flags:

| Property | Meaning |
| --- | --- |
| `IsEach` | A slide is an each when its timing point has more than one slide. Any other note is an each when its timing point has more than one note that is not a slide. |
| `IsStar` | A `Tap` that uses `$`, or that has related slides and does not use `@`. |
| `IsStarDouble` | A star with related slides whose timing point has more than one slide, not a mine, starting from the same button. |

`SimaiChart.CommaTimings` is created the first time it is read, which saves most of the memory of a parse when it is
not used.

## Syntax tolerance

Every `Parse*` method takes an optional `SimaiParserOptions`:

```C#
// Lenient (the default): reads charts the way MajSimai always has
var file = SimaiParser.Parse(stream);

// Strict: throws for every note that cannot be read and every text the grammar does not define
var strictFile = SimaiParser.Parse(stream, SimaiParserOptions.Strict);
```

| | Lenient | Strict |
| --- | --- | --- |
| A declaration that is not closed, or whose value cannot be read; a single `\|` | throws | throws |
| A malformed note (see below) | skipped (Warning log) | throws |
| A note that cannot be read, such as `1-5` without a duration | skipped (Debug log) | throws |
| Note text cut off by a declaration, `1(120)2,` | `1` dropped (Warning log) | throws |
| Note text without a comma at the end of a chart | dropped (Warning log) | throws |
| Stray characters, empty `/` or `` ` `` parts, `b` / `m` in the middle of a slide path, a malformed `\|\|s` signature | ignored | throws |
| Text after the end marker `E` | ignored (Warning log) | throws |
| Note shapes: buttons 1-8, sensors `A1`-`E8` / `C` / `C1` / `C2`, hold and touch layouts | not checked | checked |
| Slide paths: end buttons allowed for each shape, durations, Slide Code | not checked | checked |
| `&` line without `=` / unreadable `&first` | ignored (Warning log) | throws |
| A chart that fails in a file | kept with its text and no timing points (Error log) | the whole parse throws |

Exceptions:

- `InvalidSimaiSyntaxException` (both modes): an unclosed `(`, `{` or `<` declaration, or a BPM, beat or HS / SV value
  that cannot be read.
- `InvalidSimaiMarkupException` (both modes): a single `|`.
- `InvalidSimaiNoteException` (strict mode only): a malformed note, that is a note with nothing left after its markers
  (`1/b`), a touch area letter that is not at the start of a note (`12E`, `1-5E[8:1]`), an empty `*` part
  (`1-5[8:1]*`; the other parts of the group are kept), a duration with too many separators (`[8:1:2]`,
  `[1#2#3#4#5]`) or with text between `#` (`[3#9#1.5]`), or a hold whose `]` comes before its `[`. Lenient mode skips
  these notes and logs a `Warning`. Strict mode also throws it for every other note rule in the table above.

The position of an exception is the line and column of the declaration, or of the start of the timing point that holds
the note. Numbers are read with the invariant culture; exponents, `NaN` and `Infinity` are accepted, thousands
separators are not.

## Syntax checker

`SimaiSyntaxChecker` reports every problem of a file or chart instead of stopping at the first one:

```C#
foreach (var diagnostic in SimaiSyntaxChecker.Check(File.ReadAllText(path)))
{
    Console.WriteLine(diagnostic); // inote_5 34:6: Error SM301: The slide "1-2" in "1-2[8:1]" cannot be drawn: ...
}
var chartDiagnostics = SimaiSyntaxChecker.CheckChart("(120){4}1-2[8:1],");
```

An `Error` breaks the grammar: a text parses in strict mode exactly when it has no errors. A `Warning` is valid but
questionable. The checks include:

- buttons, sensors and the layout of every note;
- the end button of every slide shape, following the official simai table (for example `-` needs 2 to 6 buttons
  between start and end, `s`, `z` and `w` only end at the opposite button, `V` turns 2 buttons away);
  `1v1` is a Majdata extension and only gets a warning;
- connected slides: durations on every segment or only on the last one, and no `w` segment;
- Slide Code: command syntax and parameters, a `K` end, and connections that cannot be drawn (a node inside its circle,
  `P` directly to `Q`, circles 0 and 9); warnings for zero-length moves and three or more turns around one circle;
- fields set twice, `&` lines without `=`, and `&first` values that are not numbers.

Codes are listed in `SimaiDiagnosticCodes`: `SM1xx` chart structure, `SM2xx` notes, `SM3xx` slide paths, `SM4xx`
fields.

## Logging

MajSimai writes messages through `SimaiLogger.Current`. The default logger writes `Warning` and `Error` messages to the
console. In lenient mode a chart that fails to parse does not fail the file: it keeps its text, has no timing points, and is
logged as an `Error`. Text that is dropped is logged as a `Warning`, and notes that cannot be read as `Debug`.

```C#
// Show everything, including notes that cannot be read
SimaiLogger.Current = new ConsoleSimaiLogger(SimaiLogLevel.Debug);

// Or forward to your own logger by implementing ISimaiLogger
```

## Tests

```bash
dotnet run --project Tests/MajSimai.Tests
```

The tests compare the lenient parser with the published MajSimai 2.2.3 package (downloaded from nuget.org at restore),
on unit cases and on generated charts; the intended fixes listed above are aligned before comparing. To also compare every `maidata.txt` of a folder, download the samples and set
`MAJSIMAI_SAMPLES_DIR`:

```bash
python tools/download-samples.py "Simai Test Samples"
MAJSIMAI_SAMPLES_DIR="Simai Test Samples" dotnet run --project Tests/MajSimai.Tests -- -class "*SampleEquivalenceTests"
```

The sample tests also check that the syntax checker reports an error for exactly the files strict mode rejects, and
print how often each diagnostic occurs.

Set `MAJSIMAI_BENCHMARK=1` as well and run `-class "*ParserBenchmarkTests"` in Release to compare the speed of both
parsers.

`tools/MajSimai.ManagedBenchmark` measures the parser with [BenchmarkDotNet](https://benchmarkdotnet.org/), on the
samples named by `MAJSIMAI_SAMPLES_DIR` (or by `MAJSIMAI_BENCHMARK_CORPUS`, a corpus file written by
`tools/benchmark-native.py`). It takes the options of BenchmarkDotNet and runs every benchmark when no `--filter` is
given:

```bash
MAJSIMAI_SAMPLES_DIR="Simai Test Samples" dotnet run -c Release --project tools/MajSimai.ManagedBenchmark -f net10.0 -- --filter "*FileParseBenchmarks*"
MAJSIMAI_SAMPLES_DIR="Simai Test Samples" dotnet run -c Release --project tools/MajSimai.ManagedBenchmark -f net10.0 -- --filter "*FileParseBenchmarks*" --runtimes net10.0 nativeaot10.0
```

| Benchmark | One operation |
|---|---|
| `FileParseBenchmarks` | Parses one sample, the median, 99th percentile or largest by size (`Sample`) |
| `CorpusParseBenchmarks` | Parses every sample. An operation takes seconds, so add `--strategy Monitoring --warmupCount 1 --iterationCount 5` or let BenchmarkDotNet choose the counts |

Each class has two methods: `Parse` (`SimaiParser.Parse`, the baseline) and `ParseToNative` (`MajSimai_ParseWithTolerance`
and `MajSimai_Free` called in-process: the parse and the copy into native memory that the C++ wrapper asks for). The
memory diagnoser reports the managed memory allocated per operation. The benchmarks install a logger that keeps the
levels of the default logger, because some lenient checks run only when their level is logged, but writes nothing.
`--runtimes nativeaot10.0` needs the native AOT prerequisites (on Windows, the Visual Studio C++ build tools).

To compare the speed of the [cimai](https://github.com/re-poem/cimai) C parser, the C# parser and the C++ wrapper on the
samples, run `tools/benchmark-native.py`. It makes the package with `tools/build-cpp-package.py` (or takes
`--package`), builds `tools/NativeBenchmark` with CMake (cimai from the sources of `--cimai`), runs each native parser
over the samples in memory in its own process, and runs `CorpusParseBenchmarks` with BenchmarkDotNet on net10.0 with
the same warm-up and measured runs:

```bash
python tools/benchmark-native.py --cimai path/to/cimai --samples "Simai Test Samples" --kind shared --iterations 10
```

| Run | What is measured |
|---|---|
| `cimai` | `cimai_parse` and `cimai_file_release` |
| `majsimai-cs` | `SimaiParser.Parse` on the .NET runtime, called the way the native entry point calls it |
| `majsimai-cs-native-abi` | `MajSimai_ParseWithTolerance` and `MajSimai_Free` called in-process on the .NET runtime |
| `majsimai-cpp-view` | `SimaiParser::ParseAsView`: the parser compiled with native AOT, and the copy into native memory |
| `majsimai-cpp-model` | `SimaiParser::Parse`: as above, then a copy into `std::string` and `std::vector` |

The table and `report.md`/`report.json` in `artifacts/benchmark/<kind>` give the median time of a run over every file
and the throughput; the native rows add per-file percentiles, the C# rows the memory allocated per run (BenchmarkDotNet
writes its own reports to `artifacts/benchmark/<kind>/bdn/results`). `--parsers` picks the runs (the C# runs need
neither CMake nor cimai) and `--limit N` uses only the first N files for a quick check. On Windows the native runs need
the Visual Studio C++ build tools; CMake is taken from the path or from Visual Studio.

The C++ wrapper tests compile `Bindings/Cpp` with the compiler in `CXX` or the first of `c++`, `g++` and `clang++` on the
path, and are skipped without one; they also check that code misusing the owner of a result does not compile. To also
link a package with CMake and compare its results with the C# parser in lenient and strict mode (with
`MAJSIMAI_SAMPLES_DIR` set, on the first 200 samples as well), make a package and set `MAJSIMAI_CPP_PACKAGE`:

```bash
python tools/build-cpp-package.py --kind static
MAJSIMAI_CPP_PACKAGE=artifacts/cpp/linux-x64-static dotnet run --project Tests/MajSimai.Tests -- -class "*CppBindingNativeTests"
```

## Build

If you want to compile MajSimai yourself, please make sure that the .NET 10 SDK (or newer) is installed on your computer:
MajSimai is written in C# 14.

The target frameworks available for MajSimai are as follows:

- `netstandard2.1`
- `net5.0`
- `net6.0`
- `net7.0`
- `net8.0`
- `net9.0`
- `net10.0`

Then, use the following method to compile it:

```bash
git clone https://github.com/LingFeng-bbben/MajSimai.git

cd MajSimai

dotnet restore

# Feel free to change the target framework
# If you want to generate nuget package, please remove /p:GeneratePackageOnBuild=false
dotnet publish -c release /p:PublishAot=false /p:GeneratePackageOnBuild=false -f netstandard2.1

```

MajSimai is AOT and Trim compatible.

If you want to compile native library, you need to specify the use of AOT.

```bash
dotnet publish -c release /p:PublishAot=true -f net9.0 -r linux-x64
```

In the native library, MajSimai exports these functions (declared in `Runtime/Unmanaged/UnmanagedSimaiParser.cs`):

- `MajSimai_Parse(const char* content, int32_t contentLength, UnmanagedSimaiParseResult* result)` parses a file in
  lenient mode. The content is UTF-8 (a UTF-8 or UTF-16 byte order mark is honored).
- `MajSimai_ParseWithTolerance(const char* content, int32_t contentLength, SimaiSyntaxTolerance tolerance,
  UnmanagedSimaiParseResult* result)` parses in lenient (0) or strict (1) mode. In strict mode the first chart that
  breaks the grammar fails the parse with `CODE_INVALID_SIMAI_SYNTAX` or `CODE_INVALID_SIMAI_MARKUP`, and the line and
  column of the error are counted in the text of that chart.
- `MajSimai_Free(UnmanagedSimaiParseResult* result)` frees a result, whether parsing succeeded or not, and returns 1 (0
  for a null pointer).
- `MajSimai_FreeHGlobal(intptr_t ptr)` frees a block that MajSimai handed over to the caller and returns 1 (0 for a null
  pointer). No function hands memory over at the moment, so it is never needed for a parse result.

Ownership: the caller owns each result and frees it with `MajSimai_Free` exactly once. Everything the result points to
belongs to the result: never free those pointers, never keep them after `MajSimai_Free`, and never free two copies of
one result. The result passed to a parse function is overwritten without being freed, so it must be zero-initialized
or already freed. The signatures only use blittable types (`int32_t` instead of `bool`), so they work without
`DisableRuntimeMarshalling`.

The native layouts changed in 3.0: notes follow the 3.0 note model, related notes are indices into the notes of the
same timing point, and every text is UTF-8 with its length in bytes (2.x used the ANSI code page with lengths in UTF-16
characters). The native library logs through the default logger, so its warnings go to standard output.

## C++ wrapper

`Bindings/Cpp` is a header-only C++17 wrapper generated from the native ABI by `tools/MajSimai.CppGen`, and each release
publishes it with a static library per platform to [MajSimaiCpp](https://github.com/TeamMajdata/MajSimaiCpp). The
[README of MajSimaiCpp](https://github.com/TeamMajdata/MajSimaiCpp#readme) documents the API, the packages made by
`tools/build-cpp-package.py`, the release workflow and the rules of the ABI. Regenerate the wrapper after changing
`Runtime/Unmanaged`; a test fails while `Bindings/Cpp` is out of date:

```bash
dotnet run --project tools/MajSimai.CppGen               # writes Bindings/Cpp
dotnet run --project tools/MajSimai.CppGen -- --check    # exits with 1 when Bindings/Cpp is out of date
```
