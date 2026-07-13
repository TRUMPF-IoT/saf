// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.SQLite;
using System.Data.SQLite;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQLite-based storage infrastructure services.
    /// </summary>
    /// <param name="serviceCollection">The service collection to register services in.</param>
    /// <param name="configure">Configuration callback for SQLite storage settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSQLiteStorageInfrastructure(this IServiceCollection serviceCollection, Action<SQLiteConfiguration> configure)
    {
        ArgumentNullException.ThrowIfNull(serviceCollection);
        ArgumentNullException.ThrowIfNull(configure);

        var config = new SQLiteConfiguration();
        configure(config);

        return serviceCollection.AddSQLiteStorageInfrastructure(config);
    }

    private static IServiceCollection AddSQLiteStorageInfrastructure(this IServiceCollection serviceCollection, SQLiteConfiguration config)
    {
        return serviceCollection.AddSingleton<IStorageInfrastructure>(_ =>
            new Storage(CreateSQLiteConnection(config)));
    }

    private static SQLiteConnection CreateSQLiteConnection(SQLiteConfiguration config)
    {
        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            throw new ArgumentException("The connection string can't be null", nameof(config));
        }

        return new SQLiteConnection(config.ConnectionString);
    }
}

