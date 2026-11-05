using System;
using System.Reflection;
using System.Reflection.Emit;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// Measures how the runtime lays out a struct in memory, which is the layout native code sees through a pointer.
/// </summary>
/// <remarks>
/// <c>Marshal.OffsetOf</c> and <c>Marshal.SizeOf</c> cannot be used: they describe the marshalled layout, in which a
/// <see cref="bool"/> takes 4 bytes and a <see cref="char"/> 1 byte. The probe emits the IL <c>sizeof</c> and
/// <c>ldflda</c> instructions instead, which use the real layout.
/// </remarks>
internal static class LayoutProbe
{
    /// <summary>
    /// Gets the size of a struct in bytes.
    /// </summary>
    /// <param name="type">The struct.</param>
    /// <returns>The size, including padding at the end.</returns>
    public static int SizeOf(Type type)
    {
        var method = CreateMethod("SizeOf");
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Sizeof, type);
        il.Emit(OpCodes.Ret);

        return Invoke(method);
    }

    /// <summary>
    /// Gets the offset of an instance field from the start of its struct.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>The offset in bytes.</returns>
    public static int OffsetOf(FieldInfo field)
    {
        var method = CreateMethod("OffsetOf");
        var il = method.GetILGenerator();
        il.DeclareLocal(field.DeclaringType!);
        il.Emit(OpCodes.Ldloca_S, (byte)0);
        il.Emit(OpCodes.Ldflda, field);
        il.Emit(OpCodes.Ldloca_S, (byte)0);
        il.Emit(OpCodes.Sub);
        il.Emit(OpCodes.Conv_I4);
        il.Emit(OpCodes.Ret);

        return Invoke(method);
    }

    /// <summary>
    /// Creates a method that returns an <see cref="int"/> and may use internal types of other assemblies.
    /// </summary>
    /// <param name="name">The name of the method.</param>
    /// <returns>The method, without a body.</returns>
    private static DynamicMethod CreateMethod(string name)
    {
        return new DynamicMethod(name, typeof(int), Type.EmptyTypes, typeof(LayoutProbe).Module, skipVisibility: true);
    }

    /// <summary>
    /// Runs a method created by <see cref="CreateMethod"/>.
    /// </summary>
    /// <param name="method">The method.</param>
    /// <returns>What the method returned.</returns>
    private static int Invoke(DynamicMethod method)
    {
        return ((Func<int>)method.CreateDelegate(typeof(Func<int>)))();
    }
}
