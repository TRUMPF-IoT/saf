// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using SAF.Common;

namespace SAF.Messaging.Cde
{
    public interface ICdeMessagingInfrastructure : IMessagingInfrastructure
    {
        // Defined only to support specific CDE IMessagingInfrastructure in DI containers. 
        // The specific instance can be retieved like this: serviceProvider.GetService<ICdeMessagingInfrastructure>. 
        // Use IServiceCollection.AddCdeMessagingInfrastructure extension method to add ICdeMessagingInfrastructure into the DI container. 
    }
}