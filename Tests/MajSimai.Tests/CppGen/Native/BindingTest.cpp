// Parses simai files with the generated C++ wrapper and prints a dump of the results that CppBindingTests compares
// with the same dump written from the C# parser.
//
// Usage: BindingTest <dump file> <file | --empty>...
//
// The dump goes to a file rather than to standard output, which MajSimai's default logger writes to. For every input
// the dump is made twice, once from the views (ParseAsView) and once from the owned model (Parse);
// the program fails when the two differ. Numbers are printed as the hex of their bits, so the C# and C++ dumps match
// exactly. --empty parses empty content, which must fail.

#include "MajSimai/MajSimai.h"

#include <cstdint>
#include <cstring>
#include <fstream>
#include <initializer_list>
#include <iomanip>
#include <iostream>
#include <iterator>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    // Escapes backslashes and line breaks so every record stays on one line.
    std::string Escape(std::string_view text)
    {
        std::string escaped;
        for (const char c : text)
        {
            switch (c)
            {
            case '\\':
                escaped += "\\\\";
                break;
            case '\n':
                escaped += "\\n";
                break;
            case '\r':
                escaped += "\\r";
                break;
            case '\t':
                escaped += "\\t";
                break;
            default:
                escaped += c;
                break;
            }
        }

        return escaped;
    }

    // Prints the bits of a float or double as hex.
    template <typename T>
    std::string Bits(T value)
    {
        static_assert(sizeof(T) == 4 || sizeof(T) == 8, "Only float and double are printed as bits.");
        std::uint64_t bits = 0;
        if constexpr (sizeof(T) == 4)
        {
            std::uint32_t narrow = 0;
            std::memcpy(&narrow, &value, sizeof(T));
            bits = narrow;
        }
        else
        {
            std::memcpy(&bits, &value, sizeof(T));
        }
        std::ostringstream out;
        out << std::hex << std::setw(static_cast<int>(sizeof(T) * 2)) << std::setfill('0') << bits;

        return out.str();
    }

    // The flags of a note in a fixed order, as 0 and 1.
    std::string Flags(std::initializer_list<bool> flags)
    {
        std::string text;
        for (const bool flag : flags)
        {
            text += flag ? '1' : '0';
        }

        return text;
    }

    void DumpNote(std::ostream& out, const MajSimai::SimaiNoteView& note)
    {
        out << "note type=" << static_cast<int>(note.GetType()) << " pos=" << note.GetStartPosition()
            << " hold=" << Bits(note.GetHoldTime()) << " slideStart=" << Bits(note.GetSlideStartTime())
            << " slide=" << Bits(note.GetSlideTime()) << " area=" << static_cast<int>(note.GetTouchArea())
            << " flags="
            << Flags({ note.IsBreak(), note.IsEx(), note.IsFakeRotate(), note.IsForceStar(), note.IsHanabi(),
                       note.IsTapHeadSlide(), note.IsMine(), note.IsUsingSV(), note.IsEach(), note.IsStar(),
                       note.IsStarDouble() })
            << " raw=" << Escape(note.GetRawContent()) << " related=";
        auto first = true;
        for (const auto index : note.GetRelatedNotes())
        {
            out << (first ? "" : ",") << index;
            first = false;
        }
        out << '\n';
    }

    void DumpNote(std::ostream& out, const MajSimai::SimaiNote& note)
    {
        out << "note type=" << static_cast<int>(note.type) << " pos=" << note.startPosition
            << " hold=" << Bits(note.holdTime) << " slideStart=" << Bits(note.slideStartTime)
            << " slide=" << Bits(note.slideTime) << " area=" << static_cast<int>(note.touchArea) << " flags="
            << Flags({ note.isBreak, note.isEx, note.isFakeRotate, note.isForceStar, note.isHanabi,
                       note.isTapHeadSlide, note.isMine, note.usingSV, note.isEach, note.isStar, note.isStarDouble })
            << " raw=" << Escape(note.rawContent) << " related=";
        auto first = true;
        for (const auto index : note.relatedNotes)
        {
            out << (first ? "" : ",") << index;
            first = false;
        }
        out << '\n';
    }

    void DumpTiming(std::ostream& out, const char* kind, const MajSimai::SimaiTimingPointView& timing)
    {
        out << "timing kind=" << kind << " t=" << Bits(timing.GetTiming()) << " bpm=" << Bits(timing.GetBpm())
            << " hs=" << Bits(timing.GetHSpeed()) << " sv=" << Bits(timing.GetSVeloc())
            << " x=" << timing.GetRawTextPositionX() << " y=" << timing.GetRawTextPositionY()
            << " pos=" << timing.GetRawTextPosition() << " empty=" << timing.IsEmpty()
            << " raw=" << Escape(timing.GetRawContent()) << '\n';
        for (const auto note : timing.GetNotes())
        {
            DumpNote(out, note);
        }
    }

    void DumpTiming(std::ostream& out, const char* kind, const MajSimai::SimaiTimingPoint& timing)
    {
        out << "timing kind=" << kind << " t=" << Bits(timing.timing) << " bpm=" << Bits(timing.bpm)
            << " hs=" << Bits(timing.hSpeed) << " sv=" << Bits(timing.sVeloc) << " x=" << timing.rawTextPositionX
            << " y=" << timing.rawTextPositionY << " pos=" << timing.rawTextPosition << " empty=" << timing.isEmpty
            << " raw=" << Escape(timing.rawContent) << '\n';
        for (const auto& note : timing.notes)
        {
            DumpNote(out, note);
        }
    }

    std::string Dump(const MajSimai::SimaiFileView& file)
    {
        std::ostringstream out;
        out << "file title=" << Escape(file.GetTitle()) << " artist=" << Escape(file.GetArtist())
            << " designer=" << Escape(file.GetFinalDesigner()) << " offset=" << Bits(file.GetOffset())
            << " charts=" << file.GetCharts().Size() << " commands=" << file.GetCommands().Size() << '\n';
        for (const auto command : file.GetCommands())
        {
            out << "command " << Escape(command.GetPrefix()) << '=' << Escape(command.GetValue()) << '\n';
        }
        for (const auto chart : file.GetCharts())
        {
            out << "chart level=" << Escape(chart.GetLevel()) << " designer=" << Escape(chart.GetDesigner())
                << " empty=" << chart.IsEmpty() << " fumen=" << Escape(chart.GetFumen()) << '\n';
            for (const auto timing : chart.GetNoteTimings())
            {
                DumpTiming(out, "note", timing);
            }
            for (const auto timing : chart.GetCommaTimings())
            {
                DumpTiming(out, "comma", timing);
            }
        }

        return out.str();
    }

    std::string Dump(const MajSimai::SimaiFile& file)
    {
        std::ostringstream out;
        out << "file title=" << Escape(file.title) << " artist=" << Escape(file.artist)
            << " designer=" << Escape(file.finalDesigner) << " offset=" << Bits(file.offset)
            << " charts=" << file.charts.size() << " commands=" << file.commands.size() << '\n';
        for (const auto& command : file.commands)
        {
            out << "command " << Escape(command.prefix) << '=' << Escape(command.value) << '\n';
        }
        for (const auto& chart : file.charts)
        {
            out << "chart level=" << Escape(chart.level) << " designer=" << Escape(chart.designer)
                << " empty=" << chart.isEmpty << " fumen=" << Escape(chart.fumen) << '\n';
            for (const auto& timing : chart.noteTimings)
            {
                DumpTiming(out, "note", timing);
            }
            for (const auto& timing : chart.commaTimings)
            {
                DumpTiming(out, "comma", timing);
            }
        }

        return out.str();
    }

    std::string ReadFile(const char* path)
    {
        std::ifstream stream(path, std::ios::binary);
        if (!stream)
        {
            throw std::runtime_error(std::string("Cannot read ") + path);
        }

        return std::string(std::istreambuf_iterator<char>(stream), std::istreambuf_iterator<char>());
    }

    // Parses one input and prints its dump or its error. Returns false when the view and the model disagree.
    bool Run(std::ostream& out, const std::string& content)
    {
        try
        {
            MajSimai::ParsedSimaiFile parsed = MajSimai::SimaiParser::ParseAsView(content);
            MajSimai::ParsedSimaiFile moved = std::move(parsed);
            const auto fromView = Dump(moved);
            const auto fromModel = Dump(MajSimai::SimaiParser::Parse(content));
            if (fromView != fromModel)
            {
                std::cerr << "The view and the model differ.\n--- view\n" << fromView << "--- model\n" << fromModel;
                return false;
            }
            out << fromView;
        }
        catch (const MajSimai::SimaiParseException& e)
        {
            out << "error code=" << static_cast<int>(e.GetCode()) << " line=" << e.GetLine()
                << " column=" << e.GetColumn() << " message=" << Escape(e.what())
                << " content=" << Escape(e.GetContent()) << '\n';
        }

        return true;
    }
}

int main(int argc, char** argv)
{
    if (argc < 2)
    {
        std::cerr << "Usage: BindingTest <dump file> <file | --empty>...\n";
        return 2;
    }
    std::ofstream out(argv[1], std::ios::binary);
    for (auto i = 2; i < argc; i++)
    {
        const std::string argument = argv[i];
        out << "input " << (argument == "--empty" ? "--empty" : "file") << '\n';
        if (!Run(out, argument == "--empty" ? std::string() : ReadFile(argv[i])))
        {
            return 3;
        }
    }
    out.close();

    return out ? 0 : 4;
}
