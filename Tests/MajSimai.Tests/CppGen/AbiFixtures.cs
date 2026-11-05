using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MajSimai.Unmanaged;

namespace MajSimai.Tests.CppGen;

/// <summary>
/// Small native ABIs, valid and broken, that the generator tests read. The functions are never called.
/// </summary>
internal static unsafe class AbiFixtures
{
    /// <summary>
    /// An enum with a 64-bit underlying type.
    /// </summary>
    public enum WideEnum : long
    {
        /// <summary>The first member.</summary>
        First = 1,

        /// <summary>A large member.</summary>
        Large = 5_000_000_000,
    }

    /// <summary>
    /// Takes a valid handle struct.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_Valid", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Valid(ValidHandle* handle)
    {
    }

    /// <summary>
    /// Takes text and a handle with an unannotated pointer.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="textLength">The length of the text.</param>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_Unannotated", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Unannotated([NativeString(nameof(textLength))] byte* text, int textLength, UnmanagedUnannotated* handle)
    {
    }

    /// <summary>
    /// Takes a struct whose text is not a byte pointer.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_WrongStringType", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void WrongStringType(WrongStringTypeHandle* handle)
    {
    }

    /// <summary>
    /// Takes a struct whose array names a length that does not exist.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_MissingLength", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void MissingLength(MissingLengthHandle* handle)
    {
    }

    /// <summary>
    /// Takes a struct whose array length is not an int.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_WideLength", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void WideLength(WideLengthHandle* handle)
    {
    }

    /// <summary>
    /// Takes a struct that holds another struct by value.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_InlineStruct", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void InlineStruct(InlineStructHandle* handle)
    {
    }

    /// <summary>
    /// Takes a packed struct, whose layout differs from the C++ layout.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_Packed", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void Packed(PackedHandle* handle)
    {
    }

    /// <summary>
    /// Takes a handle whose array points to a data struct without the Unmanaged prefix.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_BadDataName", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void BadDataName(BadDataNameHandle* handle)
    {
    }

    /// <summary>
    /// Takes a handle whose data struct has a single-struct pointer.
    /// </summary>
    /// <param name="handle">The handle.</param>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_DataPointee", CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void DataPointee(DataPointeeHandle* handle)
    {
    }

    /// <summary>
    /// Does not say its calling convention.
    /// </summary>
    [UnmanagedCallersOnly(EntryPoint = "Fixture_NoCallingConvention")]
    public static void NoCallingConvention()
    {
    }

    /// <summary>
    /// A valid handle struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ValidHandle
    {
        /// <summary>A constant.</summary>
        public const long Big = 5_000_000_000;

        /// <summary>A wide enum.</summary>
        public WideEnum kind;

        /// <summary>A UTF-16 character.</summary>
        public char letter;

        /// <summary>The number of items.</summary>
        public int itemsLen;

        /// <summary>The items.</summary>
        [NativeArray(nameof(itemsLen))]
        public UnmanagedItem* items;

        /// <summary>The first item.</summary>
        [NativePointee(typeof(UnmanagedItem))]
        public void* first;
    }

    /// <summary>
    /// A valid data struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedItem
    {
        /// <summary>The length of the name.</summary>
        public int nameLen;

        /// <summary>The name.</summary>
        [NativeString(nameof(nameLen))]
        public byte* name;
    }

    /// <summary>
    /// A struct with a pointer that does not say what it points to.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedUnannotated
    {
        /// <summary>The values.</summary>
        public int* values;
    }

    /// <summary>
    /// A struct whose text is not a byte pointer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WrongStringTypeHandle
    {
        /// <summary>The length of the text.</summary>
        public int textLen;

        /// <summary>The text.</summary>
        [NativeString(nameof(textLen))]
        public char* text;
    }

    /// <summary>
    /// A struct whose array names a length that does not exist.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MissingLengthHandle
    {
        /// <summary>The values.</summary>
        [NativeArray("valuesCount")]
        public int* values;
    }

    /// <summary>
    /// A struct whose array length is not an int.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WideLengthHandle
    {
        /// <summary>The number of values.</summary>
        public long valuesLen;

        /// <summary>The values.</summary>
        [NativeArray(nameof(valuesLen))]
        public int* values;
    }

    /// <summary>
    /// A struct that holds another struct by value.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InlineStructHandle
    {
        /// <summary>The item.</summary>
        public UnmanagedItem item;
    }

    /// <summary>
    /// A packed struct.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PackedHandle
    {
        /// <summary>A byte.</summary>
        public byte small;

        /// <summary>An int that is not aligned.</summary>
        public int large;
    }

    /// <summary>
    /// A handle whose array points to a data struct without the Unmanaged prefix.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BadDataNameHandle
    {
        /// <summary>The number of items.</summary>
        public int itemsLen;

        /// <summary>The items.</summary>
        [NativeArray(nameof(itemsLen))]
        public PackedHandle* items;
    }

    /// <summary>
    /// A handle whose data struct has a single-struct pointer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DataPointeeHandle
    {
        /// <summary>The number of items.</summary>
        public int itemsLen;

        /// <summary>The items.</summary>
        [NativeArray(nameof(itemsLen))]
        public UnmanagedWithPointee* items;
    }

    /// <summary>
    /// A data struct with a single-struct pointer.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedWithPointee
    {
        /// <summary>The item.</summary>
        [NativePointee(typeof(UnmanagedItem))]
        public void* item;
    }
}
