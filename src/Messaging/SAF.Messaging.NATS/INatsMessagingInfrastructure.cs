// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using SAF.Common;

namespace SAF.Messaging.Nats;

public interface INatsMessagingInfrastructure : IMessagingInfrastructure
{
    // Defined only to support specific Redis IMessagingInfrastructure in DI containers.
    // The specific instance can be retrieved like this: serviceProvider.GetService<INatsMessagingInfrastructure>.
    // Use IServiceCollection.AddNatsMessagingInfrastructure extension method to add INatsMessagingInfrastructure into the DI container.
}
