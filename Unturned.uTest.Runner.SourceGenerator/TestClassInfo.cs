using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using uTest.Util;

namespace uTest;

internal record struct TestClassInfo(
    string MetadataName,
    string Name,
    string AssemblyFullName,
    string Namespace,
    string ManagedType,
    int DefinitionLine,
    string FileName,
    EquatableList<TestMethodInfo> Methods,
    EquatableList<TestTypeParameterInfo>? TypeParameters,
    EquatableList<TestTypeArgsAttributeInfo>? TypeArgsAttributes);

internal sealed record TestMethodInfo(
    string ManagedMethod,
    string DisplayName,
    string Uid,
    int Arity,
    int LineNumberStart,
    int LineNumberEnd,
    int ColumnNumberStart,
    int ColumnNumberEnd,
    string FilePath,
    string MethodMetadataName,
    string MethodName,
    string TreeNodePath,
    EquatableList<TestParameterInfo>? Parameters,
    EquatableList<TestArgsAttributeInfo> ArgsAttributes,
    EquatableList<TestTypeParameterInfo>? TypeParameters,
    EquatableList<TestTypeArgsAttributeInfo>? TypeArgsAttributes,
    string ReturnTypeFullName,
    string ReturnTypeGloballyQualifiedName,
    DelegateType? DelegateType
);

internal record struct TestParameterInfo(
    string FullTypeName,
    string Name,
    string GloballyQualifiedName,
    TestParameterSetAttributeInfo? SetParameter,
    TestParameterRangeAttributeInfo? RangeParameter,
    RefKind RefKind,
    bool IsEnum,
    SpecialType SpecialParameterType
);
internal record struct TestTypeParameterInfo(
    string Name,
    TestParameterSetAttributeInfo? SetParameter);

internal record struct TestTypeArgsAttributeInfo(EquatableList<EquatableTypeContainer> Types)
{
    public static bool TryCreate(AttributeData attribute, out TestTypeArgsAttributeInfo attributeInfo)
    {
        Unsafe.SkipInit(out attributeInfo);
        if (attribute == null)
            return false;

        if (!attribute.ConstructorArguments.IsDefaultOrEmpty
            && attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Array } arg0)
        {
            ImmutableArray<TypedConstant> v = arg0.Values;
            int l = v.IsDefault ? 0 : v.Length;
            EquatableList<EquatableTypeContainer> list = new EquatableList<EquatableTypeContainer>(l);
            
            for (int i = 0; i < l; ++i)
            {
                TypedConstant c = v[i];
                if (c.Kind != TypedConstantKind.Type )
                    continue;
                ITypeSymbol? t = (ITypeSymbol?)c.Value;
                if (t == null)
                    continue;

                list.Add(new EquatableTypeContainer(t.ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat)));
            }

            attributeInfo = new TestTypeArgsAttributeInfo(list);
            return true;
        }

        return false;
    }
}

internal record struct TestArgsAttributeInfo(
    string? From,
    EquatableObjectList? Values
)
{
    public static bool TryCreate(AttributeData attribute, out TestArgsAttributeInfo attributeInfo)
    {
        Unsafe.SkipInit(out attributeInfo);
        if (attribute == null)
            return false;

        if (attribute.NamedArguments.FirstOrDefault(x =>
                x.Key.Equals("From", StringComparison.Ordinal)) is { Value.Value: string fromMember })
        {
            attributeInfo = new TestArgsAttributeInfo(fromMember, null);
            return true;
        }

        if (attribute.ConstructorArguments.IsDefaultOrEmpty)
            return false;

        TypedConstant array = attribute.ConstructorArguments[0];
        if (array.Kind != TypedConstantKind.Array && !array.IsNull)
            return false;

        attributeInfo = new TestArgsAttributeInfo(null, new EquatableObjectList(in array));
        return true;
    }
}

