using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;

namespace uTest.Util;

internal static class ManagedTypeFormatter
{
    private static readonly IdentifierEscaper TypeIdEscaper = new IdentifierEscaper(IdentifierType.Type);
    private static readonly IdentifierEscaper MethodIdEscaper = new IdentifierEscaper(IdentifierType.Method);
    private static readonly IdentifierEscaper NamespaceEscaper = new IdentifierEscaper(IdentifierType.Namespace);

    public static int GetArity(this ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol inst)
            return 0;

        if (type.ContainingType is { IsGenericType: true })
        {
            return inst.Arity - type.ContainingType.Arity;
        }

        return inst.Arity;

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

    internal static string GetManagedType(ITypeSymbol type)
    {
        StringBuilder sb = new StringBuilder();
        GetTypeMetadataName(type, sb);
        return sb.ToString();
    }
    
    internal static string GetManagedMethod(IMethodSymbol method)
    {
        StringBuilder sb = new StringBuilder();
        GetMethodMetadataName(method, sb);
        return sb.ToString();
    }
    
    private static void GetMethodMetadataName(IMethodSymbol method, StringBuilder bldr)
    {
        bldr.Append(MethodIdEscaper.Escape(method.Name, method.Arity));

        if (method.Parameters.Length <= 0)
            return;

        bldr.Append('(');

        bool needsComma = false;

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (needsComma)
            {
                bldr.Append(',');
            }
            else
            {
                needsComma = true;
            }

            ITypeSymbol type = parameter.Type;
            GetTypeMetadataName(type, bldr, parameterProvider: method);

            if (parameter.RefKind != RefKind.None)
                bldr.Append('&');
        }

        bldr.Append(')');
    }

    private static void GetTypeMetadataName(ITypeSymbol type, StringBuilder bldr, bool nested = false, ISymbol? parameterProvider = null)
    {
        ITypeSymbol elementType = type;
        while (true)
        {
            if (elementType is IArrayTypeSymbol arr)
                elementType = arr.ElementType;
            else if (elementType is IPointerTypeSymbol ptr)
                elementType = ptr.PointedAtType;
            else
                break;
        }

        if (type is ITypeParameterSymbol typeParam && parameterProvider != null)
        {
            ITypeSymbol? typeParamProvider = parameterProvider as ITypeSymbol;
            if (typeParam.TypeParameterKind == TypeParameterKind.Method && parameterProvider is IMethodSymbol methodParamProvider)
            {
                int index = methodParamProvider.TypeParameters.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x, type))?.Ordinal ?? -1;
                if (index >= 0)
                {
                    bldr.Append("!!").Append(index.ToString(CultureInfo.InvariantCulture));
                    goto nameAppended;
                }

                typeParamProvider = methodParamProvider.ContainingType;
            }

            if (typeParam.TypeParameterKind != TypeParameterKind.Method && typeParamProvider is INamedTypeSymbol { IsGenericType: true } declaringTypeNamed)
            {
                int index = declaringTypeNamed.TypeParameters.FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x, type))?.Ordinal ?? -1;
                if (index >= 0)
                {
                    bldr.Append('!').Append(index.ToString(CultureInfo.InvariantCulture));
                    goto nameAppended;
                }
            }
        }
        
        if (!nested && type.ContainingNamespace != null)
        {
            bldr.Append(NamespaceEscaper.Escape(type.ContainingNamespace.ToDisplayString(UnturnedTestGenerator.FullTypeNameFormat)))
                .Append('.');
        }

        if (elementType.ContainingType != null)
        {
            GetTypeMetadataName(elementType.ContainingType, bldr, nested: true, parameterProvider: parameterProvider);
            bldr.Append('+');
        }

        bldr.Append(TypeIdEscaper.Escape(elementType.Name, elementType.GetArity()));
        nameAppended:
        if (nested)
            return;

        if (elementType is INamedTypeSymbol { IsGenericType: true, IsUnboundGenericType: false } instance)
        {
            bldr.Append('<');

            int ct = 0;
            // nested types
            if (elementType.ContainingType != null)
            {
                Stack<INamedTypeSymbol> stack = new Stack<INamedTypeSymbol>(4);
                for (ITypeSymbol c = elementType.ContainingType; c != null; c = c.ContainingType)
                {
                    if (c is INamedTypeSymbol { IsGenericType: true } i)
                        stack.Push(i);
                }

                while (stack.Count > 0)
                {
                    INamedTypeSymbol nestedType = stack.Pop();

                    ImmutableArray<ITypeParameterSymbol> nestedTypeArgs = nestedType.TypeParameters;

                    foreach (ITypeParameterSymbol s in nestedTypeArgs)
                    {
                        if (ct != 0)
                            bldr.Append(',');

                        GetTypeMetadataName(s, bldr, parameterProvider: parameterProvider);
                        ++ct;
                    }
                }
            }

            ImmutableArray<ITypeParameterSymbol> typeArgs = instance.TypeParameters;

            foreach (ITypeParameterSymbol s in typeArgs)
            {
                if (ct != 0)
                    bldr.Append(',');

                GetTypeMetadataName(s, bldr, parameterProvider: parameterProvider);
                ++ct;
            }

            bldr.Append('>');
        }

        AppendElementType(type, bldr);
    }

    private static void AppendElementType(ITypeSymbol elementType, StringBuilder bldr)
    {
        if (elementType is IArrayTypeSymbol arrType)
        {
            AppendElementType(arrType.ElementType, bldr);

            int rank = arrType.Rank;
            switch (rank)
            {
                case 0:
                    break;

                case 1:
                    bldr.Append(arrType.IsSZArray ? "[]" : "[*]");
                    break;

                default:
                    bldr.Append('[').Append(',', arrType.Rank - 1).Append(']');
                    break;
            }
        }
        else if (elementType is IPointerTypeSymbol pointerType)
        {
            AppendElementType(pointerType.PointedAtType, bldr);

            bldr.Append('*');
        }
    }
}
