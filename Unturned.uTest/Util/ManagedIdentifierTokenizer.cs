using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace uTest;

// included in Unturned.uTest.Runner and Unturned.uTest.Runner.SourceGenerator

/// <summary>
/// A tokenizer that can read the <c>ManagedType</c> and <c>ManagedMethod</c> formats specified in the document below.
/// <para>
/// <see href="https://github.com/microsoft/vstest/blob/main/docs/RFCs/0017-Managed-TestCase-Properties.md"/>
/// </para>
/// </summary>
[DebuggerDisplay("{DebuggerDisplay}")]
public ref struct ManagedIdentifierTokenizer
{
    // this default comes from System.Reflection.Metadata's TypeNameParseOptions default value
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/src/System/Reflection/Metadata/TypeNameParseOptions.cs#L8
    private const int DefaultMaxGenericDepth = 20;

    private readonly ReadOnlySpan<char> _text;
    private readonly bool _isTypeOnly;

    private int _contentStart;
    private int _contentLength;
    private bool _contentIsInBuffer;
    private char[]? _buffer;
    private ManagedIdentifierTokenType _tokenType;

    private int _readIndex;
    private int _methodArity;

    private int _parameterIndex;
    private int _typeParamDepth;
    private int _typeParamRefIndex;
    private int _arrayRank;

    private const char EndOfLine = '\0';

    /// <summary>
    /// Display string used with <see cref="DebuggerDisplayAttribute"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly string DebuggerDisplay => _readIndex < 0 || _readIndex >= _text.Length ? _text.ToString() : (_text.Slice(0, _readIndex).ToString() + "|" + _text.Slice(_readIndex).ToString());

    /// <summary>
    /// The maximum number of generic parameters that <see cref="IsSameTypeAs"/> will navigate before returning false.
    /// </summary>
    /// <remarks>This has no effect on <see cref="MoveNext"/>.</remarks>
    public int MaxGenericDepth { get; init; }

    /// <summary>
    /// Creates a tokenizer that will process the given text <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text for the tokenizer to process.</param>
    /// <param name="kind">The kind of tokenizer to read, ManagedType or ManagedMethod.</param>
    public ManagedIdentifierTokenizer(ReadOnlySpan<char> text, ManagedIdentifierKind kind)
    {
        MaxGenericDepth = DefaultMaxGenericDepth;

        _text = text;
        _isTypeOnly = kind == ManagedIdentifierKind.Type;
        if (_isTypeOnly)
        {
            Reset();
        }
        else
        {
            _contentStart = -1;
            _readIndex = -1;
            _parameterIndex = -1;
        }
    }

    /// <summary>
    /// The raw value of the current token, usually not including whitespace and certain aspects of tokens, like the '`' in arity or exclamation points in generic type parameter references.
    /// </summary>
    public readonly ReadOnlySpan<char> Value
    {
        get
        {
            if (_contentIsInBuffer)
                return _buffer.AsSpan(0, _contentLength);

            return _contentStart < 0 ? Span<char>.Empty : _text.Slice(_contentStart, _contentLength);
        }
    }

    /// <summary>
    /// The arity of a the previous method or type.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current token is not an arity token.</exception>
    public readonly int Arity
    {
        get
        {
            switch (_tokenType)
            {
                case ManagedIdentifierTokenType.Arity:
                    return _methodArity;

                case ManagedIdentifierTokenType.TypeArity:
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                    return int.Parse(Value, NumberStyles.None, CultureInfo.InvariantCulture);
#else
                    return int.Parse(Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
#endif

                default:
                    throw new InvalidOperationException("Not arity token.");
            }
        }
    }

    /// <summary>
    /// The index of the current method parameter, or -1 if the tokenizer isn't in the parameter list.
    /// </summary>
    /// <exception cref="InvalidOperationException">Reading a type.</exception>
    public readonly int ParameterIndex
    {
        get
        {
            if (_isTypeOnly)
                throw new InvalidOperationException("This tokenizer is only reading a type.");

            return _parameterIndex;
        }
    }

    /// <summary>
    /// The index of a generic parameter reference, represented as '!n' for type parameters defined in a type and '!!n' for type parameters defined in a method.
    /// </summary>
    /// <exception cref="InvalidOperationException">The current token is not a generic parameter reference.</exception>
    public readonly int GenericReferenceIndex
    {
        get
        {
            if (_tokenType is not ManagedIdentifierTokenType.TypeGenericParameterReference and not ManagedIdentifierTokenType.MethodGenericParameterReference)
                throw new InvalidOperationException("Not generic parameter reference token.");

            switch (_tokenType)
            {
                case ManagedIdentifierTokenType.TypeGenericParameterReference:
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                    return int.Parse(Value, NumberStyles.None, CultureInfo.InvariantCulture);
#else
                    return int.Parse(Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
#endif

                case ManagedIdentifierTokenType.MethodGenericParameterReference:
                    return _typeParamRefIndex;

                default:
                    throw new InvalidOperationException("Not arity token.");
            }
        }
    }

    /// <summary>
    /// The type of the current token.
    /// </summary>
    public readonly ManagedIdentifierTokenType TokenType
    {
        get
        {
            if (_isTypeOnly && _tokenType == ManagedIdentifierTokenType.OpenParameters)
                return ManagedIdentifierTokenType.Uninitialized;

            return _tokenType;
        }
    }

    /// <summary>
    /// The number of dimensions this array has.
    /// </summary>
    public int ArrayRank => _arrayRank < 0 ? 1 : _arrayRank;

    /// <summary>
    /// If the current array is a 'vector' array, or a non-multidimensional array starting at index zero.
    /// </summary>
    public bool IsSzArray => _tokenType == ManagedIdentifierTokenType.Array && _arrayRank < 0;

    /// <summary>
    /// Recursively checks if the given <paramref name="type"/> is the same as the type this tokenizer is currently on.
    /// </summary>
    /// <remarks>This method will advance the tokenizer until it reaches the next type.</remarks>
    public bool IsSameTypeAs(Type type, StringBuilder? tempStringBuilder = null)
    {
        // keep in sync with ManagedIdentifier.FullyQualifiedTypeNameEscaper
        ReadOnlySpan<char> escapedCharacters = [ '&', '*', '+', ',', '[', '\\', ']' ];

        if (_tokenType is ManagedIdentifierTokenType.NextParameter or ManagedIdentifierTokenType.OpenParameters or ManagedIdentifierTokenType.OpenTypeParameters)
        {
            if (!MoveNext() || _tokenType is not (ManagedIdentifierTokenType.TypeSegment or ManagedIdentifierTokenType.TypeGenericParameterReference or ManagedIdentifierTokenType.MethodGenericParameterReference))
                return false;
        }

        if (type.HasElementType)
        {
            // unwind element types of generic parameters (!!0[]*&, etc)
            Stack<Type> elementTypeStack = StackPool<Type>.Rent();
            try
            {
                for (Type typeWithElement = type; typeWithElement.HasElementType;)
                {
                    elementTypeStack.Push(typeWithElement);

                    Type elementTypeChild = typeWithElement.GetElementType()!;
                    if (elementTypeChild.IsGenericParameter)
                    {
                        if (!CheckGenericParam(elementTypeChild))
                            return false;

                        do
                        {
                            if (_tokenType is not ManagedIdentifierTokenType.Array
                                and not ManagedIdentifierTokenType.Pointer
                                and not ManagedIdentifierTokenType.Reference)
                            {
                                return elementTypeStack.Count == 0;
                            }

                            if (elementTypeStack.Count == 0)
                                return false;

                            typeWithElement = elementTypeStack.Pop();
                            switch (_tokenType)
                            {
                                case ManagedIdentifierTokenType.Array:

                                    if (!typeWithElement.IsArray)
                                        return false;

#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                                    bool isSzArray = typeWithElement.IsSZArray;
#else
                                    bool isSzArray = typeWithElement.GetArrayRank() == 1 && typeWithElement.GetElementType()!.MakeArrayType() == typeWithElement;
#endif
                                    if (isSzArray != IsSzArray || typeWithElement.GetArrayRank() != ArrayRank)
                                        return false;

                                    continue;

                                case ManagedIdentifierTokenType.Pointer:
                                    if (!typeWithElement.IsPointer)
                                        return false;
                                    continue;

                                default: // case ManagedIdentifierTokenType.Reference:
                                    if (!typeWithElement.IsByRef)
                                        return false;
                                    continue;
                            }
                        } while (MoveNext());
                    }

                    typeWithElement = elementTypeChild;
                }
            }
            finally
            {
                StackPool<Type>.Return(elementTypeStack);
            }
        }
        else if (type.IsGenericParameter)
        {
            return CheckGenericParam(type);
        }

        bool isGeneric = type.IsConstructedGenericType;

        Type typeDef = isGeneric ? type.GetGenericTypeDefinition() : type;

        string? typeFullName = typeDef.FullName;
        if (typeFullName == null)
            return false;

        bool isRent = false;
        if (tempStringBuilder == null)
        {
            isRent = true;
            tempStringBuilder = StringBuilderPool.Rent();
        }

        string fullName;
        do
        {
            switch (_tokenType)
            {
                case ManagedIdentifierTokenType.TypeSegment:
                case ManagedIdentifierTokenType.NestedTypeSegment:
                    ReadOnlySpan<char> val = Value;
                    if (tempStringBuilder.Length != 0)
                        tempStringBuilder.Append(_tokenType == ManagedIdentifierTokenType.NestedTypeSegment ? '+' : '.');
                    if (val.IndexOfAny(escapedCharacters) >= 0)
                    {
                        tempStringBuilder.Append(ManagedIdentifier.FullyQualifiedTypeNameEscaper.Escape(val.ToString()));
                    }
                    else
                        AppendSpan(tempStringBuilder, val);
                    continue;

                case ManagedIdentifierTokenType.TypeArity:
                    tempStringBuilder.Append('`').Append(Arity.ToString(CultureInfo.InvariantCulture));
                    continue;

                case ManagedIdentifierTokenType.Array:
                    AppendArraySpecifier(tempStringBuilder);
                    continue;

                case ManagedIdentifierTokenType.Pointer:
                    tempStringBuilder.Append('*');
                    continue;

                case ManagedIdentifierTokenType.Reference:
                    tempStringBuilder.Append('&');
                    continue;

                default:
                    try
                    {
                        fullName = tempStringBuilder.ToString();
                        tempStringBuilder.Clear();
                        if (!string.Equals(fullName, typeFullName))
                            return false;

                        if (_tokenType != ManagedIdentifierTokenType.OpenTypeParameters)
                            return !isGeneric;

                        if (!isGeneric)
                            return false;

                        int startDepth = _typeParamDepth;
                        if (startDepth > (MaxGenericDepth <= 0 ? DefaultMaxGenericDepth : MaxGenericDepth))
                        {
                            return false;
                        }

                        Type[] genericParameters = type.GetGenericArguments();
                        int paramIndex = 0;
                        if (!MoveNext() || _tokenType == ManagedIdentifierTokenType.CloseTypeParameters)
                            return false;

                        while (true)
                        {
                            if (paramIndex >= genericParameters.Length)
                                return false;
                            if (!IsSameTypeAs(genericParameters[paramIndex], tempStringBuilder))
                            {
                                return false;
                            }

                            if (_tokenType == ManagedIdentifierTokenType.NextParameter && !MoveNext())
                            {
                                return false;
                            }

                            ++paramIndex;

                            if (startDepth == _typeParamDepth + 1 && _tokenType == ManagedIdentifierTokenType.CloseTypeParameters)
                            {
                                MoveNext();
                                break;
                            }
                        }

                        return paramIndex == genericParameters.Length;
                    }
                    finally
                    {
                        if (isRent)
                            StringBuilderPool.Return(tempStringBuilder);
                    }
            }
        } while (MoveNext());

        if (isGeneric)
            return false;

        fullName = tempStringBuilder.ToString();
        if (isRent)
            StringBuilderPool.Return(tempStringBuilder);
        else
            tempStringBuilder.Clear();
        return string.Equals(fullName, typeFullName);
    }

    private bool CheckGenericParam(Type type)
    {
        MethodBase? method;
        try
        {
            method = type.DeclaringMethod;
        }
        catch (InvalidOperationException)
        {
            method = null;
        }

        if (method == null)
        {
            if (_tokenType != ManagedIdentifierTokenType.TypeGenericParameterReference
                || GenericReferenceIndex != type.GenericParameterPosition)
            {
                return false;
            }
        }
        else
        {
            if (_tokenType != ManagedIdentifierTokenType.MethodGenericParameterReference
                || _typeParamRefIndex != type.GenericParameterPosition)
            {
                return false;
            }
        }

        MoveNext();
        return true;
    }

    private static unsafe void AppendSpan(StringBuilder builder, ReadOnlySpan<char> span)
    {
        fixed (char* ptr = span)
        {
            builder.Append(ptr, span.Length);
        }
    }

    internal readonly void AppendArraySpecifier(StringBuilder bldr)
    {
        switch (_arrayRank)
        {
            case -1:
                bldr.Append("[]");
                break;
            case 0: break;
            case 1:
                bldr.Append("[*]");
                break;
            default:
                bldr.Append('[').Append(',', _arrayRank - 1).Append(']');
                break;
        }
    }

    /// <summary>
    /// Advance to the next token, returning <see langword="false"/> if the end of the identifier is reached.
    /// </summary>
    /// <exception cref="FormatException">The identifier is malformed in some way.</exception>
    [SkipLocalsInit]
    public bool MoveNext()
    {
        if (_tokenType == ManagedIdentifierTokenType.Uninitialized)
        {
            _readIndex = 0;
            SkipWhitespace();
            _tokenType = ManagedIdentifierTokenType.MethodImplementationTypeSegment;
        }

        if (_text.Length <= _readIndex)
        {
            // skips switch statement
            _tokenType = ManagedIdentifierTokenType.Uninitialized;
            if (_text.Length == 0)
            {
                throw new FormatException(_isTypeOnly
                    ? "Empty/missing type name must be quoted."
                    : "Empty/missing method name must be quoted."
                );
            }
        }

        int endIndex;
        char next;

        switch (_tokenType)
        {
            // case ManagedMethodTokenType.Uninitialized:
            case ManagedIdentifierTokenType.MethodImplementationTypeSegment:
                bool hasInitialDot = false;
                while (true) // repeats when reading an empty section starting with a dot (.cctor, .ctor)
                {
                    endIndex = ReadMethodSymbolName(true);
                    next = GetChar(endIndex);
                    switch (next)
                    {
                        case EndOfLine:
                        case '(':
                        case '`':
                            if (_contentLength == 0 && _text[_readIndex] != '\'')
                                throw new FormatException($"Empty/missing method name at {_readIndex + 1} must be quoted.");
                            if (hasInitialDot)
                            {
                                --_contentStart;
                                ++_contentLength;
                            }

                            _readIndex = endIndex;
                            SkipWhitespace();
                            _tokenType = ManagedIdentifierTokenType.MethodName;
                            return true;

                        case '.':
                            if (!_contentIsInBuffer && _contentLength == 0)
                            {
                                // support for .cctor, .ctor methods
                                _readIndex = _contentStart + _contentLength + 1;
                                if (_readIndex < _text.Length && !char.IsWhiteSpace(_text[_readIndex]) && _text[_readIndex] is not ('.' or '\'' or '(' or '`' or '<' or ')' or '>' or '[' or ']' or '&' or '*' or ','))
                                {
                                    hasInitialDot = true;
                                    continue;
                                }
                            }
                            _readIndex = endIndex + 1;
                            SkipWhitespace();
                            _tokenType = ManagedIdentifierTokenType.MethodImplementationTypeSegment;
                            return true;

                        default:
                            throw new FormatException($"Unexpected character at {endIndex + 1}: '{next}'.");
                    }
                }

            case ManagedIdentifierTokenType.MethodName:
            case ManagedIdentifierTokenType.Arity:
                switch (_text[_readIndex])
                {
                    case '(':
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        _tokenType = ManagedIdentifierTokenType.OpenParameters;
                        if (!_isTypeOnly)
                            _parameterIndex = 0;
                        SkipWhitespace();
                        return true;

                    case '`' when _text.Length > _readIndex + 1 && _tokenType != ManagedIdentifierTokenType.Arity:
                        _readIndex = ReadArity();
                        SkipWhitespace();
                        _tokenType = ManagedIdentifierTokenType.Arity;
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                        _methodArity = int.Parse(Value, NumberStyles.None, CultureInfo.InvariantCulture);
#else
                        _methodArity = int.Parse(Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
#endif
                        return true;
                }

                break;

            case ManagedIdentifierTokenType.OpenParameters:
            case ManagedIdentifierTokenType.OpenTypeParameters:
            case ManagedIdentifierTokenType.TypeSegment:
            case ManagedIdentifierTokenType.TypeArity:
            case ManagedIdentifierTokenType.NestedTypeSegment:
            case ManagedIdentifierTokenType.CloseTypeParameters:
            case ManagedIdentifierTokenType.NextParameter:
            case ManagedIdentifierTokenType.Array:
            case ManagedIdentifierTokenType.Pointer:
            case ManagedIdentifierTokenType.Reference:
            case ManagedIdentifierTokenType.TypeGenericParameterReference:
            case ManagedIdentifierTokenType.MethodGenericParameterReference:
                ManagedIdentifierTokenType tokenType = ManagedIdentifierTokenType.TypeSegment;
                switch (_text[_readIndex])
                {
                    case '[':
                        if (_tokenType == ManagedIdentifierTokenType.Reference)
                            throw new FormatException($"Array of references at {_readIndex + 1} is not supported.");
                        if (_tokenType == ManagedIdentifierTokenType.OpenParameters)
                            throw new FormatException($"Array at {_readIndex + 1} missing element type.");
                        _readIndex = ReadArraySpecifier();
                        _tokenType = ManagedIdentifierTokenType.Array;
                        SkipWhitespace();
                        return true;

                    case ']':
                        throw new FormatException($"Malformed array specifier at {_readIndex + 1}.");

                    case '*':
                        if (_tokenType == ManagedIdentifierTokenType.Reference)
                            throw new FormatException($"Pointer to a reference at {_readIndex + 1} is not supported.");
                        if (_tokenType == ManagedIdentifierTokenType.OpenParameters)
                            throw new FormatException($"Pointer at {_readIndex + 1} missing element type.");
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        _tokenType = ManagedIdentifierTokenType.Pointer;
                        SkipWhitespace();
                        return true;

                    case '&':
                        if (_tokenType == ManagedIdentifierTokenType.OpenParameters)
                            throw new FormatException($"Reference at {_readIndex + 1} missing element type.");
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        _tokenType = ManagedIdentifierTokenType.Reference;
                        SkipWhitespace();
                        return true;

                    case '(':
                        throw new FormatException($"Unexpected character at {_readIndex + 1}: '('.");

                    case ')' when !_isTypeOnly:
                        if (_tokenType == ManagedIdentifierTokenType.NextParameter)
                            throw new FormatException($"Empty/missing type name at {_readIndex} must be quoted.");
                        _tokenType = ManagedIdentifierTokenType.CloseParameters;
                        _parameterIndex = -1;
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        SkipWhitespace();
                        return true;

                    case '<':
                        // note: we keep nested type segment for the following scenerio:
                        //   System.Collections.Generic.List`1+Enumerator<System.String>
                        if (_tokenType is not (ManagedIdentifierTokenType.NestedTypeSegment
                            or ManagedIdentifierTokenType.TypeArity))
                        {
                            throw new FormatException("Unexpected generic type specifier.");
                        }

                        _tokenType = ManagedIdentifierTokenType.OpenTypeParameters;
                        ++_typeParamDepth;
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        SkipWhitespace();
                        return true;

                    case '>':
                        if (_tokenType == ManagedIdentifierTokenType.OpenTypeParameters)
                            throw new FormatException("Types can not supply an empty type argument list.");
                        if (_tokenType == ManagedIdentifierTokenType.NextParameter)
                            throw new FormatException($"Empty/missing type name at {_readIndex} must be quoted.");
                        _tokenType = ManagedIdentifierTokenType.CloseTypeParameters;
                        --_typeParamDepth;
                        if (_typeParamDepth < 0)
                            throw new FormatException("Unmatched type parameters.");
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        SkipWhitespace();
                        return true;

                    case ',':

                        if (_tokenType is not (ManagedIdentifierTokenType.TypeSegment
                            or ManagedIdentifierTokenType.NestedTypeSegment
                            or ManagedIdentifierTokenType.CloseTypeParameters
                            or ManagedIdentifierTokenType.TypeGenericParameterReference
                            or ManagedIdentifierTokenType.MethodGenericParameterReference
                            or ManagedIdentifierTokenType.Arity
                            or ManagedIdentifierTokenType.Array
                            or ManagedIdentifierTokenType.Pointer
                            or ManagedIdentifierTokenType.Reference))
                        {
                            throw new FormatException("Unexpected parameter separator.");
                        }

                        _tokenType = ManagedIdentifierTokenType.NextParameter;
                        if (_typeParamDepth == 0)
                            ++_parameterIndex;
                        SetContent(_readIndex, 1);
                        ++_readIndex;
                        SkipWhitespace();
                        return true;

                    case '`' when _text.Length > _readIndex + 1 && _tokenType != ManagedIdentifierTokenType.TypeArity:
                        _readIndex = ReadArity();
                        SkipWhitespace();
                        _tokenType = ManagedIdentifierTokenType.TypeArity;
                        return true;

                    case '!' when !_isTypeOnly:
                        int exclCt = 1;
                        for (; exclCt < _text.Length && _text[exclCt + _readIndex] == '!'; ++exclCt) ;
                        if (exclCt is 1 or 2)
                        {
                            int digitCt = 0;
                            for (; digitCt < _text.Length - exclCt && char.IsDigit(_text[digitCt + exclCt + _readIndex]); ++digitCt) ;
                            if (digitCt is > 0 and <= 5 && GetChar(digitCt + exclCt + _readIndex) is EndOfLine or ',' or '[' or '*' or '&' or '>' or ')' )
                            {
                                _tokenType = exclCt == 1
                                    ? ManagedIdentifierTokenType.TypeGenericParameterReference
                                    : ManagedIdentifierTokenType.MethodGenericParameterReference;
                                SetContent(_readIndex + exclCt, digitCt);
                                _readIndex += digitCt + exclCt;
                                if (exclCt == 2)
                                {
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP2_1_OR_GREATER
                                    _typeParamRefIndex = int.Parse(Value, NumberStyles.None, CultureInfo.InvariantCulture);
#else
                                    _typeParamRefIndex = int.Parse(Value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture);
#endif
                                    if (_typeParamRefIndex >= _methodArity)
                                    {
                                        if (_methodArity == 0)
                                            throw new FormatException("Method generic parameter reference supplied for non-generic method.");
                                        throw new FormatException($"Method generic parameter reference index {_typeParamRefIndex} is out of range for a method with arity {_methodArity}.");
                                    }
                                }
                                return true;
                            }

                            throw new FormatException($"Malformed generic parameter reference at {_readIndex + 1}.");
                        }

                        break;

                    case '+':
                        tokenType = ManagedIdentifierTokenType.NestedTypeSegment;
                        goto case '.';

                    case '.':
                        ++_readIndex;
                        SkipWhitespace();
                        break;
                }

                endIndex = ReadTypeSymbolName(true);

                if (_contentLength == 0 && _text[_readIndex] != '\'')
                    throw new FormatException($"Empty/missing type name at {_readIndex + 1} must be quoted.");

                next = GetChar(endIndex);
                switch (next)
                {
                    case EndOfLine:
                    case ')':
                    case '`':
                    case '<':
                    case '>':
                    case '.':
                    case '+':
                    case ',':
                    case '[':
                    case '*':
                    case '&':

                        _tokenType = tokenType;
                        _readIndex = endIndex;
                        SkipWhitespace();
                        return true;

                    default:
                        throw new FormatException($"Unexpected character at {endIndex + 1}: '{next}'.");
                }
        }

        if (_parameterIndex != -1)
            throw new FormatException("Unmatched parameter parenthesis.");
        if (_typeParamDepth != 0)
            throw new FormatException("Unmatched type parameter section.");
        if (_readIndex < _text.Length)
        {
            SkipWhitespace();
            if (_readIndex < _text.Length)
                throw new FormatException("Method specifier not fully consumed.");
        }

        return false;
    }

    private int ReadArraySpecifier()
    {
        int startIndex = _readIndex + 1;
        if (startIndex >= _text.Length)
            throw new FormatException($"Malformed array specifier at {startIndex + 1}.");
        ReadOnlySpan<char> specifier = _text.Slice(startIndex);
        int endIndex = specifier.IndexOf(']');
        if (endIndex < 0)
            throw new FormatException($"Malformed array specifier at {startIndex + 1}.");
        specifier = specifier.Slice(0, endIndex);

        int stars = 0, commas = 0;
        for (int i = 0; i < specifier.Length; ++i)
        {
            char c = specifier[i];
            if (char.IsWhiteSpace(c))
                continue;
            if (c == '*')
            {
                ++stars;
                if (stars > 1)
                    break;
            }
            else if (c == ',')
            {
                ++commas;
                if (commas >= ManagedIdentifier.MaxArrayRank)
                    break;
            }
            else
                throw new FormatException($"Unexpected character in array specifier at {startIndex + i + 1}: '{c}'.");
        }

        // must come before setting _arrayRank
        SetContent(_readIndex, endIndex + 2);

        if (stars == 0 && commas == 0)
        {
            _arrayRank = -1;
        }
        else if (stars > 0)
        {
            if (stars == 1 && commas == 0)
            {
                _arrayRank = 1;
            }
            else
            {
                throw new FormatException($"Invalid array specifier syntax at {_readIndex + 1}: \"{_text.Slice(_readIndex, endIndex + 2).ToString()}\".");
            }
        }
        else
        {
            int arrayRank = commas + 1;
            if (arrayRank > ManagedIdentifier.MaxArrayRank)
                throw new FormatException($"Array specifier at {_readIndex + 1} has too many dimensions.");
            _arrayRank = arrayRank;
        }

        return startIndex + endIndex + 1;
    }

    private void SkipWhitespace()
    {
        SkipWhitespace(ref _readIndex);
    }
    private void SkipWhitespace(ref int index)
    {
        for (; index < _text.Length && char.IsWhiteSpace(_text[index]); ++index) ;
    }
    private void ReverseWhitespace(ref int index)
    {
        for (; index > 0 && char.IsWhiteSpace(_text[index - 1]); --index) ;
    }

    private int ReadArity()
    {
        int numIndex = _readIndex + 1;
        SkipWhitespace(ref numIndex);
        int end = numIndex + 1;
        if (numIndex >= _text.Length || !char.IsDigit(_text[numIndex]))
            throw new FormatException($"Malformed arity specifier at {_readIndex + 1}.");
        for (; end < _text.Length && char.IsDigit(_text[end]); ++end) ;
        if (end > numIndex + 5)
            throw new FormatException($"Malformed arity specifier at {_readIndex + 1}.");
        SetContent(numIndex, end - numIndex);
        return end;
    }

    private int ReadMethodSymbolName(bool apply)
    {
        return ReadStringOrQuotedString(apply, [ '`', '.', '(', '<', '>' ], skipGenerics: true);
    }
    private int ReadTypeSymbolName(bool apply)
    {
        return ReadStringOrQuotedString(apply, [ '`', '.', '(', ')', '+', ',', '[', ']', '*', '&', '<', '>' ], skipGenerics: false);
    }

    private int ReadStringOrQuotedString(bool apply, ReadOnlySpan<char> nextSegmentChars, bool skipGenerics)
    {
        if (_text[_readIndex] == '\'')
        {
            return ReadQuotedString(apply);
        }

        return ReadString(apply, _readIndex, nextSegmentChars, skipGenerics);
    }

    private char GetChar(int index)
    {
        return index >= _text.Length ? EndOfLine : _text[index];
    }

    private int ReadString(bool apply, int offset, ReadOnlySpan<char> breaks, bool skipGenerics)
    {
        int startIndex = offset;
        int genericDepth = 0;
        while (true)
        {
            int index = _text.Slice(offset).IndexOfAny(breaks);
            if (index == -1)
            {
                int end = _text.Length;
                ReverseWhitespace(ref end);
                if (apply)
                    SetContent(startIndex, end - startIndex);
                return _text.Length;
            }

            index += offset;
            if (skipGenerics)
            {
                switch (_text[index])
                {
                    case '<':
                        ++genericDepth;
                        offset = index + 1;
                        continue;
                    case '>':
                        if (genericDepth > 0)
                            --genericDepth;
                        offset = index + 1;
                        continue;

                    case not '(':
                        if (genericDepth > 0)
                        {
                            offset = index + 1;
                            continue;
                        }

                        break;
                }
            }

            int endIndex = index;
            ReverseWhitespace(ref endIndex);
            if (apply)
                SetContent(startIndex, endIndex - startIndex);
            return index;
        }
    }
    
    private int ReadQuotedString(bool apply)
    {
        int firstTextIndex = _readIndex + 1;
        if (firstTextIndex >= _text.Length)
        {
            throw new FormatException($"Unterminated escaped identifier at {firstTextIndex + 1}.");
        }
        if (_text[firstTextIndex] == '\'')
        {
            if (apply)
                SetContent(firstTextIndex, 0);
            int lastIndex = firstTextIndex + 1;
            SkipWhitespace(ref lastIndex);
            return lastIndex;
        }

        ReadOnlySpan<char> quotedSearchChars = [ '\'', '\\' ];

        // 'ab'
        bool hasEscapes = false;
        int searchIndex = firstTextIndex;
        do
        {
            int endQuoteIndex = _text.Slice(searchIndex).IndexOfAny(quotedSearchChars);
            if (endQuoteIndex == -1)
            {
                throw new FormatException($"Unterminated escaped identifier at {firstTextIndex + 1}.");
            }

            endQuoteIndex += searchIndex;
            char c = _text[endQuoteIndex];
            if (c == '\'')
            {
                // note: IndexOfAny wouldve returned the '\' first if it was escaping this quote
                if (apply)
                    SetContent(firstTextIndex, endQuoteIndex - firstTextIndex, hasEscapes);
                int lastIndex = endQuoteIndex + 1;
                SkipWhitespace(ref lastIndex);
                return lastIndex;
            }

            int backslashCount = 1;
            for (int i = endQuoteIndex + 1; i < _text.Length; ++i)
            {
                if (_text[i] == '\\')
                    ++backslashCount;
                else
                    break;
            }

            if (backslashCount % 2 == 1 && _text.Length > endQuoteIndex + backslashCount && _text[endQuoteIndex + backslashCount] == '\'')
            {
                hasEscapes = true;
                searchIndex = endQuoteIndex + backslashCount + 1;
                continue;
            }

            if (backslashCount > 1)
            {
                hasEscapes = true;
                searchIndex = endQuoteIndex + backslashCount;
            }
        } while (searchIndex < _text.Length);

        throw new FormatException($"Unterminated escaped identifier at {firstTextIndex + 1}.");
    }

    private void SetContent(int st, int ln, bool hasEscapes = false)
    {
        if (hasEscapes)
        {
            SetEscapedContent(st, ln);
        }
        else
        {
            _contentStart = st;
            _contentLength = ln;
            _contentIsInBuffer = false;
        }

        _arrayRank = 0;
    }

    private void SetEscapedContent(int st, int ln)
    {
        if (_buffer == null || _buffer.Length < ln)
        {
            _buffer = new char[ln];
        }

        ReadOnlySpan<char> text = _text.Slice(st, ln);

        int searchIndex = 0;
        int writeIndex = 0;
        do
        {
            int escIndex = text.Slice(searchIndex).IndexOf('\\');
            if (escIndex == -1)
            {
                break;
            }

            int amt = escIndex;
            escIndex += searchIndex;
            if (amt > 0)
                text.Slice(searchIndex, amt).CopyTo(_buffer.AsSpan(writeIndex));
            writeIndex += amt;

            int backslashCount = 1;
            for (int i = escIndex + 1; i < text.Length; ++i)
            {
                if (text[i] == '\\')
                    ++backslashCount;
                else
                    break;
            }

            // (n - 1) / 2 + 1 = number of backslashes including extras as single
            int backslashes = (backslashCount - 1) / 2 + 1;
            char escapedChar = '\0';
            int escapedCharIndex = escIndex + backslashCount;
            if (backslashCount % 2 == 1 && text.Length > escapedCharIndex && text[escapedCharIndex] == '\'')
            {
                escapedChar = text[escapedCharIndex];
                --backslashes;
            }

            _buffer.AsSpan(writeIndex, backslashes).Fill('\\');
            writeIndex += backslashes;
            if (escapedChar != '\0')
            {
                _buffer[writeIndex] = escapedChar;
                ++writeIndex;
            }

            searchIndex = escapedCharIndex + (escapedChar != '\0' ? 1 : 0);

        } while (searchIndex < text.Length);

        int finalAmt = text.Length - searchIndex;
        if (finalAmt > 0)
            text.Slice(searchIndex, finalAmt).CopyTo(_buffer.AsSpan(writeIndex));
        _contentIsInBuffer = true;
        _contentStart = 0;
        _contentLength = writeIndex + finalAmt;
    }

    /// <summary>
    /// Resets the tokenizer to the beginning of the string.
    /// </summary>
    public void Reset()
    {
        _contentIsInBuffer = false;
        _contentStart = -1;
        _contentLength = 0;
        _methodArity = 0;
        _typeParamDepth = 0;
        _typeParamRefIndex = 0;
        if (_isTypeOnly)
        {
            _tokenType = ManagedIdentifierTokenType.OpenParameters;
            _readIndex = 0;
            SkipWhitespace();
        }
        else
        {
            _tokenType = ManagedIdentifierTokenType.Uninitialized;
            _readIndex = -1;
        }

        _parameterIndex = -1;
    }

    /// <inheritdoc />
    public override string ToString() => _text.ToString();
}

/// <summary>
/// A token represented by <see cref="ManagedIdentifierTokenizer"/>.
/// </summary>
public enum ManagedIdentifierTokenType
{
    /// <summary>
    /// The tokenizer hasn't started yet. Call <see cref="ManagedIdentifierTokenizer.MoveNext"/> to start it.
    /// </summary>
    Uninitialized = 0,

    /// <summary>
    /// The full name of the method.
    /// </summary>
    MethodName,

    /// <summary>
    /// The number of type arguments of a method.
    /// </summary>
    Arity,

    /// <summary>
    /// The name or namespace segment of a parameter type or parameter type generic argument.
    /// </summary>
    TypeSegment,

    /// <summary>
    /// A nested type of a parameter type or parameter type generic argument (following a '+' symbol).
    /// </summary>
    NestedTypeSegment,

    /// <summary>
    /// The number of type arguments of a type.
    /// </summary>
    TypeArity,

    /// <summary>
    /// A reference to the type parameter of this method's containing type. Denoted as '!n'.
    /// </summary>
    TypeGenericParameterReference,
    
    /// <summary>
    /// A reference to the type parameter of this method. Denoted as '!!n'.
    /// </summary>
    MethodGenericParameterReference,
    
    /// <summary>
    /// Opening the type parameter list, denoted with a '&lt;'.
    /// </summary>
    OpenTypeParameters,

    /// <summary>
    /// Closing the type parameter list, denoted with a '&gt;'.
    /// </summary>
    CloseTypeParameters,

    /// <summary>
    /// Opening the parameter list, denoted with a '('.
    /// </summary>
    OpenParameters,

    /// <summary>
    /// Closing the parameter list, denoted with a ')'.
    /// </summary>
    CloseParameters,

    /// <summary>
    /// Continues to the next parameter or type parameter.
    /// </summary>
    NextParameter,

    /// <summary>
    /// A single dotted segment of the explicitly implemented interface type of this method. Note that nested types are dotted, they don't use '+'. Also generic types use angle brackets.
    /// </summary>
    /// <remarks>Ex: NS.Class.INestedInterface&lt;NS.Struct&gt;.MethodName</remarks>
    MethodImplementationTypeSegment,

    /// <summary>
    /// The previous type was an array.
    /// </summary>
    Array,
    
    /// <summary>
    /// The previous type was a pointer.
    /// </summary>
    Pointer,
    
    /// <summary>
    /// The previous type was a by-reference value.
    /// </summary>
    Reference
}