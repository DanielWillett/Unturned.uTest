using Microsoft.Testing.Extensions.TrxReport.Abstractions;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using System.Runtime.CompilerServices;
using uTest.Discovery;

namespace uTest.Runner;

/// <summary>
/// Used to hide references to M.T.E.TrxReport.Abstractions so the assembly isn't required.
/// </summary>
internal static class TrxSwitch
{
    /// <summary>
    /// True when the TRX abstractions DLL is available.
    /// </summary>
    internal static bool HasTrx { [MethodImpl(MethodImplOptions.NoInlining)] get; } = Type.GetType("Microsoft.Testing.Extensions.TrxReport.Abstractions.ITrxReportCapability, Microsoft.Testing.Extensions.TrxReport.Abstractions", throwOnError: false, ignoreCase: false) != null;

    internal static ITrxReportCapabilityImpl? CreateTrxCapability()
    {
        return !HasTrx ? null : CreateTrxIntl();
    }

    internal static ITrxReportCapabilityImpl? GetTrxCapability(ITestFrameworkCapabilities capabilities)
    {
        return !HasTrx ? null : GetTrxCapabilityIntl(capabilities);
    }

    internal static void AddTrxTestProperties(in UnturnedTestInstance instance, PropertyBag bag)
    {
        if (!HasTrx)
            return;

        AddTrxTestPropertiesIntl(in instance, bag);
    }

    internal static void AddTrxTestSummaryProperties(TestExecutionSummary summary, PropertyBag bag)
    {
        if (!HasTrx)
            return;

        AddTrxTestSummaryPropertiesIntl(summary, bag);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ITrxReportCapabilityImpl CreateTrxIntl()
    {
        return new TrxReportCapability();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ITrxReportCapabilityImpl? GetTrxCapabilityIntl(ITestFrameworkCapabilities capabilities)
    {
        return capabilities.GetCapability<TrxReportCapability>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddTrxTestPropertiesIntl(in UnturnedTestInstance instance, PropertyBag bag)
    {
        TrxFullyQualifiedTypeNameProperty property = new TrxFullyQualifiedTypeNameProperty(instance.Type.FullName ?? instance.Type.Name);
        bag.Add(property);

        string[]? categories = instance.Test.Categories;
        if (categories is { Length: > 0 })
        {
            bag.Add(new TrxCategoriesProperty(categories));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddTrxTestSummaryPropertiesIntl(TestExecutionSummary summary, PropertyBag bag)
    {
        if (summary.ExceptionMessage != null)
        {
            string message = summary.ExceptionMessage;
            if (summary.ExceptionType != null)
                message = summary.ExceptionType + ": " + message;

            bag.Add(new TrxExceptionProperty(message, summary.StackTrace));
        }
        else
        {
            bag.Add(new TrxExceptionProperty(summary.ExceptionType, summary.StackTrace));
        }

        if (summary.OutputMessages is { Count: > 0 })
        {
            TrxMessage[] messages = new TrxMessage[summary.OutputMessages.Count];
            for (int i = 0; i < summary.OutputMessages.Count; i++)
            {
                TestOutputMessage message = summary.OutputMessages[i];
                messages[i] = message.Severity switch
                {
                    (int)LogSeverity.Information => new StandardOutputTrxMessage(message.Message),
                    (int)LogSeverity.Error       => new StandardErrorTrxMessage(message.Message),
                    _                            => new DebugOrTraceTrxMessage(message.Message)
                };
            }

            bag.Add(new TrxMessagesProperty(messages));
        }
    }
}

internal interface ITrxReportCapabilityImpl : ITestFrameworkCapability
{
    bool IsEnabled { get; }
}

internal class TrxReportCapability : ITrxReportCapability, ITrxReportCapabilityImpl
{
    static TrxReportCapability() { } // beforefieldinit

    public bool IsEnabled { get; private set; }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public void Enable()
    {
        IsEnabled = true;
    }
}