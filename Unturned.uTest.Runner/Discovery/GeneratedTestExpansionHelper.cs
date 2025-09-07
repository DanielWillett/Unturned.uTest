using Microsoft.Testing.Platform.Logging;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using uTest.Runner.Util;
using IMTPLogger = Microsoft.Testing.Platform.Logging.ILogger;

namespace uTest.Runner;

internal static class GeneratedTestExpansionHelper
{
    private static Type[]? _asyncMethodFromArgTypes;

    public static async Task<List<UnturnedTestInstance>> ExpandTestsAsync(IMTPLogger logger, List<UnturnedTest> originalTests, CancellationToken token)
    {
        TestExpandProcessor processor = new TestExpandProcessor(logger, originalTests, token);

        return await processor.ExpandTestsAsync().ConfigureAwait(false);
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
    private readonly IMTPLogger _logger;
    private readonly List<UnturnedTest> _originalTests;
    private readonly Dictionary<Type, object?> _runners;
    private readonly CancellationToken _token;
    private readonly StringBuilder _stringBuilder;

    private readonly List<UnturnedTestInstance> _instances;

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
    public TestExpandProcessor(IMTPLogger logger, List<UnturnedTest> originalTests, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        _logger = logger;
        _originalTests = originalTests;
        _token = token;

        _runners = new Dictionary<Type, object?>();
        _instances = new List<UnturnedTestInstance>();

        _stringBuilder = new StringBuilder(128);
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

    private static readonly int DefaultArgHash = UnturnedTestInstance.CalculateArgumentHash(
        Type.EmptyTypes,
        Type.EmptyTypes,
        Array.Empty<object>()
    );

    public ValueTask ExpandTestAsync(UnturnedTest test)
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

        // basic 0-arg test
        if (_parameters.Length == 0 && _methodGenericArguments.Length == 0 && _typeGenericArguments.Length == 0)
        {
            _instances.Add(new UnturnedTestInstance(
                this,
                test,
                _testType,
                test.Method,
                test.ManagedType,
                test.ManagedMethod,
                Array.Empty<object>(),
                index: 0,
                argHash: DefaultArgHash,
                Type.EmptyTypes,
                Type.EmptyTypes)
            );
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

        if (!isExpandingTypeParams)
        {
            if (parentArgs!.Length == 0)
            {
                _managedType = _test.ManagedType;
                _testTypeInstance = _testType;
            }
            else
            {
                try
                {
                    _testTypeInstance = _testType.MakeGenericType(parentArgs);
                    _managedType = ManagedIdentifier.GetManagedType(_testTypeInstance);
                }
                catch (ArgumentException)
                {
                    _testTypeInstance = null;
                }
            }

            if (_testTypeInstance == null)
            {
                await _logger.LogErrorAsync(
                    string.Format(
                        Properties.Resources.LogErrorGenericConstraints,
                        parentArgs.Length == 1
                            ? parentArgs[0].FullName
                            : $"<{string.Join(", ", parentArgs.Select(ManagedIdentifier.GetManagedType))}>",
                        _test.DisplayName
                    )
                ).ConfigureAwait(false);
                return;
            }

            CreateRunner();
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
        if (typeArguments.Length == 0)
        {
            _managedMethod = _test.ManagedMethod;
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
            _managedMethod = ManagedIdentifier.GetManagedMethod(mtd);
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

    private void AddInstance(object[] args, int startIndex, int argHash, Type[] typeArgs, Type[] methodArgs)
    {
        _instances.Add(
            new UnturnedTestInstance(
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
            )
        );
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

    internal void GetNames(in UnturnedTestInstance test, out string uid, out string displayName)
    {
        if (test.Arguments.Length == 0 && test.TypeArgs.Length == 0 && test.MethodTypeArgs.Length == 0)
        {
            uid = test.Test.Uid;
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
                typeArgs,
                useIndex ? null : argList,
                useIndex ? test.Index : null
            );

            uid = tUid.Uid;
        }
        
        displayName = TestDisplayNameFormatter.GetTestDisplayName(in test);
    }

}