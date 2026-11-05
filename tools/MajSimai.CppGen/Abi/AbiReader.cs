using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MajSimai.Unmanaged;

namespace MajSimai.CppGen.Abi;

/// <summary>
/// Reads the native ABI from compiled exports with reflection, the way FFmpeg.AutoGen reads C headers.
/// </summary>
/// <remarks>
/// Starting from functions marked with <c>[UnmanagedCallersOnly(EntryPoint = ...)]</c>, the reader follows every
/// parameter and field to the structs and enums they use. Pointer fields must say what they point to with
/// <see cref="NativeStringAttribute"/>, <see cref="NativeArrayAttribute"/> or <see cref="NativePointeeAttribute"/>.
/// The layout of every struct is measured from the runtime and checked against the layout a C++ compiler gives the
/// generated declaration.
/// </remarks>
internal sealed class AbiReader
{
    /// <summary>
    /// The prefix of the C# structs that hold parsed data; the C++ view and model drop it.
    /// </summary>
    public const string DataStructPrefix = "Unmanaged";

    /// <summary>
    /// The C++ types of the C# primitive types.
    /// </summary>
    private static readonly Dictionary<Type, AbiPrimitiveType> s_primitives = new()
    {
        [typeof(void)] = AbiPrimitiveType.Void,
        [typeof(bool)] = AbiPrimitiveType.Bool,
        [typeof(char)] = AbiPrimitiveType.Char16,
        [typeof(sbyte)] = new("int8_t", 1),
        [typeof(byte)] = new("uint8_t", 1),
        [typeof(short)] = new("int16_t", 2),
        [typeof(ushort)] = new("uint16_t", 2),
        [typeof(int)] = AbiPrimitiveType.Int32,
        [typeof(uint)] = new("uint32_t", 4),
        [typeof(long)] = new("int64_t", 8),
        [typeof(ulong)] = new("uint64_t", 8),
        [typeof(float)] = new("float", 4),
        [typeof(double)] = new("double", 8),
        [typeof(nint)] = new("intptr_t", NativeLayout.PointerSize),
        [typeof(nuint)] = new("uintptr_t", NativeLayout.PointerSize),
    };

    /// <summary>
    /// The documentation of the assembly.
    /// </summary>
    private readonly XmlDocumentation _docs;

    /// <summary>
    /// The structs that were read, by C# type.
    /// </summary>
    private readonly Dictionary<Type, AbiStruct> _structs = new();

    /// <summary>
    /// The structs that were read, in the order they were completed.
    /// </summary>
    private readonly List<AbiStruct> _structOrder = new();

    /// <summary>
    /// The structs that are being read, used to stop at a struct that points to itself.
    /// </summary>
    private readonly HashSet<Type> _structsInProgress = new();

    /// <summary>
    /// The enums that were read, by C# type.
    /// </summary>
    private readonly Dictionary<Type, AbiEnum> _enums = new();

    /// <summary>
    /// The enums that were read, in the order they were found.
    /// </summary>
    private readonly List<AbiEnum> _enumOrder = new();

    /// <summary>
    /// The structs that exported functions take by pointer.
    /// </summary>
    private readonly HashSet<Type> _handles = new();

    /// <summary>
    /// Creates a reader.
    /// </summary>
    /// <param name="docs">The documentation of the assembly that declares the ABI.</param>
    public AbiReader(XmlDocumentation docs)
    {
        _docs = docs;
    }

    /// <summary>
    /// Finds the functions an assembly exports with <c>[UnmanagedCallersOnly(EntryPoint = ...)]</c>.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>The functions, ordered by type name and then by declaration order.</returns>
    public static IReadOnlyList<MethodInfo> FindExports(Assembly assembly)
    {
        const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        return assembly.GetTypes()
                       .OrderBy(t => t.FullName, StringComparer.Ordinal)
                       .SelectMany(t => t.GetMethods(Flags).OrderBy(m => m.MetadataToken))
                       .Where(m => m.GetCustomAttribute<UnmanagedCallersOnlyAttribute>()?.EntryPoint is not null)
                       .ToArray();
    }

