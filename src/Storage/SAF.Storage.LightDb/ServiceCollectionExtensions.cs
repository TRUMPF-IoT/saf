// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using System;
using Microsoft.Extensions.DependencyInjection;
using LiteDB;
using SAF.Common;

namespace SAF.Storage.LiteDb
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLiteDbStorageInfrastructure(this IServiceCollection serviceCollection, Action<LiteDbConfiguration> configure)
        {
            var config = new LiteDbConfiguration();
            configure(config);

            return serviceCollection.AddLiteDbStorageInfrastructure(config);
        }

        private static IServiceCollection AddLiteDbStorageInfrastructure(this IServiceCollection serviceCollection, LiteDbConfiguration config)
        {
            return serviceCollection.AddSingleton<IStorageInfrastructure>(r =>
                new Storage(CreateLiteDbConnection(config)));
        }

        private static ILiteDatabase CreateLiteDbConnection(LiteDbConfiguration config)
        {
            if (string.IsNullOrEmpty(config.ConnectionString))
            {
                throw new ArgumentException("The connection string can't be null", nameof(config));
            }

            return new LiteDatabase(new ConnectionString(config.ConnectionString));
        }
    }
}