using Microsoft.Testing.Platform.Logging;
using System.Reflection;

namespace uTest.Runner;

internal class GeneratedTestRegistrationList : ITestRegistrationList
{
    private readonly Assembly _assembly;

    public GeneratedTestRegistrationList(Assembly assembly)
    {
        _assembly = assembly;
    }

    /// <inheritdoc />
    public Task<List<UnturnedTest>> GetTestsAsync(Microsoft.Testing.Platform.Logging.ILogger logger, CancellationToken token = default)
    {
        object[] arr = _assembly.GetCustomAttributes(typeof(GeneratedTestBuilderAttribute), false);
        if (arr.Length == 0)
            return Task.FromResult(new List<UnturnedTest>(0));

        List<Exception>? exceptions = null;

        GeneratedTestBuilder builder = new GeneratedTestBuilder(new List<UnturnedTest>());

        for (int i = 0; i < arr.Length; ++i)
        {
            if (arr[i] is not GeneratedTestBuilderAttribute attr)
                continue;

            Type type = attr.Type;
            if (!typeof(IGeneratedTestProvider).IsAssignableFrom(type) || type.IsAbstract)
                continue;

            IGeneratedTestProvider? provider = null;
            try
            {
                ConstructorInfo? ctor = type.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    CallingConventions.Any,
                    Type.EmptyTypes,
                    null
                );

                if (ctor == null)
                    continue;

                provider = (IGeneratedTestProvider)ctor.Invoke(Array.Empty<object>());
                provider.Build(builder);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                exceptions ??= new List<Exception> { ex.InnerException };
            }
            catch (Exception ex)
            {
                exceptions ??= new List<Exception> { ex };
            }
            finally
            {
                if (provider is IDisposable disp)
                    disp.Dispose();
            }
        }

        if (exceptions != null)
        {
            if (exceptions.Count == 1)
                throw exceptions[0];
            
            throw new AggregateException(exceptions);
        }

        return Task.FromResult(builder.Tests);
    }

    public async Task<List<UnturnedTestInstance>> ExpandTestsAsync(Microsoft.Testing.Platform.Logging.ILogger logger, List<UnturnedTest> originalTests, CancellationToken token = default)
    {
        List<UnturnedTestInstance> instances = new List<UnturnedTestInstance>();

        Dictionary<Type, object?> runners = new Dictionary<Type, object?>();

        foreach (UnturnedTest test in originalTests)
        {
            Type testType = test.Method.DeclaringType!;

            Lazy<object?> runner = new Lazy<object?>(() =>
            {
                if (runners.TryGetValue(testType, out object? obj))
                    return obj;

                object? instance;
                try
                {
                    instance = Activator.CreateInstance(testType, true);
                }
                catch (Exception ex)
                {
                    logger.LogError(string.Format(Properties.Resources.LogErrorCreatingRunner, testType.FullName), ex);
                    instance = null;
                }

                runners.Add(testType, instance);
                return instance;
            });

            if (test.Args is { Length: > 0 })
            {
                foreach (UnturnedTestArgs args in test.Args)
                {
                    if (args.From != null)
                    {
                        MemberInfo? member = GetMember(args.From, testType);
                        if (member == null)
                        {
                            await logger.LogInformationAsync(string.Format(
                                Properties.Resources.LogErrorMissingFromMember, args.From, testType.FullName)
                            );
                            continue;
                        }

                        object? value = InvokeFromMember(runner, member, logger, testType);

                    }
                    else if (args.Values != null)
                    {

                    }
                }
            }
        }

        return instances;
    }

    private object? InvokeFromMember(Lazy<object?> instance, MemberInfo member, Microsoft.Testing.Platform.Logging.ILogger logger, Type type)
    {
        try
        {
            switch (member)
            {
                case FieldInfo field:
                    if (field.IsStatic)
                        return field.GetValue(null);
                    if (instance.Value != null)
                        return field.GetValue(instance.Value);
                    return null;

                case PropertyInfo property:
                    if (property.GetMethod.IsStatic)
                        return property.GetValue(null);
                    if (instance.Value != null)
                        return property.GetValue(instance.Value);
                    return null;

                case MethodInfo method:
                    if (method.IsStatic)
                        return method.Invoke(null, Array.Empty<object>());
                    if (instance.Value != null)
                        return method.Invoke(instance.Value, Array.Empty<object>());
                    return null;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(string.Format(Properties.Resources.LogErrorReadingFromMember, member.Name, type.FullName), ex);
        }

        return null;
    }

    private static MemberInfo? GetMember(string name, Type type)
    {
        const BindingFlags flags = BindingFlags.Public
                                   | BindingFlags.NonPublic
                                   | BindingFlags.Instance
                                   | BindingFlags.Static
                                   | BindingFlags.FlattenHierarchy;

        FieldInfo? field = type.GetField(name, flags);
        if (field != null)
            return field;

        try
        {
            PropertyInfo? property = type.GetProperty(name, flags);
            if (property != null && property.CanRead)
                return property;
        }
        catch (AmbiguousMatchException) { }

        try
        {
            MethodInfo? method = type.GetMethod(name, flags, null, CallingConventions.Any, Type.EmptyTypes, null);
            if (method != null)
                return method;
        }
        catch (AmbiguousMatchException) { }

        return null;
    }
}