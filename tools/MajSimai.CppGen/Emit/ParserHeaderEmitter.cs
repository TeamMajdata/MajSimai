using System;
using System.Collections.Generic;
using System.Linq;
using MajSimai.CppGen.Abi;

namespace MajSimai.CppGen.Emit;

/// <summary>
/// Writes <c>MajSimai.h</c>, the header users include: <c>SimaiParser</c>, the RAII owner <c>ParsedSimaiFile</c> and
/// <c>SimaiParseException</c>, on top of the generated views and models.
/// </summary>
/// <remarks>
/// Unlike the other headers, this one is written against fixed names of the ABI (the parse and free functions and the
/// fields of the parse result). <see cref="CheckContract"/> verifies those names before anything is written, so a change
/// of the ABI that breaks the facade fails the generator with a clear message instead of producing C++ that does not
/// compile.
/// </remarks>
internal static class ParserHeaderEmitter
{
    /// <summary>
    /// The name of the header.
    /// </summary>
    public const string FileName = "MajSimai.h";

    /// <summary>
    /// The function that parses a file.
    /// </summary>
    public const string ParseFunction = "MajSimai_Parse";

    /// <summary>
    /// The function that frees a parse result.
    /// </summary>
    public const string FreeFunction = "MajSimai_Free";

    /// <summary>
    /// The struct the parse function writes its result to.
    /// </summary>
    public const string ResultStruct = "UnmanagedSimaiParseResult";

    /// <summary>
    /// The data struct of a parsed file.
    /// </summary>
    public const string FileStruct = "UnmanagedSimaiFile";

    /// <summary>
    /// The prefix of the result codes, which become the members of <c>SimaiParseErrorCode</c>.
    /// </summary>
    public const string CodePrefix = "CODE_";

