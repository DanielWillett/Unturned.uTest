using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Compat.Logging;
using uTest.Logging;

namespace uTest.Compat;

internal static class LoggingIntegrations
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ILoggerIntegration TryInstallOpenModLoggingIntegration(ILogger logger)
    {
        Assembly asm = Assembly.Load(
            new AssemblyName("Unturned.uTest.Compat.OpenMod, Version=0.0.1.0, Culture=neutral, PublicKeyToken=null")
        );

        Type integrationType = asm.GetType("uTest.Compat.OpenMod.Logging.OpenModLoggerIntegration", throwOnError: true);

        return (ILoggerIntegration)Activator.CreateInstance(integrationType);
    }
}