using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using uTest.Compat.Lifetime;
using uTest.Compat.Utility;
using uTest.Logging;

namespace uTest.Compat;

internal static class TestLifetimeIntegrations
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ITestLifetimeIntegration TryInstallOpenModTestLifetime(ILogger logger)
    {
        Assembly asm = HotloaderAssemblyHelper.LoadAssemblyMaybeFromHotloader(
            new AssemblyName(
                $"Unturned.uTest.Compat.OpenMod, Version={Assembly.GetExecutingAssembly().GetName().Version}, Culture=neutral, PublicKeyToken=null"
            )
        );

        Type integrationType = asm.GetType("uTest.Compat.OpenMod.Lifetime.OpenModTestLifetimeIntegration", throwOnError: true);

        FieldInfo? field = integrationType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        if (field == null)
            throw new FieldAccessException($"Field not found: {integrationType.FullName}.Instance.");

        return (ITestLifetimeIntegration?)field.GetValue(null)
               ?? throw new InvalidOperationException("OpenMod did not invoke the compatibility hook.");
    }
}