// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Routing;

using SAF.Messaging.Contracts;

public interface IRoutingMessagingInfrastructure : IMessagingInfrastructure
{
    // The specific instance can be retrieved like this: serviceProvider.GetService<IRoutingMessagingInfrastructure>.
    // Use IServiceCollection.AddRoutingMessagingInfrastructure extension method to add IRoutingMessagingInfrastructure into the DI container.
}


