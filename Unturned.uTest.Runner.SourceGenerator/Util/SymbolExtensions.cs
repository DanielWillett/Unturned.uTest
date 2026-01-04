using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace uTest.Util;

internal static class SymbolExtensions
{
    public static string? GetTypeKeyword(this SpecialType type)
    {
        return type switch
        {
            SpecialType.System_Object => "object",
            SpecialType.System_Void => "void",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_String => "string",
            SpecialType.System_IntPtr => "nint",
            SpecialType.System_UIntPtr => "nuint",
            _ => null
        };
    }

    public static IFieldSymbol? GetEnumMember(this ITypeSymbol enumType, object? underlyingValue)
    {
        ImmutableArray<ISymbol> allMembers = enumType.GetMembers();
        if (allMembers.IsDefaultOrEmpty)
        {
            return null;
        }

        IFieldSymbol? leadingObsolete = null;

        for (int i = 0; i < allMembers.Length; ++i)
        {
            if (allMembers[i] is not IFieldSymbol field)
                continue;

            if (!field.HasConstantValue)
                continue;

            if (!Equals(underlyingValue, field.ConstantValue))
                continue;

            if (field.HasAttribute("System.ObsoleteAttribute"))
                leadingObsolete ??= field;
            else
                return field;
        }

        return leadingObsolete;
    }

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

        if (data.ConstructorArguments is { IsDefaultOrEmpty: false, Length: 3 })
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

    public static int GetAttributes(this ISymbol? symbol, INamedTypeSymbol attributeType, IList<AttributeData> outputList)
    {
        bool requireInherited = false;
        bool? isInherited = null;
        int ct = 0;
        while (symbol != null)
        {
            ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

            foreach (AttributeData attr in attributes)
            {
                for (INamedTypeSymbol? baseType = attr.AttributeClass;
                     baseType != null && baseType.SpecialType != SpecialType.System_Object;
                     baseType = baseType.BaseType)
                {
                    if (!SymbolEqualityComparer.Default.Equals(baseType, attributeType))
                        continue;

                    if (requireInherited && !(isInherited ??= baseType.IsInheritedAttribute()))
                    {
                        return ct;
                    }

                    outputList.Add(attr);
                    ++ct;
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

        return ct;
    }

    public static int GetTestAttributes(this ISymbol? symbol, INamedTypeSymbol attributeType, IList<AttributeData> outputList)
    {
        int ct = 0;
        switch (symbol)
        {
            case null:
                break;

            case IAssemblySymbol assembly:
                ct += assembly.GetAttributes(attributeType, outputList);
                break;

            case IModuleSymbol module:
                ct += module.ContainingAssembly.GetAttributes(attributeType, outputList);
                ct += module.GetAttributes(attributeType, outputList);
                break;

            default:
                ct += symbol.ContainingAssembly.GetAttributes(attributeType, outputList);
                ct += symbol.ContainingModule.GetAttributes(attributeType, outputList);

                // outer types to nested type
                Stack<ITypeSymbol> stack = StackPool<ITypeSymbol>.Rent();
                for (ITypeSymbol containingType = symbol.ContainingType;
                     containingType != null;
                     containingType = containingType.ContainingType)
                {
                    stack.Push(containingType);
                }

                while (stack.Count > 0)
                {
                    ct += stack.Pop().GetAttributes(attributeType, outputList);
                }

                StackPool<ITypeSymbol>.Return(stack);
                ct += symbol.GetAttributes(attributeType, outputList);
                break;
        }

        return ct;
    }
}
