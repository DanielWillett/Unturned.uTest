using System;
using System.Globalization;
using System.Net;

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
/// | [space]({value},{value})
/// | alternative format (allows for unformattable values):
/// | [space]{variationIndex}
///
/// // Examples:
/// * 13:28:System.String.IsNullOrEmpty(System.String) ("someStringValue") 
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
    /// <summary>
    /// The raw unique ID of this test.
    /// </summary>
    public string Uid { get; } = uid;

    /// <summary>
    /// Creates a properly formatted <see cref="UnturnedTestUid"/> based on the given normalized managed identifiers.
    /// </summary>
    public static UnturnedTestUid Create(string normalizedManagedType, string normalizedManagedMethod, string? typeParameters = null, string? parameters = null, int? parameterVariantIndex = null)
    {
        int managedTypeLength = normalizedManagedType.Length;
        int managedMethodLength = normalizedManagedMethod.Length;

        if (string.IsNullOrWhiteSpace(parameters))
            parameters = null;

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
            strSize += parameters.Length + 3;
        else if (parameterVariantIndex.HasValue)
            strSize += CountDigits(parameterVariantIndex.Value) + 1;

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

            if (state.Parameters != null || state.ParameterVariantIndex.HasValue)
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
                    //++charsWritten;
                }
                else
                {
                    state.ParameterVariantIndex!.Value.TryFormat(
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
        if (!string.IsNullOrWhiteSpace(parameters))
        {
            return new UnturnedTestUid(
                $"{managedTypeLength.ToString("F0", CultureInfo.InvariantCulture)}:{
                   managedMethodLength.ToString("F0", CultureInfo.InvariantCulture)}:{
                   normalizedManagedType}.{
                   normalizedManagedMethod} {
                   parameters}"
            );
        }
        else
        {
            return new UnturnedTestUid(
                $"{managedTypeLength.ToString("F0", CultureInfo.InvariantCulture)}:{
                   managedMethodLength.ToString("F0", CultureInfo.InvariantCulture)}:{
                   normalizedManagedType}.{
                   normalizedManagedMethod}"
            );
        }
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
                DateTime dt          => $"\"{dt.ToString("O", CultureInfo.InvariantCulture)}\"",
                DateTimeOffset dt    => $"\"{dt.ToString("O", CultureInfo.InvariantCulture)}\"",
                TimeSpan ts          => ts.Ticks.ToString(CultureInfo.InvariantCulture),
                IPAddress or Version => $"\"{arg}\"",
                Type type            => CreateType(ManagedIdentifier.GetManagedType(type)),
                IntPtr ptr           => ptr.ToInt64().ToString(CultureInfo.InvariantCulture),
                UIntPtr ptr          => ptr.ToUInt64().ToString(CultureInfo.InvariantCulture),
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
        public int? ParameterVariantIndex;
    }

    private static int CountDigits(int value)
    {
        return value != 0 ? 1 + (int)Math.Log10(Math.Abs(value)) : 1;
    }
#endif
}