internal record TestParameterSetAttributeInfo(
    string? From,
    EquatableObjectList? Values
)
{
    public static TestParameterSetAttributeInfo? Create(AttributeData? attribute)
    {
        if (attribute == null)
            return null;

        if (attribute.NamedArguments.FirstOrDefault(x => 
                x.Key.Equals("From", StringComparison.Ordinal)) is { Value.Value: string fromMember })
        {
            return new TestParameterSetAttributeInfo(fromMember, null);
        }

        if (attribute.ConstructorArguments.IsDefaultOrEmpty)
            return null;

        TypedConstant array = attribute.ConstructorArguments[0];
        if (array.Kind != TypedConstantKind.Array && !array.IsNull)
            return null;

        return new TestParameterSetAttributeInfo(null, new EquatableObjectList(in array, distinct: true));
    }
}

internal record TestParameterRangeAttributeInfo(
    object From,
    object To,
    object? Step,
    SpecialType Type,
    string? EnumTypeGloballyQualified
)
{
    public static TestParameterRangeAttributeInfo? Create(AttributeData? attribute)
    {
        if (attribute?.AttributeConstructor is not { Parameters: { IsDefault: false, Length: 2 or 3 } ctorParams }
            || attribute.ConstructorArguments is not { IsDefault: false, Length: 2 or 3 } args)
        {
            return null;
        }

        TypedConstant a0 = args[0], a1 = args[1];
        SpecialType ctorType = ctorParams[0].Type.SpecialType;
        switch (ctorType)
        {
            case SpecialType.System_Object: // Enums
                if (a0.Kind != TypedConstantKind.Enum && a1.Kind != TypedConstantKind.Enum)
                    return null;

                if (a0.Type is not INamedTypeSymbol namedType1 || a1.Type is not INamedTypeSymbol namedType2)
                    return null;

                if (!SymbolEqualityComparer.Default.Equals(namedType1, namedType2) || namedType1.EnumUnderlyingType == null)
                    return null;

                SpecialType underlyingType = namedType1.EnumUnderlyingType.SpecialType;
                if (underlyingType == SpecialType.None)
                    return null;

                string qualifiedTypeName = namedType1.ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat);
                IFieldSymbol? member1 = namedType1.GetEnumMember(a0.Value!),
                              member2 = namedType1.GetEnumMember(a1.Value!);

                EquatableEnumValueContainer e1 = new EquatableEnumValueContainer(qualifiedTypeName, member1?.Name, a0.Value!),
                                            e2 = new EquatableEnumValueContainer(qualifiedTypeName, member2?.Name, a1.Value!);

                return new TestParameterRangeAttributeInfo(e1, e2, null, underlyingType, namedType1.ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat));

            case SpecialType.System_Int32:
            case SpecialType.System_Char:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
                if (a0.Kind != TypedConstantKind.Primitive && a1.Kind != TypedConstantKind.Primitive)
                    return null;

                object? step = args is { IsDefault: false, Length: > 2 } ? args[2].Value : null;
                return ctorType switch
                {
                    SpecialType.System_Int32 or SpecialType.System_Char
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1, ctorType, null),
                    SpecialType.System_UInt32
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1u, ctorType, null),
                    SpecialType.System_Int64
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1L, ctorType, null),
                    SpecialType.System_UInt64
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1UL, ctorType, null),
                    SpecialType.System_Single
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1f, ctorType, null),
                    _ // SpecialType.System_Double
                        => new TestParameterRangeAttributeInfo(a0.Value!, a1.Value!, step ?? 1d, ctorType, null)
                };
        }

        return null;
    }
}

