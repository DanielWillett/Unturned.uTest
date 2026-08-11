using System;

namespace uTest.Compat.DependencyInjection;

/// <summary>
/// Implemented by a framework to override how test runner objects are created.
/// </summary>
/// <remarks>For example, a framework may want to allow service injection. Only the highest priority integration will be used.</remarks>
public interface ITestRunnerActivator
{
    /// <summary>
    /// Activators with higher numbers will be preferred over activators with lower numbers.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Creates an instance of a test runner object.
    /// </summary>
    /// <returns>The newly created object. This should NEVER return <see langword="null"/>.</returns>
    TestInstance<T> CreateTestInstance<T>() where T : notnull;
}

/// <summary>
/// Value returned by <see cref="ITestRunnerActivator"/> that keeps track of a test instance and functionality to clean up after it when the test completes.
/// </summary>
public struct TestInstance<T> where T : notnull
{
    /// <summary>
    /// The class instance to run the test on.
    /// </summary>
    public T Instance { get; }

    /// <summary>
    /// Optional disposable cleanup to run afterwards.
    /// </summary>
    public IDisposable? Cleanup { get; }

    /// <summary>
    /// Create a new <see cref="TestInstance{T}"/> value.
    /// </summary>
    public TestInstance(T instance, IDisposable? cleanup)
    {
        Instance = instance;
        Cleanup = cleanup;
    }
}