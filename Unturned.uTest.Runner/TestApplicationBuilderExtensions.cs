using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Testing.Platform.Helpers;

#pragma warning disable TPEXP

namespace uTest.Runner;

public static class TestApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the test framework needed to run uTest for the calling assembly.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddUnturnedTest(this ITestApplicationBuilder bldr)
    {
        UnturnedTestExtension ext = new UnturnedTestExtension();

        Assembly uTestRunnerAsm = Assembly.GetExecutingAssembly();

        Assembly? sourceAssembly = null;

        StackTrace st = new StackTrace(1);
        for (int i = 0; i < st.FrameCount; ++i)
        {
            StackFrame sf = st.GetFrame(i);
            MethodBase? mtd = sf.GetMethod();
            Assembly? asm = mtd?.DeclaringType?.Assembly;
            if (asm != null && asm != uTestRunnerAsm)
            {
                sourceAssembly = asm;
                break;
            }
        }

        List<ITestFrameworkCapability> capabilities = new List<ITestFrameworkCapability>();

        if (sourceAssembly != null)
        {
            capabilities.Add(new GeneratedTestRegistrationListCapability(sourceAssembly));
        }

        capabilities.Add(new BannerCapability(ext));
        capabilities.Add(new UnturnedTestFramework.GracefulStopCapability());

        ITrxReportCapabilityImpl? trx = TrxSwitch.CreateTrxCapability();
        if (trx != null)
        {
            capabilities.Add(trx);
        }

        capabilities.Capacity = capabilities.Count;

        bldr.AddTreeNodeFilterService(ext);
        bldr.AddMaximumFailedTestsService(ext);

        bldr.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(capabilities),
            (_, sp) => new UnturnedTestFramework(ext, sp)
        );
    }

#pragma warning disable TPEXP
    private class BannerCapability(UnturnedTestExtension extension) : IBannerMessageOwnerCapability
    {
        /// <inheritdoc />
        public Task<string?> GetBannerMessageAsync()
        {
            return Task.FromResult<string?>(string.Format(Properties.Resources.BannerMessage, extension.Version));
        }
    }
#pragma warning restore TPEXP
}