using Autofac;
using Microsoft.Extensions.DependencyInjection;
using OpenMod.API.Plugins;
using System;
using System.Reflection;
using System.Threading;
using JetBrains.Annotations;
using uTest.Compat.DependencyInjection;
using uTest.Compat.Utility;

namespace uTest.Compat.OpenMod.DependencyInjection;

internal sealed class OpenModTestRunnerActivator : ITestRunnerActivator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginActivator _pluginActivator;

    [UsedImplicitly]
    public static ITestRunnerActivator? Instance;

    public int Priority => 10;

    public OpenModTestRunnerActivator(IServiceProvider serviceProvider, IPluginActivator pluginActivator)
    {
        _serviceProvider = serviceProvider;
        _pluginActivator = pluginActivator;
        Interlocked.CompareExchange(ref Instance, this, null);
    }

    public TestInstance<T> CreateTestInstance<T>() where T : notnull
    {
        AssociatedPluginAttribute? attr = TestAttributeHelper<AssociatedPluginAttribute>.GetAttribute(
            typeof(T),
            inherit: true
        );

        if (attr?.PluginType == null)
        {
            return CreateInstanceFromScope<T>(_serviceProvider.CreateScope());
        }

        Assembly assembly = attr.PluginType.Assembly;

        IOpenModPlugin? plugin = null;
        foreach (IOpenModPlugin x in _pluginActivator.ActivatedPlugins)
        {
            if (x.GetType().Assembly != assembly)
                continue;

            plugin = x;
            break;
        }

        if (plugin == null)
        {
            throw new InvalidOperationException(
                $"Given associated plugin type {attr.PluginType.FullName} doesn't belong to an activated OpenMod plugin."
            );
        }

        IServiceProvider serviceProvider = plugin.LifetimeScope.Resolve<IServiceProvider>();
        return CreateInstanceFromScope<T>(serviceProvider.CreateScope());
    }

    private static TestInstance<T> CreateInstanceFromScope<T>(IServiceScope scope) where T : notnull
    {
        try
        {
            T instance = (T)ActivatorUtilities.CreateInstance(scope.ServiceProvider, typeof(T));

            return instance != null
                ? new TestInstance<T>(instance, scope)
                : throw new InvalidOperationException($"Failed to activate test object of type {typeof(T).FullName}.");
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        Interlocked.CompareExchange(ref Instance, null, this);
    }
}