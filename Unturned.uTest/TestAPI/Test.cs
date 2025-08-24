using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace uTest;

/// <summary>
/// Manages the currently running test.
/// </summary>
[ExcludeFromCodeCoverage, DebuggerStepThrough]
public static class Test
{
    [DoesNotReturn]
    public static void Pass()
    {
        throw new TestResultException(TestResult.Pass);
    }

    [DoesNotReturn]
    public static void Pass(string message)
    {
        throw new TestResultException(TestResult.Pass, message);
    }

    [DoesNotReturn]
    public static void Fail()
    {
        throw new TestResultException(TestResult.Fail);
    }

    [DoesNotReturn]
    public static void Fail(string message)
    {
        throw new TestResultException(TestResult.Fail, message);
    }
}