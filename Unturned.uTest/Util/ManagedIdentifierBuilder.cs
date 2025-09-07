using System;
using System.Globalization;
using System.IO;
using System.Text;
#if !(NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER)
using System.Buffers;
#endif

// ReSharper disable LocalizableElement

namespace uTest;

// included in Unturned.uTest.Runner and Unturned.uTest.Runner.SourceGenerator

/// <summary>
/// A string builder that can write type names and methods in the <c>ManagedType</c> and <c>ManagedMethod</c> formats specified in the document below.
/// <para>
/// <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md"/>
/// </para>
/// </summary>
public struct ManagedIdentifierBuilder : IDisposable
{
    private readonly StringBuilder? _stringBuilder;
    private readonly TextWriter? _textWriter;
    private readonly bool _leaveOpen;

    private ManagedIdentifierTokenType _lastToken;
    private int _currentTypeArity;
    private int _typeParamDepth;
    private int _parameterIndex;
    private int _methodArity;
    private bool _disposed;

    private bool _pendingOpenParenthesis;

    public ManagedIdentifierBuilder(StringBuilder stringBuilder)
    {
        _stringBuilder = stringBuilder;
        _parameterIndex = -1;
    }

    public ManagedIdentifierBuilder(TextWriter textWriter, bool leaveOpen = true)
    {
        _textWriter = textWriter;
        _leaveOpen = leaveOpen;
        _parameterIndex = -1;
    }

