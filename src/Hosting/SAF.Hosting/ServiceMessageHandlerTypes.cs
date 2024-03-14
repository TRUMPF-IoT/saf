// SPDX-FileCopyrightText: 2017-2023 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Hosting;

using Common;
using Microsoft.Extensions.DependencyInjection;

public interface IServiceMessageHandlerTypes
{
    IEnumerable<Type> GetMessageHandlerTypes();
}

internal class ServiceMessageHandlerTypes(IServiceCollection services) : IServiceMessageHandlerTypes
{
    public IEnumerable<Type> GetMessageHandlerTypes()
    {
        var messageHandlerType = typeof(IMessageHandler);
        return services
            .Where(sd => messageHandlerType.IsAssignableFrom(sd.ServiceType))
            .Select(sd => sd.ServiceType);
    }
}