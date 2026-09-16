using System.Reflection;
using System.Reflection.Emit;

namespace CanDoItAll.Tests.Unit.Architecture;

/// <summary>
/// Test-only reader of what compiled code actually references: member signatures, locals and the method, field and
/// type tokens in IL, for a type and all of its nested (including compiler-generated) types. Unlike a text scan it is
/// independent of identifier names, aliases, qualification, whitespace and comments.
/// </summary>
internal static class CompiledTypeReferences
{
    private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
                                                 BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static readonly OpCode[] OneByteOpCodes = new OpCode[0x100];
    private static readonly OpCode[] TwoByteOpCodes = new OpCode[0x100];

    static CompiledTypeReferences()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var index = (ushort)opCode.Value & 0xFF;
            if (opCode.Size == 1)
            {
                OneByteOpCodes[index] = opCode;
            }
            else
            {
                TwoByteOpCodes[index] = opCode;
            }
        }
    }

    public static IEnumerable<Type> SelfAndNestedTypes(Type type)
    {
        yield return type;
        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var descendant in SelfAndNestedTypes(nested))
            {
                yield return descendant;
            }
        }
    }

    public static Type OutermostType(Type type)
    {
        var current = type;
        while (current.DeclaringType is not null)
        {
            current = current.DeclaringType;
        }

        return current;
    }

    public static IReadOnlySet<Type> ReferencedTypes(Type type)
    {
        var types = new HashSet<Type>();
        foreach (var candidate in SelfAndNestedTypes(type))
        {
            AddExpanded(types, candidate.BaseType);
            foreach (var implemented in candidate.GetInterfaces())
            {
                AddExpanded(types, implemented);
            }

            foreach (var field in candidate.GetFields(DeclaredMembers))
            {
                AddExpanded(types, field.FieldType);
            }

            foreach (var property in candidate.GetProperties(DeclaredMembers))
            {
                AddExpanded(types, property.PropertyType);
            }

            foreach (var method in Methods(candidate))
            {
                if (method is MethodInfo info)
                {
                    AddExpanded(types, info.ReturnType);
                }

                foreach (var parameter in method.GetParameters())
                {
                    AddExpanded(types, parameter.ParameterType);
                }

                var body = method.GetMethodBody();
                if (body is null)
                {
                    continue;
                }

                foreach (var local in body.LocalVariables)
                {
                    AddExpanded(types, local.LocalType);
                }

                foreach (var member in ResolveMembers(candidate, method, body))
                {
                    AddMember(types, member);
                }
            }
        }

        return types;
    }

    public static IReadOnlySet<MethodBase> CalledMethods(Type type)
    {
        var methods = new HashSet<MethodBase>();
        foreach (var candidate in SelfAndNestedTypes(type))
        {
            foreach (var method in Methods(candidate))
            {
                var body = method.GetMethodBody();
                if (body is null)
                {
                    continue;
                }

                foreach (var member in ResolveMembers(candidate, method, body))
                {
                    if (member is MethodBase called)
                    {
                        methods.Add(called);
                    }
                }
            }
        }

        return methods;
    }

    private static IEnumerable<MethodBase> Methods(Type type)
        => type.GetMethods(DeclaredMembers).Cast<MethodBase>().Concat(type.GetConstructors(DeclaredMembers));

    private static IEnumerable<MemberInfo> ResolveMembers(Type owner, MethodBase method, MethodBody body)
    {
        var il = body.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }

        var typeArguments = owner.IsGenericType ? owner.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        foreach (var token in MemberTokens(il))
        {
            MemberInfo? member;
            try
            {
                member = owner.Module.ResolveMember(token, typeArguments, methodArguments);
            }
            catch (ArgumentException)
            {
                member = owner.Module.ResolveMember(token);
            }

            if (member is not null)
            {
                yield return member;
            }
        }
    }

    private static IEnumerable<int> MemberTokens(byte[] il)
    {
        var position = 0;
        while (position < il.Length)
        {
            var first = il[position++];
            var opCode = first == 0xFE ? TwoByteOpCodes[il[position++]] : OneByteOpCodes[first];
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    position += 1;
                    break;
                case OperandType.InlineVar:
                    position += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    position += 8;
                    break;
                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(il, position);
                    position += 4 + (count * 4);
                    break;
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    yield return BitConverter.ToInt32(il, position);
                    position += 4;
                    break;
                default:
                    position += 4;
                    break;
            }
        }
    }

    private static void AddMember(HashSet<Type> types, MemberInfo member)
    {
        switch (member)
        {
            case Type referenced:
                AddExpanded(types, referenced);
                break;
            case FieldInfo field:
                AddExpanded(types, field.DeclaringType);
                AddExpanded(types, field.FieldType);
                break;
            case MethodBase method:
                AddExpanded(types, method.DeclaringType);
                if (method is MethodInfo info)
                {
                    AddExpanded(types, info.ReturnType);
                    foreach (var argument in info.IsGenericMethod ? info.GetGenericArguments() : [])
                    {
                        AddExpanded(types, argument);
                    }
                }

                foreach (var parameter in method.GetParameters())
                {
                    AddExpanded(types, parameter.ParameterType);
                }

                break;
            default:
                AddExpanded(types, member.DeclaringType);
                break;
        }
    }

    private static void AddExpanded(HashSet<Type> types, Type? type)
    {
        if (type is null || type.IsGenericParameter || !types.Add(type))
        {
            return;
        }

        if (type.HasElementType)
        {
            AddExpanded(types, type.GetElementType());
        }

        if (type.IsGenericType)
        {
            if (!type.IsGenericTypeDefinition)
            {
                AddExpanded(types, type.GetGenericTypeDefinition());
            }

            foreach (var argument in type.GetGenericArguments())
            {
                AddExpanded(types, argument);
            }
        }
    }
}
