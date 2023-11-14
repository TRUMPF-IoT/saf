// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Interfaces;

public interface IRegistryLifetimeHandler<TMessage>
{
    event Action<TMessage, string> RegistryUp;
    event Action<TMessage> RegistryDown;

    IList<TMessage> Registries { get; }
}