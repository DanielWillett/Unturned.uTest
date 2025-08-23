using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using System;
using System.Diagnostics.CodeAnalysis;

namespace uTest.Adapter;

public class BaseTestInterface
{
    private bool _init;

    protected IMessageLogger MessageLogger { get; private set; } = null!;

#pragma warning disable CS8774
    [MemberNotNull(nameof(MessageLogger))]
    private void AssertInitialized()
    {
        if (!_init)
            throw new InvalidOperationException();
    }
#pragma warning restore CS8774

    [MemberNotNull(nameof(MessageLogger))]
    protected void Init(IMessageLogger messageLogger)
    {
        MessageLogger = messageLogger;
        _init = true;
    }

    internal void Info(string str)
    {
        AssertInitialized();
        MessageLogger.SendMessage(TestMessageLevel.Informational, "[uTest] " + str);
    }

    internal void Warn(string str)
    {
        AssertInitialized();
        MessageLogger.SendMessage(TestMessageLevel.Warning, "[uTest] " + str);
    }

    internal void Error(string str)
    {
        AssertInitialized();
        MessageLogger.SendMessage(TestMessageLevel.Error, "[uTest] " + str);
    }
}
