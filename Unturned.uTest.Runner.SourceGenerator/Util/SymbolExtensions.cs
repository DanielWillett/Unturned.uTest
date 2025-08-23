using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace uTest.Util;

internal static class SymbolExtensions
{
    public static bool CanBeGenericArgument(this ITypeSymbol symbol)
    {
        if (symbol == null)
            return false;

        if (symbol.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer
            || symbol.SpecialType is SpecialType.System_TypedReference or SpecialType.System_ArgIterator or SpecialType.System_RuntimeArgumentHandle or SpecialType.System_Void
            || symbol.IsStatic)
        {
            return false;
        }

        return !symbol.IsRefLikeType;
    }

    public static bool IsEqualTo(this ITypeSymbol? type, string globalTypeName)
    {
        return type != null && type
            .ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat)
            .Equals(globalTypeName, StringComparison.Ordinal);
    }

    public static bool IsInheritedAttribute(this INamedTypeSymbol attributeType)
    {
        if (attributeType.IsEqualTo("global::System.AttributeUsageAttribute"))
        {
            return true;
        }

        AttributeData? data = GetAttribute(attributeType, "global::System.AttributeUsageAttribute");
        if (data == null)
            return true;

        if (data.ConstructorArguments.Length == 3)
        {
            TypedConstant arg = data.ConstructorArguments[2];
            return arg is { Kind: TypedConstantKind.Primitive, Value: true };
        }

        KeyValuePair<string, TypedConstant> parameter = data.NamedArguments.FirstOrDefault(x => string.Equals(x.Key, nameof(AttributeUsageAttribute.Inherited), StringComparison.Ordinal));
        if (parameter.Key != null)
        {
            return parameter.Value is { Kind: TypedConstantKind.Primitive, Value: true };
        }

        return true;
    }

    public static bool HasAttribute(this ISymbol? symbol, string typeName)
    {
        return GetAttribute(symbol, typeName) != null;
    }

    public static AttributeData? GetAttribute(this ISymbol? symbol, string typeName)
    {
        bool requireInherited = false;
        bool isAttribute = string.Equals(typeName, "global::System.Attribute");
        while (symbol != null)
        {
            ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

            foreach (AttributeData attr in attributes)
            {
                for (INamedTypeSymbol? baseType = attr.AttributeClass;
                     baseType != null && !baseType.IsEqualTo("global::System.Attribute");
                     baseType = baseType.BaseType)
                {
                    if (isAttribute || baseType.IsEqualTo(typeName))
                    {
                        return !requireInherited || baseType.IsInheritedAttribute() ? attr : null;
                    }
                }
            }

            requireInherited = true;
            switch (symbol)
            {
                case ITypeSymbol type:
                    symbol = type.BaseType;
                    continue;

                case IMethodSymbol method:
                    symbol = method.OverriddenMethod;
                    continue;

                case IPropertySymbol property:
                    symbol = property.OverriddenProperty;
                    continue;

                case IEventSymbol @event:
                    symbol = @event.OverriddenEvent;
                    continue;
            }

            break;
        }

        return null;
    }

    public static List<AttributeData> GetAttributes(this ISymbol? symbol, string typeName)
    {
        bool requireInherited = false;
        bool isAttribute = string.Equals(typeName, "global::System.Attribute");
        List<AttributeData> list = new List<AttributeData>();
        bool? isInherited = null;
        while (symbol != null)
        {
            ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

            foreach (AttributeData attr in attributes)
            {
                for (INamedTypeSymbol? baseType = attr.AttributeClass;
                     baseType != null && !baseType.IsEqualTo("global::System.Attribute");
                     baseType = baseType.BaseType)
                {
                    if (!isAttribute && !baseType.IsEqualTo(typeName))
                        continue;

                    if (requireInherited && !(isInherited ??= baseType.IsInheritedAttribute()))
                    {
                        return list;
                    }

                    list.Add(attr);
                    break;
                }
            }

            requireInherited = true;
            switch (symbol)
            {
                case ITypeSymbol type:
                    symbol = type.BaseType;
                    continue;

                case IMethodSymbol method:
                    symbol = method.OverriddenMethod;
                    continue;

                case IPropertySymbol property:
                    symbol = property.OverriddenProperty;
                    continue;

                case IEventSymbol @event:
                    symbol = @event.OverriddenEvent;
                    continue;
            }

            break;
        }

        return list;
    }
}
