// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess;
using SAF.Common;
using SAF.Messaging.Contracts;

public interface IInProcessMessagingInfrastructure : IMessagingInfrastructure
{
    // Defined only to support specific in-process IMessagingInfrastructure in DI containers. 
    // The specific instance can be retrieved like this: serviceProvider.GetService<IInProcessMessagingInfrastructure>. 
    // Use IServiceCollection.AddInProcessMessagingInfrastructure extension method to add IInProcessMessagingInfrastructure into the DI container. 
}


