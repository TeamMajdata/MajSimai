# Builds the native parser benchmarks that tools/benchmark-native.py runs:
#   CimaiBenchmark     cimai, compiled from the sources in CIMAI_DIR
#   MajSimaiBenchmark  MajSimai through the generated C++ wrapper, from a package made by tools/build-cpp-package.py
cmake_minimum_required(VERSION 3.21)

project(MajSimaiNativeBenchmark LANGUAGES C CXX)

set(CIMAI_DIR "" CACHE PATH "The cimai repository (it has include/ and src/).")
set(MAJSIMAI_PACKAGE_DIR "" CACHE PATH "A MajSimai C++ package made by tools/build-cpp-package.py.")
set(BENCHMARK_LIBRARY_TYPE "SHARED" CACHE STRING "Build cimai as a SHARED or STATIC library, like the MajSimai package.")
set_property(CACHE BENCHMARK_LIBRARY_TYPE PROPERTY STRINGS SHARED STATIC)

if(NOT EXISTS "${CIMAI_DIR}/include/cimai/cimai.h")
    message(FATAL_ERROR "CIMAI_DIR must name the cimai repository; '${CIMAI_DIR}' has no include/cimai/cimai.h.")
endif()
if(NOT EXISTS "${MAJSIMAI_PACKAGE_DIR}/MajSimaiNative.json")
    message(FATAL_ERROR "MAJSIMAI_PACKAGE_DIR must name a package made by tools/build-cpp-package.py.")
endif()

# Every program and DLL goes to bin/<config>, so the programs find cimai.dll next to them on Windows.
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin/$<CONFIG>")

if(MSVC)
    add_compile_options(/utf-8)
endif()

# cimai is compiled here instead of with add_subdirectory(), because its own CMakeLists.txt writes the library into
# its source tree.
file(GLOB_RECURSE CIMAI_SOURCES CONFIGURE_DEPENDS "${CIMAI_DIR}/src/*.c")
add_library(cimai ${BENCHMARK_LIBRARY_TYPE} ${CIMAI_SOURCES})
target_include_directories(cimai PUBLIC "${CIMAI_DIR}/include")
if(MSVC)
    # CMake cannot request C23 from MSVC; /std:clatest is the newest C that MSVC has.
    target_compile_options(cimai PRIVATE /std:clatest)
else()
    set_target_properties(cimai PROPERTIES C_STANDARD 23 C_STANDARD_REQUIRED ON C_EXTENSIONS OFF)
endif()
if(UNIX)
    target_link_libraries(cimai PRIVATE m)
endif()

add_executable(CimaiBenchmark CimaiBenchmark.c)
target_link_libraries(CimaiBenchmark PRIVATE cimai)
set_target_properties(CimaiBenchmark PROPERTIES C_STANDARD 11 C_STANDARD_REQUIRED ON)

add_subdirectory("${MAJSIMAI_PACKAGE_DIR}" majsimai)
add_executable(MajSimaiBenchmark MajSimaiBenchmark.cpp)
target_link_libraries(MajSimaiBenchmark PRIVATE MajSimai::MajSimaiCpp)
target_compile_features(MajSimaiBenchmark PRIVATE cxx_std_17)
if(WIN32 AND BENCHMARK_LIBRARY_TYPE STREQUAL "SHARED")
    add_custom_command(TARGET MajSimaiBenchmark POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_if_different "$<TARGET_FILE:MajSimai::Native>" "$<TARGET_FILE_DIR:MajSimaiBenchmark>")
endif()
