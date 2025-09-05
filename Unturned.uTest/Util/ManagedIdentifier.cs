using System;
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

    internal static bool IdentifierNeedsEscaping(ReadOnlySpan<char> identifier, bool ignoreGenerics)
    {
        if (identifier.IsEmpty)
            return true;

        if (char.GetUnicodeCategory(identifier[0]) == UnicodeCategory.DecimalDigitNumber)
        {
            return true;
        }

        int genericDepth = 0;
        for (int i = 0; i < identifier.Length; ++i)
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

    /// <summary>
    /// Find a method in <paramref name="type"/> that matches the given <paramref name="managedMethod"/>.
    /// </summary>
    /// <remarks>Methods in parent classes will not be included.</remarks>
    /// <param name="type">The type to search in.</param>
    /// <param name="managedMethod">A <c>ManagedMethod</c> identifier.</param>
    /// <returns>The method if it is found (without ambiguity), otherwise <see langword="null"/>.</returns>
    public static MethodInfo? FindMethod(Type type, ReadOnlySpan<char> managedMethod)
    {
        return FindMethod(
            type.GetMethods(BindingFlags.Public
                            | BindingFlags.NonPublic
                            | BindingFlags.Static
                            | BindingFlags.Instance
                            | BindingFlags.DeclaredOnly),
            managedMethod
        );
    }

    internal static MethodInfo? FindMethod(MethodInfo[] methods, ReadOnlySpan<char> managedMethod)
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
                            valueBuilder = AppendSpan(new StringBuilder(), tokenizer.Value);
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
            return null;

        MethodInfo? bestCandidate = null;

        foreach (MethodInfo method in methods)
        {
            if (!string.Equals(method.Name, methodName, StringComparison.Ordinal))
                continue;

            if (methodArity != method.GetGenericArguments().Length)
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

        return bestCandidate;
    }

    private static StringBuilder AppendSpan(StringBuilder builder, ReadOnlySpan<char> span)
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