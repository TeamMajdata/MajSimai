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
/// <para>
/// Unlike the other headers, this one is written against fixed names of the ABI (the parse and free functions and the
/// fields of the parse result). <see cref="CheckContract"/> verifies those names before anything is written, so a
/// change of the ABI that breaks the facade fails the generator with a clear message instead of producing C++ that
/// does not compile.
/// </para>
/// <para>
/// Ownership rules of the facade: a native result is owned by exactly one <c>ParsedSimaiFile</c> from the moment the
/// parse function returns, so it is freed even when building the error throws. The owner holds the result (it is not a
/// view), cannot be copied, empties the object it is moved from, and only hands out views from an lvalue, so a view
/// cannot be taken from a temporary that is about to free its memory.
/// </para>
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
    public const string ParseFunction = "MajSimai_ParseWithTolerance";

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
    /// The enum of the syntax tolerance the parse function takes.
    /// </summary>
    public const string ToleranceEnum = "SimaiSyntaxTolerance";

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
        var core = CppNames.CoreNamespace;
        var lenient = model.Enums.First(e => e.Name == ToleranceEnum).Members[0].Name;
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
                /// Thrown when MajSimai cannot parse a file. The exception owns copies of the texts of the error.
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

                    /// Gets the line of the error. In strict mode it is counted in the text of the chart that failed.
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

                class SimaiParser;

                /// A file parsed by MajSimai and kept in native memory. The object is the only owner of that memory and
                /// frees it with {{FreeFunction}} when it is destroyed; it can be moved but not copied, and the object it
                /// is moved from becomes empty. Views returned by GetFile() read the native memory, so they must not
                /// outlive this object; GetFile() is not available on a temporary for that reason. ToModel() copies the
                /// file into owned data that stays valid after this object is destroyed.
                class ParsedSimaiFile
                {
                public:
                    /// Creates an empty object that owns nothing.
                    ParsedSimaiFile() noexcept = default;

                    /// Frees the native memory.
                    ~ParsedSimaiFile()
                    {
                        Reset();
                    }

                    ParsedSimaiFile(const ParsedSimaiFile&) = delete;
                    ParsedSimaiFile& operator=(const ParsedSimaiFile&) = delete;

                    /// Takes the native memory of another object, which becomes empty.
                    /// @param other The object to move from.
                    ParsedSimaiFile(ParsedSimaiFile&& other) noexcept
                        : m_result(std::exchange(other.m_result, {{resultType}}{}))
                    {
                    }

                    /// Frees the native memory of this object and takes the memory of another object, which becomes empty.
                    /// @param other The object to move from.
                    /// @return This object.
                    ParsedSimaiFile& operator=(ParsedSimaiFile&& other) noexcept
                    {
                        if (this != &other)
                        {
                            Reset();
                            m_result = std::exchange(other.m_result, {{resultType}}{});
                        }

                        return *this;
                    }

                    /// Gets whether the object owns a parsed file.
                    /// @return false for an empty object: default-constructed or moved from.
                    explicit operator bool() const noexcept
                    {
                        return m_result.simaiFile != nullptr;
                    }

                    /// Gets a view of the parsed file, valid while this object owns it.
                    /// @return The view.
                    /// @throws std::logic_error when the object is empty.
                    {{fileView}} GetFile() const&
                    {
                        if (m_result.simaiFile == nullptr)
                        {
                            throw std::logic_error("The ParsedSimaiFile is empty: it was moved from or default-constructed.");
                        }

                        return {{fileView}}(m_result.simaiFile);
                    }

                    /// Not available on a temporary: the view would read memory that is freed at the end of the expression.
                    /// Keep the ParsedSimaiFile in a variable, or call ToModel().
                    {{fileView}} GetFile() const&& = delete;

                    /// Copies the parsed file into owned data.
                    /// @return The copy, which does not depend on this object.
                    /// @throws std::logic_error when the object is empty.
                    {{fileModel}} ToModel() const
                    {
                        return GetFile().ToModel();
                    }

                private:
                    friend class SimaiParser;

                    /// Takes ownership of a native result and empties the source, so only this object frees it.
                    /// @param result The result written by {{ParseFunction}}.
                    explicit ParsedSimaiFile({{resultType}}& result) noexcept
                        : m_result(std::exchange(result, {{resultType}}{}))
                    {
                    }

                    /// Gets whether the native result failed.
                    /// @return true when it has an error code or no file.
                    bool HasFailed() const noexcept
                    {
                        return m_result.code != {{resultType}}::CODE_NO_ERROR || m_result.simaiFile == nullptr;
                    }

                    /// Copies the error of a failed native result into an exception.
                    /// @return The exception.
                    SimaiParseException CreateException() const
                    {
                        std::string message({{core}}::MakeView(m_result.errorMsg, m_result.errorMsgLen));
                        if (message.empty())
                        {
                            message = "{{ParseFunction}} failed without a message.";
                        }

                        return SimaiParseException(static_cast<SimaiParseErrorCode>(m_result.code),
                                                   message,
                                                   m_result.errorAtLine,
                                                   m_result.errorAtColumn,
                                                   std::string({{core}}::MakeView(m_result.errorContent, m_result.errorContentLen)));
                    }

                    /// Frees the native memory, if any, and leaves the object empty.
                    void Reset() noexcept
                    {
                        if (m_result.simaiFile != nullptr || m_result.errorMsg != nullptr || m_result.errorContent != nullptr)
                        {
                            {{core}}::{{FreeFunction}}(&m_result);
                        }
                        m_result = {{resultType}}{};
                    }

                    {{resultType}} m_result{};
                };

                /// Parses simai files with MajSimai.
                class SimaiParser
                {
                public:
                    /// Parses a simai file and copies the result into owned data.
                    /// @param content The file as UTF-8; a UTF-8 or UTF-16 byte order mark is honored.
                    /// @param tolerance Lenient (the default) keeps a chart that breaks the grammar with its text but without
                    /// notes; strict fails on the first such chart.
                    /// @return The parsed file.
                    /// @throws SimaiParseException when MajSimai cannot parse the content.
                    /// @throws std::length_error when the content is 2 GiB or larger.
                    static {{fileModel}} Parse(std::string_view content, {{ToleranceEnum}} tolerance = {{ToleranceEnum}}::{{lenient}})
                    {
                        return ParseAsView(content, tolerance).ToModel();
                    }

                    /// Parses a simai file and keeps the result in native memory, which avoids copying it.
                    /// @param content The file as UTF-8; a UTF-8 or UTF-16 byte order mark is honored.
                    /// @param tolerance Lenient (the default) keeps a chart that breaks the grammar with its text but without
                    /// notes; strict fails on the first such chart.
                    /// @return The owner of the parsed file.
                    /// @throws SimaiParseException when MajSimai cannot parse the content.
                    /// @throws std::length_error when the content is 2 GiB or larger.
                    static ParsedSimaiFile ParseAsView(std::string_view content, {{ToleranceEnum}} tolerance = {{ToleranceEnum}}::{{lenient}})
                    {
                        if (content.size() > static_cast<std::size_t>(std::numeric_limits<int32_t>::max()))
                        {
                            throw std::length_error("MajSimai cannot parse content of 2 GiB or larger.");
                        }
                        {{resultType}} result{};
                        {{core}}::{{ParseFunction}}(content.data(), static_cast<int32_t>(content.size()), tolerance, &result);
                        // From here on the result is owned by `parsed`, which frees it even if building the error throws.
                        ParsedSimaiFile parsed(result);
                        if (parsed.HasFailed())
                        {
                            throw parsed.CreateException();
                        }

                        return parsed;
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
        var tolerance = model.Enums.FirstOrDefault(e => e.Name == ToleranceEnum);
        if (tolerance is null || tolerance.Members.Count == 0)
        {
            problems.Add($"enum {ToleranceEnum}");
        }
        var parse = model.FindFunction(ParseFunction);
        var expectedParse = new AbiType[]
        {
            new AbiPointerType(AbiPrimitiveType.Utf8Char, IsConst: true),
            AbiPrimitiveType.Int32,
            tolerance is null ? AbiPrimitiveType.Void : new AbiEnumType(tolerance),
            resultPointer,
        };
        if (parse is null || !parse.Parameters.Select(p => p.Type).SequenceEqual(expectedParse))
        {
            problems.Add($"{ParseFunction}(const char*, int32_t, {ToleranceEnum}, {ResultStruct}*)");
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
            RequireField(problems, result, "simaiFile", AbiFieldKind.Pointer, new AbiPointerType(new AbiStructType(FileStruct), IsConst: true));
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