    /// <summary>
    /// Reads the ABI used by exported functions.
    /// </summary>
    /// <param name="exports">The exported functions.</param>
    /// <param name="version">The version of the library, written into the generated files.</param>
    /// <returns>The ABI.</returns>
    /// <exception cref="CppGenException">Thrown when the ABI uses something the generator cannot express in C++.</exception>
    public AbiModel Read(IReadOnlyList<MethodInfo> exports, string version)
    {
        foreach (var parameter in exports.SelectMany(m => m.GetParameters()))
        {
            if (parameter.ParameterType.IsPointer && IsStruct(parameter.ParameterType.GetElementType()!))
            {
                _handles.Add(parameter.ParameterType.GetElementType()!);
            }
        }
        var functions = exports.Select(ReadFunction).ToArray();
        var duplicate = functions.GroupBy(f => f.EntryPoint).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new CppGenException($"More than one function is exported as '{duplicate.Key}'.");
        }

        return new AbiModel(version, _enumOrder.ToArray(), _structOrder.ToArray(), functions);
    }

    /// <summary>
    /// Reads an exported function.
    /// </summary>
    /// <param name="method">The method marked with <c>[UnmanagedCallersOnly]</c>.</param>
    /// <returns>The function.</returns>
    private AbiFunction ReadFunction(MethodInfo method)
    {
        var location = $"{method.DeclaringType!.Name}.{method.Name}";
        var attribute = method.GetCustomAttribute<UnmanagedCallersOnlyAttribute>()
                        ?? throw new CppGenException($"{location} is not marked with [UnmanagedCallersOnly].");
        var entryPoint = attribute.EntryPoint ?? throw new CppGenException($"{location} has no EntryPoint.");
        var parameters = method.GetParameters();
        var abiParameters = new AbiParameter[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterLocation = $"parameter '{parameter.Name}' of {location}";
            var text = parameter.GetCustomAttribute<NativeStringAttribute>();
            if (text is not null)
            {
                var length = parameters.FirstOrDefault(p => p.Name == text.LengthName);
                CheckLength(length is not null, length?.ParameterType == typeof(int), text.LengthName, parameterLocation);
            }
            abiParameters[i] = new AbiParameter(parameter.Name!, ReadSignatureType(parameter.ParameterType, parameter, parameterLocation));
        }
        var returnType = ReadSignatureType(method.ReturnType, method.ReturnParameter, $"the return value of {location}");
        CheckRuntimeMarshalling(method, returnType, abiParameters);

        return new AbiFunction(entryPoint, ReadCallingConvention(attribute, location), returnType, abiParameters, _docs.Get(method));
    }

    /// <summary>
    /// Reads the calling convention of an exported function.
    /// </summary>
    /// <param name="attribute">The <c>[UnmanagedCallersOnly]</c> attribute.</param>
    /// <param name="location">The name of the function, for error messages.</param>
    /// <returns><c>cdecl</c> or <c>stdcall</c>.</returns>
    private static string ReadCallingConvention(UnmanagedCallersOnlyAttribute attribute, string location)
    {
        var conventions = attribute.CallConvs ?? Type.EmptyTypes;
        if (conventions.Length == 1 && conventions[0] == typeof(CallConvCdecl))
        {
            return "cdecl";
        }
        if (conventions.Length == 1 && conventions[0] == typeof(CallConvStdcall))
        {
            return "stdcall";
        }

        throw new CppGenException($"{location} must set CallConvs to exactly one of CallConvCdecl or CallConvStdcall, so C++ callers know how to call it.");
    }

    /// <summary>
    /// Checks that bool and char values in a signature are passed as they are laid out in memory.
    /// </summary>
    /// <param name="method">The exported method.</param>
    /// <param name="returnType">The C++ return type.</param>
    /// <param name="parameters">The C++ parameters.</param>
    private static void CheckRuntimeMarshalling(MethodInfo method, AbiType returnType, IEnumerable<AbiParameter> parameters)
    {
        var types = parameters.Select(p => p.Type).Append(returnType);
        var needsDisabledMarshalling = types.Any(t => t == AbiPrimitiveType.Bool || t == AbiPrimitiveType.Char16);
        if (needsDisabledMarshalling && method.Module.Assembly.GetCustomAttribute<DisableRuntimeMarshallingAttribute>() is null)
        {
            throw new CppGenException($"{method.DeclaringType!.Name}.{method.Name} passes bool or char, which the runtime only allows in an [UnmanagedCallersOnly] signature when the assembly has [assembly: DisableRuntimeMarshalling].");
        }
    }

    /// <summary>
    /// Reads the type of a parameter or return value.
    /// </summary>
    /// <param name="type">The C# type.</param>
    /// <param name="site">The parameter, whose attributes describe what a pointer points to.</param>
    /// <param name="location">The name of the parameter, for error messages.</param>
    /// <returns>The C++ type.</returns>
    private AbiType ReadSignatureType(Type type, ParameterInfo site, string location)
    {
        if (site.IsDefined(typeof(NativeStringAttribute)))
        {
            if (type != typeof(byte*))
            {
                throw new CppGenException($"[NativeString] on {location} requires the type byte*.");
            }

            return new AbiPointerType(AbiPrimitiveType.Utf8Char, IsConst: true);
        }
        if (type.IsPointer)
        {
            var element = type.GetElementType()!;
            if (element == typeof(void))
            {
                throw new CppGenException($"{location} is a void*; use a typed pointer so the generator knows what it points to.");
            }

            return new AbiPointerType(ReadPointee(element, location));
        }

        return ReadValueType(type, location);
    }

    /// <summary>
    /// Reads the type a pointer points to.
    /// </summary>
    /// <param name="type">The C# type of the pointee.</param>
    /// <param name="location">The name of the member, for error messages.</param>
    /// <returns>The C++ type.</returns>
    private AbiType ReadPointee(Type type, string location)
    {
        if (IsStruct(type))
        {
            return new AbiStructType(ReadStruct(type));
        }

        return ReadValueType(type, location);
    }

    /// <summary>
    /// Reads a primitive or enum type.
    /// </summary>
    /// <param name="type">The C# type.</param>
    /// <param name="location">The name of the member, for error messages.</param>
    /// <returns>The C++ type.</returns>
    private AbiType ReadValueType(Type type, string location)
    {
        if (type.IsEnum)
        {
            return new AbiEnumType(ReadEnum(type));
        }
        if (s_primitives.TryGetValue(type, out var primitive))
        {
            return primitive;
        }
        if (IsStruct(type))
        {
            throw new CppGenException($"{location} holds the struct {type.Name} by value; the generator only supports structs behind pointers.");
        }

        throw new CppGenException($"{location} has the type {type.Name}, which has no C++ equivalent in the native ABI.");
    }

    /// <summary>
    /// Reads an enum and remembers it.
    /// </summary>
    /// <param name="type">The C# enum.</param>
    /// <returns>The enum.</returns>
    private AbiEnum ReadEnum(Type type)
    {
        if (_enums.TryGetValue(type, out var known))
        {
            return known;
        }
        var underlying = s_primitives[Enum.GetUnderlyingType(type)];
        var members = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                          .OrderBy(f => f.MetadataToken)
                          .Select(f => new AbiEnumMember(f.Name, FormatInteger(f.GetRawConstantValue()!, f.Name), _docs.Get(f)))
                          .ToArray();
        var abiEnum = new AbiEnum(type.Name, underlying, members, _docs.Get(type));
        _enums[type] = abiEnum;
        _enumOrder.Add(abiEnum);

        return abiEnum;
    }

    /// <summary>
    /// Reads a struct, and the structs it points to, and remembers them.
    /// </summary>
    /// <param name="type">The C# struct.</param>
    /// <returns>The name of the struct.</returns>
    private string ReadStruct(Type type)
    {
        if (_structs.ContainsKey(type) || !_structsInProgress.Add(type))
        {
            return type.Name;
        }
        if (type.StructLayoutAttribute?.Value != LayoutKind.Sequential || type.IsGenericType)
        {
            throw new CppGenException($"The struct {type.Name} must be a non-generic struct with [StructLayout(LayoutKind.Sequential)].");
        }
        var isHandle = _handles.Contains(type);
        if (!isHandle && !type.Name.StartsWith(DataStructPrefix, StringComparison.Ordinal))
        {
            throw new CppGenException($"The data struct {type.Name} must be named '{DataStructPrefix}<Name>'; the C++ view and model are named after <Name>.");
        }
        var fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                             .OrderBy(f => f.MetadataToken)
                             .ToArray();
        var fields = MarkLengthFields(type, fieldInfos.Select(f => ReadField(f, isHandle)).ToList());
        var constants = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                            .Where(f => f.IsLiteral)
                            .OrderBy(f => f.MetadataToken)
                            .Select(ReadConstant)
                            .ToArray();
        var abiStruct = new AbiStruct(type.Name, LayoutProbe.SizeOf(type), fields, constants, isHandle, _docs.Get(type));
        CheckLayout(abiStruct);
        _structsInProgress.Remove(type);
        _structs[type] = abiStruct;
        _structOrder.Add(abiStruct);

        return abiStruct.Name;
    }

    /// <summary>
    /// Reads an instance field.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="isHandle">Whether the struct of the field is a handle struct.</param>
    /// <returns>The field; length fields are marked later by <see cref="MarkLengthFields"/>.</returns>
    private AbiField ReadField(FieldInfo field, bool isHandle)
    {
        var location = $"{field.DeclaringType!.Name}.{field.Name}";
        var type = field.FieldType;
        var text = field.GetCustomAttribute<NativeStringAttribute>();
        var array = field.GetCustomAttribute<NativeArrayAttribute>();
        var pointee = field.GetCustomAttribute<NativePointeeAttribute>();
        if ((text is null ? 0 : 1) + (array is null ? 0 : 1) + (pointee is null ? 0 : 1) > 1)
        {
            throw new CppGenException($"{location} has more than one of [NativeString], [NativeArray] and [NativePointee].");
        }
        var kind = AbiFieldKind.Value;
        string? lengthName = null;
        AbiType abiType;
        if (text is not null)
        {
            if (type != typeof(byte*))
            {
                throw new CppGenException($"[NativeString] on {location} requires the type byte*.");
            }
            kind = AbiFieldKind.String;
            lengthName = text.LengthName;
            abiType = new AbiPointerType(AbiPrimitiveType.Utf8Char);
        }
        else if (array is not null)
        {
            if (!type.IsPointer || type.GetElementType() == typeof(void))
            {
                throw new CppGenException($"[NativeArray] on {location} requires a typed pointer.");
            }
            kind = AbiFieldKind.Array;
            lengthName = array.LengthName;
            abiType = new AbiPointerType(ReadPointee(type.GetElementType()!, location));
        }
        else if (pointee is not null)
        {
            if (!type.IsPointer || !IsStruct(pointee.Type) || (type != typeof(void*) && type.GetElementType() != pointee.Type))
            {
                throw new CppGenException($"[NativePointee] on {location} requires a void* (or {pointee.Type.Name}*) field and a struct type.");
            }
            if (!isHandle)
            {
                throw new CppGenException($"{location} points to a single struct; only handle structs (taken by pointer by an exported function) support [NativePointee].");
            }
            kind = AbiFieldKind.Pointer;
            abiType = new AbiPointerType(new AbiStructType(ReadStruct(pointee.Type)));
        }
        else if (type.IsPointer)
        {
            throw new CppGenException($"{location} is a pointer; mark it with [NativeString], [NativeArray] or [NativePointee] so the generator knows what it points to.");
        }
        else
        {
            abiType = ReadValueType(type, location);
        }

        return new AbiField(field.Name, kind, abiType, LayoutProbe.OffsetOf(field), lengthName, _docs.Get(field));
    }

    /// <summary>
    /// Marks the fields named by <see cref="AbiField.LengthName"/> as length fields.
    /// </summary>
    /// <param name="type">The struct, for error messages.</param>
    /// <param name="fields">The fields of the struct.</param>
    /// <returns>The fields, with length fields marked.</returns>
    private static IReadOnlyList<AbiField> MarkLengthFields(Type type, List<AbiField> fields)
    {
        foreach (var owner in fields.Where(f => f.LengthName is not null).ToArray())
        {
            var location = $"{type.Name}.{owner.Name}";
            var index = fields.FindIndex(f => f.Name == owner.LengthName);
            CheckLength(index >= 0, index >= 0 && fields[index].Type == AbiPrimitiveType.Int32, owner.LengthName!, location);
            if (fields[index].Kind != AbiFieldKind.Value)
            {
                throw new CppGenException($"{type.Name}.{owner.LengthName} is the length of more than one field.");
            }
            fields[index] = fields[index] with { Kind = AbiFieldKind.Length };
        }

        return fields;
    }

    /// <summary>
    /// Checks that a length field or parameter exists and is an <see cref="int"/>.
    /// </summary>
    /// <param name="exists">Whether the length exists.</param>
    /// <param name="isInt32">Whether the length is an <see cref="int"/>.</param>
    /// <param name="lengthName">The name of the length.</param>
    /// <param name="location">The member that names the length, for error messages.</param>
    /// <exception cref="CppGenException">Thrown when the length is missing or is not an <see cref="int"/>.</exception>
    private static void CheckLength(bool exists, bool isInt32, string lengthName, string location)
    {
        if (!exists)
        {
            throw new CppGenException($"{location} names the length '{lengthName}', which does not exist.");
        }
        if (!isInt32)
        {
            throw new CppGenException($"{location} names the length '{lengthName}', which must be an int.");
        }
    }

    /// <summary>
    /// Reads a constant of a struct.
    /// </summary>
    /// <param name="field">The constant.</param>
    /// <returns>The constant.</returns>
    private AbiConstant ReadConstant(FieldInfo field)
    {
        var location = $"{field.DeclaringType!.Name}.{field.Name}";
        if (ReadValueType(field.FieldType, location) is not AbiPrimitiveType type)
        {
            throw new CppGenException($"The constant {location} must have a primitive type.");
        }

        return new AbiConstant(field.Name, type, FormatInteger(field.GetRawConstantValue()!, location), _docs.Get(field));
    }

    /// <summary>
    /// Checks that a C++ compiler lays out the generated struct exactly like the runtime lays out the C# struct.
    /// </summary>
    /// <param name="abiStruct">The struct, with the offsets measured from the runtime.</param>
    private static void CheckLayout(AbiStruct abiStruct)
    {
        var (offsets, size) = NativeLayout.Compute(abiStruct.Fields.Select(f => f.Type).ToArray());
        for (var i = 0; i < offsets.Length; i++)
        {
            var field = abiStruct.Fields[i];
            if (offsets[i] != field.Offset)
            {
                throw new CppGenException($"{abiStruct.Name}.{field.Name} is at offset {field.Offset} in .NET but would be at offset {offsets[i]} in C++. Use the default sequential layout without Pack or FieldOffset.");
            }
        }
        if (size != abiStruct.Size)
        {
            throw new CppGenException($"{abiStruct.Name} has the size {abiStruct.Size} in .NET but would have the size {size} in C++. Use the default sequential layout without Pack or Size.");
        }
    }

    /// <summary>
    /// Writes an integer constant as a C++ literal.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="location">The name of the constant, for error messages.</param>
    /// <returns>The literal.</returns>
    private static string FormatInteger(object value, string location)
    {
        return value switch
        {
            sbyte or byte or short or ushort or int => Convert.ToString(value, CultureInfo.InvariantCulture)!,
            uint number => number.ToString(CultureInfo.InvariantCulture) + "u",
            long number => number.ToString(CultureInfo.InvariantCulture) + "ll",
            ulong number => number.ToString(CultureInfo.InvariantCulture) + "ull",
            _ => throw new CppGenException($"The constant {location} must be an integer."),
        };
    }

    /// <summary>
    /// Gets whether a type is a struct (a value type that is neither primitive nor an enum).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns><see langword="true"/> for a struct.</returns>
    private static bool IsStruct(Type type)
    {
        return type.IsValueType && !type.IsEnum && !type.IsPrimitive && type != typeof(void);
    }
}
