using System;

namespace uTest;

/// <summary>
/// Defines the number of players that need to be online for this test.
/// </summary>
/// <param name="playerCount">The number of players that need to be online for this test.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public sealed class PlayerCountAttribute(int playerCount) : Attribute
{
    /// <summary>
    /// The number of players that need to be online for this test.
    /// </summary>
    public int PlayerCount { get; } = playerCount;
}