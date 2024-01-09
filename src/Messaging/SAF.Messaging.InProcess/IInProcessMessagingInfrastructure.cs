// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.InProcess;
using Common;

public interface IInProcessMessagingInfrastructure : IMessagingInfrastructure
{
    // Defined only to support specific in-process IMessagingInfrastructure in DI containers. 
    // The specific instance can be retrieved like this: serviceProvider.GetService<IInProcessMessagingInfrastructure>. 
    // Use IServiceCollection.AddInProcessMessagingInfrastructure extension method to add IInProcessMessagingInfrastructure into the DI container. 
}