using System.Reflection;
using System.Runtime.ExceptionServices;
using IMTPLogger = Microsoft.Testing.Platform.Logging.ILogger;

namespace uTest.Runner;

internal class GeneratedTestRegistrationList : ITestRegistrationList
{
    private readonly Assembly _assembly;

    public GeneratedTestRegistrationList(Assembly assembly)
    {
        _assembly = assembly;
    }

    /// <inheritdoc />
    public Task<List<UnturnedTest>> GetTestsAsync(IMTPLogger logger, CancellationToken token = default)
    {
        object[] arr = _assembly.GetCustomAttributes(typeof(GeneratedTestBuilderAttribute), false);
        if (arr.Length == 0)
            return Task.FromResult(new List<UnturnedTest>(0));

        List<Exception>? exceptions = null;

        List<UnturnedTest> testList = new List<UnturnedTest>();

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

                GeneratedTestBuilder builder = new GeneratedTestBuilder(testList, attr.TestType);

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
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            
            throw new AggregateException(exceptions);
        }

        return Task.FromResult(testList);
    }

    public Task<List<UnturnedTestInstance>> ExpandTestsAsync(IMTPLogger logger, List<UnturnedTest> originalTests, CancellationToken token = default)
    {
        return GeneratedTestExpansionHelper.ExpandTestsAsync(logger, originalTests, token);
    }
}