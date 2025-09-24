using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

#pragma warning disable TPEXP

namespace uTest.Discovery;

internal class GeneratedTestRegistrationList : ITestRegistrationList
{
    private readonly Assembly _assembly;

    public GeneratedTestRegistrationList(Assembly assembly)
    {
        _assembly = assembly;
    }

    public async Task<List<UnturnedTestInstance>> GetMatchingTestsAsync(ILogger logger, ITestFilter? filter, CancellationToken token = default)
    {
        List<UnturnedTest> tests = GetPotentiallyMatchingTests(filter, token);
        return await ExpandTestsAsync(logger, tests, filter, token);
    }

    private List<UnturnedTest> GetPotentiallyMatchingTests(ITestFilter? filter, CancellationToken token)
    {
        object[] arr = _assembly.GetCustomAttributes(typeof(GeneratedTestBuilderAttribute), false);
        if (arr.Length == 0)
            return new List<UnturnedTest>(0);
        List<Exception>? exceptions = null;

        List<UnturnedTest> testList = new List<UnturnedTest>();

        for (int i = 0; i < arr.Length; ++i)
        {
            if (arr[i] is not GeneratedTestBuilderAttribute attr)
                continue;

            if (filter != null && !FilterHelper.PotentiallyMatchesFilter(attr.TestType, filter))
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

                int index = testList.Count;
                provider = (IGeneratedTestProvider)ctor.Invoke(Array.Empty<object>());
                provider.Build(builder);

                if (filter != null)
                {
                    FilterHelper.RemoveUnfilteredTests(filter, testList, index);
                }
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

        return testList;
    }

    /// <inheritdoc />
    public Task<List<UnturnedTest>> GetTestsAsync(ILogger logger, CancellationToken token = default)
    {
        return Task.FromResult(GetPotentiallyMatchingTests(null, token));
    }

    public Task<List<UnturnedTestInstance>> ExpandTestsAsync(ILogger logger, List<UnturnedTest> originalTests, ITestFilter? filter, CancellationToken token = default)
    {
        return GeneratedTestExpansionHelper.ExpandTestsAsync(logger, originalTests, filter, token);
    }
}