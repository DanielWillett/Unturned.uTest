using Autofac;
using Microsoft.Extensions.DependencyInjection;
using OpenMod.API.Plugins;
using System;
using System.Reflection;
using System.Threading;
using uTest.Compat.DependencyInjection;
using uTest.Compat.Utility;

namespace uTest.Compat.OpenMod.DependencyInjection;

internal sealed class OpenModTestRunnerActivator : ITestRunnerActivator, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPluginActivator _pluginActivator;

    public static ITestRunnerActivator? Instance;
    
    public OpenModTestRunnerActivator(IServiceProvider serviceProvider, IPluginActivator pluginActivator)
    {
        _serviceProvider = serviceProvider;
        _pluginActivator = pluginActivator;
        Interlocked.CompareExchange(ref Instance, this, null);
    }

    /// <inheritdoc />
    public int Priority => 10;

    /// <inheritdoc />
    public T CreateTestInstance<T>() where T : notnull
    {
        AssociatedPluginAttribute? attr = TestAttributeHelper<AssociatedPluginAttribute>.GetAttribute(
            typeof(T),
            inherit: true
        );

        if (attr?.PluginType == null)
        {
            return (T)ActivatorUtilities.CreateInstance(_serviceProvider, typeof(T));
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

        T obj = (T)ActivatorUtilities.CreateInstance(serviceProvider, typeof(T));

        return obj ?? throw new InvalidOperationException($"Failed to activate test object of type {typeof(T).FullName}.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Interlocked.CompareExchange(ref Instance, null, this);
    }
}