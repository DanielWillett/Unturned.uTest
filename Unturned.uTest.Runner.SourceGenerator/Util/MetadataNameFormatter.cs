using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace uTest.Util;

internal class MetadataNameFormatter
{
    private static readonly AssemblyQualifiedNameEscaper QualifiedNameEscaper = new AssemblyQualifiedNameEscaper();
    private class AssemblyQualifiedNameEscaper : TextEscaper
    {
        public AssemblyQualifiedNameEscaper() : base('\r', '\n', '\t', '\v', '\\', ',', '+', '[', ']', '*', '&') { }
    }

    private static string GetMetadataNameStr(Compilation compilation, ITypeSymbol type, bool byRef, bool includeAsmName)
    {
        if (type.ContainingType != null)
        {
            return GetMetadataNameSlow(compilation, type, byRef, includeAsmName);
        }

        string metaName = QualifiedNameEscaper.Escape(type.MetadataName);

        if (type.ContainingNamespace != null)
        {
            string ns = QualifiedNameEscaper.Escape(type.ContainingNamespace.ToDisplayString(UnturnedTestGenerator.FullTypeNameFormat));
            metaName = ns + "." + metaName;
        }

        if (byRef)
            metaName += "&";

        if (!includeAsmName || IsAssemblyMscorlib(compilation, type))
        {
            return metaName;
        }

        IAssemblySymbol asm = GetAssembly(type);
        string asmName = QualifiedNameEscaper.Escape(asm.Name);
        return metaName + ", " + asmName;
    }

    public static string GetAssemblyQualifiedNameNoVersion(Compilation compilation, ITypeSymbol type, bool byRef = false)
    {
        if (type is not INamedTypeSymbol n || !n.IsGenericType || n.IsUnboundGenericType)
            return GetMetadataNameStr(compilation, type, byRef, true);

        if (n.TypeArguments.Any(x => x is IErrorTypeSymbol))
            return GetMetadataNameStr(compilation, n, byRef, true);

        return GetMetadataNameSlow(compilation, type, byRef, true);
    }

    public static string GetFullName(Compilation compilation, ITypeSymbol type, bool byRef = false)
    {
        if (type is not INamedTypeSymbol n || !n.IsGenericType || n.IsUnboundGenericType)
            return GetMetadataNameStr(compilation, type, byRef, false);

        if (n.TypeArguments.Any(x => x is IErrorTypeSymbol))
            return GetMetadataNameStr(compilation, n, byRef, false);

        return GetMetadataNameSlow(compilation, type, byRef, false);
    }

    private static string GetMetadataNameSlow(Compilation compilation, ITypeSymbol type, bool byRef, bool includeAsmName)
    {
        StringBuilder sb = new StringBuilder(32 * (((type as INamedTypeSymbol)?.TypeArguments.Length ?? 0) + 1));
        GetMetadataName(compilation, type, sb, byRef: byRef, includeAsmName: includeAsmName);
        return sb.ToString();
    }

    private static readonly string[] MscorlibAssemblies =
    [
        "mscorlib", "System.Private.CoreLib"
    ];

    private static readonly string[] MaybeMscorlibAssemblies =
    [
        "netstandard", "System.Runtime"
    ];

    public static IAssemblySymbol GetAssembly(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return array.ElementType.ContainingAssembly;

            case IPointerTypeSymbol ptr:
                return ptr.PointedAtType.ContainingAssembly;

            default:
                return type.ContainingAssembly;
        }
    }

    private static bool IsAssemblyMscorlib(Compilation compilation, ITypeSymbol type)
    {
        type = type.OriginalDefinition;
        IAssemblySymbol asm = GetAssembly(type);
        if (Array.IndexOf(MscorlibAssemblies, asm.Name) >= 0)
            return true;

        if (Array.IndexOf(MaybeMscorlibAssemblies, asm.Name) < 0)
            return false;

        if (type is INamedTypeSymbol { IsGenericType: true } n)
            type = n.ConstructedFrom;

        while (type.ContainingType != null)
            type = type.ContainingType;

        string metaName = type.MetadataName;
        if (type.ContainingNamespace != null)
            metaName = type.ContainingNamespace.ToDisplayString(UnturnedTestGenerator.FullTypeNameFormat) + "." + metaName;

        INamedTypeSymbol? foundType = compilation.GetTypeByMetadataName(metaName);
        return foundType != null;
    }

    private static void GetMetadataName(Compilation compilation, ITypeSymbol type, StringBuilder bldr, bool nested = false, bool byRef = false, bool includeAsmName = true)
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

        if (!nested && elementType.ContainingNamespace != null)
        {
            bldr.Append(QualifiedNameEscaper.Escape(elementType.ContainingNamespace.ToDisplayString(UnturnedTestGenerator.FullTypeNameFormat)))
                .Append('.');
        }

        if (elementType.ContainingType != null)
        {
            GetMetadataName(compilation, elementType.ContainingType, bldr, nested: true);
            bldr.Append('+');
        }

        bldr.Append(QualifiedNameEscaper.Escape(elementType.MetadataName));

        if (nested)
            return;

        if (elementType is INamedTypeSymbol n && n.IsGenericType && !n.IsUnboundGenericType)
        {
            ImmutableArray<ITypeSymbol> typeArgs = n.TypeArguments;
            if (!typeArgs.Any(x => x is IErrorTypeSymbol))
            {
                bldr.Append('[');

                int ct = 0;
                // nested types
                if (elementType.ContainingType != null)
                {
                    Stack<INamedTypeSymbol> stack = new Stack<INamedTypeSymbol>(4);
                    for (INamedTypeSymbol c = elementType.ContainingType; c != null; c = c.ContainingType)
                        stack.Push(c);

                    while (stack.Count > 0)
                    {
                        INamedTypeSymbol nestedType = stack.Pop();

                        ImmutableArray<ITypeSymbol> nestedTypeArgs = nestedType.TypeArguments;

                        foreach (ITypeSymbol s in nestedTypeArgs)
                        {
                            if (ct != 0)
                                bldr.Append(',');

                            bldr.Append('[');
                            GetMetadataName(compilation, s, bldr);
                            bldr.Append(']');
                            ++ct;
                        }
                    }
                }

                foreach (ITypeSymbol s in typeArgs)
                {
                    if (ct != 0)
                        bldr.Append(',');

                    bldr.Append('[');
                    GetMetadataName(compilation, s, bldr);
                    bldr.Append(']');
                    ++ct;
                }

                bldr.Append(']');
            }
        }

        if (byRef)
            bldr.Append('&');

        elementType = type;
        while (true)
        {
            if (elementType is IArrayTypeSymbol arr)
            {
                switch (arr.Rank)
                {
                    case 0:
                        break;

                    case 1:
                        if (arr.LowerBounds.IsDefaultOrEmpty || arr.LowerBounds.Length == 1 && arr.LowerBounds[0] == 0)
                            bldr.Append("[]");
                        else
                            bldr.Append("[*]");
                        break;

                    default:
                        bldr.Append('[').Append(',', arr.Rank - 1).Append(']');
                        break;
                }

                elementType = arr.ElementType;
            }
            else if (elementType is IPointerTypeSymbol ptr)
            {
                bldr.Append('*');
                elementType = ptr.PointedAtType;
            }
            else
                break;
        }

        if (!includeAsmName || IsAssemblyMscorlib(compilation, elementType) || elementType.ContainingAssembly == null)
            return;

        bldr.Append(", ")
            .Append(QualifiedNameEscaper.Escape(elementType.ContainingAssembly.Name));
    }
}
