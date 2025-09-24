using System;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Debug = System.Diagnostics.Debug;

namespace uTest;

// included in Unturned.uTest.Runner and Unturned.uTest.Runner.SourceGenerator

/// <summary>
/// A unique ID for a test formatted like so:
/// <para>
/// <code>
///
/// // base structure
/// | {managedTypeLength}:{managedMethodLength}:{managedType}.{managedMethod}
///
/// // method type parameters (type parameters for the containing type should already be in the managedType
/// | [space]&lt;{len}:{methodParam1ManagedType},{len}:{methodParam2ManagedType}&gt;
///
/// // method parameters
/// | preferred format:
/// | [space]({value},{value})[space]{variationIndex}
/// | alternative format (allows for unformattable values):
/// | [space]{variationIndex}
///
/// variationIndex is the index of this variation within its group of type parameters
///
/// // Examples:
/// * 13:28:System.String.IsNullOrEmpty(System.String) ("someStringValue") 1
/// * 53:8:System.ValueTuple`2&lt;12:System.Int32,13:System.String&gt;.ToString
/// * 46:8:System.ArraySegment`1+Enumerator&lt;System.Int32&gt;.MoveNext;
/// * 12:7:System.Array.Empty`1 &lt;13:System.String&gt;
/// * 12:13:System.Array.Sort`1(!!0[]) &lt;13:System.String&gt; 2
///
/// Extra whitespace is not supported, must exactly match the format given above.
/// </code>
/// The space and last part can be omitted if there are no parameters.
/// </para>
/// </summary>
/// <remarks>Use <see cref="UnturnedTestUid.Create"/> to construct an ID based on it's components.</remarks>
public readonly struct UnturnedTestUid(string uid)
{
    private static readonly object TrueBox = true;
    private static readonly object FalseBox = false;

    /// <summary>
    /// The raw unique ID of this test.
    /// </summary>
    public string Uid { get; } = uid;

    public static implicit operator UnturnedTestUid(string str) => new UnturnedTestUid(str);
    public static implicit operator string(UnturnedTestUid id) => id.Uid;

    /// <summary>
    /// Checks if the <paramref name="uid"/> defines a test declared in <paramref name="type"/>.
    /// </summary>
    [SkipLocalsInit]
    public static bool IsSameTypeAs(string uid, Type type)
    {
        return TryParse(uid, out ReadOnlySpan<char> managedType, out _)
               && ManagedIdentifier.IsSameType(type, managedType);
    }

    /// <summary>
    /// Checks if the <paramref name="uid"/> defines a test declared in <paramref name="type"/> (ignoring generic parameters).
    /// </summary>
    [SkipLocalsInit]
    public static bool IsSameTypeDefinitionAs(string uid, Type type)
    {
        return TryParse(uid, out ReadOnlySpan<char> managedType, out _)
               && ManagedIdentifier.IsSameTypeDefinition(type, managedType);
    }

    [SkipLocalsInit]
    public static bool IsSameBaseMethod(string baseUid, string fullUid, bool allowTypeDefinitionMatching = false)
    {
        int firstColon1 = baseUid.IndexOf(':');
        if (firstColon1 <= 0
            || !MathHelper.TryParseInt(baseUid.AsSpan(0, firstColon1), out int managedTypeLength1))
        {
            return false;
        }

        int firstColon2 = fullUid.IndexOf(':');
        if (firstColon2 <= 0
            || !MathHelper.TryParseInt(fullUid.AsSpan(0, firstColon2), out int managedTypeLength2))
        {
            return false;
        }

        if (!allowTypeDefinitionMatching && managedTypeLength1 != managedTypeLength2)
            return false;

        int secondColon1 = baseUid.IndexOf(':', firstColon1 + 1);
        if (secondColon1 < 0
            || secondColon1 == firstColon1 + 1
            || !MathHelper.TryParseInt(baseUid.AsSpan(firstColon1 + 1, secondColon1 - (firstColon1 + 1)), out int managedMethodLength1))
        {
            return false;
        }

        int secondColon2 = fullUid.IndexOf(':', firstColon2 + 1);
        if (secondColon2 < 0
            || secondColon2 == firstColon2 + 1
            || !MathHelper.TryParseInt(fullUid.AsSpan(firstColon2 + 1, secondColon2 - (firstColon2 + 1)), out int managedMethodLength2))
        {
            return false;
        }

        if (!allowTypeDefinitionMatching && managedMethodLength1 != managedMethodLength2)
            return false;

        int managedTypeStart1 = secondColon1 + 1;
        int managedMethodStart1 = managedTypeStart1 + managedTypeLength1 + 1;
        if (managedMethodStart1 + managedMethodLength1 > baseUid.Length || baseUid[managedMethodStart1 - 1] != '.')
            return false;

        int managedTypeStart2 = secondColon2 + 1;
        int managedMethodStart2 = managedTypeStart2 + managedTypeLength2 + 1;
        if (managedMethodStart2 + managedMethodLength2 > fullUid.Length || fullUid[managedMethodStart2 - 1] != '.')
            return false;

        if (allowTypeDefinitionMatching)
        {
            ReadOnlySpan<char> managedType1 = baseUid.AsSpan(managedTypeStart1, managedTypeLength1);
            ReadOnlySpan<char> managedType2 = fullUid.AsSpan(managedTypeStart2, managedTypeLength2);
            int typeIndex = managedType2.IndexOf('<');
            if (typeIndex >= 0)
            {
                managedType2 = managedType2.Slice(0, typeIndex);
            }

            if (!managedType1.Equals(managedType2, StringComparison.Ordinal))
                return false;

            ReadOnlySpan<char> managedMethod1 = baseUid.AsSpan(managedMethodStart1, managedMethodLength1);
            ReadOnlySpan<char> managedMethod2 = fullUid.AsSpan(managedMethodStart2, managedMethodLength2);
            int methodIndex = managedMethod1.IndexOf('(');
            if (methodIndex >= 0 && managedMethod1.Slice(methodIndex).IndexOf('!') >= 0)
            {
                int fullMethodIndex = managedMethod2.IndexOf('(');
                if (fullMethodIndex >= 0)
                {
                    managedMethod2 = managedMethod2.Slice(0, fullMethodIndex);
                }
                managedMethod1 = managedMethod1.Slice(0, methodIndex);
            }

            return managedMethod1.Equals(managedMethod2, StringComparison.Ordinal);
        }

        return baseUid.AsSpan(managedTypeStart1, managedTypeLength1 + managedMethodLength1 + 1)
            .Equals(fullUid.AsSpan(managedTypeStart2, managedTypeLength2 + managedMethodLength2 + 1), StringComparison.Ordinal);
    }

    [SkipLocalsInit]
    public static bool TryParse(
        string uid,
        out ReadOnlySpan<char> managedType,
        out ReadOnlySpan<char> managedMethod)
    {
        managedType = ReadOnlySpan<char>.Empty;
        managedMethod = ReadOnlySpan<char>.Empty;

        int firstColon = uid.IndexOf(':');
        if (firstColon <= 0
            || !MathHelper.TryParseInt(uid.AsSpan(0, firstColon), out int managedTypeLength))
        {
            return false;
        }

        int secondColon = uid.IndexOf(':', firstColon + 1);
        if (secondColon < 0
            || secondColon == firstColon + 1
            || !MathHelper.TryParseInt(uid.AsSpan(firstColon + 1, secondColon - (firstColon + 1)), out int managedMethodLength))
        {
            return false;
        }

        int managedTypeStart = secondColon + 1;
        int managedMethodStart = managedTypeStart + managedTypeLength + 1;
        if (managedMethodStart + managedMethodLength > uid.Length || uid[managedMethodStart - 1] != '.')
            return false;

        managedType = uid.AsSpan(managedTypeStart, managedTypeLength);
        managedMethod = uid.AsSpan(managedMethodStart, managedMethodLength);
        return true;
    }

    [SkipLocalsInit]
    public static bool TryParse(
        string uid,
        out ReadOnlyMemory<char> managedType,
        out ReadOnlyMemory<char> managedMethod,
        out ReadOnlyMemory<char>[] methodTypeParamManagedTypes,
        out object?[] parameters,
        out int variantIndex
    )
    {
        managedType = ReadOnlyMemory<char>.Empty;
        managedMethod = ReadOnlyMemory<char>.Empty;
        methodTypeParamManagedTypes = Array.Empty<ReadOnlyMemory<char>>();
        parameters = Array.Empty<object>();
        variantIndex = 0;

        int firstColon = uid.IndexOf(':');
        if (firstColon <= 0
            || !MathHelper.TryParseInt(uid.AsSpan(0, firstColon), out int managedTypeLength))
        {
            return false;
        }

        int secondColon = uid.IndexOf(':', firstColon + 1);
        if (secondColon < 0
            || secondColon == firstColon + 1
            || !MathHelper.TryParseInt(uid.AsSpan(firstColon + 1, secondColon - (firstColon + 1)), out int managedMethodLength))
        {
            return false;
        }

        int managedTypeStart = secondColon + 1;
        int managedMethodStart = managedTypeStart + managedTypeLength + 1;
        if (managedMethodStart + managedMethodLength > uid.Length || uid[managedMethodStart - 1] != '.')
            return false;

        managedType = uid.AsMemory(managedTypeStart, managedTypeLength);
        managedMethod = uid.AsMemory(managedMethodStart, managedMethodLength);

        int typeParametersStart = managedMethodStart + managedMethodLength + 1;
        if (typeParametersStart - 1 == uid.Length)
            return true;

        if (!char.IsWhiteSpace(uid[typeParametersStart - 1]))
            return false;

        int parametersStart = typeParametersStart;
        if (uid[typeParametersStart] == '<')
        {
            int typeParamCount = 0;
            int index = typeParametersStart;
            while (index + 1 < uid.Length)
            {
                int colonIndex = uid.IndexOf(':', index + 1);
                if (colonIndex == index + 2
                    || colonIndex >= uid.Length - 1
                    || !MathHelper.TryParseInt(uid.AsSpan(index + 1, colonIndex - index - 1), out int typeParamLength)
                    || colonIndex + typeParamLength + 1 >= uid.Length)
                {
                    return false;
                }

                ++typeParamCount;
                char c = uid[colonIndex + typeParamLength + 1];
                if (c is not '>' and not ',')
                    return false;

                index = colonIndex + typeParamLength + 1;

                if (c == '>')
                    break;
            }

            if (typeParamCount == 0 || index >= uid.Length || uid[index] != '>')
                return false;

            ReadOnlyMemory<char>[] typeParams = new ReadOnlyMemory<char>[typeParamCount];

            index = typeParametersStart;
            typeParamCount = -1;
            while (index < uid.Length)
            {
                int colonIndex = uid.IndexOf(':', index + 1);
                MathHelper.TryParseInt(uid.AsSpan(index + 1, colonIndex - index - 1), out int typeParamLength);

                typeParams[++typeParamCount] = uid.AsMemory(colonIndex + 1, typeParamLength);
                index = colonIndex + typeParamLength + 1;

                if (uid[colonIndex + typeParamLength + 1] == '>')
                    break;
            }

            methodTypeParamManagedTypes = typeParams;

            if (uid.Length == index + 1)
                return true;

            if (index + 2 >= uid.Length)
                return false;

            if (!char.IsWhiteSpace(uid[index + 1]))
                return false;

            parametersStart = index + 2;
        }

        if (char.IsDigit(uid[parametersStart]))
        {
            if (!MathHelper.TryParseInt(uid.AsSpan(parametersStart), out int i4))
                return false;

            variantIndex = i4;
        }
        else if (uid[parametersStart] == '(')
        {
            if (!TryParseParameters(managedMethod.Span, out parameters, uid, parametersStart, out int endIndex))
                return false;

            if (endIndex == 0 || endIndex >= uid.Length - 2 || uid[endIndex + 1] != ' ' || !char.IsDigit(uid[endIndex + 2]))
                return false;

            if (!MathHelper.TryParseInt(uid.AsSpan(endIndex + 2), out int i4) || i4 < 0)
                return false;

            variantIndex = i4;
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool TryParseParameters(ReadOnlySpan<char> managedMethod, out object?[] parameters, string uid, int parametersStart, out int endIndex)
    {
        endIndex = 0;
        parameters = Array.Empty<object>();
        try
        {
            scoped ManagedIdentifierTokenizer tokenizer = new ManagedIdentifierTokenizer(managedMethod, ManagedIdentifierKind.Method);
            scoped ManagedIdentifierTokenizer paramEnumeratorTokenizer = default;
            int paramCount = -1;
            int typeParamDepth = 0;
            while (tokenizer.MoveNext())
            {
                switch (tokenizer.TokenType)
                {
                    case ManagedIdentifierTokenType.OpenParameters:
                        // make a copy of the tokenizer so parameter types can be established
                        paramEnumeratorTokenizer = tokenizer;
                        paramCount = 0;
                        break;

                    case ManagedIdentifierTokenType.CloseParameters:
                    case ManagedIdentifierTokenType.NextParameter when typeParamDepth == 0:
                        ++paramCount;
                        break;

                    case ManagedIdentifierTokenType.OpenTypeParameters:
                        ++typeParamDepth;
                        break;

                    case ManagedIdentifierTokenType.CloseTypeParameters:
                        --typeParamDepth;
                        break;
                }
            }

            if (paramCount <= 0)
                return false;

            object?[] p = new object?[paramCount];
            paramCount = 0;

            bool isSystem = false;
            typeParamDepth = 0;

            int index = parametersStart + 1;

            bool skipRead = false;
            bool isNullable = false;
            bool isNullableType = false;

            bool parameterHasBeenParsed = false;
            int namespaceStep = 0;

            while (skipRead || paramEnumeratorTokenizer.MoveNext())
            {
                skipRead = false;
                int readSize;
                switch (paramEnumeratorTokenizer.TokenType)
                {
                    case ManagedIdentifierTokenType.TypeSegment:
                        ++namespaceStep;
                        if (isNullable && !isNullableType)
                            return false;
                        readSize = 0;
                        ReadOnlySpan<char> value = paramEnumeratorTokenizer.Value;
                        if (isSystem)
                        {
                            isSystem = false;
                            parameterHasBeenParsed = true;
                            if (isNullable && IsNull(uid, index, ref readSize))
                            {
                                p[paramCount] = null;
                            }
                            else switch (value)
                            {
                                default:
                                    parameterHasBeenParsed = false;
                                    continue;

                                case "Boolean" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadBoolean(uid, index, ref readSize, out bool boolean))
                                        return false;

                                    p[paramCount] = boolean ? TrueBox : FalseBox;
                                    break;

                                case "Byte" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out int i4) || i4 < byte.MinValue || i4 > byte.MaxValue)
                                        return false;
                                    p[paramCount] = (byte)i4;
                                    break;

                                case "Char" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out i4) || i4 < char.MinValue || i4 > char.MaxValue)
                                        return false;
                                    p[paramCount] = (char)i4;
                                    break;

                                case "DateTime" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadString(uid, index, out readSize, out ReadOnlyMemory<char> mem))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!DateTime.TryParseExact(mem.Span, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
#else
                                    if (!DateTime.TryParseExact(mem.ToString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
#endif
                                        return false;
                                    p[paramCount] = dt;
                                    break;

                                case "DBNull" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!IsNull(uid, index, ref readSize))
                                        return false;
                                    p[paramCount] = DBNull.Value;
                                    break;

                                case "Decimal" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!decimal.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal r16))
#else
                                    if (!decimal.TryParse(uid.Substring(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal r16))
#endif
                                        return false;
                                    p[paramCount] = r16;
                                    break;

                                case "Double" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!double.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double r8))
#else
                                    if (!double.TryParse(uid.Substring(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out double r8))
#endif
                                        return false;
                                    p[paramCount] = r8;
                                    break;

                                case "Int16" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out i4) || i4 < short.MinValue || i4 > short.MaxValue)
                                        return false;
                                    p[paramCount] = (short)i4;
                                    break;

                                case "Int32" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out i4))
                                        return false;
                                    p[paramCount] = i4;
                                    break;

                                case "Int64" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!long.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
