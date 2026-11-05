#if NET7_0_OR_GREATER
using System.Runtime.CompilerServices;

// The native entry points pass bool and char values as they are laid out in memory: 1 byte for bool and 2 bytes for
// char, the same as C++ bool and char16_t. With runtime marshalling enabled, the runtime rejects bool in the signature
// of an UnmanagedCallersOnly method because it would have to convert it to a 4-byte Win32 BOOL.
[assembly: DisableRuntimeMarshalling]
#endif
