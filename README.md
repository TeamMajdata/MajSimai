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

## Note model (3.0)

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
| Malformed text (see below) | throws | throws |
| A note that cannot be read, such as `1-5` without a duration | skipped (Debug log) | throws |
| Note text cut off by a declaration, `1(120)2,` | `1` dropped (Warning log) | throws |
| Note text without a comma at the end of a chart | dropped (Warning log) | throws |
| Stray characters, empty `/` or `` ` `` parts, `b` / `m` in the middle of a slide path, a malformed `\|\|s` signature | ignored | throws |
| Text after the end marker `E` | ignored (Warning log) | throws |
| Note shapes: buttons 1-8, sensors `A1`-`E8` / `C` / `C1` / `C2`, hold and touch layouts | not checked | checked |
| Slide paths: end buttons allowed for each shape, durations, Slide Code | not checked | checked |
| `&` line without `=` / unreadable `&first` | ignored (Warning log) | throws |
| A chart that fails in a file | kept with its text and no timing points (Error log) | the whole parse throws |

Malformed text throws in both modes:

- `InvalidSimaiSyntaxException`: an unclosed `(`, `{` or `<` declaration, or a BPM, beat or HS / SV value that cannot
  be read.
- `InvalidSimaiNoteException`: a note with nothing left after its markers (`1/b`), a touch area letter that is not at
  the start of a note (`12E`, `1-5E[8:1]`), an empty `*` part (`1-5[8:1]*`), a duration with too many separators
  (`[8:1:2]`, `[1#2#3#4#5]`) or with text between `#` (`[3#9#1.5]`), and a hold whose `]` comes before its `[`.
- `InvalidSimaiMarkupException`: a single `|`.

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

## Changes from 2.2.3

Apart from the note model, lenient mode reads charts as MajSimai 2.2.3 did, except for these fixes:

- Columns are the real columns of the text (2.x counted declarations and comments wrongly).
- `||` at the very end of a chart is a comment (2.x threw an index error).
- Note timing points carry the SV and time signature in effect, and comma timing points the HS in effect (2.x used 1
  and 0/0).
- The end marker `E` ends the chart.
- A fake each keeps all its parts (2.x lost parts after the eighth).
- Field names and values are trimmed: `&title = Song` is the title `Song`.
- Parsing no longer changes `CultureInfo.DefaultThreadCurrentCulture`.
- The malformed text listed above throws instead of being read as a different note or silently dropping the rest of
  its timing point.

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

## Build

If you want to compile MajSimai yourself, please make sure that `dotnet` is installed on your computer.

The target frameworks available for MajSimai are as follows:

- `netstandard2.1`
- `net5.0`
- `net6.0`
- `net7.0`
- `net8.0`
- `net9.0`

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

In the native library, MajSimai will export functions (the native note layout changed in 3.0: see
`Runtime/Unmanaged/UnmanagedSimaiNote.cs`; related notes are indices into the notes of the same timing point):

- `MajSimai!MajSimai_Parse`
- `MajSimai!MajSimai_Free`
- `MajSimai!MajSimai_FreeHGlobal`

You can use these exported functions in C++:

```C++
struct MajSimaiParseResult
{
    int32_t code = -1;
    int32_t errorAtLine = -1;
    int32_t errorAtColumn = -1;
    int32_t errorMsgLen = 0;
    int32_t errorContentLen = 0;

    void* simaiFile = NULL;
    char* errorMsgAnsi = NULL;
    char* errorContentAnsi = NULL;
};

extern "C" void __cdecl MajSimai_Parse(char*, int32_t, void*);
extern "C" bool __cdecl MajSimai_Free(MajSimaiParseResult*);
extern "C" bool __cdecl MajSimai_FreeHGlobal(void*);

int main()
{
    const string FUMEN = R"(&title=Test
&artist=NULL
&first=0
&des=1
&lv_5=114514
&inote_5=
(197){8}1,2,3,4,5,6,7,8,)";
    MajSimaiParseResult result = MajSimaiParseResult();
    MajSimai_Parse(FUMEN.data(), FUMEN.length(), &result);
    MajSimai_Free(&result);
}
```