#else
                                    if (!long.TryParse(uid.Substring(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
#endif
                                        return false;
                                    p[paramCount] = i8;
                                    break;

                                case "SByte" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out i4) || i4 < sbyte.MinValue || i4 > sbyte.MaxValue)
                                        return false;
                                    p[paramCount] = (sbyte)i4;
                                    break;

                                case "Single" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!float.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out float r4))
#else
                                    if (!float.TryParse(uid.Substring(index, readSize), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out float r4))
#endif
                                        return false;
                                    p[paramCount] = r4;
                                    break;
                                        
                                case "String" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (IsNull(uid, index, ref readSize))
                                    {
                                        p[paramCount] = null;
                                        break;
                                    }

                                    if (!TryReadString(uid, index, out readSize, out mem))
                                        return false;
                                    p[paramCount] = mem.ToString();
                                    break;
                                        
                                case "UInt16" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
                                    if (!MathHelper.TryParseInt(uid.AsSpan(index, readSize), out i4) || i4 < ushort.MinValue || i4 > ushort.MaxValue)
                                        return false;
                                    p[paramCount] = (ushort)i4;
                                    break;

                                case "UInt32" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!uint.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out uint u4))
#else
                                    if (!uint.TryParse(uid.Substring(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out uint u4))
#endif
                                        return false;
                                    p[paramCount] = u4;
                                    break;

                                case "UInt64" when !HasMoreTypeData(ref skipRead, ref paramEnumeratorTokenizer):
                                    if (!TryReadDigits(uid, index, out readSize))
                                        return false;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    if (!ulong.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ulong u8))