    /// <summary>
    /// Writes the header.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <returns>The content of the header.</returns>
    /// <exception cref="CppGenException">Thrown when the ABI does not have what the facade uses.</exception>
    public static string Emit(AbiModel model)
    {
        CheckContract(model);
        var result = model.FindStruct(ResultStruct)!;
        var file = model.FindStruct(FileStruct)!;
        var fileView = CppNames.View(file);
        var fileModel = CppNames.Model(file);
        var resultType = CppNames.Core(ResultStruct);
        var writer = new CppWriter();
        writer.Banner(model.Version);
        writer.Lines($"""
            #pragma once

            #include "{CoreHeaderEmitter.FileName}"
            #include "{ModelHeaderEmitter.FileName}"
            #include "{ViewsHeaderEmitter.FileName}"

            #include <cstdint>
            #include <limits>
            #include <stdexcept>
            #include <string>
            #include <string_view>
            #include <utility>
            """);
        writer.Line();
        writer.Line($"namespace {CppNames.Namespace}");
        using (writer.Block())
        {
            writer.Doc($"Why {ParseFunction} failed: the {CodePrefix} constants of {resultType}.");
            writer.Line("enum class SimaiParseErrorCode : int32_t");
            using (writer.Block("};"))
            {
                foreach (var constant in result.Constants.Where(c => c.Name.StartsWith(CodePrefix, StringComparison.Ordinal)))
                {
                    writer.Doc(constant.Doc.Summary);
                    writer.Line($"{CppNames.EnumMember(constant.Name, CodePrefix)} = {resultType}::{constant.Name},");
                }
            }
            writer.Line();
            writer.Lines($$"""
                /// Thrown when MajSimai cannot parse a file.
                class SimaiParseException : public std::runtime_error
                {
                public:
                    /// Creates the exception.
                    /// @param code Why parsing failed.
                    /// @param message The description of the error.
                    /// @param line The line of the error, starting from 1, or -1.
                    /// @param column The column of the error, or -1.
                    /// @param content The text the error is about, or an empty string.
                    SimaiParseException(SimaiParseErrorCode code, const std::string& message, int32_t line, int32_t column, std::string content)
                        : std::runtime_error(message), m_code(code), m_line(line), m_column(column), m_content(std::move(content))
                    {
                    }

                    /// Gets why parsing failed.
                    /// @return The error code.
                    SimaiParseErrorCode GetCode() const noexcept
                    {
                        return m_code;
                    }

                    /// Gets the line of the error.
                    /// @return The line, starting from 1, or -1 when the error has no position.
                    int32_t GetLine() const noexcept
                    {
                        return m_line;
                    }

                    /// Gets the column of the error.
                    /// @return The column, or -1 when the error has no position.
                    int32_t GetColumn() const noexcept
                    {
                        return m_column;
                    }

                    /// Gets the text the error is about.
                    /// @return The text, or an empty string.
                    const std::string& GetContent() const noexcept
                    {
                        return m_content;
                    }

                private:
                    SimaiParseErrorCode m_code;
                    int32_t m_line;
                    int32_t m_column;
                    std::string m_content;
                };

                /// A file parsed by MajSimai and kept in native memory, which is freed when the object is destroyed. It is
                /// a {{fileView}} of that memory; views taken from it stay valid while it lives. A moved-from object must
                /// not be read.
                class ParsedSimaiFile : public {{fileView}}
                {
                public:
                    /// Takes ownership of a successful parse result.
                    /// @param result The result of {{ParseFunction}}; it is freed with {{FreeFunction}} by this object.
                    explicit ParsedSimaiFile(const {{resultType}}& result) noexcept
                        : {{fileView}}(result.simaiFile), m_result(result)
                    {
                    }

                    /// Frees the native memory.
                    ~ParsedSimaiFile()
                    {
                        {{CppNames.CoreNamespace}}::{{FreeFunction}}(&m_result);
                    }

                    ParsedSimaiFile(const ParsedSimaiFile&) = delete;
                    ParsedSimaiFile& operator=(const ParsedSimaiFile&) = delete;

                    /// Takes the native memory of another object.
                    /// @param other The object to move from.
                    ParsedSimaiFile(ParsedSimaiFile&& other) noexcept
                        : {{fileView}}(other), m_result(other.m_result)
                    {
                        other.Release();
                    }

                    /// Frees the native memory of this object and takes the memory of another object.
                    /// @param other The object to move from.
                    /// @return This object.
                    ParsedSimaiFile& operator=(ParsedSimaiFile&& other) noexcept
                    {
                        if (this != &other)
                        {
                            {{CppNames.CoreNamespace}}::{{FreeFunction}}(&m_result);
                            {{fileView}}::operator=(other);
                            m_result = other.m_result;
                            other.Release();
                        }

                        return *this;
                    }

                private:
                    /// Forgets the native memory after it was moved to another object.
                    void Release() noexcept
                    {
                        {{fileView}}::operator=({{fileView}}(nullptr));
                        m_result = {{resultType}}{};
                    }

                    {{resultType}} m_result;
                };

                /// Parses simai files with MajSimai.
                class SimaiParser
                {
                public:
                    /// Parses a simai file in lenient mode and copies the result into owned data.
                    /// @param content The file as UTF-8; a UTF-8 or UTF-16 byte order mark is honored.
                    /// @return The parsed file.
                    /// @throws SimaiParseException when MajSimai cannot parse the content.
                    /// @throws std::length_error when the content is 2 GiB or larger.
                    static {{fileModel}} Parse(std::string_view content)
                    {
                        return ParseAsView(content).ToModel();
                    }

                    /// Parses a simai file in lenient mode and keeps the result in native memory, which avoids copying it.
                    /// @param content The file as UTF-8; a UTF-8 or UTF-16 byte order mark is honored.
                    /// @return The parsed file.
                    /// @throws SimaiParseException when MajSimai cannot parse the content.
                    /// @throws std::length_error when the content is 2 GiB or larger.
                    static ParsedSimaiFile ParseAsView(std::string_view content)
                    {
                        if (content.size() > static_cast<std::size_t>(std::numeric_limits<int32_t>::max()))
                        {
                            throw std::length_error("MajSimai cannot parse content of 2 GiB or larger.");
                        }
                        {{resultType}} result{};
                        {{CppNames.CoreNamespace}}::{{ParseFunction}}(content.data(), static_cast<int32_t>(content.size()), &result);
                        if (result.code != {{resultType}}::CODE_NO_ERROR || result.simaiFile == nullptr)
                        {
                            ThrowAndFree(result);
                        }

                        return ParsedSimaiFile(result);
                    }

                private:
                    /// Throws the error of a failed parse result after freeing it.
                    /// @param result The failed result.
                    [[noreturn]] static void ThrowAndFree({{resultType}}& result)
                    {
                        std::string message({{CppNames.CoreNamespace}}::MakeView(result.errorMsg, result.errorMsgLen));
                        if (message.empty())
                        {
                            message = "{{ParseFunction}} failed without a message.";
                        }
                        SimaiParseException exception(static_cast<SimaiParseErrorCode>(result.code),
                                                      message,
                                                      result.errorAtLine,
                                                      result.errorAtColumn,
                                                      std::string({{CppNames.CoreNamespace}}::MakeView(result.errorContent, result.errorContentLen)));
                        {{CppNames.CoreNamespace}}::{{FreeFunction}}(&result);
                        throw exception;
                    }
                };
                """);
        }

        return writer.ToString();
    }

