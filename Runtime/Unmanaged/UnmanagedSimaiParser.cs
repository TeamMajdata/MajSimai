#if NET7_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MajSimai.Unmanaged;
public unsafe class UnmanagedSimaiParser
{
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Parse", CallConvs = new Type[] { typeof(CallConvCdecl) })]
    public static void Parse(char* contentPtr, int contentLen, UnmanagedSimaiParseResult* result)
    {
        var content = Marshal.PtrToStringAnsi((nint)contentPtr, contentLen);
        if (string.IsNullOrEmpty(content))
        {
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_INVALID_CONTENT
            };
            return;
        }
        try
        {
            var simaiFile = SimaiParser.Parse(content, string.Empty);
            var unmanagedSimaiFile = simaiFile.ToUnmanaged();
            var unmanagedSimaiFilePtr = (UnmanagedSimaiFile*)Marshal.AllocHGlobal(sizeof(UnmanagedSimaiFile));
            *unmanagedSimaiFilePtr = unmanagedSimaiFile;
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_NO_ERROR,
                simaiFile = unmanagedSimaiFilePtr
            };
        }
        catch(Exception e)
        {
            var errorMsg = e.ToString();
            var errorMsgPtr = (char*)null;
            if(!string.IsNullOrEmpty(errorMsg))
            {
                errorMsgPtr = (char*)Marshal.StringToHGlobalAnsi(errorMsg);
            }
            var eLine = -1;
            var eColumn = -1;
            var eContentPtr = (char*)null;
            var eContentLen = 0;
            if(e is InvalidSimaiMarkupException mE)
            {
                eLine = mE.Line;
                eColumn = mE.Column;
                if(!string.IsNullOrEmpty(mE.Content))
                {
                    eContentPtr = (char*)Marshal.StringToHGlobalAnsi(mE.Content);
                    eContentLen = mE.Content.Length;
                }
            }
            *result = new()
            {
                code = UnmanagedSimaiParseResult.CODE_INTERNAL_ERROR,
                errorMsgAnsi = errorMsgPtr,
                errorMsgLen = errorMsg.Length,
                errorAtLine = eLine,
                errorAtColumn = eColumn,
                errorContentAnsi = eContentPtr,
                errorContentLen = eContentLen,
            };
        }
    }
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_Free", CallConvs = new Type[] { typeof(CallConvCdecl) })]
    public static bool Free(UnmanagedSimaiParseResult* ptr)
    {
        try
        {
            if(ptr is null)
            {
                return false;
            }
            ptr->Free();
            return true;
        }
        catch
        {
            return false;
        }
    }
    [UnmanagedCallersOnly(EntryPoint = "MajSimai_FreeHGlobal", CallConvs = new Type[] { typeof(CallConvCdecl) })]
    public static bool Free(nint ptr)
    {
        try
        {
            if(ptr == 0)
            {
                return false;
            }
            Marshal.FreeHGlobal(ptr);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
#endif
