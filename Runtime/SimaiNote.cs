using System.Runtime.InteropServices;

namespace MajSimai
{
    public class SimaiNote
    {
        public SimaiNoteType Type { get; set; }
        public int StartPosition { get; set; } = 1; //键位（1-8）

        public double HoldTime { get; set; }
        public bool IsBreak { get; set; }
        public bool IsEx { get; set; }
        public bool IsFakeRotate { get; set; }
        public bool IsForceStar { get; set; }
        public bool IsHanabi { get; set; }
        public bool IsSlideBreak { get; set; }
        public bool IsSlideNoHead { get; set; }
        public bool IsTapHeadSlide { get; set; }
        public bool IsMine { get; set; } //炸弹音符
        public bool IsMineSlide { get; set; }
        public bool UsingSV { get; set; }

        public string RawContent { get; set; } = ""; //used for star explain

        public double SlideStartTime { get; set; }
        public double SlideTime { get; set; }
        public char TouchArea { get; set; } = ' ';

#if NET7_0_OR_GREATER
        internal unsafe MajSimai.Unmanaged.UnmanagedSimaiNote ToUnmanaged()
        {
            var rawContentPtr = (char*)null;
            if (!string.IsNullOrEmpty(RawContent))
            {
                rawContentPtr = (char*)Marshal.StringToHGlobalAnsi(RawContent);
            }

            return new()
            {
                type = Type,
                startPosition = StartPosition,
                holdTime = HoldTime,
                slideTime = SlideTime,
                slideStartTime = SlideStartTime,

                isBreak = IsBreak,
                isFakeRotate = IsFakeRotate,
                isForceStar = IsForceStar,
                isHanabi = IsHanabi,
                isSlideBreak = IsSlideBreak,
                isEx = IsEx,
                isMine = IsMine,
                isMineSlide = IsMineSlide,
                usingSV = UsingSV,
                isSlideNoHead = IsSlideNoHead,
                isTapHeadSlide = IsTapHeadSlide,
                touchArea = TouchArea,

                rawContent = rawContentPtr,
                rawContentLen = RawContent.Length
            };
        }
#endif
    }
}