    /// <summary>
    /// Checks that the ABI has every function, field and constant the facade uses.
    /// </summary>
    /// <param name="model">The native ABI.</param>
    /// <exception cref="CppGenException">Thrown with the list of everything that is missing or has another shape.</exception>
    public static void CheckContract(AbiModel model)
    {
        var problems = new List<string>();
        var resultPointer = new AbiPointerType(new AbiStructType(ResultStruct));
        var parse = model.FindFunction(ParseFunction);
        var expectedParse = new AbiType[] { new AbiPointerType(AbiPrimitiveType.Utf8Char, IsConst: true), AbiPrimitiveType.Int32, resultPointer };
        if (parse is null || !parse.Parameters.Select(p => p.Type).SequenceEqual(expectedParse))
        {
            problems.Add($"{ParseFunction}(const char*, int32_t, {ResultStruct}*)");
        }
        var free = model.FindFunction(FreeFunction);
        if (free is null || !free.Parameters.Select(p => p.Type).SequenceEqual(new AbiType[] { resultPointer }))
        {
            problems.Add($"{FreeFunction}({ResultStruct}*)");
        }
        var result = model.FindStruct(ResultStruct);
        if (result is null)
        {
            problems.Add($"struct {ResultStruct}");
        }
        else
        {
            RequireField(problems, result, "code", AbiFieldKind.Value, AbiPrimitiveType.Int32);
            RequireField(problems, result, "errorAtLine", AbiFieldKind.Value, AbiPrimitiveType.Int32);
            RequireField(problems, result, "errorAtColumn", AbiFieldKind.Value, AbiPrimitiveType.Int32);
            RequireField(problems, result, "errorMsg", AbiFieldKind.String, null);
            RequireField(problems, result, "errorContent", AbiFieldKind.String, null);
            RequireField(problems, result, "simaiFile", AbiFieldKind.Pointer, new AbiPointerType(new AbiStructType(FileStruct)));
            if (!result.Constants.Any(c => c.Name == CodePrefix + "NO_ERROR"))
            {
                problems.Add($"{ResultStruct}::{CodePrefix}NO_ERROR");
            }
        }
        if (model.FindStruct(FileStruct) is not { IsHandle: false })
        {
            problems.Add($"data struct {FileStruct}");
        }
        if (problems.Count != 0)
        {
            throw new CppGenException($"The C++ facade ({FileName}) needs these parts of the native ABI, which are missing or changed: {string.Join("; ", problems)}. Update {nameof(ParserHeaderEmitter)} to match the ABI.");
        }
    }

    /// <summary>
    /// Records a problem when a struct does not have a field of the given kind and type.
    /// </summary>
    /// <param name="problems">Receives the problem.</param>
    /// <param name="abiStruct">The struct.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="kind">The kind the field must have.</param>
    /// <param name="type">The type the field must have, or <see langword="null"/> to accept any type.</param>
    private static void RequireField(List<string> problems, AbiStruct abiStruct, string name, AbiFieldKind kind, AbiType? type)
    {
        var field = abiStruct.FindField(name);
        if (field is null || field.Kind != kind || (type is not null && field.Type != type))
        {
            problems.Add($"{abiStruct.Name}::{name} ({kind})");
        }
    }
}
