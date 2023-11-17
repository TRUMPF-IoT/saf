using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class ServiceHostBuilder : IServiceHostBuilder
{
    private Action<ServiceHostInfoOptions>? _configureServiceHostInfoAction;
    private readonly SharedServiceRegistry _sharedServices = new();

    public ServiceHostBuilder(IServiceCollection services)
    {
        Services = services;
        services.AddSingleton(SharedServices);
    }

    public IServiceCollection Services { get; }

    public ISharedServiceRegistry SharedServices => _sharedServices;

    public IServiceHostBuilder ConfigureServiceHostInfo(Action<ServiceHostInfoOptions> setupAction)
    {
        _configureServiceHostInfoAction = setupAction;
        return this;
    }

    public IServiceHostBuilder AddServiceHostInfo()
    {
        Services.AddSingleton<IServiceHostInfo>(sp =>
            {
                var options = new ServiceHostInfoOptions();
                _configureServiceHostInfoAction?.Invoke(options);

                var info = new ServiceHostInfo(options, () => GetOrInitializeHostId(sp.GetService<IStorageInfrastructure>()));
                return info;
            });

        return this;
    }

    public IServiceHostBuilder AddSharedSingleton(Type serviceType, Type implementationType)
    {
        Services.AddSingleton(serviceType, implementationType);
        _sharedServices.SharedServices.AddSingleton(serviceType, implementationType);
        return this;
    }

    public IServiceHostBuilder AddSharedSingleton<TService, TImplementation>() where TService : class where TImplementation : class, TService
        => AddSharedSingleton(typeof(TService), typeof(TImplementation));

    /// <summary>
    /// Default behavior for determining the SAF host id. This method is used, if the host id is not set in the configuration callback.
    /// - Read host id from storage key "saf/hostid"
    /// - If key is not set, generates Guid for host id and try to set it in storage
    /// </summary>
    /// <param name="storage">IStorageInfrastructure used to store and load the SAF host id. If set to null, the method will always generate a new host id (Guid).</param>
    /// <returns>The host id from storage key "saf/hostid" or a Guid.</returns>
    private static string GetOrInitializeHostId(IStorageInfrastructure? storage)
    {
        const string storageKey = "saf/hostid";

        var id = storage?.GetString(storageKey);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            storage?.Set(storageKey, id);
        }
        return id;
    }
}