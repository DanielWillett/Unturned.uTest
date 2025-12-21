//using JetBrains.Annotations;
using Microsoft.Testing.Platform.Builder;
using System.Runtime.CompilerServices;

namespace uTest.Runner;

//[UsedImplicitly(ImplicitUseKindFlags.Access, ImplicitUseTargetFlags.WithMembers)]
public static class TestingPlatformBuilderHook
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddExtensions(ITestApplicationBuilder builder, string[] args)
    {
        builder.AddUnturnedTest();
    }
}