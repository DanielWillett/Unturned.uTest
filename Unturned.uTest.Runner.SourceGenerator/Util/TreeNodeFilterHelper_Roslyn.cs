using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using uTest.Util;

#pragma warning disable IDE0130

namespace uTest;

partial class TreeNodeFilterHelper
{
    private static bool IsUnbound(INamedTypeSymbol genType, ImmutableArray<ITypeSymbol> types)
    {
        return genType.IsUnboundGenericType || types.Any(x => x is ITypeParameterSymbol p
                                                              && p.TypeParameterKind == TypeParameterKind.Type
                                                              && SymbolEqualityComparer.Default.Equals(p.DeclaringType, genType)
                                                            );
    }

    private static bool IsUnbound(IMethodSymbol genMethod, ImmutableArray<ITypeSymbol> types)
    {
        return types.Any(x => x is ITypeParameterSymbol p
                          && p.TypeParameterKind == TypeParameterKind.Method
                          && SymbolEqualityComparer.Default.Equals(p.DeclaringMethod, genMethod)
                        );
    }

    /// <summary>
    /// Gets a tree path filter that matches all tests in this type.
    /// </summary>
    /// <remarks>Generic type parameters are taken into account.</remarks>
    public static string GetTypeFilter(ITypeSymbol type, bool useWildcards = true, bool isByRef = false)
    {
        StringBuilder sb = StringBuilderPool.Rent();

        TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

        WriteTypePrefix(ref writer, type, out ITypeSymbol elementType, isByRef);

        if (useWildcards)
        {
            Stack<ITypeSymbol> genericTypeArguments = StackPool<ITypeSymbol>.Rent();

            bool anyUnbound = false;
            for (ITypeSymbol nestedType = elementType; nestedType != null; nestedType = nestedType.ContainingType)
            {
                if (nestedType is not INamedTypeSymbol { IsGenericType: true } genType)
                    continue;

                ImmutableArray<ITypeSymbol> types = genType.TypeArguments;
                if (IsUnbound(genType, types))
                {
                    anyUnbound = true;
                    break;
                }

                foreach (ITypeSymbol t in types)
                    genericTypeArguments.Push(t);
            }

            if (!anyUnbound)
            {
                writer.WriteWildcard(false);
                while (genericTypeArguments.Count > 0)
                {
                    ITypeSymbol t = genericTypeArguments.Pop();
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t, false));
                }
            }

            writer.WriteWildcard(true);
        }

        string str = writer.FlushToString();

        StringBuilderPool.Return(sb);
        return str;
    }

    /// <summary>
    /// Gets a tree path filter that matches all tests using this method.
    /// </summary>
    /// <remarks>Generic type parameters are taken into account.</remarks>
    public static string GetMethodFilter(IMethodSymbol method, bool useWildcards = true)
    {
        StringBuilder sb = StringBuilderPool.Rent();

        TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);

        WriteMethodPrefix(ref writer, method, useWildcards, useWildcards);

        string str = writer.FlushToString();

        StringBuilderPool.Return(sb);
        return str;
    }


    private static void WriteTypePrefix(ref TreeNodeFilterWriter builder, ITypeSymbol type, out ITypeSymbol elementType, bool isByRef)
    {
        ElementRoslynTypeState elementTypeState = default;
        elementType = elementTypeState.ReduceElementType(type);

        builder.WriteAssemblyName(elementType.ContainingAssembly.MetadataName);
        builder.WriteNamespace(elementType.ContainingNamespace?.ToDisplayString(UnturnedTestGenerator.FullTypeNameFormat));

        Stack<ITypeSymbol> nestedTypes = StackPool<ITypeSymbol>.Rent();

        for (ITypeSymbol nestedType = elementType; nestedType != null; nestedType = nestedType.ContainingType)
        {
            nestedTypes.Push(nestedType);
        }

        int prevArity = 0;

        bool isNested = false;
        while (nestedTypes.Count > 0)
        {
            ITypeSymbol nestedType = nestedTypes.Pop();
            int thisArity = 0;
            if (nestedType is INamedTypeSymbol { IsGenericType: true } gen)
            {
                thisArity = gen.Arity;
            }

            builder.WriteTypeName(nestedType.Name, thisArity - prevArity, isNested);
            isNested = true;

            prevArity = thisArity;
        }

        StackPool<ITypeSymbol>.Return(nestedTypes);

        elementTypeState.AppendElementType(ref builder, isByRef, AddElementType);
    }

    private static void AddElementType(ref TreeNodeFilterWriter writer, ITypeSymbol type, bool isByRef)
    {
        switch (type)
        {
            case IArrayTypeSymbol arrType:
                writer.WriteArraySpecifier(arrType.IsSZArray ? 0 : arrType.Rank);
                break;

            default:
                writer.WritePointerSpecifier();
                break;
        }

        if (isByRef)
            writer.WriteReferenceSpecifier();
    }

    private static void WriteMethodPrefix(ref TreeNodeFilterWriter writer, IMethodSymbol method, bool genericsWildcard, bool parameterWildcard)
    {
        INamedTypeSymbol? type = method.ContainingType;

        if (type != null)
            WriteTypePrefix(ref writer, type, out _, false);

        writer.WriteMethodName(method.Name, method.Arity);

        ImmutableArray<IParameterSymbol> parameters = method.Parameters;

        foreach (IParameterSymbol parameter in parameters)
        {
            writer.WriteParameterType(ManagedIdentifier.GetManagedType(parameter.Type, parameter.RefKind != RefKind.None));
        }

        ImmutableArray<ITypeSymbol> typeArguments = ImmutableArray<ITypeSymbol>.Empty;
        bool isUnboundType = false;
        if (type is { IsGenericType: true })
        {
            typeArguments = type.TypeArguments;
            isUnboundType = IsUnbound(type, typeArguments);
        }

        ImmutableArray<ITypeSymbol> methodTypeArguments;
        if (type is not { IsGenericType: true } || isUnboundType)
        {
            if (genericsWildcard)
            {
                // declaring type parameters
                if (isUnboundType)
                {
                    writer.WriteWildcard(false);
                }

                // method type parameters
                if (method is { IsGenericMethod: true })
                {
                    methodTypeArguments = method.TypeArguments;
                    if (IsUnbound(method, methodTypeArguments))
                        writer.WriteWildcard(false);
                    else
                    {
                        foreach (ITypeSymbol t in methodTypeArguments)
                            writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t, false));
                    }
                }
            }

            // parameters
            if (parameterWildcard && parameters.Length > 0)
            {
                writer.WriteWildcard(!genericsWildcard);
            }

            return;
        }

        // declaring type parameters
        foreach (ITypeSymbol t in typeArguments)
            writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t, false));

        // method type parameters
        if (method is { IsGenericMethod: true })
        {
            methodTypeArguments = method.TypeArguments;
            if (IsUnbound(method, methodTypeArguments))
            {
                if (genericsWildcard)
                    writer.WriteWildcard(false);
            }
            else
            {
                writer.WriteSeparator();
                foreach (ITypeSymbol t in methodTypeArguments)
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(t, false));
            }
        }

        // parameters
        if (parameterWildcard && parameters.Length > 0)
        {
            writer.WriteWildcard(false);
        }
    }
}
