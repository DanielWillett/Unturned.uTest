using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

#pragma warning disable TPEXP

namespace uTest.Discovery;

internal static class GeneratedTestExpansionHelper
{
    private static Type[]? _asyncMethodFromArgTypes;

    private struct ForEachUidLookupIdState
    {
        public List<string>? UidsToFind;
        public List<UnturnedTestInstance> Instances;
        public TestExpandProcessor Processor;
    }

    public static async Task<List<UnturnedTestInstance>> ExpandTestsAsync(ILogger logger, List<UnturnedTest> originalTests, ITestFilter? filter, CancellationToken token)
    {
        TestExpandProcessor processor = new TestExpandProcessor(logger, originalTests, token);

        List<string>? uidsToFind = null;
        List<UnturnedTestInstance>? instances = null;
        if (filter is { Type: TestFilterType.UidList })
        {
            ForEachUidLookupIdState state;

            int uidCount = filter.UidCount;
            instances = new List<UnturnedTestInstance>(uidCount);
            state.Instances = instances;
            state.UidsToFind = null;
            state.Processor = processor;
            await filter.ForEachUid(ref state, static (ref state, id) =>
            {
                ValueTask<UnturnedTestInstance?> task = state.Processor.GetTestFromUid(id);
                if (!task.IsCompleted)
                {
                    state.UidsToFind ??= new List<string>();
                    return new ValueTask(CoreTask(task, state, id));
                }

                UnturnedTestInstance? instance = task.GetAwaiter().GetResult();
                if (!instance.HasValue)
                {
                    (state.UidsToFind ??= new List<string>()).Add(id);
                }
                else
                {
                    state.Instances.Add(instance.Value);
                }

                return default;

                static async Task CoreTask(ValueTask<UnturnedTestInstance?> task, ForEachUidLookupIdState state, string id)
                {
                    UnturnedTestInstance? instance = await task.ConfigureAwait(false);
                    if (!instance.HasValue)
                    {
                        state.UidsToFind!.Add(id);
                        return;
                    }

                    state.Instances.Add(instance.Value);
                }
            });

            if (uidsToFind == null)
                return instances;
        }

        if (filter is { Type: TestFilterType.TreePath })
        {
            processor.TreeFilter = filter;
        }

        List<UnturnedTestInstance> allInstances = await processor.ExpandTestsAsync().ConfigureAwait(false);

        if (uidsToFind == null)
            return allInstances;

        foreach (UnturnedTestInstance instance in allInstances)
        {
            int index = uidsToFind.IndexOf(instance.Uid);
            if (index < 0)
                continue;

            instances!.Add(instance);
            uidsToFind.RemoveAt(index);
            if (uidsToFind.Count == 0)
                break;
        }

        foreach (string uid in uidsToFind)
        {
            await logger.LogErrorAsync(string.Format(Properties.Resources.LogErrorUnresolvedTestUid, uid))
                .ConfigureAwait(false);
        }

        if (instances!.Count != filter!.UidCount)
        {
            await logger.LogErrorAsync(string.Format(Properties.Resources.LogErrorUnresolvedTestUids,
                filter.UidCount - instances.Count, filter.UidCount)).ConfigureAwait(false);
        }

        return instances!;
    }

    internal enum FromMemberType { Field, Property, Method, MethodWithCancellationToken }

    /// <summary>
    /// Finds a member described by the 'From' attribute property.
    /// </summary>
    internal static MemberInfo? GetMember(string name, Type type, out FromMemberType memberType)
    {
        const BindingFlags flags = BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.Instance
                                   | BindingFlags.Static
                                   | BindingFlags.FlattenHierarchy;

        FieldInfo? field = type.GetField(name, flags);
        if (field != null)
        {
            memberType = FromMemberType.Field;
            return field;
        }

        try
        {
            PropertyInfo? property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
            {
                memberType = FromMemberType.Property;
                return property;
            }
        }
        catch (AmbiguousMatchException) { }

        try
        {
            MethodInfo? method = type.GetMethod(name, flags, null, CallingConventions.Any, Type.EmptyTypes, null);
            if (method != null)
            {
                memberType = FromMemberType.Method;
                return method;
            }
        }
        catch (AmbiguousMatchException) { }

        try
        {
            _asyncMethodFromArgTypes ??= [ typeof(CancellationToken) ];
            MethodInfo? method = type.GetMethod(name, flags, null, CallingConventions.Any, _asyncMethodFromArgTypes, null);
            if (method != null)
            {
                memberType = FromMemberType.MethodWithCancellationToken;
                return method;
            }
        }
        catch (AmbiguousMatchException) { }

        memberType = 0;
        return null;
    }
}

internal class TestExpandProcessor
{
    private readonly ILogger _logger;
    private readonly List<UnturnedTest> _originalTests;
    private readonly Dictionary<Type, object?> _runners;
    private readonly CancellationToken _token;

    private readonly List<UnturnedTestInstance> _instances;

    private bool _treeFilterNeedsPropertyBag;
    internal ITestFilter? TreeFilter
    {
        get;
        set
        {
            field = value;
            _treeFilterNeedsPropertyBag = value != null && value.TreePath.IndexOf('[') >= 0;
        }
    }

#nullable disable