    /// <summary>
    /// Appends the name of the interface this method is explicitly implemented with.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void AddExplicitImplementationInterfaceName(ReadOnlySpan<char> interfaceName)
    {
        AssertNotDisposed();

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.Uninitialized:
                break;

            case ManagedIdentifierTokenType.MethodImplementationTypeSegment:
                Append('.');
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.MethodImplementationTypeSegment);
                break;
        }

        AppendOrEscapeAndAppend(interfaceName, true);
        _lastToken = ManagedIdentifierTokenType.MethodImplementationTypeSegment;
    }

    /// <summary>
    /// Appends the initial method name. <paramref name="methodName"/> should not include the explicit implementation interface name.
    /// </summary>
    /// <param name="arity">The number of generic parameters this method has, or 0 if it's not generic.</param>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void AddMethodName(ReadOnlySpan<char> methodName, int arity = 0)
    {
        AssertNotDisposed();

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.Uninitialized:
                break;

            case ManagedIdentifierTokenType.MethodImplementationTypeSegment:
                Append('.');
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.MethodName);
                break;
        }

        AppendOrEscapeAndAppend(methodName, isMethodName: true);

        _lastToken = ManagedIdentifierTokenType.MethodName;
        AppendArity(arity, ManagedIdentifierTokenType.Arity);
        _methodArity = arity;
    }

    private readonly unsafe void AppendOrEscapeAndAppend(ReadOnlySpan<char> tokenizerValue, bool ignoreGenerics = false, bool isMethodName = false)
    {
        if (ManagedIdentifier.IdentifierNeedsEscaping(tokenizerValue, ignoreGenerics, isMethodName))
        {
            Append('\'');
            ReadOnlySpan<char> rewriteValues = ['\'', '\\'];
            int nextEscIndex = tokenizerValue.IndexOfAny(rewriteValues);

            fixed (char* ptr = tokenizerValue)
            {
                int previousEscIndex = 0;
                if (nextEscIndex >= 0)
                {
                    while (true)
                    {
                        int length = nextEscIndex - previousEscIndex;
                        if (length != 0)
                            Append(ptr + previousEscIndex, length);

                        if (nextEscIndex >= tokenizerValue.Length)
                            break;

                        Append(ptr[nextEscIndex] == '\'' ? @"\'" : @"\\");

                        previousEscIndex = nextEscIndex + 1;
                        if (previousEscIndex >= tokenizerValue.Length)
                            break;

                        nextEscIndex = tokenizerValue.Slice(previousEscIndex).IndexOfAny(rewriteValues);
                        if (nextEscIndex == -1)
                            nextEscIndex = tokenizerValue.Length;
                        else
                            nextEscIndex += previousEscIndex;
                    }
                }
                else
                {
                    Append(ptr, tokenizerValue.Length);
                }
            }
            Append('\'');
        }
        else
        {
            fixed (char* ptr = tokenizerValue)
                Append(ptr, tokenizerValue.Length);
        }
    }


    /// <summary>
    /// Appends the comma separating parameters or type parameters.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void NextParameter()
    {
        AssertNotDisposed();

        if (_pendingOpenParenthesis)
            ThrowInvalidLastToken(ManagedIdentifierTokenType.CloseTypeParameters, ManagedIdentifierTokenType.OpenParameters);

        if (_typeParamDepth == 0 && _parameterIndex == -1)
            throw new InvalidOperationException("BeginParameters was not called.");

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.CloseTypeParameters:
            case ManagedIdentifierTokenType.Array:
            case ManagedIdentifierTokenType.Pointer:
            case ManagedIdentifierTokenType.Reference:
            case ManagedIdentifierTokenType.TypeGenericParameterReference:
            case ManagedIdentifierTokenType.MethodGenericParameterReference:
            case ManagedIdentifierTokenType.TypeArity:
            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.NestedTypeSegment:
                _lastToken = ManagedIdentifierTokenType.NextParameter;
                _currentTypeArity = 0;
                Append(',');
                if (_typeParamDepth == 0)
                    ++_parameterIndex;
                break;
        }
    }

    /// <summary>
    /// Appends the parenthesis marking the beginning of the method's parameter list.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void BeginParameters()
    {
        AssertNotDisposed();

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.MethodName:
            case ManagedIdentifierTokenType.Arity:
                _pendingOpenParenthesis = true;
                _currentTypeArity = 0;
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.OpenTypeParameters);
                break;
        }
    }

    /// <summary>
    /// Appends the parenthesis marking the end of the method's parameter list.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void EndParameters()
    {
        AssertNotDisposed();

        if (_pendingOpenParenthesis)
        {
            _pendingOpenParenthesis = false;
            return;
        }

        if (_parameterIndex == -1)
            throw new InvalidOperationException("BeginParameters was not called.");

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.OpenParameters:
            case ManagedIdentifierTokenType.CloseTypeParameters:
            case ManagedIdentifierTokenType.Array:
            case ManagedIdentifierTokenType.Pointer:
            case ManagedIdentifierTokenType.Reference:
            case ManagedIdentifierTokenType.TypeGenericParameterReference:
            case ManagedIdentifierTokenType.MethodGenericParameterReference:
            case ManagedIdentifierTokenType.TypeArity:
            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.NestedTypeSegment:
                Append(')');
                _lastToken = ManagedIdentifierTokenType.CloseParameters;
                _currentTypeArity = 0;
                _parameterIndex = -1;
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.CloseParameters);
                break;
        }
    }

    /// <summary>
    /// Appends the bracket marking the beginning of a type's generic parameter list.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void BeginTypeParameters()
    {
        AssertNotDisposed();

        if (_pendingOpenParenthesis)
            ThrowInvalidLastToken(ManagedIdentifierTokenType.OpenTypeParameters, ManagedIdentifierTokenType.OpenParameters);

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.TypeArity:
            case ManagedIdentifierTokenType.NestedTypeSegment when _currentTypeArity > 0:
                Append('<');
                ++_typeParamDepth;
                _lastToken = ManagedIdentifierTokenType.OpenTypeParameters;
                _currentTypeArity = 0;
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.OpenTypeParameters);
                break;
        }
    }

    /// <summary>
    /// Appends the bracket marking the end of a type's generic parameter list.
    /// </summary>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void EndTypeParameters()
    {
        AssertNotDisposed();

        if (_pendingOpenParenthesis)
            ThrowInvalidLastToken(ManagedIdentifierTokenType.CloseTypeParameters, ManagedIdentifierTokenType.OpenParameters);

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.CloseTypeParameters:
            case ManagedIdentifierTokenType.Array:
            case ManagedIdentifierTokenType.Pointer:
            case ManagedIdentifierTokenType.Reference:
            case ManagedIdentifierTokenType.TypeGenericParameterReference:
            case ManagedIdentifierTokenType.MethodGenericParameterReference:
            case ManagedIdentifierTokenType.TypeArity:
            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.NestedTypeSegment:
                Append('>');
                --_typeParamDepth;
                _lastToken = ManagedIdentifierTokenType.CloseTypeParameters;
                _currentTypeArity = 0;
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.CloseTypeParameters);
                break;
        }
    }

    /// <summary>
    /// Appends a segment of a namespace, type, or nested type.
    /// </summary>
    /// <param name="type">Single segment of a namespace, type, or class.</param>
    /// <param name="isNested">If the type is defined within the previous type.</param>
    /// <param name="arity">Number of type parameters this type defines. This does not include parent types.</param>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void AddTypeSegment(ReadOnlySpan<char> type, bool isNested, int arity = 0)
    {
        AssertNotDisposed();
        ApplyOpenParenthesis();

        switch (_lastToken)
        {
            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.NestedTypeSegment:
            case ManagedIdentifierTokenType.TypeArity when isNested:
                Append(isNested ? '+' : '.');
                break;

            case ManagedIdentifierTokenType.Uninitialized:
            case ManagedIdentifierTokenType.OpenParameters:
            case ManagedIdentifierTokenType.OpenTypeParameters:
            case ManagedIdentifierTokenType.NextParameter:
                break;

            default:
                ThrowInvalidLastToken(ManagedIdentifierTokenType.TypeSegment);
                break;
        }

        AppendOrEscapeAndAppend(type);
        _lastToken = isNested ? ManagedIdentifierTokenType.NestedTypeSegment : ManagedIdentifierTokenType.TypeSegment;
        AppendArity(arity, ManagedIdentifierTokenType.TypeArity);
        _currentTypeArity += arity;
    }

    /// <summary>
    /// Appends a reference to a generic parameter on this method, designated like: '!!<paramref name="index"/>'.
    /// </summary>
    /// <param name="index">The zero-based index of the generic parameter.</param>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value provided for <paramref name="index"/> is either less than 0 or greater than or equal to than the amount initially specified as the method's arity.</exception>
    public void AddMethodGenericParameterReference(int index)
    {
        AssertNotDisposed();
        ApplyOpenParenthesis();

        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_lastToken is not (ManagedIdentifierTokenType.OpenParameters
                                   or ManagedIdentifierTokenType.OpenTypeParameters
                                   or ManagedIdentifierTokenType.NextParameter))
        {
            if (index > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(index));

            ThrowInvalidLastToken(ManagedIdentifierTokenType.MethodGenericParameterReference);
        }

        if (index >= _methodArity)
        {
            throw new ArgumentOutOfRangeException(nameof(index), _methodArity == 0
                ? "This method did not specify an arity."
                : $"This method only specified an arity of {_methodArity}."
            );
        }

        Append("!!");
        Append(index);
        _currentTypeArity = 0;
        _lastToken = ManagedIdentifierTokenType.MethodGenericParameterReference;
    }

    /// <summary>
    /// Appends a reference to a generic parameter on this method's declaring type, designated like: '!<paramref name="index"/>'.
    /// </summary>
    /// <remarks>The index must also include the full nested type hierarchy, where the outer-most type's first generic parameter is 0, then it increases for each nested type.</remarks>
    /// <param name="index">The zero-based index of the generic parameter.</param>
    /// <exception cref="ObjectDisposedException"/>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The value provided for <paramref name="index"/> is less than 0 or too high to be a type parameter reference.</exception>
    public void AddTypeGenericParameterReference(int index)
    {
        AssertNotDisposed();
        ApplyOpenParenthesis();

        if (index is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_lastToken is not (ManagedIdentifierTokenType.OpenParameters
                                   or ManagedIdentifierTokenType.OpenTypeParameters
                                   or ManagedIdentifierTokenType.NextParameter))
        {
            ThrowInvalidLastToken(ManagedIdentifierTokenType.TypeGenericParameterReference);
        }

        Append('!');
        Append(index);
        _currentTypeArity = 0;
        _lastToken = ManagedIdentifierTokenType.TypeGenericParameterReference;
    }

    /// <summary>
    /// Converts the type just written to an array type.
    /// </summary>
    /// <param name="rank">0 for a 'vector' (SZ) array, otherwise the rank of a multi-dimensional array. 1-rank MD arrays are represented as '[*]', not '[]', so 0 and 1 are not the same.</param>
    /// <exception cref="ArgumentOutOfRangeException">Rank is less than 0.</exception>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void MakeArrayType(int rank = 0)
    {
        if (_lastToken is not (ManagedIdentifierTokenType.TypeSegment
                                   or ManagedIdentifierTokenType.NestedTypeSegment
                                   or ManagedIdentifierTokenType.Array
                                   or ManagedIdentifierTokenType.Pointer
                                   or ManagedIdentifierTokenType.CloseTypeParameters
                                   or ManagedIdentifierTokenType.TypeArity
                                   or ManagedIdentifierTokenType.TypeGenericParameterReference
                                   or ManagedIdentifierTokenType.MethodGenericParameterReference))
        {
            ThrowInvalidLastToken(ManagedIdentifierTokenType.Array);
        }

        switch (rank)
        {
            case < 0 or > ManagedIdentifier.MaxArrayRank:
                throw new ArgumentOutOfRangeException(nameof(rank));
            case 0:
                Append("[]");
                break;
            case 1:
                Append("[*]");
                break;
            default:
                Append('[');
                Append(',', rank - 1);
                Append(']');
                break;
        }

        _lastToken = ManagedIdentifierTokenType.Array;
    }

    /// <summary>
    /// Converts the type just written to a pointer type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void MakePointerType()
    {
        if (_lastToken is not (ManagedIdentifierTokenType.TypeSegment
                                   or ManagedIdentifierTokenType.NestedTypeSegment
                                   or ManagedIdentifierTokenType.Array
                                   or ManagedIdentifierTokenType.Pointer
                                   or ManagedIdentifierTokenType.CloseTypeParameters
                                   or ManagedIdentifierTokenType.TypeArity
                                   or ManagedIdentifierTokenType.TypeGenericParameterReference
                                   or ManagedIdentifierTokenType.MethodGenericParameterReference))
        {
            ThrowInvalidLastToken(ManagedIdentifierTokenType.Array);
        }

        Append('*');
        _lastToken = ManagedIdentifierTokenType.Pointer;
    }

    /// <summary>
    /// Converts the type just written to a reference type.
    /// </summary>
    /// <exception cref="InvalidOperationException">Invalid location for this token.</exception>
    public void MakeReferenceType()
    {
        if (_lastToken is not (ManagedIdentifierTokenType.TypeSegment
                                   or ManagedIdentifierTokenType.NestedTypeSegment
                                   or ManagedIdentifierTokenType.Array
                                   or ManagedIdentifierTokenType.Pointer
                                   or ManagedIdentifierTokenType.CloseTypeParameters
                                   or ManagedIdentifierTokenType.TypeArity
                                   or ManagedIdentifierTokenType.TypeGenericParameterReference
                                   or ManagedIdentifierTokenType.MethodGenericParameterReference))
        {
            ThrowInvalidLastToken(ManagedIdentifierTokenType.Array);
        }

        Append('&');
        _lastToken = ManagedIdentifierTokenType.Reference;
    }

    /// <summary>
    /// Appends the current token from a <paramref name="tokenizer"/>.
    /// </summary>
    public void WriteToken(in ManagedIdentifierTokenizer tokenizer)
    {
        ManagedIdentifierTokenType tokenType = tokenizer.TokenType;
        switch (tokenType)
        {
            case ManagedIdentifierTokenType.MethodName:
                AddMethodName(tokenizer.Value);
                return;

            case ManagedIdentifierTokenType.Arity:
                int arity = tokenizer.Arity;
                if (arity != 0)
                {
                    Append('`');
                    Append(arity);
                }
                _methodArity = arity;
                break;

            case ManagedIdentifierTokenType.TypeArity:
                arity = tokenizer.Arity;
                if (arity != 0)
                {
                    Append('`');
                    Append(arity);
                }

                _currentTypeArity += arity;
                break;

            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.NestedTypeSegment:
                AddTypeSegment(tokenizer.Value, tokenType == ManagedIdentifierTokenType.NestedTypeSegment);
                return;

            case ManagedIdentifierTokenType.TypeGenericParameterReference:
                AddTypeGenericParameterReference(tokenizer.GenericReferenceIndex);
                return;

            case ManagedIdentifierTokenType.MethodGenericParameterReference:
                AddMethodGenericParameterReference(tokenizer.GenericReferenceIndex);
                return;

            case ManagedIdentifierTokenType.OpenTypeParameters:
                BeginTypeParameters();
                return;

            case ManagedIdentifierTokenType.CloseTypeParameters:
                EndTypeParameters();
                return;

            case ManagedIdentifierTokenType.OpenParameters:
                BeginParameters();
                return;

            case ManagedIdentifierTokenType.CloseParameters:
                EndParameters();
                return;

            case ManagedIdentifierTokenType.NextParameter:
                NextParameter();
                return;

            case ManagedIdentifierTokenType.MethodImplementationTypeSegment:
                AddExplicitImplementationInterfaceName(tokenizer.Value);
                return;

            case ManagedIdentifierTokenType.Array:
                MakeArrayType(tokenizer.IsSzArray ? 0 : tokenizer.ArrayRank);
                return;

            case ManagedIdentifierTokenType.Pointer:
                Append('*');
                break;

            case ManagedIdentifierTokenType.Reference:
                Append('&');
                break;
        }

        _lastToken = tokenType;
    }

    private void ApplyOpenParenthesis()
    {
        if (!_pendingOpenParenthesis)
            return;

        Append('(');
        _lastToken = ManagedIdentifierTokenType.OpenParameters;
        _pendingOpenParenthesis = false;
        _parameterIndex = 0;
    }

    /// <exception cref="ObjectDisposedException"/>
    private readonly void AssertNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedIdentifierBuilder));
    }

    private readonly void ThrowInvalidLastToken(ManagedIdentifierTokenType appending)
    {
        ThrowInvalidLastToken(appending, _lastToken);
    }
    private static void ThrowInvalidLastToken(ManagedIdentifierTokenType appending, ManagedIdentifierTokenType previous)
    {
        throw new InvalidOperationException($"Unable to append token {appending} after the previous token: {previous}.");
    }

    private void AppendArity(int arity, ManagedIdentifierTokenType arityType)
    {
        switch (arity)
        {
            case 0:
                break;
            case < 0 or > ushort.MaxValue:
                throw new ArgumentOutOfRangeException(nameof(arity));
            default:
                Append('`');
                Append(arity);
                _lastToken = arityType;
                break;
        }
    }

    private readonly void Append(string str)
    {
        if (_stringBuilder != null)
            _stringBuilder.Append(str);
        else
            _textWriter?.Write(str);
    }

    private readonly void Append(char c)
    {
        if (_stringBuilder != null)
            _stringBuilder.Append(c);
        else
            _textWriter?.Write(c);
    }

    private readonly unsafe void Append(char* ptr, int amount)
    {
        if (_stringBuilder != null)
        {
            _stringBuilder.Append(ptr, amount);
            return;
        }

        if (_textWriter == null)
            return;

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
        _textWriter.Write(new ReadOnlySpan<char>(ptr, amount));
#else
        if (amount <= 64)
        {
            char[] array = ArrayPool<char>.Shared.Rent(amount);
            fixed (char* dst = array)
            {
                Buffer.MemoryCopy(ptr, dst, array.Length * sizeof(char), amount * sizeof(char));
            }
            try
            {
                _textWriter?.Write(array, 0, amount);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(array);
            }
        }
        else
        {
            char[] array = new char[amount];
            fixed (char* dst = array)
            {
                Buffer.MemoryCopy(ptr, dst, amount * sizeof(char), amount * sizeof(char));
            }
            _textWriter?.Write(array, 0, amount);
        }
#endif
    }

    private readonly void Append(char c, int ct)
    {
        if (ct <= 0)
            return;

        if (_stringBuilder != null)
            _stringBuilder.Append(c, ct);
        else if (_textWriter != null)
        {
            if (ct == 1)
            {
                _textWriter.Write(c);
            }
            else
            {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                if (ct <= 256)
                {
                    Span<char> span = stackalloc char[ct];
                    span.Fill(c);
                    _textWriter.Write(span);
                }
                else
                {
                    _textWriter.Write(new string(c, ct));
                }
#else
                _textWriter.Write(new string(c, ct));
#endif
            }
        }
    }

    private readonly void Append(int n)
    {
        Append(n.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Checks that all syntaxes have been closed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Invalid format created by this builder.</exception>
    public readonly void Close()
    {
        if (_pendingOpenParenthesis || _parameterIndex != -1)
            throw new InvalidOperationException($"Parameter list not closed after parameter {_parameterIndex + 1}.");

        if (_typeParamDepth != 0)
            throw new InvalidOperationException("Type parameter list not closed.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        if (_textWriter != null && !_leaveOpen)
        {
            _textWriter.Dispose();
        }
    }

    /// <inheritdoc />
    public readonly override string ToString()
    {
        if (_stringBuilder != null)
            return _stringBuilder.ToString();

        return _textWriter?.ToString() ?? string.Empty;
    }
}
