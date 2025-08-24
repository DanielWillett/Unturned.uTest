using System;

namespace uTest;

internal static class TestAnnotations
{
    internal const AttributeTargets Targets = AttributeTargets.Method
                                            | AttributeTargets.Class
                                            | AttributeTargets.Struct
                                            | AttributeTargets.Module
                                            | AttributeTargets.Interface
                                            | AttributeTargets.Assembly;
}

/// <summary>
/// Indicates that this test does not need to be in Unturned to be ran, allowing the test executer to run it without spinning up a server.
/// </summary>
/// <remarks>This test should be able to be ran in .NET or .NET Framework without Unity running. UniTask can not be used in these tests.</remarks>
[AttributeUsage(TestAnnotations.Targets)]
public sealed class InProcessAttribute : Attribute;