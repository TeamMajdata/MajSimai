#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai.Unmanaged;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnmanagedSimaiNote
{
    public SimaiNoteType type;
    public int startPosition;

    public double holdTime;
    public double slideStartTime;
    public double slideTime;
    public char touchArea;

    public bool isBreak;
    public bool isEx;
    public bool isFakeRotate;
    public bool isForceStar;
    public bool isHanabi;
    public bool isSlideBreak;
    public bool isSlideNoHead;
    public bool isTapHeadSlide;
    public bool isMine;
    public bool isMineSlide;
    public bool usingSV;
    public int rawContentLen;

    public char* rawContent;

    public void Free()
    {
        Marshal.FreeHGlobal((nint)rawContent);
        rawContent = null;
        rawContentLen = 0;
    }
}
#endif