# The root of the MajSimaiCpp repository (https://github.com/TeamMajdata/MajSimaiCpp).
#
# The release workflow of MajSimai (.github/workflows/main.yml) copies this file there with a static package per
# runtime identifier in packages/<rid>, each made by tools/build-cpp-package.py. This file picks the package of the
# target platform, so a C++17 program only needs:
#
#     add_subdirectory(MajSimaiCpp)
#     target_link_libraries(app PRIVATE MajSimai::MajSimaiCpp)
#
# Set MAJSIMAI_RUNTIME_IDENTIFIER (such as win-x64) to choose the package yourself.
cmake_minimum_required(VERSION 3.15)

project(MajSimaiCppPackages LANGUAGES CXX)

set(MAJSIMAI_RUNTIME_IDENTIFIER "" CACHE STRING
    "The .NET runtime identifier of the MajSimai package to link, such as win-x64. Default: the target platform.")

if(MAJSIMAI_RUNTIME_IDENTIFIER)
    set(MAJSIMAI_PACKAGE_RID "${MAJSIMAI_RUNTIME_IDENTIFIER}")
else()
    if(CMAKE_SYSTEM_NAME STREQUAL "Windows")
        set(MAJSIMAI_PACKAGE_OS "win")
    elseif(CMAKE_SYSTEM_NAME STREQUAL "Darwin")
        set(MAJSIMAI_PACKAGE_OS "osx")
    elseif(CMAKE_SYSTEM_NAME STREQUAL "Linux")
        set(MAJSIMAI_PACKAGE_OS "linux")
    else()
        message(FATAL_ERROR "MajSimaiCpp has no package for ${CMAKE_SYSTEM_NAME}; set MAJSIMAI_RUNTIME_IDENTIFIER.")
    endif()

    # MSVC names the architecture it compiles for; elsewhere the target processor does.
    if(MSVC AND CMAKE_CXX_COMPILER_ARCHITECTURE_ID)
        set(MAJSIMAI_PACKAGE_ARCH "${CMAKE_CXX_COMPILER_ARCHITECTURE_ID}")
    elseif(APPLE AND CMAKE_OSX_ARCHITECTURES)
        list(LENGTH CMAKE_OSX_ARCHITECTURES MAJSIMAI_OSX_ARCHITECTURE_COUNT)
        if(NOT MAJSIMAI_OSX_ARCHITECTURE_COUNT EQUAL 1)
            message(FATAL_ERROR "MajSimaiCpp links one architecture, but CMAKE_OSX_ARCHITECTURES is '${CMAKE_OSX_ARCHITECTURES}'.")
        endif()
        set(MAJSIMAI_PACKAGE_ARCH "${CMAKE_OSX_ARCHITECTURES}")
    else()
        set(MAJSIMAI_PACKAGE_ARCH "${CMAKE_SYSTEM_PROCESSOR}")
    endif()

    string(TOLOWER "${MAJSIMAI_PACKAGE_ARCH}" MAJSIMAI_PACKAGE_ARCH)
    if(MAJSIMAI_PACKAGE_ARCH MATCHES "^(x64|x86_64|amd64)$")
        set(MAJSIMAI_PACKAGE_ARCH "x64")
    elseif(MAJSIMAI_PACKAGE_ARCH MATCHES "^(arm64|aarch64)$")
        set(MAJSIMAI_PACKAGE_ARCH "arm64")
    else()
        message(FATAL_ERROR "MajSimaiCpp has no package for the processor '${MAJSIMAI_PACKAGE_ARCH}'; set MAJSIMAI_RUNTIME_IDENTIFIER.")
    endif()

    set(MAJSIMAI_PACKAGE_RID "${MAJSIMAI_PACKAGE_OS}-${MAJSIMAI_PACKAGE_ARCH}")
endif()

set(MAJSIMAI_PACKAGE_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/packages/${MAJSIMAI_PACKAGE_RID}")
if(NOT EXISTS "${MAJSIMAI_PACKAGE_DIRECTORY}/CMakeLists.txt")
    file(GLOB MAJSIMAI_AVAILABLE_PACKAGES RELATIVE "${CMAKE_CURRENT_SOURCE_DIR}/packages" "${CMAKE_CURRENT_SOURCE_DIR}/packages/*")
    message(FATAL_ERROR "MajSimaiCpp has no package for ${MAJSIMAI_PACKAGE_RID}. Available: ${MAJSIMAI_AVAILABLE_PACKAGES}.")
endif()

message(STATUS "MajSimaiCpp: linking the static package for ${MAJSIMAI_PACKAGE_RID}")
add_subdirectory("${MAJSIMAI_PACKAGE_DIRECTORY}" "${CMAKE_CURRENT_BINARY_DIR}/packages/${MAJSIMAI_PACKAGE_RID}")
