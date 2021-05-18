// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Data.SQLite;
using Microsoft.Extensions.DependencyInjection;
using SAF.Common;

namespace SAF.Storage.SqLite
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSqLiteStorageInfrastructure(this IServiceCollection serviceCollection, Action<SqLiteConfiguration> configure)
        {
            var config = new SqLiteConfiguration();
            configure(config);

            return serviceCollection.AddSqLiteStorageInfrastructure(config);
        }

        private static IServiceCollection AddSqLiteStorageInfrastructure(this IServiceCollection serviceCollection, SqLiteConfiguration config)
        {
            return serviceCollection.AddSingleton<IStorageInfrastructure>(r =>
                new Storage(CreateSqLiteConnection(config)));
        }

        private static SQLiteConnection CreateSqLiteConnection(SqLiteConfiguration config)
        {
            if (string.IsNullOrEmpty(config.ConnectionString))
            {
                throw new ArgumentException("The connection string can't be null", nameof(config));
            }

            return new SQLiteConnection(config.ConnectionString);
        }
    }
}