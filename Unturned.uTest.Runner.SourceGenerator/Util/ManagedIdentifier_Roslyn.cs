using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using uTest.Util;

#pragma warning disable IDE0130

namespace uTest;

#pragma warning restore IDE0130

partial class ManagedIdentifier
{
    internal static readonly SymbolDisplayFormat ExplicitInterfaceImplementationFormat;

    static ManagedIdentifier()
    {
        const string fieldName = "ExplicitInterfaceImplementationFormat";

        FieldInfo? explicitField = typeof(SymbolDisplayFormat)
            .GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (explicitField == null || explicitField.GetValue(null) is not SymbolDisplayFormat fmt)
            throw new MissingFieldException(nameof(SymbolDisplayFormat), fieldName);

        ExplicitInterfaceImplementationFormat = fmt;
    }

    public static string GetManagedType(ITypeSymbol type, bool isByRef)
    {
        if (type == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(sb);
        AppendType(ref builder, type, isByRef);
        builder.Close();
        return builder.ToString();
    }

    public static string GetManagedMethod(IMethodSymbol method)
    {
        if (method == null)
            return string.Empty;

        StringBuilder sb = new StringBuilder();
        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(sb);
        AppendMethod(ref builder, method);
        builder.Close();
        return builder.ToString();
    }

    private static void AppendMethod(ref ManagedIdentifierBuilder builder, IMethodSymbol method)
    {
        ImmutableArray<IMethodSymbol> implMethods = method.ExplicitInterfaceImplementations;
        string methodName;
        if (!implMethods.IsDefaultOrEmpty)
        {
            // adapted from roslyn source - naming an explicit interface method
            // https://github.com/dotnet/roslyn/blob/main/src/Compilers/CSharp/Portable/Symbols/Source/ExplicitInterfaceHelpers.cs
            IMethodSymbol explicitImplementationTarget = implMethods[0];
            INamedTypeSymbol interfaceType = explicitImplementationTarget.ContainingType;

            string interfaceName = interfaceType.ToDisplayString(ExplicitInterfaceImplementationFormat);
            interfaceName = interfaceName.Replace(" ", string.Empty);

            ReadOnlySpan<char> intxNameSpan = interfaceName;

            int ct = ManagedIdentifier.Count(intxNameSpan, '.') + 1;
            Span<Range> sections = stackalloc Range[ct];
            ct = ManagedIdentifier.SplitExplicitlyImplementedMethodName(intxNameSpan, sections);
            for (int i = 0; i < ct; ++i)
            {
                builder.AddExplicitImplementationInterfaceName(intxNameSpan[sections[i]]);
            }

            methodName = method.Name;

            int lastDotIndex = methodName.LastIndexOf('.');
            methodName = lastDotIndex > 0 ? methodName.Substring(lastDotIndex + 1) : methodName; // don't consider leading dots
        }
        else
        {
            methodName = method.MetadataName;
        }

        builder.AddMethodName(methodName, method.Arity);

        ImmutableArray<IParameterSymbol> parameters = method.Parameters;
        if (parameters.IsDefaultOrEmpty)
            return;

        builder.BeginParameters();

        bool isFirst = true;
        foreach (IParameterSymbol parameter in parameters)
        {
            if (isFirst)
                isFirst = false;
            else
                builder.NextParameter();

            ITypeSymbol pType = parameter.Type;
            AppendType(ref builder, pType, isByRef: parameter.RefKind != RefKind.None);
        }

        builder.EndParameters();
    }

    private static void AppendType(ref ManagedIdentifierBuilder builder, ITypeSymbol type, bool isByRef)
    {
        ElementRoslynTypeState elementTypeState = default;
        ITypeSymbol elementType = elementTypeState.ReduceElementType(type);

        if (elementType is ITypeParameterSymbol typeParameter)
        {
            if (typeParameter.TypeParameterKind == TypeParameterKind.Method)
            {
                builder.AddMethodGenericParameterReference(typeParameter.Ordinal);
            }
            else
            {
                builder.AddTypeGenericParameterReference(typeParameter.Ordinal);
            }

            elementTypeState.AppendElementType(ref builder, isByRef, AddElementType);
            return;
        }

        Stack<INamespaceSymbol> namespaces = StackPool<INamespaceSymbol>.Rent();

        for (INamespaceSymbol ns = elementType.ContainingNamespace; ns != null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
        {
            namespaces.Push(ns);
        }

        while (namespaces.Count > 0)
        {
            INamespaceSymbol ns = namespaces.Pop();
            builder.AddTypeSegment(ns.MetadataName, false);
        }

        StackPool<INamespaceSymbol>.Return(namespaces);

        Stack<ITypeSymbol> nestedTypes = StackPool<ITypeSymbol>.Rent();
        Stack<ITypeSymbol> genericTypes = StackPool<ITypeSymbol>.Rent();

        bool anyOpenGenericTypes = false;
        for (ITypeSymbol nestedType = elementType; nestedType != null; nestedType = nestedType.ContainingType)
        {
            nestedTypes.Push(nestedType);

            if (anyOpenGenericTypes || nestedType is not INamedTypeSymbol { IsGenericType: true } namedType)
                continue;

            if (namedType.IsUnboundGenericType
                || namedType.TypeParameters.Any(x => x.TypeParameterKind == TypeParameterKind.Type
                                                     && SymbolEqualityComparer.Default.Equals(x.DeclaringType, namedType)))
            {
                anyOpenGenericTypes = true;
                continue;
            }

            ImmutableArray<ITypeSymbol> args = namedType.TypeArguments;
            if (!args.IsDefaultOrEmpty)
            {
                for (int i = args.Length - 1; i >= 0; --i)
                    genericTypes.Push(args[i]);
            }
        }

        bool isFirst = true;
        int prevArity = 0;

        while (nestedTypes.Count > 0)
        {
            ITypeSymbol nestedType = nestedTypes.Pop();
            int thisArity = 0;
            if (nestedType is INamedTypeSymbol { IsGenericType: true } gen)
            {
                thisArity = gen.Arity;
            }

            builder.AddTypeSegment(nestedType.Name, !isFirst, thisArity - prevArity);
            isFirst = false;

            prevArity = thisArity;
        }

        StackPool<ITypeSymbol>.Return(nestedTypes);

        if (!anyOpenGenericTypes && genericTypes.Count > 0)
        {
            builder.BeginTypeParameters();

            bool first = true;
            while (genericTypes.Count > 0)
            {
                if (!first)
                    builder.NextParameter();
                else
                    first = false;
                ITypeSymbol arg = genericTypes.Pop();
                AppendType(ref builder, arg, false);
            }

            builder.EndTypeParameters();
        }

        StackPool<ITypeSymbol>.Return(genericTypes);

        elementTypeState.AppendElementType(ref builder, isByRef, AddElementType);
    }

    private static void AddElementType(ref ManagedIdentifierBuilder builder, ITypeSymbol type, bool isByRef)
    {
        switch (type)
        {
            case IArrayTypeSymbol arrType:
                builder.MakeArrayType(arrType.IsSZArray ? 0 : arrType.Rank);
                break;

            default:
                builder.MakePointerType();
                break;
        }

        if (isByRef)
            builder.MakeReferenceType();
    }
}
