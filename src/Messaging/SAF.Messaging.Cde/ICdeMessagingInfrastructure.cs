// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde;
using Common;

public interface ICdeMessagingInfrastructure : IMessagingInfrastructure
{
    // Defined only to support specific CDE IMessagingInfrastructure in DI containers. 
    // The specific instance can be retrieved like this: serviceProvider.GetService<ICdeMessagingInfrastructure>. 
    // Use IServiceCollection.AddCdeMessagingInfrastructure extension method to add ICdeMessagingInfrastructure into the DI container. 
}