/*
 * Measures the cimai C parser, built from source by CMakeLists.txt, on the corpus given on standard input. See BenchmarkCommon.h for the arguments and tools/benchmark-native.py for how it is run.
 *
 * Mode "parse": cimai_parse parses the metadata and every chart of a file, then cimai_file_release frees it.
 */
#include "BenchmarkCommon.h"

#include <cimai/cimai.h>

/* Parses one file with cimai and frees it. cimai has no error result, so every file counts as parsed. */
static int parse_with_cimai(const BenchFile *file, uint64_t *notes)
{
    String_View text = { file->length, file->data };
    SimaiFile parsed = { 0 };
    size_t index = 0;

    cimai_parse(&text, &parsed);
    for (index = 0; index < DIFFICULTY_COUNT; index++)
    {
        if (parsed.charts[index] != NULL)
        {
            *notes += parsed.charts[index]->notes.len;
        }
    }
    cimai_file_release(&parsed);
    return 0;
}

/* Runs the benchmark. */
int BENCH_MAIN(int argc, bench_char **argv)
{
    BenchParseFunction parse = parse_with_cimai;
    if (argc > 4 && BENCH_STRCMP(argv[4], BENCH_TEXT("parse")) != 0)
    {
        parse = NULL;
    }
    return bench_main(argc, argv, "cimai", "parse", parse);
}
