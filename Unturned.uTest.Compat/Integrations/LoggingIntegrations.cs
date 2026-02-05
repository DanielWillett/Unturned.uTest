using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Compat.Logging;
using uTest.Compat.Utility;
using uTest.Logging;

namespace uTest.Compat;

internal static class LoggingIntegrations
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ILoggerIntegration TryInstallOpenModLoggingIntegration(ILogger logger)
    {
        Assembly asm = HotloaderAssemblyHelper.LoadAssemblyMaybeFromHotloader(
            new AssemblyName(
                $"Unturned.uTest.Compat.OpenMod, Version={Assembly.GetExecutingAssembly().GetName().Version}, Culture=neutral, PublicKeyToken=null"
            )
        );

        Type integrationType = asm.GetType("uTest.Compat.OpenMod.Logging.OpenModLoggerIntegration", throwOnError: true);

        return (ILoggerIntegration)Activator.CreateInstance(integrationType);
    }
}