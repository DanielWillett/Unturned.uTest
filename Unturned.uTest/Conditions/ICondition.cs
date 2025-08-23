using System;

namespace uTest;

/// <summary>
/// A condition that must be met for an assertion to pass.
/// </summary>
public interface ICondition
{
    /// <summary>
    /// Evaluates whether or not a condition is met, throwing an exception if it isn't met.
    /// </summary>
    /// <exception cref="TestResultException"/>
    /// <exception cref="InvalidOperationException"/>
    void Evaluate();
}