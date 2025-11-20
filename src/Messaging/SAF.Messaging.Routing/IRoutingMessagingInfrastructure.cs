// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Routing;
using Common;

public interface IRoutingMessagingInfrastructure : IMessagingInfrastructure
{
    // Defined only to support specific Redis IMessagingInfrastructure in DI containers.
    // The specific instance can be retrieved like this: serviceProvider.GetService<IRedisMessagingInfrastructure>.
    // Use IServiceCollection.AddRedisMessagingInfrastructure extension method to add IRedisMessagingInfrastructure into the DI container.
}