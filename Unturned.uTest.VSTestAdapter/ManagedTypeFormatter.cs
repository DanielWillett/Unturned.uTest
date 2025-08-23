using System;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace uTest.Adapter;

internal static class ManagedTypeFormatter
{
    private static readonly IdentifierEscaper TypeIdEscaper = new IdentifierEscaper(IdentifierType.Type);
    private static readonly IdentifierEscaper MethodIdEscaper = new IdentifierEscaper(IdentifierType.Method);
    private static readonly IdentifierEscaper NamespaceEscaper = new IdentifierEscaper(IdentifierType.Namespace);

    public static int GetArity(this TypeReference type)
    {
        if (type is IGenericInstance inst)
        {
            if (type.DeclaringType is IGenericInstance declInst)
            {
                return inst.GenericArguments.Count - declInst.GenericArguments.Count;
            }

            return inst.GenericArguments.Count;
        }

        if (!type.HasGenericParameters)
        {
            return type.DeclaringType?.GetArity() ?? 0;
        }

        if (type.DeclaringType == null)
        {
            return type.GenericParameters.Count;
        }

        return type.GenericParameters.Count - type.DeclaringType.GetArity();
    }

    public static int GetArity(this MethodReference method)
    {
        if (method is IGenericInstance inst)
        {
            return inst.GenericArguments.Count;
        }

        return !method.HasGenericParameters ? 0 : method.GenericParameters.Count;
    }

    private enum IdentifierType
    {
        Method,
        Type,
        Namespace
    }

    private class IdentifierEscaper : TextEscaper
    {
        private static readonly char[] NsSplit = [ '.' ];

        private readonly IdentifierType _type;

        public IdentifierEscaper(IdentifierType type) : base('\'', '\\')
        {
            _type = type;
        }


        public string Escape(string value, int expectedArity)
        {
            if (_type == IdentifierType.Namespace)
            {
                expectedArity = -1;
            }

            string esc = base.Escape(value);

            bool needsEscaping = NeedsEscaping(esc, esc.Length - 1, 0, expectedArity);

            if (!needsEscaping)
            {
                if (_type != IdentifierType.Namespace || esc.IndexOf('`') == -1)
                    return esc;
            }
            else if (_type != IdentifierType.Namespace)
                return $"'{esc}'";

            string[] namespaces = esc.Split(NsSplit, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < namespaces.Length; ++i)
            {
                ref string ns = ref namespaces[i];
                if (NeedsEscaping(ns, ns.Length - 1, 0, expectedArity))
                {
                    ns = $"'{ns}'";
                }
            }

            return string.Join(".", namespaces);
        }
        public override string Escape(string value)
        {
            return Escape(value, -1);
        }

        private bool NeedsEscaping(string esc, int end, int start, int expectedArity = -1)
        {
            bool isDigit = true;
            
            if (start < esc.Length && char.GetUnicodeCategory(esc[start]) == UnicodeCategory.DecimalDigitNumber)
            {
                return true;
            }

            for (int i = end; i >= start; --i)
            {
                char c = esc[i];

                // handles false arity
                if (isDigit)
                {
                    if (c == '`' && i != esc.Length - 1)
                    {
                        if (int.TryParse(esc.Substring(i + 1), NumberStyles.Number, CultureInfo.InvariantCulture, out int arity))
                        {
                            if (expectedArity == -1 || arity != expectedArity)
                                return true;

                            continue;
                        }
                    }

                    if (!char.IsDigit(c))
                        isDigit = false;
                    else continue;
                }

                // invalid characters and whitespace
                UnicodeCategory category = char.GetUnicodeCategory(c);
                if (c == '.' && _type == IdentifierType.Namespace)
                    continue;
                switch (category)
                {
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                    case UnicodeCategory.TitlecaseLetter:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                    case UnicodeCategory.DecimalDigitNumber:
                    case UnicodeCategory.LetterNumber:
                    case UnicodeCategory.NonSpacingMark:
                    case UnicodeCategory.SpacingCombiningMark:
                    case UnicodeCategory.ConnectorPunctuation:
                    case UnicodeCategory.Format:
                        break;
                    default:
                        return true;
                }
            }

            return false;
        }
    }

    internal static string GetManagedType(TypeReference type)
    {
        StringBuilder sb = new StringBuilder();
        GetTypeMetadataName(type, sb);
        return sb.ToString();
    }
    
    internal static string GetManagedMethod(MethodReference method)
    {
        StringBuilder sb = new StringBuilder();
        GetMethodMetadataName(method, sb);
        return sb.ToString();
    }
    