    private UnturnedTest _test;
    private Type _testType;
    private Type _testTypeInstance;
    private MethodInfo _testMethodInstance;
    private Lazy<object> _runner;
    private ParameterInfo[] _parameters;
    private Type[] _methodGenericArguments;
    private Type[] _typeGenericArguments;

    private string _managedType;
    private string _managedMethod;

#nullable restore
    public TestExpandProcessor(ILogger logger, List<UnturnedTest> originalTests, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        _logger = logger;
        _originalTests = originalTests;
        _token = token;

        _runners = new Dictionary<Type, object?>();
        _instances = new List<UnturnedTestInstance>();
    }
    
    private MethodInfo? _expandedTestMethod;
    
    /// <summary>
    /// Creates a <see cref="UnturnedTestInstance"/> from a <see cref="UnturnedTestUid"/> with no additional information.
    /// </summary>
    internal ValueTask<UnturnedTestInstance?> GetTestFromUid(UnturnedTestUid uid)
    {
        if (!UnturnedTestUid.TryParse(
                uid.Uid,
                out ReadOnlyMemory<char> managedType,
                out ReadOnlyMemory<char> managedMethod,
                out ReadOnlyMemory<char>[] methodTypeParams,
                out object?[]? parameters,
                out int variantIndex))
        {
            return default;
        }

        ReadOnlyMemory<char> managedTypeWithoutTypeArgs;
        ReadOnlySpan<char> managedTypeSpan = managedType.Span;
        int arityIndex = managedTypeSpan.LastIndexOf('`');
        bool isConstructedGenericType = arityIndex >= 0;
        int genericParameterCount = 0;
        if (isConstructedGenericType)
        {
            int arityStartIndex = arityIndex;
            while (arityIndex < managedTypeSpan.Length && char.IsDigit(managedTypeSpan[arityIndex + 1]))
                ++arityIndex;
            
            if (managedTypeSpan[arityIndex] == '`'
                || !MathHelper.TryParseInt(managedTypeSpan.Slice(arityStartIndex + 1, arityIndex - arityStartIndex), out genericParameterCount))
            {
                // 'TypeWithoutDigits`'
                isConstructedGenericType = false;
                managedTypeWithoutTypeArgs = managedType;
            }
            else
            {
                managedTypeWithoutTypeArgs = managedType.Slice(0, arityIndex + 1);
                if (managedTypeWithoutTypeArgs.Length == managedTypeSpan.Length)
                    isConstructedGenericType = false;
            }
        }
        else
        {
            managedTypeWithoutTypeArgs = managedType;
        }

        if (genericParameterCount > 0 && !isConstructedGenericType)
        {
            return default;
        }

        Type? testType = null;
        Type[]? typeArgs = null;

        foreach (UnturnedTest test in _originalTests)
        {
            if (test.Owner == null)
                continue;

            // check parameter count
            int paramCount = test.Parameters.Length;
            if (paramCount == 0)
            {
                if (parameters is { Length: > 0 } || variantIndex > 0)
                    continue;
            }
            else if (parameters != null && parameters.Length != paramCount)
                continue;

            // check type generic parameter count
            if (genericParameterCount != 0)
            {
                if (test.Owner.TypeParameters == null || test.Owner.TypeParameters.Length != genericParameterCount)
                    continue;
            }
            else if (test.Owner.TypeParameters is { Length: > 0 })
                continue;

            // check type name
            if (!managedTypeWithoutTypeArgs.Span.Equals(test.ManagedType.AsSpan(), StringComparison.Ordinal))
                continue;

            if (!managedMethod.Span.Equals(test.ManagedMethod.AsSpan(), StringComparison.Ordinal))
            {
                continue;
            }

            Type type = test.Owner.Type;
            if (isConstructedGenericType)
            {
                // fill in type parameters using the values from SetAtribute/TypeArgsAttribute.
                if (testType == null)
                {
                    ManagedIdentifierTokenizer typeTokenizer = new ManagedIdentifierTokenizer(managedType.Span, ManagedIdentifierKind.Type);
                    while (typeTokenizer.MoveNext())
                    {
                        if (typeTokenizer.TokenType == ManagedIdentifierTokenType.OpenTypeParameters)
                            break;
                    }

                    if (typeTokenizer.TokenType != ManagedIdentifierTokenType.OpenTypeParameters)
                        continue;

                    StringBuilder sb = StringBuilderPool.Rent();

                    bool anyFailed = false;

                    typeArgs = new Type[test.Owner.TypeParameters!.Length];
                    for (int i = 0; i < typeArgs.Length; ++i)
                    {
                        if (typeTokenizer.TokenType == ManagedIdentifierTokenType.CloseTypeParameters)
                        {
                            anyFailed = true;
                            break;
                        }

                        ManagedIdentifierBuilder builder = new ManagedIdentifierBuilder(sb);
                        int depth = 0;
                        while (typeTokenizer.MoveNext())
                        {
                            ManagedIdentifierTokenType tkn = typeTokenizer.TokenType;
                            if (depth == 0 && tkn is ManagedIdentifierTokenType.NextParameter or ManagedIdentifierTokenType.CloseTypeParameters)
                            {
                                break;
                            }

                            if (tkn == ManagedIdentifierTokenType.OpenTypeParameters)
                            {
                                ++depth;
                            }
                            else if (tkn == ManagedIdentifierTokenType.CloseTypeParameters)
                            {
                                if (depth > 0)
                                    --depth;
                            }

                            builder.WriteToken(in typeTokenizer);
                        }

                        Type? t = FindTypeByManagedTypeName(builder.ToString().AsSpan(), test.Owner, i);
                        if (t == null)
                        {
                            anyFailed = true;
                            break;
                        }

                        typeArgs[i] = t;
                        sb.Clear();
                    }

                    StringBuilderPool.Return(sb);

                    if (anyFailed || typeTokenizer.TokenType != ManagedIdentifierTokenType.CloseTypeParameters)
                        continue;

                    try
                    {
                        testType = type.MakeGenericType(typeArgs);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                type = testType;
            }
            else
            {
                typeArgs = Type.EmptyTypes;
            }

            Type[] methodTypeArgs;
            // by now we know this is the correct type and method name
            if (methodTypeParams.Length > 0)
            {
                if (test.TypeParameters == null || test.TypeParameters.Length != methodTypeParams.Length)
                    continue;

                methodTypeArgs = new Type[methodTypeParams.Length];
                bool anyFailed = false;
                for (int i = 0; i < methodTypeParams.Length; ++i)
                {
                    Type? methodTypeArg = FindTypeByManagedTypeName(methodTypeParams[i].Span, test, i);
                    if (methodTypeArg == null)
                    {
                        anyFailed = true;
                        break;
                    }

                    methodTypeArgs[i] = methodTypeArg;
                }

                if (anyFailed)
                    continue;
            }
            else if (test.TypeParameters is { Length: > 0 })
            {
                continue;
            }
            else
            {
                methodTypeArgs = Type.EmptyTypes;
            }

            MethodInfo? testMethod;
            if (typeArgs!.Length > 0)
                testMethod = (MethodInfo?)MethodBase.GetMethodFromHandle(test.Method.MethodHandle, type.TypeHandle);
            else
                testMethod = test.Method;

            if (testMethod == null)
                continue;

            if (methodTypeArgs.Length > 0)
            {
                try
                {
                    testMethod = testMethod.MakeGenericMethod(methodTypeArgs);
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            if (parameters != null || paramCount == 0)
            {
                object?[] p = parameters ?? Array.Empty<object>();
                UnturnedTestInstance instance = new UnturnedTestInstance(
                    this,
                    test,
                    type,
                    testMethod,
                    typeArgs.Length == 0 ? test.ManagedType : ManagedIdentifier.GetManagedType(type),
                    test.ManagedMethod,
                    p,
                    variantIndex,
                    UnturnedTestInstance.CalculateArgumentHash(typeArgs, methodTypeArgs, p),
                    typeArgs,
                    methodTypeArgs
                );
                if (!string.Equals(instance.Uid, uid))
                    continue;

                return new ValueTask<UnturnedTestInstance?>(instance);
            }

            if (_expandedTestMethod != testMethod)
            {
                return new ValueTask<UnturnedTestInstance?>(ExpandTestAndReturn(this, test, testMethod, typeArgs, methodTypeArgs, variantIndex));
            }

            return variantIndex >= _instances.Count ? default : new ValueTask<UnturnedTestInstance?>(_instances[variantIndex]);
        }

        return new ValueTask<UnturnedTestInstance?>(default(UnturnedTestInstance?));

        static async Task<UnturnedTestInstance?> ExpandTestAndReturn(TestExpandProcessor processor, UnturnedTest test, MethodInfo testMethod, Type[] typeArgs, Type[] methodTypeArgs, int variantIndex)
        {
            processor._instances.Clear();
            processor._expandedTestMethod = testMethod;
            processor.SetupExpandTest(test);
            await processor.SetupExpandTest(typeArgs).ConfigureAwait(false);
            await processor.ExpandComplexTest(typeArgs, methodTypeArgs, (ulong)(typeArgs.Length * methodTypeArgs.Length)).ConfigureAwait(false);

            if (variantIndex >= processor._instances.Count)
                return null;

            return processor._instances[variantIndex];
        }

        // theres no effecient way to go from managed type to CLR Type without the assembly name so we just find it from the parameter info
        static Type? FindTypeByManagedTypeName(ReadOnlySpan<char> typeName, ITypeParamsProvider provider, int index)
        {
            if (typeName.IsEmpty)
                return null;

            if (provider.TypeParameters != null
                && provider.TypeParameters.Length > index
                && provider.TypeParameters[index] is UnturnedTestSetParameter { Values: Type[] setTypes }
                )
            {
                for (int i = 0; i < setTypes.Length; ++i)
                {
                    if (ManagedIdentifier.IsSameType(setTypes[i], typeName))
                        return setTypes[i];
                }
            }

            if (provider.TypeArgs != null)
            {
                for (int i = 0; i < provider.TypeArgs.Length; ++i)
                {
                    if (provider.TypeArgs[i] is { Values: Type[] types }
                        && types.Length > index
                        && ManagedIdentifier.IsSameType(types[index], typeName))
                        return types[index];
                }
            }

            return null;
        }
    }

    public async Task<List<UnturnedTestInstance>> ExpandTestsAsync()
    {
        Stopwatch sw = Stopwatch.StartNew();
        foreach (UnturnedTest test in _originalTests)
        {
            await ExpandTestAsync(test).ConfigureAwait(false);
        }
        sw.Stop();
        await _logger.LogInformationAsync($"Expanded {_originalTests.Count} tests to {_instances.Count} in {sw.Elapsed.Milliseconds} ms.").ConfigureAwait(false);

        return _instances;
    }

    internal static readonly int DefaultArgHash = UnturnedTestInstance.CalculateArgumentHash(
        Type.EmptyTypes,
        Type.EmptyTypes,
        Array.Empty<object>()
    );

    private void SetupExpandTest(UnturnedTest test)
    {
        _test = test;
        if (_testType != test.Method.DeclaringType)
        {
            _testType = test.Method.DeclaringType!;
            _testTypeInstance = _testType;
            try
            {
                _typeGenericArguments = _testType.GetGenericArguments();
            }
            catch (NotSupportedException)
            {
                _typeGenericArguments = Type.EmptyTypes;
            }

            CreateRunner();
        }

        _parameters = test.Method.GetParameters();
        try
        {
            _methodGenericArguments = test.Method.GetGenericArguments();
        }
        catch (NotSupportedException)
        {
            _methodGenericArguments = Type.EmptyTypes;
        }
    }

    private ValueTask<bool> SetupExpandTest(Type[] typeArgs)
    {
        if (typeArgs.Length == 0)
        {
            _managedType = _test.ManagedType;
            _testTypeInstance = _testType;
        }
        else
        {
            try
            {
                _testTypeInstance = _testType.MakeGenericType(typeArgs);
                _managedType = ManagedIdentifier.GetManagedType(_testTypeInstance);
            }
            catch (ArgumentException)
            {
                _testTypeInstance = null;
            }
        }

        if (_testTypeInstance == null)
        {
            return new ValueTask<bool>(LogCore(this, typeArgs));
        }

        CreateRunner();
        return new ValueTask<bool>(true);

        static async Task<bool> LogCore(TestExpandProcessor processor, Type[] typeArgs)
        {
            await processor._logger.LogErrorAsync(
                string.Format(
                    Properties.Resources.LogErrorGenericConstraints,
                    typeArgs.Length == 1
                        ? typeArgs[0].FullName
                        : $"<{string.Join(", ", typeArgs.Select(ManagedIdentifier.GetManagedType))}>",
                    processor._test.DisplayName
                )
            ).ConfigureAwait(false);
            return false;
        }
    }

    public ValueTask ExpandTestAsync(UnturnedTest test)
    {
        SetupExpandTest(test);

        // basic 0-arg test
        if (!test.Expandable)
        {
            UnturnedTestInstance instance = new UnturnedTestInstance(test);
            if (TreeFilter == null || TreeFilter.MatchesTreePathFilter(in instance, _treeFilterNeedsPropertyBag))
                _instances.Add(instance);
            return default;
        }

        return new ValueTask(ExpandComplexTest(null, _test.Owner, _testType.IsGenericTypeDefinition, 1, _typeGenericArguments.Length));
    }

    private void CreateRunner()
    {
        _runner = new Lazy<object?>(() =>
        {
            if (_runners.TryGetValue(_testTypeInstance, out object? obj))
                return obj;

            object? instance;
            try
            {
                instance = Activator.CreateInstance(_testTypeInstance, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format(Properties.Resources.LogErrorCreatingRunner, _testTypeInstance.FullName), ex);
                instance = null;
            }

            _runners.Add(_testTypeInstance, instance);
            return instance;
        }, LazyThreadSafetyMode.None);
    }

    private async Task ExpandComplexTest(Type[]? parentArgs, ITypeParamsProvider? provider, bool isUnbound, ulong expansionFactor, int expectedArity)
    {
        // instead of method type params
        bool isExpandingTypeParams = parentArgs == null;
        if (!isExpandingTypeParams && TreeFilter != null)
        {
            if (!FilterHelper.TreeCouldContainThisTypeConfiguration(_test, parentArgs!, null, null, TreeFilter.TreePath))
                return;
        }

        if (!isExpandingTypeParams && !await SetupExpandTest(parentArgs!))
        {
            return;
        }

        int arity = isExpandingTypeParams ? GetArity(_test.Method) : 0;
        if (provider == null || (provider.TypeParameters == null && provider.TypeArgs == null))
        {
            if (isUnbound)
                return;

            if (isExpandingTypeParams)
                await ExpandComplexTest(Type.EmptyTypes, _test, _test.Method.IsGenericMethodDefinition, 1, arity).ConfigureAwait(false);
            else
                await ExpandComplexTest(parentArgs!, Type.EmptyTypes, expansionFactor);
            return;
        }

        UnturnedTestParameter[]? typeParams = provider.TypeParameters;
        bool hasSetParams = false;
        Type[][]? sets = null;
        if (typeParams != null && typeParams.Length == expectedArity)
        {
            sets = new Type[typeParams.Length][];
            ulong variationCount = 1;
            for (int i = 0; i < typeParams.Length; ++i)
            {
                if (typeParams[i] is not UnturnedTestSetParameter { Values: Type[] set })
                {
                    variationCount = 0;
                    continue;
                }

                set = Distinctify(set);
                sets[i] = set;
                variationCount *= (ulong)set.Length;
            }

            // if any type parameters dont have a set attribute it can't be expanded
            if (variationCount * expansionFactor is > 0 and <= RangeHelper.MaxTestVariations)
            {
                expansionFactor *= variationCount;

                int[] indices = new int[typeParams.Length];
                
                int variationCountFixed = checked ( (int)variationCount );

                int newCt = _instances.Count + variationCountFixed;
                if (_instances.Capacity < newCt)
                    _instances.Capacity = newCt;

                bool reachedEnd = false;
                do
                {
                    for (int p = indices.Length - 1; p >= 0; --p)
                    {
                        ref int index = ref indices[p];
                        if ((uint)index < sets[p].Length - 1u)
                        {
                            ++index;
                            break;
                        }

                        index = 0;
                        if (p != 0)
                            continue;

                        reachedEnd = true;
                        break;
                    }

                    Type[] args = new Type[indices.Length];
                    for (int p = 0; p < indices.Length; ++p)
                    {
                        args[p] = sets[p][indices[p]];
                    }

                    if (!isExpandingTypeParams)
                        await ExpandComplexTest(parentArgs!, args, expansionFactor).ConfigureAwait(false);
                    else
                        await ExpandComplexTest(args, _test, _test.Method.IsGenericMethodDefinition, expansionFactor, arity).ConfigureAwait(false);
                } while (!reachedEnd);
            }
            else
            {
                sets = null;
            }
        }

        bool anyArgs = false;
        UnturnedTestArgs[]? typeArgs = provider.TypeArgs;
        if (typeArgs is { Length: > 0 })
        {
            for (int ti = 0; ti < typeArgs.Length; ti++)
            {
                UnturnedTestArgs args = typeArgs[ti];
                args.IsValid = true;
                if (args.Values is not Type[] types)
                {
                    args.IsValid = false;
                    continue;
                }

                if (types.Length != expectedArity)
                {
                    // skip test: arg values length != expected parameter length
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMismatchedArgsTypeParameterCount,
                            types.Length,
                            provider.DisplayName,
                            expectedArity
                        )
                    ).ConfigureAwait(false);
                    args.IsValid = false;
                    continue;
                }

                if (sets != null)
                {
                    for (int i = 0; i < types.Length; ++i)
                    {
                        Type[] set = sets[i];
                        if (Array.IndexOf(set, types[i]) < 0)
                            goto notAlreadyUsed;
                    }

                    // args value already represented by set.
                    continue;

                    notAlreadyUsed: ;
                }

                bool hasDuplicate = false;
                for (int i = 0; i < ti; ++i)
                {
                    if (!typeArgs[i].IsValid)
                        continue;

                    Type[] values = (Type[])typeArgs[i].Values!;
                    for (int j = 0; j < types.Length; ++j)
                    {
                        Type t1 = types[j], t2 = values[i];
                        if (t1 != t2)
                            goto notDuplicate;
                    }

                    // duplicate arg list
                    hasDuplicate = true;
                    continue;

                    notDuplicate: ;
                }

                if (hasDuplicate)
                    continue;

                anyArgs = true;
                if (!isExpandingTypeParams)
                    await ExpandComplexTest(parentArgs!, types, expansionFactor).ConfigureAwait(false);
                else
                    await ExpandComplexTest(types, _test, _test.Method.IsGenericMethodDefinition, expansionFactor, arity).ConfigureAwait(false);
            }
        }

        if (!anyArgs && !hasSetParams)
        {
            await _logger.LogErrorAsync(
                string.Format(
                    Properties.Resources.LogErrorParametersMissingValues,
                    _test.DisplayName,
                    _testType.FullName,
                    RangeHelper.MaxTestVariations
                )
            ).ConfigureAwait(false);
        }
    }

    private static int GetArity(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition)
            return 0;

        try
        {
            return method.GetGenericArguments().Length;
        }
        catch (NotSupportedException)
        {
            return 0;
        }
    }


    private static Type[] Distinctify(Type[] set)
    {
        int unique = 0;
        for (int i = 0; i < set.Length; ++i)
        {
            Type t = set[i];
            for (int j = i + 1; j < set.Length; ++j)
            {
                if (t == set[j])
                    goto notUnique;
            }

            ++unique;
            notUnique:;
        }

        if (unique == set.Length)
            return set;

        Type[] newSet = new Type[unique];
        unique = -1;
        for (int i = 0; i < set.Length; ++i)
        {
            Type t = set[i];
            for (int j = i + 1; j < set.Length; ++j)
            {
                if (t == set[j])
                    goto notUnique;
            }

            newSet[++unique] = t;
            notUnique:;
        }

        return newSet;
    }

    private async Task ExpandComplexTest(Type[] typeArguments, Type[] methodTypeArguments, ulong expansionFactor)
    {
        if (TreeFilter != null)
        {
            if (!FilterHelper.TreeCouldContainThisTypeConfiguration(_test, typeArguments, methodTypeArguments, null, TreeFilter.TreePath))
                return;
        }

        _managedMethod = _test.ManagedMethod;
        if (typeArguments.Length == 0)
        {
            if (methodTypeArguments.Length == 0)
            {
                _testMethodInstance = _test.Method;
            }
            else
            {
                try
                {
                    _testMethodInstance = _test.Method.MakeGenericMethod(methodTypeArguments);
                }
                catch (ArgumentException)
                {
                    _testMethodInstance = null;
                }
            }
        }
        else if (MethodBase.GetMethodFromHandle(_test.Method.MethodHandle, _testTypeInstance.TypeHandle) is MethodInfo mtd)
        {
            if (methodTypeArguments.Length == 0)
            {
                _testMethodInstance = mtd;
            }
            else
            {
                try
                {
                    _testMethodInstance = mtd.MakeGenericMethod(methodTypeArguments);
                }
                catch (ArgumentException)
                {
                    _testMethodInstance = null;
                }
            }
        }
        else
        {
            _testMethodInstance = null;
        }

        if (_testMethodInstance == null)
        {
            await _logger.LogErrorAsync(
                string.Format(
                    Properties.Resources.LogErrorGenericConstraints,
                    methodTypeArguments.Length == 1
                        ? methodTypeArguments[0].FullName
                        : $"<{string.Join(", ", methodTypeArguments.Select(ManagedIdentifier.GetManagedType))}>",
                    _test.DisplayName
                )
            ).ConfigureAwait(false);
            return;
        }

        _parameters = _testMethodInstance.GetParameters();

        int startIndex = _instances.Count;

        if (_test.Parameters.Length == 0)
        {
            int argHash = UnturnedTestInstance.CalculateArgumentHash(typeArguments, methodTypeArguments, Array.Empty<object>());

            AddInstance(Array.Empty<object>(), startIndex, argHash, typeArguments, methodTypeArguments);
            return;
        }

        // range and set parameters
        ParameterValuesInfo[] infos = new ParameterValuesInfo[_test.Parameters.Length];
        bool hasAnySetsOrRanges = false;
        for (int i = 0; i < _test.Parameters.Length; ++i)
        {
            ParameterValuesInfo info = default;
            UnturnedTestParameter p = _test.Parameters[i];

            string? setFrom = null;

            if (p is IUnturnedTestRangeParameter rangeParam)
            {
                hasAnySetsOrRanges = true;
                info.Values = RangeHelper.GetRangeValues(rangeParam);
                UnturnedTestSetParameterInfo set = rangeParam.SetParameterInfo;
                if (set.Values != null)
                {
                    info.AdditionalValues = set.Values;
                }
                else if (set.From != null)
                {
                    setFrom = set.From;
                }
            }
            else if (p is UnturnedTestSetParameter setParam)
            {
                hasAnySetsOrRanges = true;
                if (setParam.From != null)
                    setFrom = setParam.From;
                else
                    info.Values = setParam.Values;
            }
            else
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(setFrom))
            {
                Array array;
                MemberInfo? member = GeneratedTestExpansionHelper.GetMember(setFrom!, _testTypeInstance, out _);

                if (member == null)
                {
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMissingFromMember,
                            setFrom,
                            _testTypeInstance.FullName
                        )
                    ).ConfigureAwait(false);
                    break;
                }

                object? value = await InvokeFromMember(member).ConfigureAwait(false);
                if (value is IEnumerable enumerable)
                {
                    Type? allType = null;
                    ArrayList list = new ArrayList(value is ICollection collection ? collection.Count : 16);
                    foreach (object element in enumerable)
                    {
                        if (list.Count == 0)
                            allType = element.GetType();
                        else if (allType != null && !allType.IsInstanceOfType(element))
                            allType = null;
                        list.Add(element);
                    }

                    array = allType != null ? list.ToArray(allType) : list.ToArray();
                }
                else if (value != null)
                {
                    array = Array.CreateInstance(value.GetType(), 1);
                    array.SetValue(value, 0);
                }
                else
                {
                    array = new object[1];
                }

                if (info.Values == null)
                    info.Values = array;
                else
                    info.AdditionalValues = array;
            }