#else
                                    if (!ulong.TryParse(uid.Substring(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ulong u8))
#endif
                                        return false;
                                    p[paramCount] = u8;
                                    break;

                                case "Nullable":
                                    if (isNullable)
                                        return false;

                                    isNullable = true;
                                    continue;
                            }
                        }
                        else if (value.Equals("System", StringComparison.Ordinal))
                        {
                            isSystem = namespaceStep == 1;
                            continue;
                        }
                        else
                            continue;

                        goto parsedValue;

                    case ManagedIdentifierTokenType.CloseParameters:
                    case ManagedIdentifierTokenType.NextParameter when typeParamDepth == 0:

                        if (!parameterHasBeenParsed) // assume its an enum
                        {
                            if (!TryReadDigits(uid, index, out readSize))
                                return false;

                            if (uid[index] == '-')
                            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                if (!long.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
#else
                                if (!long.TryParse(uid.Substring(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long i8))
#endif
                                    return false;

                                p[paramCount] = i8;
                            }
                            else
                            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                if (!ulong.TryParse(uid.AsSpan(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ulong u8))
#else
                                if (!ulong.TryParse(uid.Substring(index, readSize), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ulong u8))
#endif
                                    return false;

                                p[paramCount] = u8;
                            }

                            ++paramCount;
                            parameterHasBeenParsed = false;
                        }

                        namespaceStep = 0;
                        isNullable = false;
                        isNullableType = false;
                        break;

                    // next type parameter
                    case ManagedIdentifierTokenType.NextParameter:
                        if (isNullableType)
                            return false;
                        break;

                    case ManagedIdentifierTokenType.OpenTypeParameters:
                        if (isNullable)
                            isNullableType = true;
                        namespaceStep = 0;
                        ++typeParamDepth;
                        break;

                    case ManagedIdentifierTokenType.CloseTypeParameters:
                        if (typeParamDepth > 0)
                            --typeParamDepth;
                        isNullableType = false;
                        break;

                    case ManagedIdentifierTokenType.MethodGenericParameterReference:
                    case ManagedIdentifierTokenType.TypeGenericParameterReference:
                        if (typeParamDepth != 0)
                            break;

                        readSize = 0;
                        parameterHasBeenParsed = true;
                        // could be any type pretty much
                        if (IsNull(uid, index, ref readSize))
                        {
                            p[paramCount] = null;
                        }
                        else if (TryReadBoolean(uid, index, ref readSize, out bool boolean))
                        {
                            p[paramCount] = boolean ? TrueBox : FalseBox;
                        }
                        else if (TryReadDigits(uid, index, out readSize))
                        {
                            if (MathHelper.TryParseNumber(uid.AsSpan(index, readSize), out object? number))
                            {
                                p[paramCount] = number;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else if (TryReadString(uid, index, out readSize, out ReadOnlyMemory<char> mem))
                        {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                            if (DateTime.TryParseExact(mem.Span, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
#else
                            if (DateTime.TryParseExact(mem.ToString(), "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
#endif
                            {
                                p[paramCount] = dt;
                            }
                            else
                            {
                                p[paramCount] = mem.Span.ToString();
                            }
                        }

                        goto parsedValue;
                }

                continue;
                parsedValue:
                if (readSize == 0)
                    return false;

                if (index + readSize >= uid.Length)
                    return false;

                char c = uid[index + readSize];
                if (c is not ',' and not ')')
                    return false;

                ++paramCount;

                if (c == ')' == paramCount < p.Length)
                    return false;

                endIndex = index + readSize;
                index += readSize + 1;
            }

            if (paramCount < p.Length)
            {
                return false;
            }

            parameters = p;
        }
        catch (FormatException)
        {
            return false;
        }

        return true;

        static bool HasMoreTypeData(ref bool skipRead, ref ManagedIdentifierTokenizer paramEnumeratorTokenizer)
        {
            if (!paramEnumeratorTokenizer.MoveNext())
                return true;

            skipRead = true;
            return paramEnumeratorTokenizer.TokenType is not (ManagedIdentifierTokenType.NextParameter or ManagedIdentifierTokenType.CloseParameters or ManagedIdentifierTokenType.CloseTypeParameters);
        }

        static bool TryReadBoolean(string uid, int index, ref int readSize, out bool boolean)
        {
            boolean = false;
            if (uid.Length <= index + 4)
            {
                return false;
            }

            ReadOnlySpan<char> b = uid.AsSpan(index, 4);
            if (b.Equals("true", StringComparison.Ordinal))
            {
                readSize = 4;
                boolean = true;
                return true;
            }

            if (b.Equals("fals", StringComparison.Ordinal) && uid.Length > index + 5 && uid[index + 4] == 'e')
            {
                readSize = 5;
                return true;
            }

            return false;
        }

        static bool TryReadDigits(string uid, int index, out int readSize)
        {
            readSize = 1;
            if (index >= uid.Length || !IsDigitLike(uid[index]))
                return false;

            while (index + readSize < uid.Length && IsDigitLike(uid[index + readSize]))
                ++readSize;

            return true;

            static bool IsDigitLike(char c) => char.IsDigit(c) || c is '-' or '.';
        }

        static bool IsNull(string uid, int index, ref int readSize)
        {
            if (index + 4 >= uid.Length)
                return false;

            if (!uid.AsSpan(index, 4).Equals("null", StringComparison.Ordinal))
                return false;

            readSize = 4;
            return true;
        }

        static bool TryReadString(string uid, int index, out int readSize, out ReadOnlyMemory<char> str)
        {
            str = ReadOnlyMemory<char>.Empty;
            readSize = 0;
            if (index + 1 >= uid.Length || uid[index] != '"')
                return false;

            int slashCt = 0;

            int endIndex = index + 1;
            bool hasEscapes = false;
            for (; endIndex < uid.Length; ++endIndex)
            {
                char c = uid[endIndex];
                if (c == '\\')
                {
                    hasEscapes = true;
                    ++slashCt;
                    continue;
                }

                if (slashCt % 2 == 1 && !StringLiteralEscaper.IsEscapeSequenceChar(c))
                {
                    return false;
                }

                if (c == '\"' && slashCt % 2 == 0)
                {
                    break;
                }

                slashCt = 0;
            }

            if (endIndex >= uid.Length)
                return false;

            if (!hasEscapes)
            {
                str = uid.AsMemory(index + 1, endIndex - index - 1);
            }
            else
            {
                str = StringLiteralEscaper.Unescape(uid.AsSpan(index + 1, endIndex - index - 1)).AsMemory();
            }

            readSize = endIndex - index + 1;
            return true;
        }
    }

    /// <summary>
    /// Creates a properly formatted <see cref="UnturnedTestUid"/> based on the given normalized managed identifiers.
    /// </summary>
    public static UnturnedTestUid Create(string normalizedManagedType, string normalizedManagedMethod, int parameterVariantIndex = -1, string? typeParameters = null, string? parameters = null)
    {
        int managedTypeLength = normalizedManagedType.Length;
        int managedMethodLength = normalizedManagedMethod.Length;

        if (string.IsNullOrWhiteSpace(parameters))
            parameters = null;

        if (parameters != null)
            parameterVariantIndex = Math.Max(0, parameterVariantIndex);

        // format:
        // {managedTypeLength}:{managedMethodLength}:type.method
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER

        int strSize = CountDigits(managedTypeLength)
                      + CountDigits(managedMethodLength)
                      + managedTypeLength
                      + managedMethodLength
                      + 3 /* [::.] */;

        if (typeParameters != null)
            strSize += typeParameters.Length + 3;

        if (parameters != null)
        {
            strSize += parameters.Length + 3;
            strSize += CountDigits(parameterVariantIndex) + 1;
        }
        else if (parameterVariantIndex >= 0)
        {
            strSize += CountDigits(parameterVariantIndex) + 1;
        }

        UnturnedTestUidCreateState state = default;
        state.ManagedType = normalizedManagedType;
        state.ManagedMethod = normalizedManagedMethod;
        state.Parameters = parameters;
        state.TypeParameters = typeParameters;
        state.ParameterVariantIndex = parameterVariantIndex;

        return new UnturnedTestUid(string.Create(strSize, state, static (span, state) =>
        {
            int managedTypeLength = state.ManagedType.Length;
            int managedMethodLength = state.ManagedMethod.Length;

            managedTypeLength.TryFormat(span, out int charsWritten, "F0", CultureInfo.InvariantCulture);

            span[charsWritten] = ':';
            ++charsWritten;

            managedMethodLength.TryFormat(span[charsWritten..], out int methodCharsWritten, "F0", CultureInfo.InvariantCulture);
            charsWritten += methodCharsWritten;

            span[charsWritten] = ':';
            ++charsWritten;

            state.ManagedType.AsSpan().CopyTo(span[charsWritten..]);
            charsWritten += managedTypeLength;

            span[charsWritten] = '.';
            ++charsWritten;

            state.ManagedMethod.AsSpan().CopyTo(span[charsWritten..]);
            charsWritten += managedMethodLength;

            if (state.TypeParameters != null)
            {
                span[charsWritten] = ' ';
                ++charsWritten;
                span[charsWritten] = '<';
                ++charsWritten;

                state.TypeParameters.AsSpan().CopyTo(span[charsWritten..]);
                charsWritten += state.TypeParameters.Length;

                span[charsWritten] = '>';
                ++charsWritten;
            }

            if (state.Parameters != null || state.ParameterVariantIndex >= 0)
            {
                span[charsWritten] = ' ';
                ++charsWritten;
                if (state.Parameters != null)
                {
                    span[charsWritten] = '(';
                    ++charsWritten;

                    state.Parameters.AsSpan().CopyTo(span[charsWritten..]);
                    charsWritten += state.Parameters.Length;

                    span[charsWritten] = ')';
                    ++charsWritten;
                }

                if (state.ParameterVariantIndex >= 0)
                {
                    if (span[charsWritten - 1] != ' ')
                    {
                        span[charsWritten] = ' ';
                        ++charsWritten;
                    }
                    state.ParameterVariantIndex.TryFormat(
                        span[charsWritten..],
                        out _ /* out int parameterVariantCharsWritten */,
                        "F0",
                        CultureInfo.InvariantCulture
                    );
                    // charsWritten += parameterVariantCharsWritten;
                }
            }
        }));
#else
        StringBuilder sb = StringBuilderPool.Rent();

        sb.Append(managedTypeLength.ToString("F0", CultureInfo.InvariantCulture))
            .Append(':')
            .Append(managedMethodLength.ToString("F0", CultureInfo.InvariantCulture))
            .Append(':')
            .Append(normalizedManagedType)
            .Append('.')
            .Append(normalizedManagedMethod)
        ;

        if (typeParameters != null)
        {
            sb.Append(" <").Append(typeParameters).Append('>');
        }

        if (parameters != null || parameterVariantIndex >= 0)
        {
            sb.Append(' ');
            if (parameters != null)
            {
                sb.Append('(').Append(parameters).Append(')');
            }

            if (parameterVariantIndex >= 0)
            {
                if (parameters != null)
                    sb.Append(' ');
                sb.Append(parameterVariantIndex.ToString("F0", CultureInfo.InvariantCulture));
            }
        }

        string str = sb.ToString();
        StringBuilderPool.Return(sb);
        return str;
#endif
    }

    /// <summary>
    /// Formats a managed type with it's length prefix.
    /// </summary>
    public static string CreateType(string normalizedManagedType)
    {
        int len = normalizedManagedType.Length;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        return string.Create(len + 1 + CountDigits(len), normalizedManagedType, static (span, state) =>
        {
            state.Length.TryFormat(span, out int charsWritten, "F0", CultureInfo.InvariantCulture);
            span[charsWritten] = ':';
            ++charsWritten;

            state.AsSpan().CopyTo(span[charsWritten..]);
        });
#else
        return $"{len.ToString("F0", CultureInfo.InvariantCulture)}:{normalizedManagedType}";
#endif
    }

    private static readonly TextEscaper StringLiteralEscaper = new TextEscaper('\r', '\n', '\t', '\v', '\\', '\"', '\0');

    public static string FormatTypeParameters(Type[] methodTypeParameters)
    {
        int ttlSize = methodTypeParameters.Length;

        if (ttlSize == 0)
        {
            return string.Empty;
        }

        string[] types = new string[ttlSize];
        for (int i = 0; i < methodTypeParameters.Length; ++i)
        {
            types[i] = CreateType(ManagedIdentifier.GetManagedType(methodTypeParameters[i]));
        }

#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return string.Join(',', types);
#else
        return string.Join(",", types);
#endif
    }

    internal static void WriteParameterForTreeNode(object? parameter, StringBuilder sb)
    {
        switch (parameter)
        {
            case null:
                sb.Append("null");
                break;

            case string str:
                sb.Append('"').Append(TreeNodeFilterHelper.Escape(StringLiteralEscaper.Escape(str))).Append('"');
                break;

            case char c:
                sb.Append(((ushort)c).ToString(CultureInfo.InvariantCulture));
                break;

            case bool b:
                sb.Append(b ? "true" : "false");
                break;

            case DateTime dt:
                sb.Append('"').Append(dt.ToString("O", CultureInfo.InvariantCulture)).Append('"');
                break;

            case DateTimeOffset dt:
                sb.Append('"').Append(dt.ToString("O", CultureInfo.InvariantCulture)).Append('"');
                break;

            case TimeSpan ts:
                sb.Append(ts.Ticks.ToString(CultureInfo.InvariantCulture));
                break;

            case Version v:
                sb.Append('"').Append(v).Append('"');
                break;

            case Type type:
                sb.Append(TreeNodeFilterHelper.Escape(CreateType(ManagedIdentifier.GetManagedType(type))));
                break;

            case IntPtr ptr:
                sb.Append(ptr.ToInt64().ToString(CultureInfo.InvariantCulture));
                break;

            case UIntPtr ptr:
                sb.Append(ptr.ToUInt64().ToString(CultureInfo.InvariantCulture));
                break;

            case Enum @enum:
                sb.Append(@enum.ToString("D"));
                break;

            case IFormattable f:
                sb.Append(f.ToString(null, CultureInfo.InvariantCulture));
                break;

            case IConvertible c:
                sb.Append('"').Append(TreeNodeFilterHelper.Escape(StringLiteralEscaper.Escape(c.ToString(CultureInfo.InvariantCulture)))).Append('"');
                break;

            default:
                sb.Append('"').Append(TreeNodeFilterHelper.Escape(StringLiteralEscaper.Escape(parameter.ToString()))).Append('"');
                break;
        }
    }

    public static bool TryFormatParameters(object?[]? args, out string argList)
    {
        if (args == null || args.Length == 0)
        {
            argList = string.Empty;
            return true;
        }

        argList = null!;
        for (int i = 0; i < args.Length; ++i)
        {
            if (!CanConvert(args[i]))
                return false;
        }

        string[] values = new string[args.Length];
        for (int i = 0; i < args.Length; ++i)
        {
            object? arg = args[i];
            values[i] = arg switch
            {
                string str           => $"\"{StringLiteralEscaper.Escape(str)}\"",
                char c               => ((ushort)c).ToString(CultureInfo.InvariantCulture),
                bool b               => b ? "true" : "false",
                DateTime dt          => $"\"{dt.ToString("O", CultureInfo.InvariantCulture)}\"",
                DateTimeOffset dt    => $"\"{dt.ToString("O", CultureInfo.InvariantCulture)}\"",
                TimeSpan ts          => ts.Ticks.ToString(CultureInfo.InvariantCulture),
                IPAddress or Version => $"\"{arg}\"",
                Type type            => CreateType(ManagedIdentifier.GetManagedType(type)),
                IntPtr ptr           => ptr.ToInt64().ToString(CultureInfo.InvariantCulture),
                UIntPtr ptr          => ptr.ToUInt64().ToString(CultureInfo.InvariantCulture),
                Enum @enum           => @enum.ToString("D"),
                IFormattable f       => f.ToString(null, CultureInfo.InvariantCulture),
                IConvertible c       => $"\"{c.ToString(CultureInfo.InvariantCulture)}\"",
                _                    => "null"
            };
        }

#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        argList = string.Join(',', values);
#else
        argList = string.Join(",", values);
#endif
        return true;

        static bool CanConvert(object? arg)
        {
            return arg is null
                or IConvertible
                or DateTime
                or DateTimeOffset
                or TimeSpan
                or IPAddress
                or Version
                or Guid
                or Type
                or IntPtr
                or UIntPtr;
        }
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
    private struct UnturnedTestUidCreateState
    {
        public string ManagedType;
        public string ManagedMethod;
        public string? Parameters;
        public string? TypeParameters;
        public int ParameterVariantIndex;
    }

    private static int CountDigits(int value)
    {
        return value != 0 ? 1 + (int)Math.Log10(Math.Abs(value)) : 1;
    }
#endif
}
