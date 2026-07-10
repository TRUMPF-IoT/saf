// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Extensions;

using Microsoft.Extensions.DependencyInjection;
using SAF.Messaging.Contracts;

internal sealed class MessageHandlerResolver(IServiceProvider serviceProvider) : IMessageHandlerResolver
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public bool CanResolve(string handlerTypeFullName)
        => ResolveRegisteredHandler(handlerTypeFullName) is not null;

    public IMessageHandler Resolve(string handlerTypeFullName)
    {
        var handler = ResolveRegisteredHandler(handlerTypeFullName);
        if (handler is null)
            throw new InvalidOperationException($"Handler '{handlerTypeFullName}' is not supported by resolver '{GetType().FullName}'.");

        return handler;
    }

    private IMessageHandler? ResolveRegisteredHandler(string handlerTypeFullName)
    {
        var handlers = _serviceProvider.GetServices<IMessageHandler>();
        return handlers.FirstOrDefault(h => h.GetType().FullName == handlerTypeFullName);
    }
}
