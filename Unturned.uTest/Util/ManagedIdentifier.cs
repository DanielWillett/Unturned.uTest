using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace uTest;

// included in Unturned.uTest.Runner and Unturned.uTest.Runner.SourceGenerator

/// <summary>
/// Static utilities for creating and reading the <c>ManagedType</c> and <c>ManagedMethod</c> formats specified in the document below.
/// <para>
/// <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md"/>
/// </para>
/// </summary>
public static class ManagedIdentifier
{
    // https://github.com/dotnet/runtime/blob/main/src/coreclr/vm/array.h#L12
    // also the same on Mono
    internal const int MaxArrayRank = 32;

    private static readonly ConcurrentDictionary<Type, string> ManagedTypeCache
        = new ConcurrentDictionary<Type, string>();

    internal static TextEscaper FullyQualifiedTypeNameEscaper { get; } = new TextEscaper('&', '*', '+', ',', '[', '\\', ']');

    /// <summary>
    /// Normalizes a managed identifier.
    /// </summary>
    /// <exception cref="FormatException"/>
    public static string Normalize(ReadOnlySpan<char> text, ManagedIdentifierKind kind)
    {
        ManagedIdentifierTokenizer tokenizer = new ManagedIdentifierTokenizer(text, kind);
        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(new StringBuilder(text.Length));

        try
        {
            while (tokenizer.MoveNext())
            {
                builder.WriteToken(in tokenizer);
            }

            builder.Close();
        }
        catch (InvalidOperationException ex)
        {
            throw new FormatException("Invalid managed identifier format.", ex);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets the managed type identifier for a CLR type.
    /// </summary>
    public static string GetManagedType(Type type)
    {
        if (type == null)
            return string.Empty;

        if (ManagedTypeCache.TryGetValue(type, out string v))
            return v;

        StringBuilder sb = new StringBuilder();
        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(sb);
        AppendType(ref builder, type);
        builder.Close();
        string str = builder.ToString();
        ManagedTypeCache.TryAdd(type, str);
        return str;
    }

    /// <summary>
    /// Gets the managed method identifier for a CLR method.
    /// </summary>
    public static string GetManagedMethod(MethodBase method)
    {
        if (method == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(sb);
        AppendMethod(ref builder, method);
        builder.Close();
        return builder.ToString();
    }

    private static void AppendMethod(ref ManagedIdentifierBuilder builder, MethodBase method)
    {
        Type[] typeParams;
        try
        {
            typeParams = method.GetGenericArguments();
        }
        catch (NotSupportedException)
        {
            typeParams = Type.EmptyTypes;
        }

        int methodArity = typeParams.Length;

        ReadOnlySpan<char> methodName = method.Name;
        if (methodName.LastIndexOf('.') <= 0) // first char being a period is okay
        {
            builder.AddMethodName(methodName, methodArity);
        }
        else
        {
            int ct = Count(methodName, '.') + 1;
            Span<Range> sections = stackalloc Range[ct];
            ct = SplitExplicitlyImplementedMethodName(methodName, sections);
            for (int i = 0; i < ct; ++i)
            {
                ReadOnlySpan<char> section = methodName[sections[i]];
                if (section.IndexOfAny(FullyQualifiedTypeNameEscaper.Escapables) >= 0)
                    section = FullyQualifiedTypeNameEscaper.Unescape(section);

                if (i == ct - 1)
                    builder.AddMethodName(section, methodArity);
                else
                    builder.AddExplicitImplementationInterfaceName(section);
            }
        }

        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Length == 0)
            return;

        builder.BeginParameters();

        for (int i = 0; i < parameters.Length; ++i)
        {
            ParameterInfo p = parameters[i];
            if (i != 0)
                builder.NextParameter();

            AppendType(ref builder, p.ParameterType);
        }

        builder.EndParameters();
    }

    private static readonly char[] NamespaceSplitChars = [ '.' ];

    private static void AppendType(ref ManagedIdentifierBuilder builder, Type type)
    {
        Type elementType = ReduceElementType(type, out ElementTypeState elementTypeState);

        if (elementType.IsGenericParameter)
        {
            MethodBase? method;
            try
            {
                method = elementType.DeclaringMethod;
            }
            catch
            {
                method = null;
            }
            if (method != null)
            {
                int index;
                try
                {
                    index = Array.IndexOf(method.GetGenericArguments(), elementType);
                }
                catch (NotSupportedException)
                {
                    index = -1;
                }
                if (index < 0)
                    throw new InvalidOperationException("Should be unreachable, unable to find type index in method parameters.");
                builder.AddMethodGenericParameterReference(index);
            }
            else if (elementType.DeclaringType != null)
            {
                int index;
                try
                {
                    index = Array.IndexOf(elementType.DeclaringType.GetGenericArguments(), elementType);
                }
                catch (NotSupportedException)
                {
                    index = -1;
                }
                if (index < 0)
                    throw new InvalidOperationException("Should be unreachable, unable to find type index in type parameters.");
                builder.AddTypeGenericParameterReference(index);
            }
            else
            {
                throw new InvalidOperationException("Should be unreachable, unable to identify generic parameter owner.");
            }

            AppendElementType(ref builder, elementTypeState);
            return;
        }

        string? @namespace = elementType.Namespace;
        if (@namespace != null)
        {
            if (@namespace.IndexOf('.') < 0)
            {
                builder.AddTypeSegment(FullyQualifiedTypeNameEscaper.Unescape(@namespace), false);
            }
            else foreach (string section in @namespace.Split(NamespaceSplitChars, StringSplitOptions.RemoveEmptyEntries))
            {
                builder.AddTypeSegment(FullyQualifiedTypeNameEscaper.Unescape(section), false);
            }
        }

        Stack<Type> nestedTypes = StackPool<Type>.Rent();
        for (Type? t = elementType; t != null; t = t.DeclaringType)
        {
            nestedTypes.Push(t);
        }

        bool isFirst = true;
        int prevArity = 0;

        Type[]? lastGenericArgs = null;

        while (nestedTypes.Count > 0)
        {
            Type nestedType = nestedTypes.Pop();
            int thisArity = 0;
            if (nestedType.IsGenericType)
            {
                try
                {
                    thisArity = (lastGenericArgs = nestedType.GetGenericArguments()).Length;
                }
                catch (NotSupportedException)
                {
                    thisArity = 0;
                }
            }

            int arity = thisArity - prevArity;
            ReadOnlySpan<char> name = TryRemoveArity(nestedType.Name, arity);

            builder.AddTypeSegment(name, !isFirst, arity);
            isFirst = false;

            prevArity = thisArity;
        }

        StackPool<Type>.Return(nestedTypes);

        if (!elementType.IsConstructedGenericType)
        {
            AppendElementType(ref builder, in elementTypeState);
            return;
        }

        if (lastGenericArgs == null)
        {
            try
            {
                lastGenericArgs = elementType.GetGenericArguments();
            }
            catch (NotSupportedException)
            {
                lastGenericArgs = Type.EmptyTypes;
            }
        }

        if (lastGenericArgs.Length == 0)
        {
            AppendElementType(ref builder, in elementTypeState);
            return;
        }

        builder.BeginTypeParameters();

        for (int i = 0; i < lastGenericArgs.Length; i++)
        {
            if (i != 0)
                builder.NextParameter();

            Type genericParam = lastGenericArgs[i];
            AppendType(ref builder, genericParam);
        }

        builder.EndTypeParameters();

        AppendElementType(ref builder, in elementTypeState);
    }

    private static ReadOnlySpan<char> TryRemoveArity(string name, int expectedArity)
    {
        if (expectedArity == 0)
            return name;

        int arityStartIndex;
        for (arityStartIndex = name.Length - 1; arityStartIndex > 0; --arityStartIndex)
        {
            if (!char.IsDigit(name[arityStartIndex]))
                return name;
            if (name[arityStartIndex - 1] == '`')
                break;
        }

        if (arityStartIndex <= 0)
            return name;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        if (!int.TryParse(name.AsSpan(arityStartIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int arity))
#else
        if (!int.TryParse(name.Substring(arityStartIndex), NumberStyles.None, CultureInfo.InvariantCulture, out int arity))
#endif
        {
            return name;
        }

        return expectedArity != arity ? name : name.AsSpan(0, arityStartIndex - 1);
    }

    internal static ReadOnlySpan<char> TryRemoveArity(string name)
    {
        int arityStartIndex;
        for (arityStartIndex = name.Length - 1; arityStartIndex > 0; --arityStartIndex)
        {
            if (!char.IsDigit(name[arityStartIndex]))
                return name;
            if (name[arityStartIndex - 1] == '`')
                break;
        }

        if (arityStartIndex <= 0)
            return name;

        return name.AsSpan(0, arityStartIndex - 1);
    }

    internal static bool IdentifierNeedsEscaping(ReadOnlySpan<char> identifier, bool ignoreGenerics, bool isMethodName = false)
    {
        if (identifier.IsEmpty)
            return true;

        int i = 0;
        if (isMethodName && identifier[0] == '.' && identifier.Length > 1)
            i = 1;

        if (char.GetUnicodeCategory(identifier[i]) == UnicodeCategory.DecimalDigitNumber)
        {
            return true;
        }

        int genericDepth = 0;
        for (; i < identifier.Length; ++i)
        {
            char c = identifier[i];
            if (ignoreGenerics)
            {
                switch (c)
                {
                    case '<':
                        ++genericDepth;
                        continue;
                    case '>':
                        if (genericDepth > 0)
                            --genericDepth;
                        continue;
                    case '.':
                    case ',':
                        if (genericDepth > 0)
                            continue;
                        return false;
                }
            }

            UnicodeCategory category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.ConnectorPunctuation
                or UnicodeCategory.Format
               )
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // find type is impossible since generic types require the assembly-qualified name in the []s.
    // System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Collections.Generic.KeyValuePair`2[[System.Int32, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.String, System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]], System.Private.CoreLib, Version=9.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]

    /// <summary>
    /// Find a method in <paramref name="type"/> that matches the given <paramref name="managedMethod"/>.
    /// </summary>
    /// <remarks>Methods in parent classes will not be included.</remarks>
    /// <param name="type">The type to search in.</param>
    /// <param name="managedMethod">A <c>ManagedMethod</c> identifier.</param>
    /// <returns>The method if it is found (without ambiguity), otherwise <see langword="null"/>.</returns>
    public static MethodBase? FindMethod(Type type, ReadOnlySpan<char> managedMethod)
    {
        if (managedMethod.StartsWith(".cctor", StringComparison.Ordinal))
        {
            return FindMethod(
                // ReSharper disable once CoVariantArrayConversion
                type.GetConstructors(BindingFlags.Static
                                     | BindingFlags.NonPublic
                                     | BindingFlags.Public
                                     | BindingFlags.DeclaredOnly),
                managedMethod
            );
        }

        if (managedMethod.StartsWith(".ctor", StringComparison.Ordinal))
        {
            return FindMethod(
                // ReSharper disable once CoVariantArrayConversion
                type.GetConstructors(BindingFlags.Instance
                                     | BindingFlags.NonPublic
                                     | BindingFlags.Public
                                     | BindingFlags.DeclaredOnly),
                managedMethod
            );
        }

        return FindMethod(
            // ReSharper disable once CoVariantArrayConversion
            type.GetMethods(BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                            | BindingFlags.DeclaredOnly),
            managedMethod
        );
    }

    internal static MethodBase? FindMethod(MethodBase[] methods, ReadOnlySpan<char> managedMethod)
    {
        if (methods == null || methods.Length == 0)
            return null;

        string? methodName = null;
        int methodArity = 0;
        StringBuilder? valueBuilder = null;

        ManagedIdentifierTokenizer tokenizer = new ManagedIdentifierTokenizer(managedMethod, ManagedIdentifierKind.Method);

        if (managedMethod.IsEmpty)
        {
            methodName = string.Empty;
        }
        else
        {
            bool doBreak = false;
            while (tokenizer.MoveNext() && !doBreak)
            {
                switch (tokenizer.TokenType)
                {
                    case ManagedIdentifierTokenType.MethodImplementationTypeSegment when methodName == null:
                        if (valueBuilder == null)
                        {
                            valueBuilder = AppendSpan(StringBuilderPool.Rent(), tokenizer.Value);
                        }
                        else
                        {
                            AppendSpan(valueBuilder.Append('.'), tokenizer.Value);
                        }

                        break;

                    case ManagedIdentifierTokenType.Arity:
                        methodArity = tokenizer.Arity;
                        break;

                    case ManagedIdentifierTokenType.MethodName:
                        if (valueBuilder != null)
                        {
                            methodName = AppendSpan(valueBuilder.Append('.'), tokenizer.Value).ToString();
                        }
                        else
                        {
                            methodName = tokenizer.Value.ToString();
                        }

                        break;

                    case ManagedIdentifierTokenType.OpenParameters:
                        doBreak = true;
                        break;
                }
            }

            valueBuilder?.Clear();
        }

        if (methodName == null)
        {
            if (valueBuilder != null)
                StringBuilderPool.Return(valueBuilder);
            return null;
        }

        MethodBase? bestCandidate = null;

        foreach (MethodBase method in methods)
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;

            int arity;
            try
            {
                arity = method.GetGenericArguments().Length;
            }
            catch (NotSupportedException)
            {
                arity = 0;
            }

            if (methodArity != arity)
                continue;

            ManagedIdentifierTokenizer t2 = tokenizer;
            ParameterInfo[] parameters = method.GetParameters();
            bool isMatch = true;
            int parameterCount = 0;
            if (t2.TokenType is not ManagedIdentifierTokenType.CloseParameters and not ManagedIdentifierTokenType.Uninitialized)
            {
                while (true)
                {
                    if (parameterCount >= parameters.Length
                        || !t2.IsSameTypeAs(parameters[parameterCount].ParameterType, valueBuilder))
                    {
                        isMatch = false;
                        break;
                    }

                    ++parameterCount;

                    if (t2.TokenType is ManagedIdentifierTokenType.CloseParameters or ManagedIdentifierTokenType.Uninitialized)
                        break;
                }

                if (parameterCount < parameters.Length)
                    isMatch = false;
            }
            else if (parameters.Length != 0)
                continue;

            if (!isMatch)
                continue;

            if (bestCandidate == null)
                bestCandidate = method;
            else
            {
                bestCandidate = null;
                break;
            }
        }

        if (valueBuilder != null)
            StringBuilderPool.Return(valueBuilder);
        return bestCandidate;
    }

    internal static StringBuilder AppendSpan(StringBuilder builder, ReadOnlySpan<char> span)
    {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        builder.Append(span);
#else
        unsafe
        {
            fixed (char* ptr = span)
            {
                builder.Append(ptr, span.Length);
            }
        }
#endif

        return builder;
    }


    internal struct ElementTypeState
    {
        public Type? FirstElement;
        public Stack<Type>? Stack;
    }

    internal static Type ReduceElementType(Type type, out ElementTypeState types)
    {
        Type elementType = type;
        types = default;
        while (true)
        {
            Type nextElementType;
            if (elementType.HasElementType)
            {
                nextElementType = elementType.GetElementType()!;
            }
            else
            {
                if (types.Stack != null)
                    types.FirstElement = null;
                return elementType;
            }

            if (types.FirstElement == null)
                types.FirstElement = elementType;
            else if (types.Stack == null)
            {
                types.Stack = StackPool<Type>.Rent();
                types.Stack.Push(types.FirstElement);
                types.Stack.Push(elementType);
            }
            else
            {
                types.Stack.Push(elementType);
            }

            elementType = nextElementType;
        }
    }

    private static void AppendElementType(ref ManagedIdentifierBuilder builder, in ElementTypeState elementTypeState)
    {
        if (elementTypeState.Stack != null)
        {
            while (elementTypeState.Stack.Count > 0)
            {
                AddElementType(ref builder, elementTypeState.Stack.Pop());
            }

            StackPool<Type>.Return(elementTypeState.Stack);
        }
        else if (elementTypeState.FirstElement != null)
        {
            AddElementType(ref builder, elementTypeState.FirstElement);
        }

        return;

        static void AddElementType(ref ManagedIdentifierBuilder builder, Type type)
        {
            if (type.IsArray)
            {
#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                builder.MakeArrayType(type.IsSZArray ? 0 : type.GetArrayRank());
#else
                // no better way to check if type is SZ array ([]) or MD 1-rank array ([*])
                int rank = type.GetArrayRank();
                if (rank == 1 && type.GetElementType()!.MakeArrayType() == type)
                    rank = 0;
                builder.MakeArrayType(rank);
#endif
            }
            else if (type.IsByRef)
            {
                builder.MakeReferenceType();
            }
            else if (type.IsPointer)
            {
                builder.MakePointerType();
            }
        }
    }

    internal static int Count(ReadOnlySpan<char> text, char c)
    {
        int count = 0;
        int index = 0;
        while (index < text.Length)
        {
            int find = text[index..].IndexOf(c);
            if (find == -1)
                break;
            ++count;
            index += find + 1;
        }

        return count;
    }

    // note: use methodName.Count('.') + 1 for the initial size of output, there will never be more than that
    internal static int SplitExplicitlyImplementedMethodName(ReadOnlySpan<char> methodName, Span<Range> output)
    {
        if (methodName.Length == 0)
            return 0;

        ReadOnlySpan<char> escapeChars = [ '.', '<', '>' ];

        int rangeIndex = 0;

        int prevIndex = -1;
        int prevDotIndex = -1;
        int genericDepth = 0;
        while (true)
        {
            int startIndex = prevIndex + 1;
            int nextIndex = startIndex >= methodName.Length ? -1 : methodName[startIndex..].IndexOfAny(escapeChars);
            if (nextIndex < 0)
            {
                output[rangeIndex] = Range.StartAt(new Index(prevDotIndex + 1));
                ++rangeIndex;
                break;
            }
            
            nextIndex += startIndex;
            switch (methodName[nextIndex])
            {
                case '<':
                    ++genericDepth;
                    break;

                case '>':
                    if (genericDepth > 0)
                        --genericDepth;
                    break;

                case '.' when genericDepth == 0:
                    output[rangeIndex] = new Range(new Index(prevDotIndex + 1), new Index(nextIndex));
                    ++rangeIndex;
                    prevDotIndex = nextIndex;
                    break;
            }

            prevIndex = nextIndex;
        }

        return rangeIndex;
    }
}

/// <summary>
/// A kind of managed identifier. See <see cref="ManagedIdentifier"/> for more info.
/// </summary>
public enum ManagedIdentifierKind
{
    /// <summary>
    /// A <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md#managedmethod-property">managed method</see> identifier.
    /// </summary>
    Method,

    /// <summary>
    /// A <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md#managedtype-property">managed type</see> identifier.
    /// </summary>
    Type
}