            info.Distinctify();
            infos[i] = info;
        }

        ulong variationCount = 1;
        for (int i = 0; i < infos.Length && variationCount != 0; ++i)
        {
            variationCount *= infos[i].UniqueCount;
        }

        if (variationCount * expansionFactor is > 0 and <= RangeHelper.MaxTestVariations)
        {
            int variationCountFixed = checked( (int)variationCount );

            int newCt = _instances.Count + variationCountFixed;
            if (_instances.Capacity < newCt)
                _instances.Capacity = newCt;

            bool reachedEnd = false;
            do
            {
                for (int p = infos.Length - 1; p >= 0; --p)
                {
                    ref ParameterValuesInfo info = ref infos[p];
                    if ((uint)info.Index < info.UniqueCount - 1u)
                    {
                        ++info.Index;
                        break;
                    }

                    info.Index = 0;
                    if (p != 0)
                        continue;

                    reachedEnd = true;
                    break;
                }

                object[] args = new object[infos.Length];
                for (int p = 0; p < infos.Length; ++p)
                {
                    ref ParameterValuesInfo info = ref infos[p];
                    args[p] = info.Values!.GetValue(info.Index);
                }

                int argHash = UnturnedTestInstance.CalculateArgumentHash(typeArguments, methodTypeArguments, args);

                AddInstance(args, startIndex, argHash, typeArguments, methodTypeArguments);
            } while (!reachedEnd);
        }
        else if (hasAnySetsOrRanges)
        {
            await _logger.LogErrorAsync(
                string.Format(
                    Properties.Resources.LogErrorParametersMissingValues,
                    _test.DisplayName,
                    _testTypeInstance.FullName,
                    RangeHelper.MaxTestVariations
                )
            ).ConfigureAwait(false);
        }

        // arg lists
        foreach (UnturnedTestArgs argList in _test.Args)
        {
            if (argList.Values != null)
            {
                if (argList.Values.Length != _parameters.Length)
                {
                    // skip test: arg values length != expected parameter length
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMismatchedArgsParameterCount,
                            argList.Values.Length,
                            _test.DisplayName,
                            _parameters.Length
                        )
                    ).ConfigureAwait(false);
                    continue;
                }

                object[] args = new object[_parameters.Length];
                bool anyErrors = false;
                for (int i = 0; i < _parameters.Length; ++i)
                {
                    object? value = argList.Values.GetValue(i);
                    ParameterInfo param = _parameters[i];
                    if (value == null)
                    {
                        if (!param.ParameterType.IsValueType)
                        {
                            //args[i] = default; (already set to default on array init)
                            continue;
                        }

                        // skip test: null value type
                        anyErrors = true;
                        await _logger.LogErrorAsync(
                            string.Format(
                                Properties.Resources.LogErrorMismatchedParameterType,
                                "null",
                                param.Name,
                                _test.DisplayName
                            )
                        ).ConfigureAwait(false);
                        break;
                    }

                    if (!param.ParameterType.IsInstanceOfType(value))
                    {
                        try
                        {
                            value = Convert.ChangeType(value, param.ParameterType, CultureInfo.InvariantCulture);
                        }
                        catch (Exception ex)
                        {
                            // skip test: mismatched parameter type
                            anyErrors = true;
                            await _logger.LogErrorAsync(
                                string.Format(
                                    Properties.Resources.LogErrorMismatchedParameterType,
                                    $"{{{TestDisplayNameFormatter.FormatTestParameterValue(value)}}}",
                                    param.Name,
                                    _test.DisplayName
                                ),
                                ex
                            ).ConfigureAwait(false);
                            break;
                        }
                    }

                    args[i] = value;
                }

                if (anyErrors)
                    continue;

                AddInstanceIfNotExists(args, startIndex, typeArguments, methodTypeArguments);
            }
            else if (!string.IsNullOrWhiteSpace(argList.From))
            {
                MemberInfo? member = GeneratedTestExpansionHelper.GetMember(argList.From!, _testTypeInstance, out _);

                if (member == null)
                {
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMissingFromMember,
                            argList.From,
                            _testTypeInstance.FullName
                        )
                    ).ConfigureAwait(false);
                    continue;
                }

                object? value = await InvokeFromMember(member).ConfigureAwait(false);
                ParameterInfo[] parameters = _testMethodInstance.GetParameters();
                if (value is IEnumerable enumerable)
                {
                    foreach (object argListValue in enumerable)
                    {
                        if (!AnonymousTypeHelper.TryMapObjectToMethodParameters(
                                argListValue, _testMethodInstance, member, parameters, out object[] args
                            ))
                        {
                            continue;
                        }

                        AddInstanceIfNotExists(args, startIndex, typeArguments, methodTypeArguments);
                    }
                }
                else if (value != null)
                {
                    if (!AnonymousTypeHelper.TryMapObjectToMethodParameters(
                            value, _testMethodInstance, member, parameters, out object[] args
                        ))
                    {
                        continue;
                    }

                    AddInstanceIfNotExists(args, startIndex, typeArguments, methodTypeArguments);
                }
            }
        }
    }

    private void AddInstanceIfNotExists(object[] args, int startIndex, Type[] typeArgs, Type[] methodArgs)
    {
        if (InstanceAlreadyExists(args, startIndex, out int argHash, typeArgs, methodArgs))
            return;

        AddInstance(args, startIndex, argHash, typeArgs, methodArgs);
    }

    private bool AddInstance(object[] args, int startIndex, int argHash, Type[] typeArgs, Type[] methodArgs)
    {
        UnturnedTestInstance instance = new UnturnedTestInstance(
            this,
            _test,
            _testTypeInstance,
            _testMethodInstance,
            _managedType,
            _managedMethod,
            args,
            _instances.Count - startIndex,
            argHash,
            typeArgs,
            methodArgs
        );

        if (TreeFilter != null && !TreeFilter.MatchesTreePathFilter(in instance, _treeFilterNeedsPropertyBag))
            return false;

        _instances.Add(instance);
        return true;
    }

    private struct ParameterValuesInfo
    {
        public Array? Values;
        public Array? AdditionalValues;
        public ulong UniqueCount;
        public int Index;

        public void Distinctify()
        {
            if (Values == null)
            {
                if (AdditionalValues == null)
                    UniqueCount = 0;
                else
                {
                    Values = AdditionalValues;
                    AdditionalValues = null;
                    UniqueCount = (ulong)Values.Length;
                }
                
                return;
            }

            if (AdditionalValues == null)
            {
                UniqueCount = (ulong)Values.LongLength;
                return;
            }

            Array a1 = Values;
            Array a2 = AdditionalValues;
            long unique = Values.LongLength;
            for (int i = 0; i < a2.Length; ++i)
            {
                if (Array.IndexOf(a1, a2.GetValue(i)) >= 0)
                    continue;
                ++unique;
            }

            Type elementType = a1.GetType().GetElementType()!;
            bool areSameElementType = elementType == a2.GetType().GetElementType()!;
            Array a3;
            if (unique <= int.MaxValue)
            {
                a3 = Array.CreateInstance(areSameElementType ? elementType : typeof(object), (int)unique);
            }
            else
            {
                a3 = Array.CreateInstance(areSameElementType ? elementType : typeof(object), unique);
            }

            long index = a1.LongLength;
            if (areSameElementType)
                Array.Copy(a1, a3, a1.LongLength);
            else
            {
                for (long i = 0; i < index; ++i)
                {
                    a3.SetValue(a1.GetValue(i), i);
                }
            }

            if (areSameElementType && unique == index + a2.Length)
            {
                Array.Copy(a2, 0, a3, index, a2.Length);
            }
            else
            {
                for (int i = 0; i < a2.Length; ++i)
                {
                    object value = a2.GetValue(i);
                    if (Array.IndexOf(a1, value) >= 0)
                        continue;
                    a3.SetValue(value, index);
                    ++index;
                }
            }

            Values = a3;
            AdditionalValues = null;
            UniqueCount = (ulong)a3.LongLength;
        }
    }

    private bool InstanceAlreadyExists(object[] args, int startIndex, out int argHash, Type[] typeArguments, Type[] methodTypeArguments)
    {
        argHash = UnturnedTestInstance.CalculateArgumentHash(typeArguments, methodTypeArguments, args);
        for (int i = startIndex; i < _instances.Count; ++i)
        {
            UnturnedTestInstance inst = _instances[i];
            if (inst.Arguments.Length != args.Length || inst.ArgHash != argHash)
                continue;

            bool sequenceEquals = true;
            for (int a = 0; a < args.Length; ++a)
            {
                if (Equals(inst.Arguments[a], args[a]))
                    continue;

                sequenceEquals = false;
                break;
            }

            if (sequenceEquals)
                return true;
        }

        return false;
    }

    private Task<object?> InvokeFromMember(MemberInfo member)
    {
        if (member == null)
            throw new ArgumentNullException(nameof(member));

        object? returnValue;
        try
        {
            switch (member)
            {
                case FieldInfo field:
                    if (field.IsStatic)
                        returnValue = field.GetValue(null);
                    else if (_runner.Value != null)
                        returnValue = field.GetValue(_runner.Value);
                    else
                        returnValue = null;
                    break;

                case PropertyInfo property:
                    if (property.GetMethod.IsStatic)
                        returnValue = property.GetValue(null);
                    else if (_runner.Value != null)
                        returnValue = property.GetValue(_runner.Value);
                    else
                        returnValue = null;
                    break;

                case MethodInfo method:

                    object[] args = method.GetParameters().Length == 1 ? [ _token ] : Array.Empty<object>();

                    if (method.IsStatic)
                        returnValue = method.Invoke(null, args);
                    else if (_runner.Value != null)
                        returnValue = method.Invoke(_runner.Value, args);
                    else
                        returnValue = null;
                    break;

                default:
                    throw new Exception("Unreachable");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(string.Format(Properties.Resources.LogErrorReadingFromMember, member.Name, _testType.FullName), ex);
            return Task.FromResult<object?>(null);
        }

        return TaskAwaitableHelper.CreateTaskFromReturnValue(returnValue);
    }

    internal void GetNames(in UnturnedTestInstance test, out string uid, out string displayName, out string treePath)
    {
        if (!test.Test.Expandable)
        {
            uid = test.Test.Uid;
            treePath = test.Test.TreePath;
        }
        else
        {
            bool useIndex = !UnturnedTestUid.TryFormatParameters(test.Arguments, out string argList);

            string? typeArgs = null;

            if (test.MethodTypeArgs.Length != 0)
            {
                typeArgs = UnturnedTestUid.FormatTypeParameters(test.MethodTypeArgs);
            }

            UnturnedTestUid tUid = UnturnedTestUid.Create(
                test.ManagedType,
                test.ManagedMethod,
                test.HasParameters ? test.Index : -1,
                typeArgs,
                useIndex ? null : argList
            );

            uid = tUid.Uid;

            StringBuilder sb = StringBuilderPool.Rent().Append(test.Test.TreePath);
            TreeNodeFilterWriter writer = new TreeNodeFilterWriter(sb);
            if (test.TypeArgs.Length > 0)
            {
                foreach (Type type in test.TypeArgs)
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(type));
            }

            if (test.MethodTypeArgs.Length > 0)
            {
                writer.WriteSeparator();
                foreach (Type type in test.MethodTypeArgs)
                    writer.WriteGenericParameter(ManagedIdentifier.GetManagedType(type));
            }

            if (test.HasParameters)
            {
                writer.WriteSeparator();
                sb.Append(argList);
                object?[] args = test.Arguments;
                for (int i = 0; i < args.Length; ++i)
                {
                    writer.WriteParameterValue(
                        ref args[i],
                        static (ref object? arg, StringBuilder sb) => UnturnedTestUid.WriteParameterForTreeNode(arg, sb)
                    );
                }
            }

            treePath = sb.ToString();
            StringBuilderPool.Return(sb);
        }
        
        displayName = TestDisplayNameFormatter.GetTestDisplayName(in test);
    }
}