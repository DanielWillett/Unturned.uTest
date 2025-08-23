using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using uTest.Util;


namespace uTest;

[Generator(LanguageNames.CSharp)]
public class UnturnedTestGenerator : IIncrementalGenerator
{
    private static readonly TextEscaper StringLiteralEscaper = new TextEscaper('\r', '\n', '\t', '\v', '\\', '\"');

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<TestClassInfo> testFixtures = context.SyntaxProvider.ForAttributeWithMetadataName(
            "uTest.TestAttribute",
            static (n, _) => n is ClassDeclarationSyntax or StructDeclarationSyntax,
            static (ctx, token) =>
            {
                token.ThrowIfCancellationRequested();

                EquatableList<TestMethodInfo> methods = new EquatableList<TestMethodInfo>();

                string assemblyName = string.Empty;
                if (ctx.TargetSymbol.ContainingAssembly != null)
                {
                    assemblyName = ctx.TargetSymbol.ContainingAssembly.Identity.GetDisplayName(false);
                }

                string @namespace = string.Empty;
                if (ctx.TargetSymbol.ContainingNamespace != null)
                {
                    @namespace = ctx.TargetSymbol.ContainingNamespace.ToDisplayString(FullTypeNameFormat);
                }

                string typeName = ctx.TargetSymbol.ToDisplayString(FullTypeNameFormat);
                string managedType;

                INamedTypeSymbol? expectedTestAttribute = ctx.SemanticModel.Compilation.GetTypeByMetadataName("uTest.TestAttribute");

                if (ctx.TargetSymbol is INamedTypeSymbol namedType)
                {
                    managedType = ManagedTypeFormatter.GetManagedType(namedType);
                    ImmutableArray<ISymbol> allMembers = namedType.GetMembers();
                    foreach (ISymbol symbol in allMembers)
                    {
                        if (symbol is not IMethodSymbol method)
                            continue;

                        if (!method
                                .GetAttributes()
                                .Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, expectedTestAttribute)))
                        {
                            continue;
                        }

                        string managedMethod = ManagedTypeFormatter.GetManagedMethod(method);
                        string name = managedType + "." + managedMethod;

                        string fileName = string.Empty;
                        int lineStart = -1, lineEnd = -1;
                        int charStart = -1, charEnd = -1;

                        Location? location = symbol.Locations.FirstOrDefault();
                        if (location != null)
                        {
                            FileLinePositionSpan lineSpan = location.GetLineSpan();
                            if (lineSpan is { HasMappedPath: false, IsValid: true })
                            {
                                LinePosition st = lineSpan.StartLinePosition;
                                LinePosition end = lineSpan.EndLinePosition;

                                lineStart = st.Line;
                                lineEnd = end.Line;
                                charStart = st.Character;
                                charEnd = end.Character;
                                fileName = lineSpan.Path;
                            }
                        }

                        ImmutableArray<IParameterSymbol> parameters = method.Parameters;
                        EquatableList<TestParameterInfo>? parameterInfo = null;
                        
                        if (parameters.Length > 0)
                        {
                            parameterInfo = new EquatableList<TestParameterInfo>(parameters.Length);
                            foreach (IParameterSymbol parameter in parameters)
                            {
                                parameterInfo.Add(new TestParameterInfo(
                                    MetadataNameFormatter.GetFullName(ctx.SemanticModel.Compilation, parameter.Type, parameter.RefKind != RefKind.None),
                                    parameter.Type.ToDisplayString(FullTypeNameWithGlobalFormat)
                                ));
                            }
                        }

                        methods.Add(
                            new TestMethodInfo(
                                ManagedMethod: managedMethod,
                                DisplayName: method.ToDisplayString(MethodDisplayNameFormat),
                                Uid: name,
                                Arity: method.Arity,
                                LineNumberStart: lineStart,
                                LineNumberEnd: lineEnd,
                                ColumnNumberStart: charStart,
                                ColumnNumberEnd: charEnd,
                                FilePath: fileName,
                                MethodMetadataName: method.MetadataName,
                                MethodName: method.Name,
                                Parameters: parameterInfo,
                                ReturnTypeFullName: MetadataNameFormatter.GetFullName(ctx.SemanticModel.Compilation, method.ReturnType),
                                ReturnTypeGloballyQualifiedName: method.ReturnType.ToDisplayString(FullTypeNameWithGlobalFormat),
                                DelegateType: new DelegateType(method)
                            ));
                    }
                }
                else
                {
                    managedType = typeName;
                }

