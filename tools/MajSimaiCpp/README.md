# MajSimaiCpp

The C++17 wrapper of [MajSimai](https://github.com/TeamMajdata/MajSimai), with MajSimai compiled by native AOT into a
static library for each platform. The program contains the whole parser and needs no .NET runtime or MajSimai library
at run time.

The wrapper is header-only and generated from the native ABI of MajSimai by
[`tools/MajSimai.CppGen`](https://github.com/TeamMajdata/MajSimai/tree/master/tools/MajSimai.CppGen), the way
FFmpeg.AutoGen generates C# bindings from the FFmpeg headers, so it always matches the exported structs.

This repository is published by the release workflow of MajSimai: each MajSimai tag replaces its content and is tagged
here with the same version. Do not edit it by hand; change MajSimai instead (this README is
[`tools/MajSimaiCpp/README.md`](https://github.com/TeamMajdata/MajSimai/blob/master/tools/MajSimaiCpp/README.md)
there).

## Usage

```cmake
add_subdirectory(MajSimaiCpp)
target_link_libraries(app PRIVATE MajSimai::MajSimaiCpp)
```

The root `CMakeLists.txt` links the package of the target platform from `packages/<rid>`. Set
`MAJSIMAI_RUNTIME_IDENTIFIER` (such as `win-x64`) to choose one yourself.

| Package | Platform |
|---|---|
| `win-x64`, `win-arm64` | Windows, MSVC |
| `linux-x64`, `linux-arm64` | Linux (glibc) |
| `osx-x64`, `osx-arm64` | macOS |

On Windows the native AOT runtime is compiled with the static release C runtime, so the program must use it too:

```cmake
set(CMAKE_MSVC_RUNTIME_LIBRARY MultiThreaded) # before the targets are created
```

## API

```C++
#include <MajSimai/MajSimai.h>

// Copy the result into owned data
MajSimai::SimaiFile file = MajSimai::SimaiParser::Parse(text);
for (const auto& note : file.charts[4].noteTimings[0].notes)
{
    bool star = note.isStar;
}

// Or read the native memory without copying it; `parsed` owns it and frees it when destroyed
MajSimai::ParsedSimaiFile parsed = MajSimai::SimaiParser::ParseAsView(text);
for (auto chart : parsed.GetFile().GetCharts())
{
    for (auto timing : chart.GetNoteTimings())
    {
        for (auto note : timing.GetNotes())
        {
            std::string_view raw = note.GetRawContent();
        }
    }
}

// Strict mode throws on the first chart that breaks the grammar
MajSimai::SimaiParser::Parse(text, MajSimai::SimaiSyntaxTolerance::Strict);
```

A failed parse throws `MajSimai::SimaiParseException`, with `GetCode()`, `GetLine()`, `GetColumn()` and `GetContent()`.

Ownership:

- `ParsedSimaiFile` is the only owner of a native result. It cannot be copied; moving it leaves the source empty
  (`explicit operator bool` is false and `GetFile()` throws `std::logic_error`).
- Views (`SimaiFileView`, `SimaiNoteView`, `ArrayView`, `std::string_view`) read the native memory and must not
  outlive the `ParsedSimaiFile`. `GetFile()` is deleted on a temporary, so
  `SimaiParser::ParseAsView(text).GetFile()` does not compile; `ParsedSimaiFile` is not a view either, so it cannot be
  sliced into one. Use `ToModel()` or `SimaiParser::Parse` for data that outlives the result.
- The result is owned by a `ParsedSimaiFile` as soon as the native call returns, so it is freed even when building the
  exception throws. The native structs only have pointers to `const` data.

| Header | Content |
|---|---|
| `MajSimai.h` | `SimaiParser`, `ParsedSimaiFile` (owns the native result) and `SimaiParseException` |
| `MajSimai.Core.g.h` | The enums, the native structs and the exported functions, with `static_assert`s that every offset and size matches MajSimai on 64-bit platforms |
| `MajSimai.Views.g.h` | A view per struct (`SimaiNoteView`, ...) that reads the native memory without copying it |
| `MajSimai.Model.g.h` | An owned struct per native struct (`SimaiNote`, ...) with `std::string` and `std::vector` |
| `MajSimai.ArrayView.g.h` | `ArrayView`, the iterable view of a native array |

## How the packages are made

The commands in this section run in a clone of MajSimai.

`tools/build-cpp-package.py` publishes MajSimai with native AOT as a shared or static library and puts it next to a
freshly generated wrapper, with a `CMakeLists.txt` that links it:

```bash
python tools/build-cpp-package.py --kind shared            # artifacts/cpp/<rid>-shared
python tools/build-cpp-package.py --kind static --verify   # artifacts/cpp/<rid>-static, then builds and runs a test program
```

```cmake
add_subdirectory(path/to/package)
target_link_libraries(app PRIVATE MajSimai::MajSimaiCpp)
```

| Kind | What the package links |
|---|---|
| `shared` | `lib/MajSimai.so` (with the soname `MajSimai.so`, found through the rpath CMake sets) or `lib/MajSimai.dylib`; on Windows `lib/MajSimai.lib` with `bin/MajSimai.dll`, which the program needs next to it |
| `static` | `lib/MajSimai.a` (`MajSimai.lib` on Windows), then the native AOT runtime in `lib/aot/` and the system libraries, in link order. The program contains the whole parser and needs no MajSimai library at run time |

The script reads the runtime libraries from the publish itself (`tools/MajSimai.NativePackage.targets`), so the package
matches the .NET SDK that built it, and writes them to `MajSimaiNative.json`; the generator turns that file into the
`CMakeLists.txt` (`--native-manifest`). Options: `--rid`, `--output`, `--framework`, `--configuration`, `--version`
(overrides the version in `MajSimai.csproj`). Native AOT needs a C++ toolchain and cannot cross-compile to another
operating system.

When a version tag is pushed, the release workflow of MajSimai (`.github/workflows/main.yml`) builds a static package
for each platform above, and pushes them to this repository under `packages/<rid>` with the root files in
`tools/MajSimaiCpp`, tagged with the same version. It needs the secret `MAJSIMAICPP_TOKEN`, a token that can push to
MajSimaiCpp. To test it, run the workflow by hand (Actions → Run workflow, with an optional version such as
`3.0.1-test`): it builds everything, uploads the NuGet packages, the C++ packages and the content of MajSimaiCpp as
artifacts, and publishes nothing; the push to MajSimaiCpp is a dry run that still checks the token.

Without a package, add `Bindings/Cpp` of MajSimai and point it at a library you published yourself:

```cmake
set(MAJSIMAI_NATIVE_LIBRARY "path/to/MajSimai.so") # MajSimai.lib on Windows, MajSimai.dylib on macOS
add_subdirectory(MajSimai/Bindings/Cpp)
target_link_libraries(app PRIVATE MajSimai::MajSimaiCpp)
```

## Regenerating the wrapper

The generator reads the compiled ABI with reflection: it starts from the methods marked with
`[UnmanagedCallersOnly(EntryPoint = ...)]` and follows their parameters and fields to the structs and enums they use.
Run it after changing `Runtime/Unmanaged`; a test of MajSimai fails while `Bindings/Cpp` is out of date.

```bash
dotnet run --project tools/MajSimai.CppGen               # writes Bindings/Cpp
dotnet run --project tools/MajSimai.CppGen -- --check    # exits with 1 when Bindings/Cpp is out of date
```

The ABI follows these rules, and the generator stops with a message that names the member when one is broken:

- A pointer field says what it points to with an attribute. The field that holds the length is hidden in the views and
  models, and the pointer is declared as a pointer to `const`.

  | Attribute | Field | C++ view / model |
  |---|---|---|
  | `[NativeString(nameof(textLen))]` | `byte*`: UTF-8 text, length in bytes | `std::string_view` / `std::string` |
  | `[NativeArray(nameof(itemsLen))]` | `T*`: an array | `ArrayView` / `std::vector` |
  | `[NativePointee(typeof(T))]` | `void*`: one `T`, only in the parse result | `const T*` |

- A data struct is named `Unmanaged<Name>`; its view is `<Name>View` and its model `<Name>`. A struct that exported
  functions take by pointer (the parse result) gets no view or model.
- Structs use the default sequential layout. The generator measures every offset in .NET and checks that C++ gives the
  same one.
- Exported functions set `CallConvs` and only use blittable types: `bool` and `char` are rejected in their signatures
  (they are fine as struct fields, which are not marshalled).
- XML documentation comments become Doxygen comments.
- `MajSimai.h` is written against fixed names (`MajSimai_ParseWithTolerance`, `MajSimai_Free`, `SimaiSyntaxTolerance`
  and the fields of `UnmanagedSimaiParseResult`); the generator checks them before writing anything.

## Tests and platforms

The C++ tests of MajSimai (see its README) compile the wrapper and link both kinds of package with GCC 13 and Clang 18
on Linux x64. The release workflow builds and runs a test program with each static package, except `win-arm64`, which
is cross-compiled on x64. 32-bit platforms are not supported, and 32-bit layouts are not checked by the
`static_assert`s.

## License

GPL-3.0-or-later, as MajSimai.
