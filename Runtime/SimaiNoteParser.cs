using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
#nullable enable
namespace MajSimai
{
    using zString = ReadOnlySpan<char>;
    internal static class SimaiNoteParser
    {
        internal static SimaiNote[] GetNotes(double timing, double bpm, string noteContent)
        {
            var simaiNotes = new List<SimaiNote>();
            GetNotes(timing, bpm, noteContent, simaiNotes);

            return simaiNotes.ToArray();
        }
        internal static void GetNotes(double timing, double bpm, zString noteContent, IList<SimaiNote> buffer)
        {
            if (noteContent.IsEmpty)
            {
                return;
            }
            try
            {
                if (noteContent.Length == 2 && int.TryParse(noteContent, out _)) //连写数字
                {
                    if (TryGetSingleNote(timing, bpm, noteContent.Slice(0, 1), out var note1))
                    {
                        buffer.Add(note1);
                    }
                    if (TryGetSingleNote(timing, bpm, noteContent.Slice(1, 1), out var note2))
                    {
                        buffer.Add(note2);
                    }
                    return;
                }
                var tagCount = noteContent.Count('/');
                var rentedArray = ArrayPool<Range>.Shared.Rent(tagCount + 1);

                try
                {
                    var ranges = rentedArray.AsSpan(0, tagCount + 1);
                    noteContent.Split(ranges, '/');

                    if (tagCount != 0)
                    {
                        for (var i = 0; i < ranges.Length; i++)
                        {
                            var range = ranges[i];
                            var noteText = noteContent[range];

                            if (noteText.IsEmpty)
                            {
                                continue;
                            }
                            if (noteText.Contains('*'))
                            {
                                GetSameHeadSlide(timing, bpm, noteText, buffer);
                            }
                            else
                            {
                                if (TryGetSingleNote(timing, bpm, noteText, out var simaiNote))
                                {
                                    buffer.Add(simaiNote);
                                }
                                else
                                {
                                    Debug.WriteLine($"Cannot parse note from text: \"{new string(noteText)}\"");
                                }
                            }
                        }
                    }
                    else
                    {
                        if (noteContent.Contains('*'))
                        {
                            GetSameHeadSlide(timing, bpm, noteContent, buffer);
                        }
                        else
                        {
                            if (TryGetSingleNote(timing, bpm, noteContent, out var note))
                            {
                                buffer.Add(note);
                            }
                            else
                            {
                                Debug.WriteLine($"Cannot parse note from text: \"{new string(noteContent)}\"");
                            }
                        }
                    }
                }
                finally
                {
                    ArrayPool<Range>.Shared.Return(rentedArray, true);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                return;
            }
        }

        internal static void GetSameHeadSlide(double timing, double bpm, zString content, IList<SimaiNote> buffer)
        {
            Span<Range> ranges = stackalloc Range[content.Count('*') + 1];
            _ = content.Split(ranges, '*', StringSplitOptions.RemoveEmptyEntries);
            if (TryGetSingleNote(timing, bpm, content[ranges[0]], out var note1))
            {
                buffer.Add(note1);
            }
            else
            {
                Debug.WriteLine($"Cannot parse slide from text: \"{new string(content[ranges[0]])}\"");
                return;
            }
            //var newNoteContent = noteContents.ToList();
            //newNoteContent.RemoveAt(0);
            ////删除第一个NOTE

            for (var i = 1; i < ranges.Length; i++)
            {
                var partNoteText = content[ranges[i]];
                var rentedArray = ArrayPool<char>.Shared.Rent(partNoteText.Length + 1);
                try
                {
                    rentedArray[0] = content[ranges[0]][0];
                    var noteText = rentedArray.AsSpan();
                    partNoteText.CopyTo(noteText.Slice(1));
                    noteText = noteText.Slice(0, partNoteText.Length + 1);

                    if (TryGetSingleNote(timing, bpm, noteText, out var note2))
                    {
                        note2.IsSlideNoHead = true;
                        buffer.Add(note2);
                    }
                    else
                    {
                        Debug.WriteLine($"Cannot parse slide from text: \"{new string(noteText)}\"");
                        continue;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(rentedArray, true);
                }
            }
        }

        internal static bool TryGetSingleNote(double timing, double bpm, zString noteText, [NotNullWhen(true)] out SimaiNote? outSimaiNote)
        {
            outSimaiNote = default;
            Span<char> noteTextCopy = stackalloc char[noteText.Length];
            noteText.CopyTo(noteTextCopy);
            var detectResult = NoteHelper.NoteFlag.Detect(noteText, noteTextCopy);
            noteTextCopy = detectResult.NoteContent;

            var simaiNote = new SimaiNote();

            if (detectResult.IsTouchNote)
            {
                simaiNote.TouchArea = noteTextCopy[0];
                if (simaiNote.TouchArea != 'C')
                {
                    if (noteTextCopy.Length < 2)
                    {
                        return false;
                    }
                    else if (int.TryParse(noteTextCopy.Slice(1, 1), out var startPosition))
                    {
                        simaiNote.StartPosition = startPosition;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    simaiNote.StartPosition = 8;
                }
                simaiNote.Type = SimaiNoteType.Touch;
            }
            else
            {
                if (int.TryParse(noteTextCopy.Slice(0, 1), out var startPosition))
                {
                    simaiNote.StartPosition = startPosition;
                }
                else
                {
                    return false;
                }
                simaiNote.Type = SimaiNoteType.Tap; //if nothing happen in following if
            }
            simaiNote.IsHanabi = detectResult.IsHanabi;

            //hold
            if (detectResult.IsHold)
            {
                if (detectResult.IsTouchNote)
                {
                    simaiNote.Type = SimaiNoteType.TouchHold;
                    if (NoteHelper.TryGetHoldTimeFromBeats(bpm, noteTextCopy, out var holdTime))
                    {
                        simaiNote.HoldTime = holdTime;
                    }
                    else
                    {
                        simaiNote.HoldTime = 0;
                    }
                    //Console.WriteLine("Hold:" +simaiNote.touchArea+ simaiNote.startPosition + " TimeLastFor:" + simaiNote.holdTime);
                }
                else
                {
                    simaiNote.Type = SimaiNoteType.Hold;
                    if (noteTextCopy[^1] == 'h')
                    {
                        simaiNote.HoldTime = 0;
                    }
                    else
                    {
                        if (NoteHelper.TryGetHoldTimeFromBeats(bpm, noteTextCopy, out var holdTime))
                        {
                            simaiNote.HoldTime = holdTime;
                        }
                        else
                        {
                            simaiNote.HoldTime = 0;
                        }
                    }
                    //Console.WriteLine("Hold:" + simaiNote.startPosition + " TimeLastFor:" + simaiNote.holdTime);
                }
            }

            //slide
            if (detectResult.IsSlide)
            {
                simaiNote.Type = SimaiNoteType.Slide;
                if (!NoteHelper.TryGetSlideParams(bpm, noteTextCopy, out var slideParams))
                {
                    return false;
                }
                var (slideWaitTime, slideTime) = slideParams;
                simaiNote.SlideTime = slideTime;
                simaiNote.SlideStartTime = timing + slideWaitTime;
                if (detectResult.IsSlideNoHead)
                {
                    simaiNote.IsSlideNoHead = true;
                    //simaiNote.SlideStartTime = timing;
                }
                else if (detectResult.IsSlideNoHeadAndDelay)
                {
                    simaiNote.IsSlideNoHead = true;
                    //simaiNote.SlideStartTime = timing + slideWaitTime;
                }
                //Console.WriteLine("Slide:" + simaiNote.startPosition + " TimeLastFor:" + simaiNote.slideTime);
            }

            //break
            simaiNote.IsBreak = detectResult.IsBreak;
            simaiNote.IsSlideBreak = detectResult.IsBreakSlide;

            //EX
            simaiNote.IsEx = detectResult.IsEx;

            //starHead
            simaiNote.IsForceStar = detectResult.IsForceStar;
            simaiNote.IsFakeRotate = detectResult.IsFakeRotate;
            simaiNote.IsTapHeadSlide = detectResult.IsTapHeadSlide;

            // mine
            simaiNote.IsMine = detectResult.IsMine;
            simaiNote.IsMineSlide = detectResult.IsMineSlide;

            //UsingSV
            simaiNote.UsingSV = detectResult.UsingSV;

            simaiNote.RawContent = new string(noteTextCopy.Trim());
            outSimaiNote = simaiNote;
            return true;
        }


        static class NoteHelper
        {
            public static bool TryGetSlideParams(double bpm, zString noteText, out (double slideWaitTime, double slideTime) outParams)
            {
                outParams = (default, default);
                // 组合slide 有多个时长
                var slideWaitTime = 0d;
                var slideTime = 0d;
                var customBpm = (double?)null;
                var nextStartIndex = 0;
                var startIndex = 0;
                Span<Range> ranges = stackalloc Range[4];
                startIndex = noteText[nextStartIndex..].IndexOf('[');
                if (startIndex == -1)
                {
                    return false;
                }
                do
                {
                    var slideBody = noteText[(nextStartIndex + startIndex)..];
                    var overIndex = slideBody.IndexOf(']');
                    if (overIndex == -1)
                    {
                        return false;
                    }

                    nextStartIndex += overIndex + startIndex + 1;
                    var slideParamsBody = slideBody.Slice(1, overIndex - 1);
                    var tagCount = slideParamsBody.Split(ranges, '#', StringSplitOptions.None);

                    switch (tagCount)
                    {
                        case 1: // [8:1]
                            {
                                if (!TryGetTimeFromRatio(bpm, slideParamsBody, out var time))
                                {
                                    return false;
                                }
                                slideTime += time;
                            }
                            break;
                        case 2: // [160#8:3] or [160#2s] or [#8:1] or [8:1#]
                            {
                                var param1 = slideParamsBody[ranges[0]];
                                var param2 = slideParamsBody[ranges[1]];
                                var isAnyParamEmpty = param1.IsEmpty || param2.IsEmpty;
                                if (isAnyParamEmpty || !double.TryParse(param1, out var cBpm))
                                {
                                    return false;
                                }
                                if (double.TryParse(param2, out var time)) // [160#2s]
                                {
                                    slideTime += time;
                                }
                                else if (TryGetTimeFromRatio(cBpm, param2, out time)) // [160#8:3]
                                {
                                    slideTime += time;
                                }
                                else // undefined behavior
                                {
                                    return false;
                                }
                                customBpm ??= cBpm;
                            }
                            break;
                        case 3: // [3s##1.5s] or [3s##8:3]
                            {
                                var param1 = slideParamsBody[ranges[0]];
                                var param2 = slideParamsBody[ranges[2]];
                                var isAnyParamEmpty = param1.IsEmpty || param2.IsEmpty;
                                if (isAnyParamEmpty || !double.TryParse(param1, out var cWaitTime))
                                {
                                    return false;
                                }
                                if (double.TryParse(param2, out var time)) // [3s##1.5s]
                                {
                                    slideTime += time;
                                }
                                else if (TryGetTimeFromRatio(bpm, param2, out time)) // [3s##8:3]
                                {
                                    slideTime += time;
                                }
                                else // undefined behavior
                                {
                                    return false;
                                }
                                customBpm ??= (60d / cWaitTime);
                            }
                            break;
                        case 4: // [3s##160#8:3]
                            {
                                var param1 = slideParamsBody[ranges[0]];
                                var param2 = slideParamsBody[ranges[2]];
                                var param3 = slideParamsBody[ranges[3]];
                                var isAnyParamEmpty = param1.IsEmpty || param2.IsEmpty || param3.IsEmpty;
                                if (isAnyParamEmpty || !double.TryParse(param1, out var cWaitTime))
                                {
                                    return false;
                                }
                                if (!double.TryParse(param2, out var cBpm))
                                {
                                    return false;
                                }
                                if (!TryGetTimeFromRatio(cBpm, param3, out var time))
                                {
                                    return false;
                                }
                                slideTime += time;
                                customBpm ??= (60d / cWaitTime);
                            }
                            break;
                        default://undefined behavior
                            return false;
                    }
                } while ((startIndex = noteText[nextStartIndex..].IndexOf('[')) != -1);

                slideWaitTime = 1d / ((customBpm ?? bpm) / 60d);

                outParams = (slideWaitTime, slideTime);
                return true;
            }
            //1: 是星星头的break
            //2: 是slide本体的break
            public static (bool isBreak, bool isBreakSlide) CheckHeadOrSlide(zString noteText, char detectChar)
            {
                // 如果是Slide 则要检查这个b到底是星星头的还是Slide本体的

                // !!! **SHIT CODE HERE** !!!
                bool isBreak = false;
                bool isBreakSlide = false;
                var startIndex = 0;
                while ((startIndex = noteText.Slice(startIndex).IndexOf(detectChar)) != -1)
                {
                    if (startIndex < noteText.Length - 1)
                    {
                        // 如果b不是最后一个字符 我们就检查b之后一个字符是不是`[`符号：如果是 那么就是break slide
                        // startIndex + 1 < noteText.Length 防越界
                        if (startIndex + 1 < noteText.Length && noteText[startIndex + 1] == '[')
                        {
                            isBreakSlide = true;
                        }
                        else
                        {
                            // 否则 那么不管这个break出现在slide的哪一个地方 我们都认为他是星星头的break
                            // SHIT CODE!
                            isBreak = true;
                        }
                    }
                    else
                    {
                        // 如果b符号是整个文本的最后一个字符 那么也是break slide（Simai语法）
                        isBreakSlide = true;
                    }

                    startIndex++;
                }
                return (isBreak, isBreakSlide);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSlideNote(zString noteText)
            {
                const string SLIDE_MARKS = "-^v<>Vpqszw";
                foreach (var mark in SLIDE_MARKS)
                {
                    if (noteText.Contains(mark))
                    {
                        return true;
                    }
                }
                return false;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTouchNote(zString noteText)
            {
                if (noteText.IsEmpty)
                {
                    return false;
                }
                //const string TOUCH_MARKS = "ABCDE";

                var c = noteText[0];

                return c >= 'A' && c <= 'E';
                //foreach (var mark in TOUCH_MARKS)
                //{
                //    if (noteText.StartsWith(mark))
                //    {
                //        return true;
                //    }
                //}
                //return false;
            }
            public static bool TryGetHoldTimeFromBeats(double bpm, zString noteText, out double time)
            {
                time = default;
                var startIndex = noteText.IndexOf('[');
                var endIndex = noteText.IndexOf(']');
                if (startIndex == -1 || endIndex == -1)
                {
                    time = 0;
                    return false;
                }
                var holdParamsBody = noteText.Slice(startIndex + 1, endIndex - startIndex - 1);
                Span<Range> ranges = stackalloc Range[2];
                var tagCount = holdParamsBody.Split(ranges, '#', StringSplitOptions.None);

                switch (tagCount)
                {
                    case 1: // 2h[8:3]
                        return TryGetTimeFromRatio(bpm, holdParamsBody, out time);
                    case 2: // 2h[#5.678] or 2h[150#2:1]
                        var param1 = holdParamsBody[ranges[0]];
                        var param2 = holdParamsBody[ranges[1]];

                        if (param1.IsEmpty) //2h[#5.678]
                        {
                            return double.TryParse(param2, out time);
                        }
                        else //2h[150#2:1]
                        {
                            if (param2.IsEmpty)
                            {
                                return false;
                            }
                            else if (!double.TryParse(param1, out bpm))
                            {
                                return false;
                            }
                            return TryGetTimeFromRatio(bpm, param2, out time);
                        }
                    default:
                        return false; //undefined behavior
                }
            }
            /// <summary>
            /// Calculates the <paramref name="time"/> from <paramref name="noteText"/> in "x:y" format according to a given <paramref name="bpm"/>
            /// </summary>
            /// <param name="bpm"></param>
            /// <param name="noteText"></param>
            /// <param name="time"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.NoInlining)]
            static bool TryGetTimeFromRatio(double bpm, zString noteText, out double time)
            {
                time = default;
                var timeOneBeat = 1d / (bpm / 60d);
                Span<Range> ranges = stackalloc Range[2];
                var tagCount = noteText.Split(ranges, ':', StringSplitOptions.None);
                if (tagCount != 2)
                {
                    return false;
                }
                var divideStr = noteText[ranges[0]];
                var countStr = noteText[ranges[1]];
                if (divideStr.IsEmpty || countStr.IsEmpty)
                {
                    return false;
                }
                if (!int.TryParse(divideStr, out var divide) || !int.TryParse(countStr, out var count))
                {
                    return false;
                }
                time = timeOneBeat * 4d / divide * count;

                return true;
            }

            public readonly ref struct NoteFlag
            {
                public readonly bool IsTouchNote;           // ABCDE
                public readonly bool IsBreak;               // b
                public readonly bool IsBreakSlide;          // b
                public readonly bool IsHanabi;              // f
                public readonly bool IsHold;                // h
                public readonly bool IsSlide;               // -^v<>Vpqszw
                public readonly bool IsSlideNoHead;         // !
                public readonly bool IsSlideNoHeadAndDelay; // ?
                public readonly bool IsTapHeadSlide;        // @
                public readonly bool IsEx;                  // x
                public readonly bool IsForceStar;           // $
                public readonly bool IsFakeRotate;          // $$
                public readonly bool IsMine;                // m
                public readonly bool IsMineSlide;           // m
                public readonly bool UsingSV;               // c

                public readonly Span<char> NoteContent;

                public NoteFlag(bool isTouchNote,
                                bool isBreak,
                                bool isBreakSlide,
                                bool isHanabi,
                                bool isHold,
                                bool isSlide,
                                bool isSlideNoHead,
                                bool isSlideNoHeadAndDelay,
                                bool isTapHeadSlide,
                                bool isEx,
                                bool isForceStar,
                                bool isFakeRotate,
                                bool isMine,
                                bool isMineSlide,
                                bool usingSV,
                                Span<char> noteContent)
                {
                    IsTouchNote = isTouchNote;
                    IsBreak = isBreak;
                    IsBreakSlide = isBreakSlide;
                    IsHanabi = isHanabi;
                    IsHold = isHold;
                    IsSlide = isSlide;
                    IsSlideNoHead = isSlideNoHead;
                    IsSlideNoHeadAndDelay = isSlideNoHeadAndDelay;
                    IsTapHeadSlide = isTapHeadSlide;
                    IsEx = isEx;
                    IsForceStar = isForceStar;
                    IsFakeRotate = isFakeRotate;
                    IsMine = isMine;
                    IsMineSlide = isMineSlide;
                    NoteContent = noteContent;
                    UsingSV = usingSV;
                }

                public static NoteFlag Detect(zString noteContent, Span<char> dst)
                {
                    if (noteContent.IsEmpty)
                    {
                        return default;
                    }
                    // Flags
                    var isTouchNote = false;
                    var isBreak = false;
                    var isBreakSlide = false;
                    var isHanabi = false;
                    var isHold = false;
                    var isSlide = false;
                    var isSlideNoHead = false;
                    var isSlideNoHeadAndDelay = false;
                    var isTapHeadSlide = false;
                    var isEx = false;
                    var isForceStar = false;
                    var isFakeRotate = false;
                    var isMine = false;
                    var isMineSlide = false;
                    var usingSV = true;

                    var forceStarTagCount = 0;
                    var j = 0;
                    for (var i = 0; i < noteContent.Length; i++)
                    {
                        ref readonly var curChar = ref noteContent[i];
                        switch (curChar)
                        {
                            case '-':
                            case '^':
                            case 'v':
                            case '<':
                            case '>':
                            case 'V':
                            case 'p':
                            case 'q':
                            case 's':
                            case 'z':
                            case 'w':
                            case 'K':
                                isSlide = true;
                                break;
                            case 'A':
                            case 'B':
                            case 'C':
                            case 'D':
                            case 'E':
                                isTouchNote = true;
                                break;
                            case 'f':
                                isHanabi = true;
                                break;
                            case 'x':
                                isEx = true;
                                continue;
                            case 'c':
                                usingSV = false;
                                continue;
                            case 'h':
                                isHold = true;
                                break;
                            case '!':
                                isSlideNoHead = true;
                                continue;
                            case '?':
                                isSlideNoHeadAndDelay = true;
                                continue;
                            case '@':
                                isTapHeadSlide = true;
                                continue;
                            case '$':
                                forceStarTagCount++;
                                continue;
                            case 'b':
                                {
                                    if (isBreak && isBreakSlide)
                                    {
                                        continue;
                                    }
                                    if (isSlide)
                                    {
                                        if (i != noteContent.Length - 1) // 1-3b[8:1]
                                        {
                                            isBreakSlide |= noteContent[i + 1] == '[';
                                        }
                                        else // 1-3[8:1]b
                                        {
                                            isBreakSlide = true;
                                        }
                                    }
                                    else
                                    {
                                        isBreak = true;
                                    }
                                }
                                continue;
                            case 'm':
                                {
                                    if (isMine && isMineSlide)
                                    {
                                        continue;
                                    }
                                    if (isSlide)
                                    {
                                        if (i != noteContent.Length - 1) // 1-3m[8:1]
                                        {
                                            isMineSlide |= noteContent[i + 1] == '[';
                                        }
                                        else // 1-3[8:1]m
                                        {
                                            isMineSlide = true;
                                        }
                                    }
                                    else
                                    {
                                        isMine = true;
                                    }
                                }
                                continue;
                        }
                        dst[j++] = curChar;
                    }

                    isForceStar = forceStarTagCount > 0;
                    isFakeRotate = forceStarTagCount > 1;

                    if (isSlide)
                    {
                        isHold = false;
                        isTouchNote = false;
                    }

                    return new NoteFlag(isTouchNote,
                                        isBreak,
                                        isBreakSlide,
                                        isHanabi,
                                        isHold,
                                        isSlide,
                                        isSlideNoHead,
                                        isSlideNoHeadAndDelay,
                                        isTapHeadSlide,
                                        isEx,
                                        isForceStar,
                                        isFakeRotate,
                                        isMine,
                                        isMineSlide,
                                        usingSV,
                                        dst.Slice(0, j));
                }
            }
        }
    }
}
