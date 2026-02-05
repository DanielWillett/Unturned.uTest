using Cysharp.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenMod.API.Ioc;
using OpenMod.API.Plugins;
using OpenMod.Unturned.Plugins;
using System;
using System.Threading;
using System.Threading.Tasks;
using uTest.Compat.OpenMod.DependencyInjection;
using uTest.Compat.OpenMod.Lifetime;

[assembly: PluginMetadata(
    "uTest",
    Author = "Daniel Willett",
    DisplayName = "uTest",
    Description = "uTest OpenMod Integration",
    Website = "github.com/DanielWillett/Unturned.uTest"
)]

namespace uTest.Compat.OpenMod;

internal sealed class OpenModCompatPlugin : OpenModUnturnedPlugin
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OpenModCompatPlugin> _logger;

    public OpenModCompatPlugin(IServiceProvider serviceProvider, ILogger<OpenModCompatPlugin> logger)
        : base(serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override UniTask OnLoadAsync()
    {
        try
        {
            if (Interlocked.Exchange(ref OpenModTestLifetimeIntegration.Instance, null) is IDisposable disp)
            {
                disp.Dispose();
            }

            OpenModTestLifetimeIntegration lifetime
                = ActivatorUtilities.CreateInstance<OpenModTestLifetimeIntegration>(_serviceProvider, this);

            if (OpenModTestLifetimeIntegration.Instance != lifetime)
                _logger.LogWarning("Failed to instantiate OpenModTestLifetimeIntegration.");
            else
                _logger.LogTrace("Created OpenModTestLifetimeIntegration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OpenModTestLifetimeIntegration.");
        }

        return UniTask.CompletedTask;
    }
}

internal sealed class UnturnedTestOpenModHost : IServiceConfigurator
{
    public void ConfigureServices(
        IOpenModServiceConfigurationContext openModStartupContext,
        IServiceCollection serviceCollection
    )
    {
        serviceCollection.Add(
            new ServiceDescriptor(
                typeof(IHostedService),
                typeof(HostedService),
                ServiceLifetime.Transient
            )
        );

        openModStartupContext.LoggerFactory
            .CreateLogger("uTest")
            .LogTrace("uTest service configurer invoked.");
    }

    private class HostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OpenModCompatPlugin> _logger;

        public HostedService(IServiceProvider serviceProvider, ILogger<OpenModCompatPlugin> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                OpenModTestRunnerActivator activator
                    = ActivatorUtilities.CreateInstance<OpenModTestRunnerActivator>(_serviceProvider);
                
                if (OpenModTestRunnerActivator.Instance != activator)
                    _logger.LogWarning("Failed to instantiate OpenModTestRunnerActivator.");
                else
                    _logger.LogTrace("Created OpenModTestRunnerActivator.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create OpenModTestRunnerActivator.");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}