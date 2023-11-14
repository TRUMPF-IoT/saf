using Microsoft.Extensions.DependencyInjection;
using SAF.Common;
using SAF.Hosting.Abstractions;

namespace SAF.Hosting;

internal class ServiceHostBuilder(IServiceCollection services) : IServiceHostBuilder
{
    private Action<ServiceHostInfo>? _configureServiceHostInfoAction;

    public IServiceCollection Services { get; } = services;

    public IServiceHostBuilder WithServiceHostInfo(Action<ServiceHostInfoOptions> setupAction)
    {
        _configureServiceHostInfoAction = setupAction;
        return this;
    }

    public IServiceHostBuilder AddServiceHostInfo()
    {
        Services.AddSingleton<IServiceHostInfo>(sp =>
            {
                var info = new ServiceHostInfo(() => GetOrInitializeHostId(sp.GetService<IStorageInfrastructure>()));
                _configureServiceHostInfoAction?.Invoke(info);
                return info;
            });

        return this;
    }

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