internal sealed class DelegateType : IEquatable<DelegateType>
{
    public PredefinedDelegateType Predefined { get; }
    public string? ReturnType { get; }
    public RefKind ReturnRefKind { get; }
    public EquatableList<DelegateParameter>? Parameters { get; }
    public string Name { get; }
    public bool NeedsUnsafe { get; }
    public DelegateType(IMethodSymbol methodSymbol)
    {
        if (!methodSymbol.ReturnsByRef && !methodSymbol.ReturnsByRefReadonly &&
            (methodSymbol.Parameters.IsDefaultOrEmpty
             || !methodSymbol.Parameters.Any(x => x.RefKind != RefKind.None
                                                  || x.IsParams
                                                  || x.ScopedKind != ScopedKind.None
                                                  || x.HasAttribute("global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute")
                                                  || !x.Type.CanBeGenericArgument()
                                                )
             )
            )
        {
            int paramCt = methodSymbol.Parameters.IsDefault ? 0 : methodSymbol.Parameters.Length;
            if (paramCt <= 16)
            {
                Predefined = methodSymbol.ReturnType.SpecialType == SpecialType.System_Void
                    ? PredefinedDelegateType.Action0 + paramCt
                    : PredefinedDelegateType.Func0 + paramCt;
                Name = methodSymbol.ReturnType.SpecialType == SpecialType.System_Void
                    ? "Action"
                    : "Func";
                return;
            }
        }

        NeedsUnsafe = methodSymbol.ReturnType is IPointerTypeSymbol or IFunctionPointerTypeSymbol
                      || methodSymbol.Parameters.Any(x => x.Type is IPointerTypeSymbol or IFunctionPointerTypeSymbol);

        ReturnType = methodSymbol.ReturnType.ToDisplayString(UnturnedTestGenerator.FullTypeNameWithGlobalFormat);

        ReturnRefKind = methodSymbol.ReturnsByRef
            ? RefKind.Ref : methodSymbol.ReturnsByRefReadonly
                ? RefKind.RefReadOnly : RefKind.None;

        Name = $"__uTestGeneratedDelegate_{methodSymbol.Name}_{{0}}";

        if (!methodSymbol.Parameters.IsDefaultOrEmpty)
        {
            Parameters = new EquatableList<DelegateParameter>(methodSymbol.Parameters.Length);
            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
            {
                Parameters.Add(new DelegateParameter(parameter));
            }
        }
    }

    public string GetMethodByExpressionString(in TestClassInfo classInfo, TestMethodInfo method, string typeName, string? name)
    {
        string methodName = method.MethodName;
        switch (Predefined)
        {
            case PredefinedDelegateType.Action0:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Action>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            case PredefinedDelegateType.Action1:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Action<{GetParameterName(method.Parameters![0])}>>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            case PredefinedDelegateType.Action2:
            case PredefinedDelegateType.Action3:
            case PredefinedDelegateType.Action4:
            case PredefinedDelegateType.Action5:
            case PredefinedDelegateType.Action6:
            case PredefinedDelegateType.Action7:
            case PredefinedDelegateType.Action8:
            case PredefinedDelegateType.Action9:
            case PredefinedDelegateType.Action10:
            case PredefinedDelegateType.Action11:
            case PredefinedDelegateType.Action12:
            case PredefinedDelegateType.Action13:
            case PredefinedDelegateType.Action14:
            case PredefinedDelegateType.Action15:
            case PredefinedDelegateType.Action16:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Action<{string.Join(", ", method.Parameters!.Select(GetParameterName))}>>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            case PredefinedDelegateType.Func0:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Func<{GetReturnName(method)}>>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            case PredefinedDelegateType.Func1:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Func<{GetParameterName(method.Parameters![0])}, {GetReturnName(method)}>>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            case PredefinedDelegateType.Func2:
            case PredefinedDelegateType.Func3:
            case PredefinedDelegateType.Func4:
            case PredefinedDelegateType.Func5:
            case PredefinedDelegateType.Func6:
            case PredefinedDelegateType.Func7:
            case PredefinedDelegateType.Func8:
            case PredefinedDelegateType.Func9:
            case PredefinedDelegateType.Func10:
            case PredefinedDelegateType.Func11:
            case PredefinedDelegateType.Func12:
            case PredefinedDelegateType.Func13:
            case PredefinedDelegateType.Func14:
            case PredefinedDelegateType.Func15:
            case PredefinedDelegateType.Func16:
                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, global::System.Func<{string.Join(", ", method.Parameters!.Select(GetParameterName))}, {method.ReturnTypeGloballyQualifiedName}>>(__uTest_instance__ => __uTest_instance__.@{methodName})";

            default:
                if (name == null)
                    throw new ArgumentNullException(nameof(name));

                return $"global::uTest.Runner.Util.SourceGenerationServices.GetMethodByExpression<{typeName}, {name}>(__uTest_instance__ => __uTest_instance__.@{methodName})";
        }

        static string GetReturnName(TestMethodInfo x)
        {
            return x.ReturnTypeGloballyQualifiedName;
        }

        static string GetParameterName(TestParameterInfo x)
        {
            return x.GloballyQualifiedName;
        }
    }