                return new TestClassInfo(ctx.TargetSymbol.MetadataName, ctx.TargetSymbol.Name, assemblyName, @namespace, managedType, methods);
            });

        context.RegisterSourceOutput(
            testFixtures,
            (context, classInfo) =>
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                SourceStringBuilder bldr = new SourceStringBuilder(CultureInfo.InvariantCulture);

                string className = classInfo.Name.Replace('+', '_') + "__uTest__IGeneratedTestProvider";

                bldr.String("// <auto-generated/>")
                    .Preprocessor("#nullable disable")
                    .Empty();

                string? @namespace = string.IsNullOrEmpty(classInfo.Namespace)
                    ? null
                    : NamespaceHelper.SanitizeNamespace(classInfo.Namespace);

                string globalName, globalTestName;
                if (classInfo.Namespace.Length > 0)
                {
                    globalName = "global::" + @namespace + "." + className;
                    globalTestName = "global::" + @namespace + ".@" + classInfo.Name;
                }
                else
                {
                    globalName = "global::" + className;
                    globalTestName = "global::@" + classInfo.Name;
                }

                bldr.Build($"[assembly: global::uTest.Runner.GeneratedTestBuilderAttribute(typeof({globalTestName}), typeof({globalName}))]")
                    .Empty();

                if (@namespace != null)
                {
                    // start namespace
                    bldr.Build($"namespace {@namespace}")
                        .String("{").In();
                }

                // start class
                bldr.String("[global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Never)]")
                    .String("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]")
                    .Build($"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(\"Unturned.uTest\", \"{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}\")]")
                    .Build($"internal sealed class {className} : global::uTest.Runner.IGeneratedTestProvider")
                    .String("{").In();

                List<(DelegateType, string)> createdTypes = new List<(DelegateType, string)>();
                int ovlNum = 0;
                string? ovlName = null;
                foreach (TestMethodInfo method in classInfo.Methods)
                {
                    if (ovlName == null || !string.Equals(method.MethodName, ovlName, StringComparison.Ordinal))
                    {
                        ovlNum = 0;
                        ovlName = method.MethodName;
                    }
                    else
                    {
                        ++ovlNum;
                    }
                    DelegateType deleType = method.DelegateType;
                    if (deleType.Predefined != PredefinedDelegateType.None)
                        continue;

                    string name = string.Format(deleType.Name, ovlNum);
                    createdTypes.Add((deleType, name));
                    bldr.String("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
                    deleType.WriteDefinition(bldr, Accessibility.Private, name);
                }

                // start method
                bldr    .String("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]")
                        .String("public void Build(global::uTest.Runner.IGeneratedTestBuilder builder)")
                        .String("{").In();

                string escManagedType = StringLiteralEscaper.Escape(classInfo.ManagedType);
                string escAssemblyFullName = StringLiteralEscaper.Escape(classInfo.AssemblyFullName);
                string escNamespace = StringLiteralEscaper.Escape(classInfo.Namespace);
                string escTypeName = StringLiteralEscaper.Escape(classInfo.Name);

                bldr.Build($"builder.MethodCount = {classInfo.Methods.Count};");
                bldr.Build($"builder.TestType = typeof({globalTestName});");


                bool isFirst = true;
                foreach (TestMethodInfo method in classInfo.Methods)
                {
                    DelegateType delegateType = method.DelegateType;
                    string delegateName = method.DelegateType.Name;
                    if (delegateType.Predefined == PredefinedDelegateType.None)
                    {
                        string? foundName = createdTypes.Find(x => ReferenceEquals(method.DelegateType, x.Item1)).Item2;
                        if (foundName != null)
                            delegateName = foundName;
                    }

                    if (!isFirst)
                        bldr.Empty();
                    else
                        isFirst = false;

                    bldr.String("builder.Add(new global::uTest.Runner.UnturnedTest()")
                        .In().String("{").In()
                            .Build($"ManagedType = \"{escManagedType}\",")
                            .Build($"ManagedMethod = \"{StringLiteralEscaper.Escape(method.ManagedMethod)}\",")
                            .Build($"IdentifierInfo = new global::Microsoft.Testing.Platform.Extensions.Messages.TestMethodIdentifierProperty(").In()
                                .Build($"\"{escAssemblyFullName}\",")
                                .Build($"\"{escNamespace}\",")
                                .Build($"\"{escTypeName}\",")
                                .Build($"\"{StringLiteralEscaper.Escape(method.MethodMetadataName)}\",")
                                .Build($"{method.Arity},");

                    if (method.Parameters == null || method.Parameters.Count == 0)
                    {
                        bldr    .String("global::System.Array.Empty<string>(),");
                    }
                    else
                    {
                        string list = string.Join("\", \"", method.Parameters.Select(p => StringLiteralEscaper.Escape(p.FullName)));
                        bldr    .Build($"new string[] {{ \"{list}\" }},");
                    }

                    bldr        .Build($"\"{StringLiteralEscaper.Escape(method.ReturnTypeFullName)}\"").Out()
                            .String("),")
                            .Build($"LocationInfo = new global::Microsoft.Testing.Platform.Extensions.Messages.TestFileLocationProperty(").In()
                                .Build($"\"{StringLiteralEscaper.Escape(method.FilePath)}\",")
                                .String("new global::Microsoft.Testing.Platform.Extensions.Messages.LinePositionSpan(").In()
                                    .Build($"new global::Microsoft.Testing.Platform.Extensions.Messages.LinePosition({method.LineNumberStart}, {method.ColumnNumberStart}),")
                                    .Build($"new global::Microsoft.Testing.Platform.Extensions.Messages.LinePosition({method.LineNumberEnd}, {method.ColumnNumberEnd})").Out()
                                .String(")").Out()
                            .String("),")
                            .Build($"DisplayName = \"{StringLiteralEscaper.Escape(method.DisplayName)}\",")
                            .Build($"Uid = \"{StringLiteralEscaper.Escape(method.Uid)}\",")
                            .Build($"Method = {delegateType.GetMethodByExpressionString(method, globalTestName, delegateName)}").Out()
                        .String("}").Out()
                    .String(");");
                }

                // end method
                bldr    .Out()
                        .String("}");

                // end class
                bldr.Out()
                    .String("}");

                if (@namespace != null)
                {
                    // end namespace
                    bldr.Out().String("}");
                }

                bldr.Preprocessor("#nullable restore");

                // save file
                context.AddSource(classInfo.Namespace.Replace('.', '/') + "/" + classInfo.Name + ".cs", bldr.ToString());
            }
        );
    }

    internal static readonly SymbolDisplayFormat FullTypeNameWithGlobalFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
        parameterOptions: SymbolDisplayParameterOptions.None,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
        propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
        localOptions: SymbolDisplayLocalOptions.None,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandNullable | SymbolDisplayMiscellaneousOptions.ExpandValueTuple
    );

    internal static readonly SymbolDisplayFormat FullTypeNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
        delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
        parameterOptions: SymbolDisplayParameterOptions.None,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.Default,
        propertyStyle: SymbolDisplayPropertyStyle.NameOnly,
        localOptions: SymbolDisplayLocalOptions.None,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandNullable | SymbolDisplayMiscellaneousOptions.ExpandValueTuple
    );

    internal static readonly SymbolDisplayFormat MethodDisplayNameFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeParameters,
        delegateStyle: SymbolDisplayDelegateStyle.NameAndParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeOptionalBrackets | SymbolDisplayParameterOptions.IncludeDefaultValue,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.InstanceMethod,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        localOptions: SymbolDisplayLocalOptions.IncludeConstantValue | SymbolDisplayLocalOptions.IncludeModifiers | SymbolDisplayLocalOptions.IncludeType,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
    );

    public static readonly SymbolDisplayFormat MethodDeclarationFormat = new SymbolDisplayFormat(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeModifiers | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
        delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
        parameterOptions: SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        localOptions: SymbolDisplayLocalOptions.IncludeModifiers | SymbolDisplayLocalOptions.IncludeType,
        kindOptions: SymbolDisplayKindOptions.IncludeMemberKeyword | SymbolDisplayKindOptions.IncludeNamespaceKeyword | SymbolDisplayKindOptions.IncludeTypeKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.ExpandNullable | SymbolDisplayMiscellaneousOptions.ExpandValueTuple
    );
}