// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Common;
using Contracts;

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