/*
 * The parts that the cimai benchmark (C) and the MajSimai benchmark (C++) share: reading the corpus, timing the parser
 * and writing the result file that tools/benchmark-native.py reads.
 *
 * A benchmark program is run as
 *
 *     <program> <result file> <warm-up runs> <measured runs> [mode]
 *
 * with the corpus on standard input. The corpus is written by tools/benchmark-native.py:
 *
 *     uint32 file count, then for every file: uint32 length, then the bytes of the file (all little-endian)
 *
 * The corpus is read from standard input and the result is written to a file because MajSimai writes its log to
 * standard output. On Windows the program takes wide arguments (wmain) so the result file may have any name.
 */
#ifndef MAJSIMAI_BENCHMARK_COMMON_H
#define MAJSIMAI_BENCHMARK_COMMON_H

#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#include <wchar.h>
#else
#include <time.h>
#endif

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
/* Declared here instead of including windows.h, whose macros clash with the names of the MajSimai wrapper. */
__declspec(dllimport) int __stdcall QueryPerformanceCounter(long long *count);
__declspec(dllimport) int __stdcall QueryPerformanceFrequency(long long *frequency);

typedef wchar_t bench_char;
#define BENCH_MAIN wmain
#define BENCH_TEXT(text) L##text
#define BENCH_STRCMP wcscmp
#define BENCH_STRTOL wcstol
#define BENCH_OPEN_OUTPUT(path) _wfopen((path), L"wb")
#else
typedef char bench_char;
#define BENCH_MAIN main
#define BENCH_TEXT(text) text
#define BENCH_STRCMP strcmp
#define BENCH_STRTOL strtol
#define BENCH_OPEN_OUTPUT(path) fopen((path), "wb")
#endif

/* One file of the corpus. The bytes belong to the corpus. */
typedef struct
{
    const char *data;
    size_t length;
} BenchFile;

/* Every file of the corpus, read into one buffer. */
typedef struct
{
    unsigned char *buffer;
    size_t size;
    BenchFile *files;
    size_t count;
    size_t totalBytes;
} BenchCorpus;

/* What a benchmark measured. */
typedef struct
{
    /* Nanoseconds of every measured run over the whole corpus. */
    uint64_t *runNs;
    /* The fastest time of every file over the measured runs, in nanoseconds. */
    uint64_t *fileMinNs;
    /* Files the parser rejected in one run. */
    size_t failures;
    /* The notes the parser found in one run; a checksum that keeps the work from being optimized away. */
    uint64_t notes;
} BenchResult;

/*
 * Parses one file. Returns 0 when the file was parsed and 1 when the parser rejected it, and adds the notes it found
 * to *notes.
 */
typedef int (*BenchParseFunction)(const BenchFile *file, uint64_t *notes);

/* Gets a monotonic time in nanoseconds. */
static uint64_t bench_now_ns(void)
{
#ifdef _WIN32
    static long long frequency = 0;
    long long counter = 0;
    if (frequency == 0)
    {
        QueryPerformanceFrequency(&frequency);
    }
    QueryPerformanceCounter(&counter);
    return (uint64_t)((counter / frequency) * 1000000000LL + (counter % frequency) * 1000000000LL / frequency);
#else
    struct timespec now;
    clock_gettime(CLOCK_MONOTONIC, &now);
    return (uint64_t)now.tv_sec * 1000000000ULL + (uint64_t)now.tv_nsec;
#endif
}

/* Reads a little-endian uint32 at *offset and moves the offset. Returns 0 when the corpus ends before it. */
static int bench_read_u32(const BenchCorpus *corpus, size_t *offset, uint32_t *value)
{
    const unsigned char *bytes = corpus->buffer + *offset;
    if (corpus->size - *offset < 4)
    {
        return 0;
    }
    *value = (uint32_t)bytes[0] | ((uint32_t)bytes[1] << 8) | ((uint32_t)bytes[2] << 16) | ((uint32_t)bytes[3] << 24);
    *offset += 4;
    return 1;
}

/* Reads the corpus from standard input. Returns 0 and prints the reason to standard error when it is malformed. */
static int bench_load_corpus(BenchCorpus *corpus)
{
    size_t capacity = 1 << 20;
    size_t offset = 0;
    uint32_t count = 0;

    memset(corpus, 0, sizeof *corpus);
#ifdef _WIN32
    _setmode(_fileno(stdin), _O_BINARY);
#endif
    corpus->buffer = (unsigned char *)malloc(capacity);
    if (corpus->buffer == NULL)
    {
        fprintf(stderr, "error: out of memory reading the corpus\n");
        return 0;
    }
    for (;;)
    {
        size_t read = fread(corpus->buffer + corpus->size, 1, capacity - corpus->size, stdin);
        corpus->size += read;
        if (read == 0)
        {
            break;
        }
        if (corpus->size == capacity)
        {
            unsigned char *grown = (unsigned char *)realloc(corpus->buffer, capacity * 2);
            if (grown == NULL)
            {
                fprintf(stderr, "error: out of memory reading the corpus\n");
                return 0;
            }
            corpus->buffer = grown;
            capacity *= 2;
        }
    }

    if (!bench_read_u32(corpus, &offset, &count) || count == 0)
    {
        fprintf(stderr, "error: the corpus on standard input is empty or malformed\n");
        return 0;
    }
    corpus->files = (BenchFile *)calloc(count, sizeof(BenchFile));
    if (corpus->files == NULL)
    {
        fprintf(stderr, "error: out of memory reading the corpus\n");
        return 0;
    }
    for (corpus->count = 0; corpus->count < count; corpus->count++)
    {
        uint32_t length = 0;
        if (!bench_read_u32(corpus, &offset, &length) || corpus->size - offset < length)
        {
            fprintf(stderr, "error: the corpus ends inside file %zu\n", corpus->count);
            return 0;
        }
        corpus->files[corpus->count].data = (const char *)(corpus->buffer + offset);
        corpus->files[corpus->count].length = length;
        corpus->totalBytes += length;
        offset += length;
    }

    return 1;
}

