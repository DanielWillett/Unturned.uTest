using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace uTest.Util;

internal sealed class EquatableObjectList : IEquatable<EquatableObjectList?>
{
    private readonly object?[] _objects;

    public object?[] UnderlyingList => _objects;

    public EquatableObjectList(in TypedConstant value, bool distinct = false)
    {
        // if Attribute(params object[] value):
        //  Attribute(null) is parsed as a null array instead of [ null ].
        if (value.IsNull)
        {
            _objects = new object[1];
            return;
        }

        ImmutableArray<TypedConstant> objArgs = value.Values;
        if (objArgs.IsDefaultOrEmpty)
        {
            _objects = Array.Empty<object>();
            return;
        }

        object?[] args = new object?[objArgs.Length];
        for (int i = 0; i < args.Length; ++i)
        {
            TypedConstant element = objArgs[i];
            switch (element.Kind)
            {
                case TypedConstantKind.Array:
                    args[i] = new EquatableObjectList(in element, false);
                    break;
                
                case TypedConstantKind.Type:
                    args[i] = new EquatableTypeContainer(((ITypeSymbol)element.Value!).ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat));
                    break;

                case TypedConstantKind.Enum:
                    string qualifiedTypeName = element.Type?.ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat) ?? string.Empty;
                    IFieldSymbol? field = element.Type!.GetEnumMember(element.Value);
                    args[i] = new EquatableEnumValueContainer(
                        qualifiedTypeName,
                        field is not { CanBeReferencedByName: true } ? null : field.Name,
                        element.Value!
                    );

                    break;

                default:
                    args[i] = element.Value;
                    break;
            }
        }

        if (distinct)
        {
            args = Distict(args);
        }

        _objects = args;
    }

    private static object?[] Distict(object?[] args)
    {
        if (args.Length < 2)
            return args;

        LightweightBitArray bitMask = new LightweightBitArray(args.Length);
        int outCt = 1;
        for (int i = 1; i < args.Length; ++i)
        {
            bool isDistinct = true;
            for (int j = i - 1; j >= 0; --j)
            {
                if (Equals(args[i], args[j]))
                {
                    isDistinct = false;
                    break;
                }
            }

            if (isDistinct)
            {
                bitMask[i] = true;
                ++outCt;
            }
        }

        if (outCt == args.Length)
            return args;

        object?[] newArgs = new object?[outCt];
        outCt = 0;
        newArgs[0] = args[0];
        for (int i = 1; i < args.Length; ++i)
        {
            if (bitMask[i])
                newArgs[++outCt] = args[i];
        }

        return newArgs;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is EquatableObjectList l && Equals(l);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        int hashCode = 0;
        foreach (object? obj in _objects)
        {
            if (obj != null)
                hashCode ^= obj.GetHashCode() * 397;
        }

        hashCode += _objects.Length;
        return hashCode;
    }

    /// <inheritdoc />
    public bool Equals(EquatableObjectList? other)
    {
        if (ReferenceEquals(this, other))
            return true;

        if (other == null)
            return false;

        if (other._objects.Length != _objects.Length)
            return false;

        for (int i = 0; i < _objects.Length; ++i)
        {
            object? obj1 = _objects[i];
            object? obj2 = other._objects[i];
            if (ReferenceEquals(obj1, obj2))
                continue;

            if (obj2 == null || obj1 == null)
                return false;

            if (!obj1.Equals(obj2))
                return false;
        }

        return true;
    }

    public struct ObjectArrayType
    {
        public SpecialType TargetType { get; }
        public string? TargetEnumType { get; }

        public ObjectArrayType(SpecialType type)
        {
            TargetType = type == SpecialType.System_Enum ? SpecialType.None : type;
        }

        public ObjectArrayType(string enumType)
        {
            TargetType = SpecialType.System_Enum;
            TargetEnumType = enumType;
        }
    }

    public CodeSegment<ToCodeStringState> ToCodeString(ObjectArrayType targetType, IList<ObjectArrayType>? parameterTypes)
    {
        ToCodeStringState state = default;
        state.Array = UnderlyingList;
        state.Type = targetType.TargetType;
        state.EnumName = targetType.TargetEnumType;
        state.ParameterTypes = parameterTypes;

        return new CodeSegment<ToCodeStringState>(BuildArray, state);
    }

    public struct ToCodeStringState
    {
        internal Array? Array;
        internal SpecialType Type;
        internal string? EnumName;
        internal IList<ObjectArrayType>? ParameterTypes;
    }

    private static void BuildArray(ref ToCodeStringState state, StringBuilder bldr)
    {
        Array? arr = state.Array;

        if (arr == null)
        {
            bldr.Append("null");
            return;
        }

        string keyword = state.Type.GetTypeKeyword() ?? "object";
        if (state.Type == SpecialType.System_TypedReference)
            keyword = "global::System.Type";
        else if (state.Type == SpecialType.System_Enum)
            keyword = state.EnumName!;

        if (arr.Length == 0)
        {
            bldr.Append("global::System.Array.Empty<").Append(keyword).Append(">()");
            return;
        }

        bool canUseSpecialType = true;
        switch (state.Type)
        {
            case SpecialType.None:
            case SpecialType.System_Object:
                canUseSpecialType = false;
                break;

            case SpecialType.System_Enum:
                for (int i = 0; i < arr.Length; ++i)
                {
                    object? element = arr.GetValue(i);
                    if (element is EquatableEnumValueContainer c && string.Equals(c.QualifiedTypeName, keyword, StringComparison.Ordinal))
                        continue;

                    canUseSpecialType = false;
                    break;
                }

                break;

            case SpecialType.System_TypedReference: // Type
                for (int i = 0; i < arr.Length; ++i)
                {
                    object? element = arr.GetValue(i);
                    if (element is EquatableTypeContainer)
                        continue;

                    canUseSpecialType = false;
                    break;
                }

                break;

            default:
                for (int i = 0; i < arr.Length; ++i)
                {
                    object? element = arr.GetValue(i);
                    if (CanBeConvertedTo(element?.GetType(), state.Type, element))
                        continue;

                    canUseSpecialType = false;
                    break;
                }

                break;
        }

        SpecialType arrayType;
        if (canUseSpecialType)
        {
            arrayType = state.Type;
            bldr.Append("new ").Append(keyword).Append("[] { ");
        }
        else
        {
            arrayType = SpecialType.System_Object;
            bldr.Append("new object[] { ");
        }

        ObjectArrayType type = arrayType == SpecialType.System_Enum
            ? new ObjectArrayType(state.EnumName!)
            : new ObjectArrayType(arrayType);

        for (int i = 0; i < arr.Length; ++i)
        {
            object? element = arr.GetValue(i);
            if (i != 0)
                bldr.Append(", ");
            
            if (state.ParameterTypes != null && state.ParameterTypes.Count > i)
            {
                ObjectArrayType paramType = state.ParameterTypes[i];
                AppendLiteral(element, bldr, in paramType);
            }
            else
            {
                AppendLiteral(element, bldr, in type);
            }
        }

        bldr.Append(" }");
    }

    internal static CodeSegment<LiteralState> AppendLiteral(object element, in ObjectArrayType expectedValueType)
    {
        LiteralState state = default;
        state.Object = element;
        state.Type = expectedValueType;

        return new CodeSegment<LiteralState>((ref state, bldr) =>
        {
            AppendLiteral(state.Object, bldr, in state.Type);
        }, state);
    }

    internal struct LiteralState
    {
        public object Object;
        public ObjectArrayType Type;
    }

    internal static void AppendLiteral(object element, StringBuilder bldr, in ObjectArrayType expectedValueType)
    {
        switch (element)
        {
            case null:
                bldr.Append("null");
                break;

            case string str:
                if (expectedValueType.TargetType == SpecialType.System_Enum)
                {
                    if (str.Length == 0)
                        bldr.Append('(').Append(expectedValueType.TargetEnumType).Append(')').Append('0');
                    else
                        bldr.Append(expectedValueType.TargetEnumType).Append(".@").Append(str);
                }
                else if (str.Length == 0)
                    bldr.Append("string.Empty");
                else if (expectedValueType.TargetType == SpecialType.System_Char)
                    bldr.Append('\'').Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(str)).Append('\'');
                else
                    bldr.Append('"').Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(str)).Append('"');
                break;

            case char c:
                if (expectedValueType.TargetType == SpecialType.System_Enum)
                    bldr.Append(expectedValueType.TargetEnumType).Append(".@").Append(c);
                else if (expectedValueType.TargetType == SpecialType.System_String)
                    bldr.Append('"').Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(new string(c, 1))).Append('"');
                else
                    bldr.Append("(char)").Append(((ushort)c).ToString(null, CultureInfo.InvariantCulture));
                break;

            case byte bt:
                AppendValue(bt, "byte", SpecialType.System_Byte, false, x => x == 0, in expectedValueType, bldr);
                break;

            case sbyte sbt:
                AppendValue(sbt, "sbyte", SpecialType.System_SByte, true, x => x == 0, in expectedValueType, bldr);
                break;

            case ushort u:
                AppendValue(u, "ushort", SpecialType.System_UInt16, false, x => x == 0, in expectedValueType, bldr);
                break;

            case short s:
                AppendValue(s, "short", SpecialType.System_Int16, true, x => x == 0, in expectedValueType, bldr);
                break;

            case uint u:
                AppendValue(u, "uint", SpecialType.System_UInt32, false, x => x == 0, in expectedValueType, bldr);
                break;

            case int s:
                AppendValue(s, "int", SpecialType.System_Int32, true, x => x == 0, in expectedValueType, bldr);
                break;

            case ulong u:
                AppendValue(u, "ulong", SpecialType.System_UInt64, false, x => x == 0, in expectedValueType, bldr);
                break;

            case long s:
                AppendValue(s, "long", SpecialType.System_Int64, true, x => x == 0, in expectedValueType, bldr);
                break;

            case float fl:
                AppendFloat(fl, "float", SpecialType.System_Single, x => x == 0, x => Math.Round(x), in expectedValueType, bldr);
                break;

            case double db:
                AppendFloat(db, "double", SpecialType.System_Double, x => x == 0, Math.Round, in expectedValueType, bldr);
                break;

            case UIntPtr u:
                switch (expectedValueType.TargetType)
                {
                    case SpecialType.System_Object:
                    case SpecialType.System_UIntPtr:
                        bldr.Append("new global::System.UIntPtr(")
                            .Append(u.ToUInt64().ToString(null, CultureInfo.InvariantCulture)).Append(')');
                        break;

                    case SpecialType.System_IntPtr:
                        bldr.Append("new global::System.IntPtr(")
                            .Append(u.ToUInt64().ToString(null, CultureInfo.InvariantCulture)).Append(')');
                        break;

                    default:
                        AppendValue(u.ToUInt64(), "ulong", SpecialType.System_UInt64, false, x => x == 0, in expectedValueType, bldr);
                        break;
                }
                break;

            case IntPtr s:
                switch (expectedValueType.TargetType)
                {
                    case SpecialType.System_Object:
                    case SpecialType.System_IntPtr:
                        bldr.Append("new global::System.IntPtr(")
                            .Append(s.ToInt64().ToString(null, CultureInfo.InvariantCulture)).Append(')');
                        break;

                    case SpecialType.System_UIntPtr:
                        bldr.Append("new global::System.UIntPtr(")
                            .Append(s.ToInt64().ToString(null, CultureInfo.InvariantCulture)).Append(')');
                        break;

                    default:
                        AppendValue(s.ToInt64(), "long", SpecialType.System_Int64, true, x => x == 0, in expectedValueType, bldr);
                        break;
                }
                break;

            case decimal db:
                AppendDecimal(db, in expectedValueType, bldr);
                break;

            case Array array:
                ToCodeStringState state2 = default;
                state2.Array = array;
                state2.Type = SpecialType.System_Object;
                BuildArray(ref state2, bldr);
                break;

            case EquatableTypeContainer type:
                if (expectedValueType.TargetType == SpecialType.System_String)
                {
                    string tn = type.QualifiedTypeName;
                    if (tn.StartsWith("global::", StringComparison.Ordinal) && tn.Length > 8)
                        tn = tn.Substring(8);
                    bldr.Append('"').Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(tn)).Append('"');
                }
                else
                    bldr.Append("typeof(").Append(type.QualifiedTypeName).Append(')');
                break;

            case EquatableEnumValueContainer enumValue:
                if (expectedValueType.TargetType == SpecialType.System_String)
                {
                    bldr.Append('"');
                    if (enumValue.ValueName != null)
                    {
                        bldr.Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(enumValue.ValueName!));
                    }
                    else
                    {
                        switch (enumValue.UnqualifiedValue)
                        {
                            case IFormattable fmt:
                                bldr.Append(fmt.ToString(null, CultureInfo.InvariantCulture));
                                break;

                            case null:
                                bldr.Append("null");
                                break;

                            default:
                                bldr.Append(enumValue.UnqualifiedValue);
                                break;
                        }
                    }
                    bldr.Append('"');
                }
                else if (enumValue.ValueName != null)
                {
                    bldr.Append(enumValue.QualifiedTypeName).Append(".@").Append(enumValue.ValueName);
                }
                else
                {
                    bldr.Append('(').Append(enumValue.QualifiedTypeName).Append(")(");
                    AppendLiteral(enumValue.UnqualifiedValue!, bldr, new ObjectArrayType(SpecialType.System_Object));
                    bldr.Append(')');
                }

                break;

            default:
                throw new InvalidOperationException($"Unexpected value type: {element.GetType()}.");
        }
    }

    private static void AppendValue<T>(T value, string keyword, SpecialType self, bool canBeNegative, Func<T, bool> isZero, in ObjectArrayType expectedValueType, StringBuilder bldr) where T : IFormattable
    {
        if (self == expectedValueType.TargetType)
        {
            bldr.Append(value.ToString(null, CultureInfo.InvariantCulture));
            return;
        }

        switch (expectedValueType.TargetType)
        {
            case SpecialType.System_Object:
                bldr.Append('(').Append(keyword).Append(')');
                Append(canBeNegative, value, bldr);
                break;

            case SpecialType.System_Enum:
                bldr.Append("checked ( (")
                    .Append(expectedValueType.TargetType.GetTypeKeyword())
                    .Append(')');
                Append(canBeNegative, value, bldr);
                bldr.Append(" )");
                break;

            case SpecialType.System_Boolean:
                bldr.Append(isZero(value) ? "false" : "true");
                return;

            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                bldr.Append("checked ( (")
                    .Append(expectedValueType.TargetType.GetTypeKeyword())
                    .Append(')');
                Append(canBeNegative, value, bldr);
                bldr.Append(" )");
                return;

            case SpecialType.System_Decimal:
                bldr.Append("new decimal((double)");
                Append(canBeNegative, value, bldr);
                bldr.Append("))");
                break;

            case SpecialType.System_Single:
            case SpecialType.System_Double:
                bldr.Append('(').Append(expectedValueType.TargetType.GetTypeKeyword()).Append(')');
                Append(canBeNegative, value, bldr);
                break;

            case SpecialType.System_String:
                bldr.Append('"')
                    .Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(value.ToString(null, CultureInfo.InvariantCulture)))
                    .Append('"');
                break;

            case SpecialType.System_IntPtr:
                bldr.Append("new global::System.IntPtr(")
                    .Append(value.ToString(null, CultureInfo.InvariantCulture))
                    .Append(" )");
                break;

            case SpecialType.System_UIntPtr:
                bldr.Append("new global::System.UIntPtr(")
                    .Append(value.ToString(null, CultureInfo.InvariantCulture))
                    .Append(" )");
                break;

            default:
                bldr.Append(value.ToString(null, CultureInfo.InvariantCulture));
                break;
        }

        return;

        static void Append(bool canBeNegative, T value, StringBuilder bldr)
        {
            if (canBeNegative)
                bldr.Append('(');
            bldr.Append(value.ToString(null, CultureInfo.InvariantCulture));
            if (canBeNegative)
                bldr.Append(')');
        }
    }

    private static void AppendFloat<T>(T value, string keyword, SpecialType self, Func<T, bool> isZero, Func<T, double> round, in ObjectArrayType expectedValueType, StringBuilder bldr) where T : IFormattable
    {
        char suffix = self switch
        {
            SpecialType.System_Single => 'f',
            SpecialType.System_Decimal => 'm',
            _ => 'd'
        };

        if (self == expectedValueType.TargetType)
        {
            Append(value, bldr, suffix);
            bldr.Append(value.ToString("F", CultureInfo.InvariantCulture)).Append(suffix);
            return;
        }

        switch (expectedValueType.TargetType)
        {
            case SpecialType.System_Object:
                Append(value, bldr, suffix, keyword);
                break;

            case SpecialType.System_Enum:
                bldr.Append("checked ( (").Append(expectedValueType.TargetEnumType).Append(')');
                AppendRounded(value, bldr, round);
                bldr.Append(" )");
                break;

            case SpecialType.System_Boolean:
                bldr.Append(isZero(value) ? "false" : "true");
                break;

            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                bldr.Append("checked ( (")
                    .Append(expectedValueType.TargetType.GetTypeKeyword())
                    .Append(')');
                AppendRounded(value, bldr, round);
                bldr.Append(" )");
                break;

            case SpecialType.System_Decimal:
                bldr.Append("new decimal(");
                Append(value, bldr, suffix);
                bldr.Append(")");
                break;

            case SpecialType.System_Single:
            case SpecialType.System_Double:
                Append(value, bldr, expectedValueType.TargetType == SpecialType.System_Single ? 'f' : 'd');
                break;

            case SpecialType.System_String:
                bldr.Append('"')
                    .Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(value.ToString(null, CultureInfo.InvariantCulture)))
                    .Append('"');
                break;

            case SpecialType.System_IntPtr:
                bldr.Append("new global::System.IntPtr(");
                AppendRounded(value, bldr, round);
                bldr.Append(" )");
                break;

            case SpecialType.System_UIntPtr:
                bldr.Append("new global::System.UIntPtr(");
                AppendRounded(value, bldr, round);
                bldr.Append(" )");
                break;

            default:
                bldr.Append(value.ToString(null, CultureInfo.InvariantCulture));
                break;
        }

        return;

        static void AppendRounded(T value, StringBuilder bldr, Func<T, double> round)
        {
            string? getSpecialName = GetSpecialName(value);
            if (getSpecialName != null)
            {
                bldr.Append('0');
                return;
            }

            double rounded = round(value);
            bldr.Append('(').Append(rounded.ToString("F0", CultureInfo.InvariantCulture)).Append(')');
        }

        static void Append(T value, StringBuilder bldr, char suffix, string? keyword = null)
        {
            string? getSpecialName = GetSpecialName(value);
            if (getSpecialName != null)
            {
                bldr.Append(getSpecialName);
                return;
            }

            if (keyword != null)
            {
                bldr.Append('(').Append(keyword).Append(')');
            }

            bldr.Append('(').Append(value.ToString("F", CultureInfo.InvariantCulture)).Append(suffix).Append(')');
        }

        static string? GetSpecialName(T value)
        {
            if (typeof(T) == typeof(float))
            {
                float v = Unsafe.As<T, float>(ref value);
                if (float.IsNaN(v))
                {
                    return  "float.NaN";
                }
                if (float.IsPositiveInfinity(v))
                {
                    return "float.PositiveInfinity";
                }
                if (float.IsNegativeInfinity(v))
                {
                    return "float.NegativeInfinity";
                }
            }
            else if (typeof(T) == typeof(double))
            {
                double v = Unsafe.As<T, double>(ref value);
                if (double.IsNaN(v))
                {
                    return "double.NaN";
                }
                if (double.IsPositiveInfinity(v))
                {
                    return "double.PositiveInfinity";
                }
                if (double.IsNegativeInfinity(v))
                {
                    return "double.NegativeInfinity";
                }
            }

            return null;
        }
    }

    private static void AppendDecimal(decimal value, in ObjectArrayType expectedValueType, StringBuilder bldr)
    {
        switch (expectedValueType.TargetType)
        {
            case SpecialType.System_Decimal:
            case SpecialType.System_Object:
                bldr.Append(value.ToString("F", CultureInfo.InvariantCulture)).Append('m');
                break;

            case SpecialType.System_Enum:
                bldr.Append("checked ( (").Append(expectedValueType.TargetEnumType).Append(')');
                AppendRounded(value, bldr);
                bldr.Append(" )");
                break;

            case SpecialType.System_Boolean:
                bldr.Append(value == decimal.Zero ? "false" : "true");
                break;

            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
                bldr.Append("checked ( (")
                    .Append(expectedValueType.TargetType.GetTypeKeyword())
                    .Append(')');
                AppendRounded(value, bldr);
                bldr.Append(" )");
                break;

            case SpecialType.System_Single:
            case SpecialType.System_Double:
                Append(value, bldr, expectedValueType.TargetType == SpecialType.System_Single ? 'f' : 'd');
                break;

            case SpecialType.System_String:
                bldr.Append('"')
                    .Append(UnturnedTestGenerator.StringLiteralEscaper.Escape(value.ToString(null, CultureInfo.InvariantCulture)))
                    .Append('"');
                break;

            case SpecialType.System_IntPtr:
                bldr.Append("new global::System.IntPtr(");
                AppendRounded(value, bldr);
                bldr.Append(" )");
                break;

            case SpecialType.System_UIntPtr:
                bldr.Append("new global::System.UIntPtr(");
                AppendRounded(value, bldr);
                bldr.Append(" )");
                break;

            default:
                bldr.Append(value.ToString(null, CultureInfo.InvariantCulture));
                break;
        }

        return;

        static void AppendRounded(decimal value, StringBuilder bldr)
        {
            decimal rounded = decimal.Round(value);
            bldr.Append('(').Append(rounded.ToString("F0", CultureInfo.InvariantCulture)).Append(')');
        }

        static void Append(decimal value, StringBuilder bldr, char suffix, string? keyword = null)
        {
            if (keyword != null)
            {
                bldr.Append('(').Append(keyword).Append(')');
            }

            bldr.Append('(').Append(value.ToString("F", CultureInfo.InvariantCulture)).Append(suffix).Append(')');
        }
    }

    private static bool CanBeConvertedTo(Type? type, SpecialType expectedType, object? value)
    {
        if (expectedType == SpecialType.None)
            return false;

        switch (expectedType)
        {
            case SpecialType.System_Object:
                return true;

            case SpecialType.System_ValueType:
                return type is { IsValueType: true };

            case SpecialType.System_Boolean:
                return type == typeof(bool);

            case SpecialType.System_Char:
                return type == typeof(char);

            case SpecialType.System_SByte:
                return type == typeof(sbyte) || Within(value, sbyte.MinValue, (ulong)sbyte.MaxValue);

            case SpecialType.System_Byte:
                return type == typeof(byte) || Within(value, 0, byte.MaxValue);

            case SpecialType.System_Int16:
                return type == typeof(short) || Within(value, short.MinValue, (ulong)short.MaxValue);

            case SpecialType.System_UInt16:
                return type == typeof(ushort) || Within(value, 0, ushort.MaxValue);

            case SpecialType.System_Int32:
                return type == typeof(int) || Within(value, int.MinValue, int.MaxValue);

            case SpecialType.System_UInt32:
                return type == typeof(uint) || Within(value, 0, uint.MaxValue);

            case SpecialType.System_Int64:
                return type == typeof(long) || Within(value, long.MinValue, long.MaxValue);

            case SpecialType.System_UInt64:
                return type == typeof(ulong) || Within(value, 0, ulong.MaxValue);

            case SpecialType.System_IntPtr:
                return type == typeof(IntPtr) || Within(value,
                    IntPtr.Size == 4 ? int.MinValue : long.MinValue,
                    IntPtr.Size == 4 ? int.MaxValue : (ulong)long.MaxValue
                );

            case SpecialType.System_UIntPtr:
                return type == typeof(UIntPtr) || Within(value, 0, UIntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue);

            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
                return Numeric(type);

            case SpecialType.System_String:
                return type == typeof(string);

            default:
                return false;
        }

        bool Numeric(Type? type)
        {
            if (type == null)
                return false;

            return type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort) || type == typeof(short) ||
                   type == typeof(uint) || type == typeof(int) || type == typeof(ulong) || type == typeof(long) ||
                   type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
                   type == typeof(IntPtr) || type == typeof(UIntPtr);
        }

        bool Within(object? value, long min, ulong max)
        {
            return value switch
            {
                byte u => u <= max,
                sbyte s => s >= min && (s < 0 || (ulong)s <= max),
                ushort u => u <= max,
                short s => s >= min && (s < 0 || (ulong)s <= max),
                uint u => u <= max,
                int s => s >= min && (s < 0 || (ulong)s <= max),
                ulong u => u <= max,
                long s => s >= min && (s < 0 || (ulong)s <= max),
                _ => false
            };
        }
    }

}

public record EquatableTypeContainer(
    string QualifiedTypeName
);
public record EquatableEnumValueContainer(
    string QualifiedTypeName,
    string? ValueName,
    object UnqualifiedValue
);