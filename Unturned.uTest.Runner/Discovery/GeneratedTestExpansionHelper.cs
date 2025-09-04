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

file class TestExpandProcessor
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
    private Lazy<object> _runner;
    private ParameterInfo[] _parameters;
    private Type[] _genericArguments;

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
        await _logger.LogInformationAsync($"Expanded {_originalTests.Count} tests to {_instances.Count} in {sw.Elapsed.Milliseconds} ms.");

        return _instances;
    }

    private static readonly int DefaultArgHash = UnturnedTestInstance.CalculateArgumentHash(
        Array.Empty<UnturnedTestArgument>(),
        Array.Empty<UnturnedTestArgument>()
    );

    public ValueTask ExpandTestAsync(UnturnedTest test)
    {
        _test = test;
        _testType = test.Method.DeclaringType!;
        _parameters = test.Method.GetParameters();
        _genericArguments = test.Method.GetGenericArguments();
        _runner = new Lazy<object?>(() =>
        {
            if (_runners.TryGetValue(_testType, out object? obj))
                return obj;

            object? instance;
            try
            {
                instance = Activator.CreateInstance(_testType, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(string.Format(Properties.Resources.LogErrorCreatingRunner, _testType.FullName), ex);
                instance = null;
            }

            _runners.Add(_testType, instance);
            return instance;
        }, LazyThreadSafetyMode.None);

        // basic 0-arg test
        if (_parameters.Length == 0 && _genericArguments.Length == 0)
        {
            _instances.Add(new UnturnedTestInstance(
                test,
                Array.Empty<UnturnedTestArgument>(),
                Array.Empty<UnturnedTestArgument>(),
                test.Uid,
                test.DisplayName,
                index: 0,
                argHash: DefaultArgHash)
            );
            return default;
        }

        return new ValueTask(ExpandComplexTest(Array.Empty<UnturnedTestArgument>()));
    }

    private async Task ExpandComplexTest(UnturnedTestArgument[] typeArguments)
    {
        int startIndex = _instances.Count;

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
                MemberInfo? member = GeneratedTestExpansionHelper.GetMember(setFrom!, _testType, out _);

                if (member == null)
                {
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMissingFromMember,
                            setFrom,
                            _testType.FullName
                        )
                    );
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

        if (variationCount is > 0 and <= RangeHelper.MaxTestVariations)
        {
            int variationCountFixed = checked((int)variationCount);

            if (_instances.Capacity < variationCountFixed)
                _instances.Capacity = variationCountFixed;

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

                UnturnedTestArgument[] args = new UnturnedTestArgument[infos.Length];
                for (int p = 0; p < infos.Length; ++p)
                {
                    ref ParameterValuesInfo info = ref infos[p];
                    args[p] = new UnturnedTestArgument(info.Values!.GetValue(info.Index));
                }

                int argHash = UnturnedTestInstance.CalculateArgumentHash(typeArguments, args);

                CreateTestNames(args, out string uid, out string displayName);
                _instances.Add(new UnturnedTestInstance(_test, typeArguments, args, uid, displayName, _instances.Count - startIndex, argHash));
            } while (!reachedEnd);
        }
        else if (hasAnySetsOrRanges)
        {
            await _logger.LogErrorAsync(
                string.Format(
                    Properties.Resources.LogErrorParametersMissingValues,
                    _test.DisplayName,
                    _testType.FullName,
                    RangeHelper.MaxTestVariations
                )
            );
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
                    );
                    continue;
                }

                UnturnedTestArgument[] args = new UnturnedTestArgument[_parameters.Length];
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
                        );
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
                                    $"{{{Format(value)}}}",
                                    param.Name,
                                    _test.DisplayName
                                ),
                                ex
                            );
                            break;
                        }
                    }

                    args[i] = new UnturnedTestArgument(value);
                }

                if (anyErrors || InstanceAlreadyExists(typeArguments, args, startIndex, out int argHash))
                    continue;

                CreateTestNames(args, out string uid, out string displayName);
                _instances.Add(new UnturnedTestInstance(_test, typeArguments, args, uid, displayName, _instances.Count - startIndex, argHash));
            }
            else if (!string.IsNullOrWhiteSpace(argList.From))
            {
                MemberInfo? member = GeneratedTestExpansionHelper.GetMember(argList.From!, _testType, out _);

                if (member == null)
                {
                    await _logger.LogErrorAsync(
                        string.Format(
                            Properties.Resources.LogErrorMissingFromMember,
                            argList.From,
                            _testType.FullName
                        )
                    );
                    continue;
                }

                object? value = await InvokeFromMember(member).ConfigureAwait(false);
                ParameterInfo[] parameters = _test.Method.GetParameters();
                if (value is IEnumerable enumerable)
                {
                    foreach (object argListValue in enumerable)
                    {
                        if (!AnonymousTypeHelper.TryMapObjectToMethodParameters(
                                argListValue, _test.Method, member, parameters, out UnturnedTestArgument[] args
                            ))
                        {
                            continue;
                        }

                        if (InstanceAlreadyExists(typeArguments, args, startIndex, out int argHash))
                            continue;

                        CreateTestNames(args, out string uid, out string displayName);
                        _instances.Add(new UnturnedTestInstance(_test, typeArguments, args, uid, displayName, _instances.Count - startIndex, argHash));
                    }
                }
                else if (value != null)
                {
                    if (!AnonymousTypeHelper.TryMapObjectToMethodParameters(
                            value, _test.Method, member, parameters, out UnturnedTestArgument[] args
                        ))
                    {
                        continue;
                    }

                    if (InstanceAlreadyExists(typeArguments, args, startIndex, out int argHash))
                        continue;

                    CreateTestNames(args, out string uid, out string displayName);
                    _instances.Add(new UnturnedTestInstance(_test, typeArguments, args, uid, displayName, _instances.Count - startIndex, argHash));
                }
            }
        }
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

    private void CreateTestNames(UnturnedTestArgument[] args, out string uid, out string displayName)
    {
        if (args.Length == 0)
        {
            uid = _test.Uid;
            displayName = _test.DisplayName;
            return;
        }

        _stringBuilder.Clear().Append(_test.Uid).Append(" { ");
        for (int i = 0; i < args.Length; ++i)
        {
            string paramName = _parameters[i].Name;
            if (i != 0)
                _stringBuilder.Append(", ");
            _stringBuilder.Append(paramName).Append(" = ").Append(Format(args[i].Value));
        }

        _stringBuilder.Append(" }");

        uid = _stringBuilder.ToString();

        _stringBuilder.Clear()
            .Append(_test.ManagedType)
            .Append('.').Append(_test.Method.Name)
            .Append('(');
        for (int i = 0; i < args.Length; ++i)
        {
            if (i != 0)
                _stringBuilder.Append(", ");
            _stringBuilder.Append(Format(args[i].Value));
        }
        displayName = _stringBuilder.Append(')').ToString();
    }

    private bool InstanceAlreadyExists(UnturnedTestArgument[] typeArguments, UnturnedTestArgument[] args, int startIndex, out int argHash)
    {
        argHash = UnturnedTestInstance.CalculateArgumentHash(typeArguments, args);
        for (int i = startIndex; i < _instances.Count; ++i)
        {
            UnturnedTestInstance inst = _instances[i];
            if (inst.Arguments.Length != args.Length || inst.ArgHash != argHash)
                continue;

            bool sequenceEquals = true;
            for (int a = 0; a < args.Length; ++a)
            {
                ref UnturnedTestArgument a1 = ref inst.Arguments[a];
                ref UnturnedTestArgument a2 = ref args[a];
                if (a1.ValueEquals(in a2))
                    continue;

                sequenceEquals = false;
                break;
            }

            if (sequenceEquals)
                return true;
        }

        return false;
    }

    private static unsafe string Format(object? value)
    {
        switch (value)
        {
            case null:
                return "<null>";

            case string str:
                return $"\"{str}\"";
            
            case '\0':
                return @"'\0'";
            
            case char c when char.IsControl(c):
                return $"(char){((int)c).ToString(null, CultureInfo.InvariantCulture)}";
            
            case char c:
                char* span = stackalloc char[3];
                span[0] = '\'';
                span[1] = c;
                span[2] = '\'';
                return new string(span);

            case IFormattable f:
                return f.ToString(null, CultureInfo.InvariantCulture);

            default:
                return value.ToString();
        }
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
}