// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿
using SAF.Common;

namespace SAF.Messaging.Redis
{
    public interface IRedisMessagingInfrastructure : IMessagingInfrastructure
    {
        // Defined only to support specific Redis IMessagingInfrastructure in DI containers.
        // The specific instance can be retieved like this: serviceProvider.GetService<IRedisMessagingInfrastructure>.
        // Use IServiceCollection.AddRedisMessagingInfrastructure extension method to add IRedisMessagingInfrastructure into the DI container.
    }
}