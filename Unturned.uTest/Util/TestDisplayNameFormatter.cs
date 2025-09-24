using System;
using System.Globalization;
using System.Text;
using uTest;
using uTest.Discovery;

namespace uTest;

internal static class TestDisplayNameFormatter
{
    /// <summary>
    /// Formats the display name for a single test instance.
    /// </summary>
    public static string GetTestDisplayName(in UnturnedTestInstance test)
    {
        if (!test.Test.Expandable)
        {
            return test.Test.DisplayName;
        }

        StringBuilder stringBuilder = StringBuilderPool.Rent();

        if (!string.IsNullOrEmpty(test.Type.Name))
        {
            ReadOnlySpan<char> withoutArity = ManagedIdentifier.TryRemoveArity(test.Type.Name.AsSpan());
            ManagedIdentifier.AppendSpan(stringBuilder, withoutArity);
        }

        if (test.TypeArgs.Length > 0)
        {
            stringBuilder.Append('<');
            for (int i = 0; i < test.TypeArgs.Length; ++i)
            {
                if (i != 0)
                    stringBuilder.Append(", ");
                AppendTypeDisplayName(stringBuilder, test.TypeArgs[i]);
            }

            stringBuilder.Append(">.");
        }
        else if (!string.IsNullOrEmpty(test.Type.Name))
        {
            stringBuilder.Append('.');
        }

        stringBuilder.Append(test.Method.Name);
        if (test.MethodTypeArgs.Length > 0)
        {
            stringBuilder.Append('<');
            foreach (Type t in test.MethodTypeArgs)
            {
                AppendTypeDisplayName(stringBuilder, t);
            }

            stringBuilder.Append('>');
        }

        if (test.Arguments.Length > 0)
        {
            stringBuilder.Append('(');

            for (int i = 0; i < test.Arguments.Length; ++i)
            {
                if (i != 0)
                    stringBuilder.Append(", ");

                stringBuilder.Append(FormatTestParameterValue(test.Arguments[i]));
            }

            stringBuilder.Append(')');
        }

        string displayName = stringBuilder.ToString();

        StringBuilderPool.Return(stringBuilder);

        return displayName;
    }

    /// <summary>
    /// Formats the display name for a CLR type.
    /// </summary>
    public static string GetTypeDisplayName(Type type)
    {
        string? keyword = TypeKeywordHelper.GetTypeKeyword(type);
        if (keyword != null)
            return keyword;

        StringBuilder stringBuilder = StringBuilderPool.Rent();

        AppendTypeDisplayName(stringBuilder, type);

        string displayName = stringBuilder.ToString();
        StringBuilderPool.Return(stringBuilder);

        return displayName;
    }


    private static void AppendTypeDisplayName(StringBuilder sb, Type t, string? refType = null)
    {
        if (t.IsByRef)
        {
            t = t.GetElementType()!;
            if (refType == null)
                sb.Append("ref ");
            else
                sb.Append(refType).Append(' ');
        }

        ElementTypeState state = default;
        Type elementType = state.ReduceElementType(t);

        string? keyword = TypeKeywordHelper.GetTypeKeyword(elementType);
        if (keyword != null)
        {
            sb.Append(keyword);
        }
        else
        {
            string? @namespace = elementType.Namespace;
            if (!string.IsNullOrEmpty(@namespace))
                sb.Append(@namespace).Append('.');

            ReadOnlySpan<char> withoutArity = ManagedIdentifier.TryRemoveArity(elementType.Name.AsSpan());
            ManagedIdentifier.AppendSpan(sb, withoutArity);

            if (elementType.IsGenericTypeDefinition)
            {
                sb.Append('<').Append(',', elementType.GetGenericArguments().Length - 1).Append('>');
            }
            else if (elementType.IsConstructedGenericType)
            {
                Type[] genArgs = elementType.GetGenericArguments();
                sb.Append('<');
                for (int i = 0; i < genArgs.Length; ++i)
                {
                    if (i != 0)
                        sb.Append(", ");
                    AppendTypeDisplayName(sb, genArgs[i]);
                }

                sb.Append('>');
            }
        }

        AppendElementType(sb, in state);
    }

    private static void AppendElementType(StringBuilder stringBuilder, in ElementTypeState elementTypeState)
    {
        if (elementTypeState.Stack != null)
        {
            Stack<Type>? arrayReversalStack = null;
            while (elementTypeState.Stack.Count > 0)
            {
                Type type = elementTypeState.Stack.Pop();
                if (!type.IsArray)
                {
                    // responsible for reversing multiple array specifiers (why would you do this C#)
                    if (arrayReversalStack != null)
                    {
                        while (arrayReversalStack.Count > 0)
                        {
                            AppendArraySpecifier(stringBuilder, arrayReversalStack.Pop());
                        }
                    }

                    if (type.IsPointer)
                        stringBuilder.Append('*');
                    else if (type.IsByRef)
                        stringBuilder.Append('&');
                }
                else if (arrayReversalStack != null)
                {
                    arrayReversalStack.Push(type);
                }
                else
                {
                    if (elementTypeState.Stack.Count > 0 && elementTypeState.Stack.Peek().IsArray)
                    {
                        arrayReversalStack = StackPool<Type>.Rent();
                        arrayReversalStack.Push(type);
                    }
                    else
                    {
                        AppendArraySpecifier(stringBuilder, type);
                    }
                }
            }

            if (arrayReversalStack != null)
            {
                while (arrayReversalStack.Count > 0)
                {
                    AppendArraySpecifier(stringBuilder, arrayReversalStack.Pop());
                }
            }

            StackPool<Type>.Return(elementTypeState.Stack);
            if (arrayReversalStack != null)
                StackPool<Type>.Return(arrayReversalStack);
        }
        else if (elementTypeState.FirstElement != null)
        {
            Type type = elementTypeState.FirstElement;
            if (type.IsPointer)
                stringBuilder.Append('*');
            else if (type.IsByRef)
                stringBuilder.Append('&');
            else if (type.IsArray)
                AppendArraySpecifier(stringBuilder, type);
        }

        return;

        static void AppendArraySpecifier(StringBuilder stringBuilder, Type type)
        {
            int rank = type.GetArrayRank();
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            if (type.IsSZArray)
#else
            // no better way to check if type is SZ array ([]) or MD 1-rank array ([*])
            if (rank == 1 && type.GetElementType()!.MakeArrayType() == type)
#endif
            {
                stringBuilder.Append("[]");
            }
            else if (rank == 1)
            {
                stringBuilder.Append("[*]");
            }
            else
            {
                stringBuilder.Append('[').Append(',', rank - 1).Append(']');
            }
        }
    }

    internal static unsafe string FormatTestParameterValue(object? value)
    {
        switch (value)
        {
            case null:
                return "null";

            case string str:
                return $"\"{str}\"";

            case '\0':
                return @"'\0'";

            case char c when char.IsControl(c):
                return $"(char){((int)c).ToString(null, CultureInfo.InvariantCulture)}";

            case char c:
                char* span = stackalloc char[3];
                span[0] = '\'';
                span[1] = c;
                span[2] = '\'';
                return new string(span);

            case IFormattable f:
                return f.ToString(null, CultureInfo.InvariantCulture);

            default:
                return value.ToString();
        }
    }
}
