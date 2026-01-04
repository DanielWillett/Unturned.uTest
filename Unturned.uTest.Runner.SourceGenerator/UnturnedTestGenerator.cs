using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using uTest.Util;

namespace uTest;

[Generator(LanguageNames.CSharp)]
public class UnturnedTestGenerator : IIncrementalGenerator
{
    internal static readonly TextEscaper StringLiteralEscaper = new TextEscaper('\r', '\n', '\t', '\v', '\\', '\"', '\0');

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
#if RELEASE
        // stops roslyn from running the source generator at design time
        // so it only runs at compile time. There's no reason to run it at design time.
        // see: https://gist.github.com/xoofx/ddcc713d941417de48524c200e74db14
        if (Assembly.GetEntryAssembly() == null)
            return;
#endif

        IncrementalValuesProvider<TestClassInfo> testFixtures = context.SyntaxProvider.ForAttributeWithMetadataName(
            "uTest.TestAttribute",
            static (n, _) => n is ClassDeclarationSyntax or StructDeclarationSyntax,
            static (ctx, token) =>
            {
                List<AttributeData> attributeBuffer = new List<AttributeData>(16);
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

                Compilation compilation = ctx.SemanticModel.Compilation;
                INamedTypeSymbol? expectedTestAttribute = compilation.GetTypeByMetadataName("uTest.TestAttribute");
                INamedTypeSymbol? setAttribute = compilation.GetTypeByMetadataName("uTest.SetAttribute");
                INamedTypeSymbol? rangeAttribute = compilation.GetTypeByMetadataName("uTest.RangeAttribute");
                INamedTypeSymbol? testArgsAttribute = compilation.GetTypeByMetadataName("uTest.TestArgsAttribute");
                INamedTypeSymbol? typeArgsAttribute = compilation.GetTypeByMetadataName("uTest.TypeArgsAttribute");
                INamedTypeSymbol? workshopItemAttribute = compilation.GetTypeByMetadataName("uTest.RequiredWorkshopItemAttribute");
                INamedTypeSymbol? mapAttribute = compilation.GetTypeByMetadataName("uTest.RequiredMapAttribute");

                EquatableList<TestTypeArgsAttributeInfo>? classTypeArgs = null;
                EquatableList<TestTypeParameterInfo>? classTypeParameters = null;

                FileLinePositionSpan loc = ctx.TargetNode?.GetLocation().GetLineSpan() ?? default;
                int line = 1;
                if (loc.IsValid)
                {
                    line = loc.StartLinePosition.Line;
                    if (line == 0) line = 1;
                }

                if (ctx.TargetSymbol is INamedTypeSymbol namedType)
                {
                    ImmutableArray<AttributeData> classAttributes = namedType.GetAttributes();

                    managedType = ManagedIdentifier.GetManagedType(namedType, false);
                    ImmutableArray<ISymbol> allMembers = namedType.GetMembers();
                    foreach (ISymbol symbol in allMembers)
                    {
                        if (symbol is not IMethodSymbol method)
                            continue;

                        ImmutableArray<AttributeData> methodAttributes = method.GetAttributes();
                        if (!methodAttributes.Any(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, expectedTestAttribute)))
                        {
                            continue;
                        }

                        string managedMethod = ManagedIdentifier.GetManagedMethod(method);
                        string uid = UnturnedTestUid.Create(managedType, managedMethod).Uid;

                        string fileName = string.Empty;
                        int lineStart = -1, lineEnd = -1;
                        int charStart = -1, charEnd = -1;

                        SyntaxReference? syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
                        if (syntaxRef != null)
                        {
                            GetMethodLocation(syntaxRef, ref lineStart, ref lineEnd, ref charStart, ref charEnd, ref fileName);
                            if (lineStart == 0) lineStart = 1;
                            if (lineEnd == 0) lineEnd = 1;
                            if (charStart == 0) charStart = 1;
                            if (charEnd == 0) charEnd = 1;
                        }

                        ImmutableArray<IParameterSymbol> parameters = method.Parameters;
                        EquatableList<TestParameterInfo>? parameterInfo = null;
                        
                        // Method parameters (Set, Range)
                        if (!parameters.IsDefaultOrEmpty)
                        {
                            parameterInfo = new EquatableList<TestParameterInfo>(parameters.Length);
                            foreach (IParameterSymbol parameter in parameters)
                            {
                                ImmutableArray<AttributeData> attributes = parameter.GetAttributes();
                                AttributeData? setAttributeData = attributes
                                    .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, setAttribute));

                                AttributeData? rangeAttributeData = attributes
                                    .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, rangeAttribute));

                                parameterInfo.Add(new TestParameterInfo(
                                    MetadataNameFormatter.GetFullName(ctx.SemanticModel.Compilation, parameter.Type, parameter.RefKind != RefKind.None, method),
                                    parameter.Name,
                                    parameter.Type.ToDisplayString(FullTypeNameWithGlobalFormat),
                                    TestParameterSetAttributeInfo.Create(setAttributeData),
                                    TestParameterRangeAttributeInfo.Create(rangeAttributeData),
                                    parameter.RefKind,
                                    parameter.Type.TypeKind == TypeKind.Enum,
                                    parameter.Type.SpecialType
                                ));
                            }
                        }

                        // Method parameters (TestArgs)
                        EquatableList<TestArgsAttributeInfo> argsInfo = new EquatableList<TestArgsAttributeInfo>(0);
                        foreach (AttributeData argsAttribute in methodAttributes)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(argsAttribute.AttributeClass, testArgsAttribute))
                                continue;

                            if (TestArgsAttributeInfo.TryCreate(argsAttribute, out TestArgsAttributeInfo attributeInfo))
                                argsInfo.Add(attributeInfo);
                        }

                        EquatableList<TestTypeParameterInfo>? methodTypeParameters = null;
                        EquatableList<TestTypeArgsAttributeInfo>? methodTypeArgs = null;
                        if (method.IsGenericMethod)
                        {
                            // Type parameters (TestTypeArgs)
                            foreach (AttributeData argsAttribute in methodAttributes)
                            {
                                if (!SymbolEqualityComparer.Default.Equals(argsAttribute.AttributeClass, typeArgsAttribute))
                                    continue;

                                if (!TestTypeArgsAttributeInfo.TryCreate(argsAttribute, out TestTypeArgsAttributeInfo attributeInfo))
                                    continue;

                                methodTypeArgs ??= new EquatableList<TestTypeArgsAttributeInfo>();
                                methodTypeArgs.Add(attributeInfo);
                            }

                            // Type parameters (Set)
                            ImmutableArray<ITypeParameterSymbol> tps = method.TypeParameters;
                            if (!tps.IsDefaultOrEmpty)
                            {
                                methodTypeParameters = new EquatableList<TestTypeParameterInfo>(tps.Length);
                                foreach (ITypeParameterSymbol typeParam in tps)
                                {
                                    AttributeData? setAttributeData = typeParam.GetAttributes()
                                        .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, setAttribute));

                                    methodTypeParameters.Add(new TestTypeParameterInfo(
                                        typeParam.Name,
                                        TestParameterSetAttributeInfo.Create(setAttributeData))
                                    );
                                }
                            }
                        }

                        EquatableList<ulong>? workshopItems = null;
                        string? map = null;

                        // Workshop items
                        if (workshopItemAttribute != null)
                        {
                            method.GetTestAttributes(workshopItemAttribute, attributeBuffer);
                            foreach (AttributeData attribute in attributeBuffer)
                            {
                                if (attribute.ConstructorArguments.Length > 0
                                    && attribute.ConstructorArguments[0] is { Kind: TypedConstantKind.Primitive, Value: ulong ul })
                                {
                                    (workshopItems ??= new EquatableList<ulong>(attributeBuffer.Count)).Add(ul);
                                }
                            }

                            attributeBuffer.Clear();
                        }

                        // Map name
                        if (mapAttribute != null)
                        {
                            method.GetTestAttributes(mapAttribute, attributeBuffer);
                            foreach (AttributeData attribute in attributeBuffer)
                            {
                                if (attribute.ConstructorArguments.Length <= 0
                                    || attribute.ConstructorArguments[0] is not { Kind: TypedConstantKind.Primitive } arg)
                                {
                                    continue;
                                }
                                if (arg.Value is string str)
                                {
                                    if (string.IsNullOrEmpty(str))
                                        map = null;
                                    else
                                        map = str;
                                }
                                else
                                {
                                    map = null;
                                }
                            }

                            attributeBuffer.Clear();
                        }

                        methods.Add(
                            new TestMethodInfo(
                                ManagedMethod: managedMethod,
                                DisplayName: method.ToDisplayString(MethodDisplayNameFormat),
                                Uid: uid,
                                Arity: method.Arity,
                                LineNumberStart: lineStart,
                                LineNumberEnd: lineEnd,
                                ColumnNumberStart: charStart,
                                ColumnNumberEnd: charEnd,
                                FilePath: fileName,
                                MethodMetadataName: method.MetadataName,
                                MethodName: method.Name,
                                TreeNodePath: TreeNodeFilterHelper.GetMethodFilter(method, useWildcards: false),
                                Parameters: parameterInfo,
                                ArgsAttributes: argsInfo,
                                ReturnTypeFullName: MetadataNameFormatter.GetFullName(ctx.SemanticModel.Compilation, method.ReturnType, method.ReturnsByRef || method.ReturnsByRefReadonly, method),
                                ReturnTypeGloballyQualifiedName: method.ReturnType.ToDisplayString(FullTypeNameWithGlobalFormat),
                                DelegateType: method.IsGenericMethod || namedType.IsGenericType ? null : new DelegateType(method),
                                TypeParameters: methodTypeParameters,
                                TypeArgsAttributes: methodTypeArgs,
                                WorkshopItems: workshopItems,
                                Map: map
                            ));
                    }

                    if (namedType.IsGenericType)
                    {
                        ImmutableArray<ITypeParameterSymbol> tps = namedType.TypeParameters;
                        if (!tps.IsDefaultOrEmpty)
                        {
                            classTypeParameters = new EquatableList<TestTypeParameterInfo>(tps.Length);
                            foreach (ITypeParameterSymbol typeParam in tps)
                            {
                                AttributeData? setAttributeData = typeParam.GetAttributes()
                                    .FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, setAttribute));

                                classTypeParameters.Add(new TestTypeParameterInfo(
                                    typeParam.Name,
                                    TestParameterSetAttributeInfo.Create(setAttributeData))
                                );
                            }
                        }

                        foreach (AttributeData argsAttribute in classAttributes)
                        {
                            if (!SymbolEqualityComparer.Default.Equals(argsAttribute.AttributeClass, typeArgsAttribute))
                                continue;

                            if (!TestTypeArgsAttributeInfo.TryCreate(argsAttribute, out TestTypeArgsAttributeInfo attributeInfo))
                                continue;

                            classTypeArgs ??= new EquatableList<TestTypeArgsAttributeInfo>();
                            classTypeArgs.Add(attributeInfo);
                        }
                    }
                }
                else
                {
                    managedType = typeName;
                }

                return new TestClassInfo(
                    ctx.TargetSymbol.MetadataName,
                    ctx.TargetSymbol.Name,
                    assemblyName,
                    @namespace,
                    managedType,
                    line,
                    loc.Path,
                    methods,
                    classTypeParameters,
                    classTypeArgs
                );
            });

        context.RegisterSourceOutput(
            testFixtures,
            (context, input) =>
            {
                ref TestClassInfo classInfo = ref input;
                context.CancellationToken.ThrowIfCancellationRequested();

                string fileName = (string.IsNullOrEmpty(classInfo.Namespace) ? "[global]/" : (classInfo.Namespace.Replace('.', '/') + "/"))
                                  + classInfo.Name + ".cs";

                SourceStringBuilder bldr = new SourceStringBuilder(CultureInfo.InvariantCulture);

                string className = classInfo.Name.Replace('+', '_') + "__uTest__IGeneratedTestProvider";

                bldr.String("// <auto-generated/>")
                    .Preprocessor("#nullable disable")
                    .Empty();

                string? @namespace = string.IsNullOrEmpty(classInfo.Namespace)
                    ? null
                    : NamespaceHelper.SanitizeNamespace(classInfo.Namespace);

                string globalName, globalTestName, openGlobalTestName;
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
                if (classInfo.TypeParameters is { Count: > 0 })
                {
                    openGlobalTestName = globalTestName + "<" + new string(',', classInfo.TypeParameters.Count - 1) + ">";
                }
                else
                {
                    openGlobalTestName = globalTestName;
                }

                bldr.Build($"[assembly: global::uTest.Discovery.GeneratedTestBuilderAttribute(typeof({openGlobalTestName}), typeof({globalName}))]")
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
                    .Build($"internal sealed class {className} : global::uTest.Discovery.IGeneratedTestProvider")
                    .String("{").In();

                List<(DelegateType, string)> createdTypes = new List<(DelegateType, string)>();
                int ovlNum = 0;
                string? ovlName = null;
                bool expandableType = classInfo.TypeParameters is { Count: > 0 };
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
                    DelegateType? deleType = method.DelegateType;
                    if (deleType == null)
                        continue;
                    if (deleType.Predefined != PredefinedDelegateType.None)
                        continue;

                    string name = string.Format(deleType.Name, ovlNum);
                    createdTypes.Add((deleType, name));
                    bldr.String("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]");
                    deleType.WriteDefinition(bldr, Accessibility.Private, name);
                }

                // start method
                bldr    .String("[global::System.Runtime.CompilerServices.CompilerGeneratedAttribute]")
                        .String("public void Build(global::uTest.Discovery.IGeneratedTestBuilder builder)")
                        .String("{").In();

                string escManagedType = StringLiteralEscaper.Escape(classInfo.ManagedType);
                string escAssemblyFullName = StringLiteralEscaper.Escape(classInfo.AssemblyFullName);
                string escNamespace = StringLiteralEscaper.Escape(classInfo.Namespace);
                string escTypeName = StringLiteralEscaper.Escape(classInfo.Name);

                bldr.Build($"builder.MethodCount = {classInfo.Methods.Count};");
                bldr.Build($"builder.TestType = typeof({openGlobalTestName});");

                if (classInfo.TypeParameters is { Count: > 0 })
                {
                    bldr.String("builder.TypeParameters = ").In();
                    AppendTypeParameterInfo(bldr, classInfo.TypeParameters, "};", $"#line {classInfo.DefinitionLine} \"{classInfo.FileName}\"");
                    bldr.Out();
                }

                if (classInfo.TypeArgsAttributes is { Count: > 0 })
                {
                    bldr.String("builder.TypeArgs = ").In();
                    AppendTypeArgsInfo(bldr, classInfo.TypeArgsAttributes, "};", $"#line {classInfo.DefinitionLine} \"{classInfo.FileName}\"");
                    bldr.Out();
                }

                if (classInfo.Methods.Any(x => x.DelegateType == null))
                {
                    bldr.Empty()
                        .Build($"global::System.Reflection.MethodInfo[] methods = typeof({openGlobalTestName}).GetMethods(global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic | global::System.Reflection.BindingFlags.DeclaredOnly);")
                        .Empty();
                }

                bool isFirst = true;
                foreach (TestMethodInfo method in classInfo.Methods)
                {
                    string file = method.FilePath;

                    string linePrep = $"#line {method.LineNumberStart.ToString(CultureInfo.InvariantCulture)} \"{file}\"";

                    DelegateType? delegateType = method.DelegateType;
                    string? delegateName = method.DelegateType?.Name;
                    if (delegateType != null && delegateType.Predefined == PredefinedDelegateType.None)
                    {
                        string? foundName = createdTypes.Find(x => ReferenceEquals(method.DelegateType, x.Item1)).Item2;
                        if (foundName != null)
                            delegateName = foundName;
                    }

                    if (!isFirst)
                        bldr.Empty();
                    else
                        isFirst = false;

                    bool expandableMethod = expandableType || method.TypeParameters is { Count: > 0 } || method.Parameters is { Count: > 0 };

                    string escManagedMethod = StringLiteralEscaper.Escape(method.ManagedMethod);
                    bldr.String("builder.Add(new global::uTest.Runner.UnturnedMTPTest()")
                        .In().String("{").In()
                            .Build($"ManagedType = \"{escManagedType}\",")
                            .Build($"ManagedMethod = \"{escManagedMethod}\",")
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
                        string list = string.Join("\", \"", method.Parameters.Select(p => StringLiteralEscaper.Escape(p.FullTypeName)));
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
                            .Build($"Expandable = {(expandableMethod ? "true" : "false")},")
                            .Build($"DisplayName = \"{StringLiteralEscaper.Escape(method.DisplayName)}\",")
                            .Build($"Uid = \"{StringLiteralEscaper.Escape(method.Uid)}\",")
                            .Build($"TreePath = \"{StringLiteralEscaper.Escape(method.TreeNodePath)}\",");


                    if (delegateType != null)
                    {
                        bldr.Build($"Method = {delegateType.GetMethodByExpressionString(classInfo, method, globalTestName, delegateName)},");
                    }
                    else
                    {
                        bldr.Build($"Method = global::uTest.Runner.Util.SourceGenerationServices.GetMethodInfoByManagedMethod(typeof({openGlobalTestName}), methods, \"{escManagedMethod}\"),");
                    }
                    bldr.Build($"Map = {(method.Map == null ? "null" : $"\"{StringLiteralEscaper.Escape(method.Map)}\"")},");
                    bldr.Build($"WorkshopItems = {(method.WorkshopItems is not { Count: > 0 } ? "global::System.Array.Empty<ulong>()" : $"new ulong[] {{ {string.Join(", ", method.WorkshopItems)} }}")},");

                    bldr    .String("Parameters = ").In();
                    if (method.Parameters is not { Count: > 0 })
                    {
                        bldr.String("global::System.Array.Empty<global::uTest.Discovery.UnturnedTestParameter>(),").Out();
                    }
                    else
                    {
                        bldr.String("new global::uTest.Discovery.UnturnedTestParameter[]")
                            .String("{").In();

                        for (int i = 0; i < method.Parameters.Count; i++)
                        {
                            TestParameterInfo parameter = method.Parameters[i];

                            SpecialType destinationType = SpecialType.None;

                            int mode;
                            if (parameter.RangeParameter is null && parameter.SetParameter is null)
                            {
                                mode = 0;
                                bldr.String("new global::uTest.Discovery.UnturnedTestParameter()");
                            }
                            else if (parameter.RangeParameter is null)
                            {
                                mode = 1;
                                bldr.String("new global::uTest.Discovery.UnturnedTestSetParameter()");
                            }
                            else if (parameter.RangeParameter.EnumTypeGloballyQualified != null)
                            {
                                mode = 2;
                                destinationType = parameter.RangeParameter.Type;
                                bldr.Build($"new global::uTest.Discovery.UnturnedTestRangeEnumParameter<{parameter.RangeParameter.EnumTypeGloballyQualified}, {parameter.RangeParameter.Type.GetTypeKeyword()}>()");
                            }
                            else if (parameter.RangeParameter.Type == SpecialType.System_Char)
                            {
                                mode = 3;
                                destinationType = parameter.RangeParameter.Type;
                                bldr.Build($"new global::uTest.Discovery.UnturnedTestRangeCharParameter()");
                            }
                            else
                            {
                                mode = 4;
                                destinationType = parameter.SpecialParameterType;
                                if (destinationType is not (
                                    SpecialType.System_Byte or
                                    SpecialType.System_SByte or
                                    SpecialType.System_UInt16 or
                                    SpecialType.System_Int16 or
                                    SpecialType.System_UInt32 or
                                    SpecialType.System_Int32 or
                                    SpecialType.System_UInt64 or
                                    SpecialType.System_Int64 or
                                    SpecialType.System_UIntPtr or
                                    SpecialType.System_IntPtr or
                                    SpecialType.System_Char or
                                    SpecialType.System_Single or
                                    SpecialType.System_Double or
                                    SpecialType.System_Decimal or
                                    SpecialType.System_String)
                                )
                                {
                                    destinationType = parameter.RangeParameter.Type;
                                }

                                SpecialType encodedType = destinationType == SpecialType.System_String
                                    ? parameter.RangeParameter.Type
                                    : destinationType;

                                string rangeType = encodedType switch
                                {
                                    SpecialType.System_Byte => "byte",
                                    SpecialType.System_SByte => "sbyte",
                                    SpecialType.System_UInt16 => "ushort",
                                    SpecialType.System_Int16 => "short",
                                    SpecialType.System_UInt32 => "uint",
                                    SpecialType.System_Int32 => "int",
                                    SpecialType.System_UInt64 => "ulong",
                                    SpecialType.System_Int64 => "long",
                                    SpecialType.System_UIntPtr => "UIntPtr",
                                    SpecialType.System_IntPtr => "IntPtr",
                                    SpecialType.System_Char => "char",
                                    SpecialType.System_Single => "float",
                                    SpecialType.System_Double => "double",
                                    _ /* SpecialType.System_Decimal */ => "decimal"
                                };

                                bldr.Build($"new global::uTest.Discovery.UnturnedTestRangeParameter<{rangeType}>()");
                            }

                            bldr.String("{").In()
                                .Build($"Name = \"{StringLiteralEscaper.Escape(parameter.Name)}\",")
                                .Build($"IsByRef = {(parameter.RefKind == RefKind.None ? "false" : "true")},");
                            if (mode == 0)
                                bldr.Build($"Position = {i}");
                            else
                                bldr.Build($"Position = {i},");
                            switch (mode)
                            {
                                case 1: // basic set
                                    ExtendSet(in parameter, bldr, method, file, linePrep);
                                    break;

                                case 2: // enum range
                                    EquatableEnumValueContainer from = (EquatableEnumValueContainer)parameter.RangeParameter!.From;
                                    EquatableEnumValueContainer to = (EquatableEnumValueContainer)parameter.RangeParameter!.To;

                                    EquatableObjectList.ObjectArrayType destinationArrayType = new EquatableObjectList.ObjectArrayType(parameter.RangeParameter!.EnumTypeGloballyQualified!);
                                    bldr.Preprocessor(linePrep)
                                        .Build($"From = {EquatableObjectList.AppendLiteral(from, destinationArrayType)},")
                                        .Preprocessor(linePrep)
                                        .Build($"To = {EquatableObjectList.AppendLiteral(to, destinationArrayType)},");

                                    if (from.ValueName != null)
                                        bldr.Build($"FromFieldName = \"{StringLiteralEscaper.Escape(from.ValueName)}\",");
                                    else
                                        bldr.Build($"FromFieldName = string.Empty,");
                                    if (to.ValueName != null)
                                        bldr.Build($"ToFieldName = \"{StringLiteralEscaper.Escape(to.ValueName)}\",");
                                    else
                                        bldr.Build($"ToFieldName = string.Empty,");

                                    string comma = parameter.SetParameter is not null ? "," : string.Empty;

                                    destinationArrayType = new EquatableObjectList.ObjectArrayType(parameter.RangeParameter.Type);
                                    bldr.Preprocessor(linePrep)
                                        .Build($"FromUnderlying = {EquatableObjectList.AppendLiteral(from.UnqualifiedValue, destinationArrayType)},")
                                        .Preprocessor(linePrep)
                                        .Build($"ToUnderlying = {EquatableObjectList.AppendLiteral(to.UnqualifiedValue, destinationArrayType)}{comma}");
                                    break;

                                case 3: // char range
                                    destinationArrayType = new EquatableObjectList.ObjectArrayType(SpecialType.System_Char);
                                    bldr.Preprocessor(linePrep)
                                        .Build($"From = {EquatableObjectList.AppendLiteral(parameter.RangeParameter!.From, destinationArrayType)},")
                                        .Preprocessor(linePrep)
                                        .Build($"To = {EquatableObjectList.AppendLiteral(parameter.RangeParameter!.To, destinationArrayType)},");

                                    if (parameter.RangeParameter!.Step != null)
                                    {
                                        comma = parameter.SetParameter is not null ? "," : string.Empty;
                                        bldr.Build($"Step = {EquatableObjectList.AppendLiteral(
                                            parameter.RangeParameter!.Step,
                                            new EquatableObjectList.ObjectArrayType(SpecialType.System_Int32))
                                        }{comma}");
                                    }
                                    break;
                                    
                                case 4: // other range
                                    destinationArrayType = new EquatableObjectList.ObjectArrayType(destinationType);
                                    bldr.Preprocessor(linePrep)
                                        .Build($"From = {EquatableObjectList.AppendLiteral(parameter.RangeParameter!.From, destinationArrayType)},")
                                        .Preprocessor(linePrep)
                                        .Build($"To = {EquatableObjectList.AppendLiteral(parameter.RangeParameter!.To, destinationArrayType)},");

                                    if (parameter.RangeParameter!.Step != null)
                                    {
                                        comma = parameter.SetParameter is not null ? "," : string.Empty;
                                        bldr.Preprocessor(linePrep)
                                            .Build($"Step = {EquatableObjectList.AppendLiteral(parameter.RangeParameter!.Step, destinationArrayType)}{comma}");
                                    }
                                    break;
                            }

                            if (mode is 2 or 3 or 4)
                            {
                                if (parameter.SetParameter is not null)
                                {
                                    bldr.Build($"SetParameterInfo = new global::uTest.Discovery.UnturnedTestSetParameterInfo()")
                                        .String("{").In();
                                    ExtendSet(in parameter, bldr, method, file, linePrep);
                                    bldr.Out()
                                        .String("}");
                                }
                            }

                            bldr.Out().String(i == method.Parameters.Count - 1 ? "}" : "},");
                        }

                        bldr.Out()
                            .String("},")
                            .Out();
                    }

                    if (method.TypeParameters is { Count: > 0 })
                    {
                        bldr.String("TypeParameters = ").In();
                        AppendTypeParameterInfo(bldr, method.TypeParameters, "},", linePrep);
                        bldr.Out();
                    }
                    
                    if (method.TypeArgsAttributes is { Count: > 0 })
                    {
                        bldr.String("TypeArgs = ").In();
                        AppendTypeArgsInfo(bldr, method.TypeArgsAttributes, "},", linePrep);
                        bldr.Out();
                    }
                    
                    bldr.String("Args = ").In();
                    if (method.ArgsAttributes is not { Count: > 0 })
                    {
                        bldr.String("global::System.Array.Empty<global::uTest.Discovery.UnturnedTestArgs>(),").Out();
                    }
                    else
                    {
                        bldr.String("new global::uTest.Discovery.UnturnedTestArgs[]")
                            .String("{").In();

                        for (int i = 0; i < method.ArgsAttributes.Count; i++)
                        {
                            TestArgsAttributeInfo argsAttribute = method.ArgsAttributes[i];
                            bldr.String("new global::uTest.Discovery.UnturnedTestArgs()")
                                .String("{").In();
                            if (argsAttribute.From != null)
                            {
                                bldr.Build($"From = \"{StringLiteralEscaper.Escape(argsAttribute.From)}\"");
                            }
                            else if (argsAttribute.Values != null)
                            {
                                int paramCount = method.Parameters?.Count ?? 0;
                                EquatableObjectList.ObjectArrayType[] parameterTypes;
                                if (paramCount == 0)
                                    parameterTypes = Array.Empty<EquatableObjectList.ObjectArrayType>();
                                else
                                    parameterTypes = new EquatableObjectList.ObjectArrayType[paramCount];

                                for (int j = 0; j < paramCount; ++j)
                                {
                                    TestParameterInfo p = method.Parameters![j];
                                    if (p.IsEnum)
                                    {
                                        parameterTypes[j] = new EquatableObjectList.ObjectArrayType(p.GloballyQualifiedName);
                                    }
                                    else
                                    {
                                        parameterTypes[j] = new EquatableObjectList.ObjectArrayType(
                                            p.FullTypeName.Equals("System.Type", StringComparison.Ordinal)
                                                ? SpecialType.System_TypedReference
                                                : p.SpecialParameterType
                                        );
                                    }
                                }

                                bldr.Preprocessor(linePrep)
                                    .Build($"Values = {argsAttribute.Values.ToCodeString(default, parameterTypes)}");
                            }

                            bldr.Out().String(i == method.ArgsAttributes.Count - 1 ? "}" : "},");
                        }

                        bldr.Out()
                            .String("},")
                            .Out();
                    }
                    bldr    .Out()
                        .String("}").Out()
                    .String(");")
                    .Preprocessor("#line default");
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
                context.AddSource(fileName, bldr.ToString());
            }
        );
    }

    private static void AppendTypeArgsInfo(SourceStringBuilder bldr, EquatableList<TestTypeArgsAttributeInfo> typeArgs, string end, string? linePrep)
    {
        bldr.Build($"new global::uTest.Discovery.UnturnedTestArgs[]")
            .String("{")
            .In();

        for (int i = 0; i < typeArgs.Count; i++)
        {
            TestTypeArgsAttributeInfo typeParam = typeArgs[i];

            bldr.Build($"new global::uTest.Discovery.UnturnedTestArgs()")
                .String("{").In();

            if (linePrep != null)
                bldr.Preprocessor(linePrep);
            
            bldr.Build($"Values = new global::System.Type[] {{ {string.Join(", ", typeParam.Types.Select(x => $"typeof({x.QualifiedTypeName})"))} }},");

            if (linePrep != null)
                bldr.Preprocessor("#line default");

            bldr.Out()
                .String(i == typeArgs.Count - 1 ? "}" : "},");
        }

        bldr.Out()
            .String(end);
    }

    private static void AppendTypeParameterInfo(SourceStringBuilder bldr, EquatableList<TestTypeParameterInfo> typeParameters, string end, string? linePrep)
    {
        bldr.Build($"new global::uTest.Discovery.UnturnedTestParameter[]")
            .String("{")
            .In();

        for (int i = 0; i < typeParameters.Count; i++)
        {
            TestTypeParameterInfo typeParam = typeParameters[i];
            string type;
            string comma;
            bool hasSet;
            if (typeParam.SetParameter?.Values is not null)
            {
                type = "UnturnedTestSetParameter";
                comma = ",";
                hasSet = true;
            }
            else
            {
                type = "UnturnedTestParameter";
                comma = string.Empty;
                hasSet = false;
            }

            bldr.Build($"new global::uTest.Discovery.{type}()")
                .String("{").In()
                .Build($"Name = \"{StringLiteralEscaper.Escape(typeParam.Name)}\",")
                .Build($"Position = {i}{comma}");

            if (hasSet)
            {
                if (linePrep != null)
                    bldr.Preprocessor(linePrep);

                bldr.Build($"Values = {typeParam.SetParameter!.Values!.ToCodeString(new EquatableObjectList.ObjectArrayType(SpecialType.System_TypedReference), null)}");

                if (linePrep != null)
                    bldr.Preprocessor("#line default");

            }

            bldr.Out()
                .String(i == typeParameters.Count - 1 ? "}" : "},");
        }

        bldr.Out()
            .String(end);
    }

    private static void ExtendSet(in TestParameterInfo parameter, SourceStringBuilder bldr, TestMethodInfo method, string file, string linePrep)
    {
        if (parameter.SetParameter!.From != null)
        {
            bldr.Preprocessor(linePrep)
                .Build($"From = \"{StringLiteralEscaper.Escape(parameter.SetParameter!.From)}\"");
        }
        else if (parameter.SetParameter!.Values != null)
        {
            EquatableObjectList.ObjectArrayType targetType;
            if (parameter.IsEnum)
                targetType = new EquatableObjectList.ObjectArrayType(parameter.GloballyQualifiedName);
            else
                targetType =
                    new EquatableObjectList.ObjectArrayType(
                        parameter.FullTypeName.Equals("System.Type", StringComparison.Ordinal)
                            ? SpecialType.System_TypedReference
                            : parameter.SpecialParameterType
                    );
            bldr.Preprocessor(linePrep)
                .Build($"Values = {parameter.SetParameter.Values.ToCodeString(targetType, null)}");
        }
    }

    private static void GetMethodLocation(SyntaxReference syntaxReference, ref int lineStart, ref int lineEnd, ref int charStart, ref int charEnd, ref string fileName)
    {
        if (syntaxReference.GetSyntax() is not MethodDeclarationSyntax methodDecl)
        {
            return;
        }

        LinePosition start;

        SyntaxTokenList mods = methodDecl.Modifiers;
        if (mods.Count > 0)
        {
            start = mods[0].GetLocation().GetLineSpan().StartLinePosition;
        }
        else
        {
            start = methodDecl.Identifier.GetLocation().GetLineSpan().StartLinePosition;
        }

        FileLinePositionSpan methodSpan = methodDecl.GetLocation().GetLineSpan();
        LinePosition end = methodSpan.EndLinePosition;

        if (methodSpan is { HasMappedPath: false, IsValid: true })
        {
            lineStart = start.Line;
            lineEnd = end.Line;
            charStart = start.Character;
            charEnd = end.Character;
            fileName = methodSpan.Path;
        }
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
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers | SymbolDisplayMiscellaneousOptions.ExpandNullable | SymbolDisplayMiscellaneousOptions.ExpandValueTuple | SymbolDisplayMiscellaneousOptions.UseSpecialTypes
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