    public void WriteDefinition(SourceStringBuilder bldr, Accessibility accessibility, string name)
    {
        if (Predefined != PredefinedDelegateType.None)
            throw new InvalidOperationException("Predefined.");

        string @unsafe = NeedsUnsafe ? "unsafe " : string.Empty;

        bldr.Build($"{accessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.ProtectedAndFriend => "private protected",
            Accessibility.Protected => "protected",
            Accessibility.Friend => "internal",
            Accessibility.ProtectedOrFriend => "protected internal",
            _ => "public"
        }} {@unsafe}delegate {ReturnRefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.RefReadOnly => "ref readonly ",
            _ => string.Empty
        }}{ReturnType} @{name}({(Parameters == null || Parameters.Count == 0 ? ")" : string.Empty)}");

        if (Parameters != null && Parameters.Count != 0)
        {
            bldr.In();
            for (int i = 0; i < Parameters.Count; i++)
            {
                DelegateParameter parameter = Parameters[i];
                string def = parameter.Definition;
                if (parameter.UnscopedRef)
                    def = "[global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute] " + def;
                if (i == Parameters.Count - 1)
                    bldr.String(def);
                else
                    bldr.Build($"{def},");
            }
        }

        bldr.Out().String(");");
    }

    /// <inheritdoc />
    public bool Equals(DelegateType? other)
    {
        return other != null && (ReferenceEquals(this, other) || Predefined == other.Predefined && ReturnType == other.ReturnType && ReturnRefKind == other.ReturnRefKind && Equals(Parameters, other.Parameters));
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DelegateType t && Equals(t);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = (int)Predefined;
            hashCode = (hashCode * 397) ^ (ReturnType != null ? ReturnType.GetHashCode() : 0);
            hashCode = (hashCode * 397) ^ (int)ReturnRefKind;
            hashCode = (hashCode * 397) ^ (Parameters != null ? Parameters.GetHashCode() : 0);
            return hashCode;
        }
    }
}

public sealed class DelegateParameter : IEquatable<DelegateParameter>
{
    public string Definition { get; }
    public bool UnscopedRef { get; }

    public DelegateParameter(IParameterSymbol parameter)
    {
        UnscopedRef = parameter.HasAttribute("global::System.Diagnostics.CodeAnalysis.UnscopedRefAttribute");
        Definition = parameter.ToDisplayString(UnturnedTestGenerator.MethodDeclarationFormat);
    }

    /// <inheritdoc />
    public bool Equals(DelegateParameter? other)
    {
        return other != null && UnscopedRef == other.UnscopedRef && string.Equals(Definition, other.Definition, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is DelegateParameter p && Equals(p);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked(Definition.GetHashCode() + (UnscopedRef ? 1 : 0));
}

public enum PredefinedDelegateType
{
    None,
    Action0,
    Action1,
    Action2,
    Action3,
    Action4,
    Action5,
    Action6,
    Action7,
    Action8,
    Action9,
    Action10,
    Action11,
    Action12,
    Action13,
    Action14,
    Action15,
    Action16,
    Func0,
    Func1,
    Func2,
    Func3,
    Func4,
    Func5,
    Func6,
    Func7,
    Func8,
    Func9,
    Func10,
    Func11,
    Func12,
    Func13,
    Func14,
    Func15,
    Func16
}