/* Frees the corpus. */
static void bench_free_corpus(BenchCorpus *corpus)
{
    free(corpus->files);
    free(corpus->buffer);
    memset(corpus, 0, sizeof *corpus);
}

/*
 * Parses the whole corpus warmUpRuns times without measuring, then measuredRuns times, timing every run and every
 * file. Returns 0 when it runs out of memory.
 */
static int bench_run(const BenchCorpus *corpus, int warmUpRuns, int measuredRuns, BenchParseFunction parse, BenchResult *result)
{
    int run = 0;
    size_t index = 0;

    memset(result, 0, sizeof *result);
    result->runNs = (uint64_t *)calloc((size_t)measuredRuns, sizeof(uint64_t));
    result->fileMinNs = (uint64_t *)malloc(corpus->count * sizeof(uint64_t));
    if (result->runNs == NULL || result->fileMinNs == NULL)
    {
        fprintf(stderr, "error: out of memory\n");
        return 0;
    }
    for (index = 0; index < corpus->count; index++)
    {
        result->fileMinNs[index] = UINT64_MAX;
    }

    for (run = 0; run < warmUpRuns; run++)
    {
        uint64_t notes = 0;
        for (index = 0; index < corpus->count; index++)
        {
            parse(&corpus->files[index], &notes);
        }
    }

    for (run = 0; run < measuredRuns; run++)
    {
        size_t failures = 0;
        uint64_t notes = 0;
        uint64_t runStart = bench_now_ns();
        for (index = 0; index < corpus->count; index++)
        {
            uint64_t fileStart = bench_now_ns();
            uint64_t elapsed = 0;
            failures += (size_t)parse(&corpus->files[index], &notes);
            elapsed = bench_now_ns() - fileStart;
            if (elapsed < result->fileMinNs[index])
            {
                result->fileMinNs[index] = elapsed;
            }
        }
        result->runNs[run] = bench_now_ns() - runStart;
        result->failures = failures;
        result->notes = notes;
    }

    return 1;
}

/* Writes the result as JSON. Returns 0 when the file cannot be written. */
static int bench_write_result(const bench_char *path, const char *parser, const char *mode, const BenchCorpus *corpus, int measuredRuns, const BenchResult *result)
{
    FILE *output = BENCH_OPEN_OUTPUT(path);
    int run = 0;
    size_t index = 0;

    if (output == NULL)
    {
        fprintf(stderr, "error: cannot write the result file\n");
        return 0;
    }
    fprintf(output, "{\"parser\":\"%s\",\"mode\":\"%s\",\"files\":%zu,\"bytes\":%zu,\"failures\":%zu,\"notes\":%llu,\"runNs\":[",
            parser, mode, corpus->count, corpus->totalBytes, result->failures, (unsigned long long)result->notes);
    for (run = 0; run < measuredRuns; run++)
    {
        fprintf(output, "%s%llu", run == 0 ? "" : ",", (unsigned long long)result->runNs[run]);
    }
    fprintf(output, "],\"fileMinNs\":[");
    for (index = 0; index < corpus->count; index++)
    {
        fprintf(output, "%s%llu", index == 0 ? "" : ",", (unsigned long long)result->fileMinNs[index]);
    }
    fprintf(output, "]}\n");
    return fclose(output) == 0;
}

/* Frees the result. */
static void bench_free_result(BenchResult *result)
{
    free(result->runNs);
    free(result->fileMinNs);
    memset(result, 0, sizeof *result);
}

/*
 * Reads the arguments, the corpus, runs the benchmark and writes the result. Returns the exit code of the program.
 * parse is the parse function of the chosen mode, or NULL when the mode is unknown.
 */
static int bench_main(int argc, bench_char **argv, const char *parser, const char *mode, BenchParseFunction parse)
{
    BenchCorpus corpus;
    BenchResult result;
    long warmUpRuns = 0;
    long measuredRuns = 0;
    int succeeded = 0;

    if (argc < 4)
    {
        fprintf(stderr, "usage: <program> <result file> <warm-up runs> <measured runs> [mode] < corpus\n");
        return 2;
    }
    if (parse == NULL)
    {
        fprintf(stderr, "error: unknown mode\n");
        return 2;
    }
    warmUpRuns = BENCH_STRTOL(argv[2], NULL, 10);
    measuredRuns = BENCH_STRTOL(argv[3], NULL, 10);
    if (warmUpRuns < 0 || measuredRuns < 1)
    {
        fprintf(stderr, "error: the warm-up runs must be 0 or more and the measured runs 1 or more\n");
        return 2;
    }
    if (!bench_load_corpus(&corpus))
    {
        bench_free_corpus(&corpus);
        return 1;
    }
    succeeded = bench_run(&corpus, (int)warmUpRuns, (int)measuredRuns, parse, &result)
             && bench_write_result(argv[1], parser, mode, &corpus, (int)measuredRuns, &result);
    bench_free_result(&result);
    bench_free_corpus(&corpus);
    return succeeded ? 0 : 1;
}

#ifdef __cplusplus
}
#endif

#endif