    private static void GetMethodMetadataName(MethodReference method, StringBuilder bldr)
    {
        bldr.Append(MethodIdEscaper.Escape(method.Name, method.GetArity()));

        if (!method.HasParameters)
            return;

        bldr.Append('(');

        bool needsComma = false;

        foreach (ParameterDefinition parameter in method.Parameters)
        {
            if (needsComma)
            {
                bldr.Append(',');
            }
            else
            {
                needsComma = true;
            }

            TypeReference type = parameter.ParameterType;
            GetTypeMetadataName(type, bldr, parameterProvider: method);
        }

        bldr.Append(')');
    }

    private static void GetTypeMetadataName(TypeReference type, StringBuilder bldr, bool nested = false, IGenericParameterProvider? parameterProvider = null)
    {
        TypeReference elementType = type;
        while (true)
        {
            if (elementType is TypeSpecification ts && (ts.IsArray || ts.IsPointer || ts.IsByReference))
                elementType = ts.ElementType;
            else
                break;
        }

        if (type.IsGenericParameter && parameterProvider != null)
        {
            if (parameterProvider.HasGenericParameters)
            {
                int index = parameterProvider.GenericParameters.FirstOrDefault(x => x == type)?.Position ?? -1;
                if (index >= 0)
                {
                    bldr.Append(parameterProvider.GenericParameterType == GenericParameterType.Method
                        ? "!!"
                        : "!"
                    ).Append(index.ToString(CultureInfo.InvariantCulture));
                    goto nameAppended;
                }
            }

            TypeReference? declaringType = parameterProvider.GenericParameterType == GenericParameterType.Method
                ? ((MethodReference)parameterProvider).DeclaringType
                : null;
            if (declaringType is { HasGenericParameters: true })
            {
                int index = declaringType.GenericParameters.FirstOrDefault(x => x == type)?.Position ?? -1;
                if (index >= 0)
                {
                    bldr.Append('!').Append(index.ToString(CultureInfo.InvariantCulture));
                    goto nameAppended;
                }
            }
        }

        if (!nested && !string.IsNullOrEmpty(elementType.Namespace))
        {
            bldr.Append(NamespaceEscaper.Escape(elementType.Namespace))
                .Append('.');
        }

        if (elementType.IsNested)
        {
            GetTypeMetadataName(elementType.DeclaringType, bldr, nested: true, parameterProvider: parameterProvider);
            bldr.Append('+');
        }

        bldr.Append(TypeIdEscaper.Escape(elementType.Name, elementType.GetArity()));
        nameAppended:
        if (nested)
            return;

        if (elementType is IGenericInstance instance)
        {
            bldr.Append('<');

            int ct = 0;
            // nested types
            if (elementType.IsNested)
            {
                Stack<IGenericInstance> stack = new Stack<IGenericInstance>(4);
                for (TypeReference c = elementType.DeclaringType; c != null; c = c.DeclaringType)
                {
                    if (c is IGenericInstance { HasGenericArguments: true } i)
                        stack.Push(i);
                }

                while (stack.Count > 0)
                {
                    IGenericInstance nestedType = stack.Pop();

                    Collection<TypeReference> nestedTypeArgs = nestedType.GenericArguments;

                    foreach (TypeReference s in nestedTypeArgs)
                    {
                        if (ct != 0)
                            bldr.Append(',');

                        GetTypeMetadataName(s, bldr, parameterProvider: parameterProvider);
                        ++ct;
                    }
                }
            }

            if (instance.HasGenericArguments)
            {
                Collection<TypeReference>? typeArgs = instance.GenericArguments;

                foreach (TypeReference s in typeArgs)
                {
                    if (ct != 0)
                        bldr.Append(',');

                    GetTypeMetadataName(s, bldr, parameterProvider: parameterProvider);
                    ++ct;
                }
            }

            bldr.Append('>');
        }

        AppendElementType(type, bldr);
    }

    private static void AppendElementType(TypeReference elementType, StringBuilder bldr)
    {
        if (elementType is not TypeSpecification ts)
            return;

        if (ts.IsArray)
        {
            AppendElementType(ts.ElementType, bldr);

            ArrayType? at = ts as ArrayType;
            int rank = at?.Rank ?? 1;
            switch (rank)
            {
                case 0:
                    break;

                case 1:
                    if (at == null || at.IsVector)
                        bldr.Append("[]");
                    else
                        bldr.Append("[*]");
                    break;

                default:
                    bldr.Append('[').Append(',', at!.Rank - 1).Append(']');
                    break;
            }
        }
        else if (ts.IsPointer)
        {
            AppendElementType(ts.ElementType, bldr);

            bldr.Append('*');
        }
        else if (ts.IsByReference)
        {
            AppendElementType(ts.ElementType, bldr);

            bldr.Append('&');
        }
    }
}
