// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Extensions;

using SAF.Messaging.Contracts;

internal sealed class MessageHandlerResolver(IServiceProvider serviceProvider) : IMessageHandlerResolver
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public IMessageHandler? Resolve(Type handlerType)
        => _serviceProvider.GetService(handlerType) as IMessageHandler;        
}
