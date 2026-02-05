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
    T CreateTestInstance<T>() where T : notnull;
}