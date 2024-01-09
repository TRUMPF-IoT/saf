// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde;
using Interfaces;

internal class RegistryIdentity
{
    public RegistryIdentity(string address, string instanceId)
    {
        this.address = address;
        this.instanceId = instanceId;
    }

#pragma warning disable IDE1006 // naming convention

    // ReSharper disable once InconsistentNaming
    public string address;
    // ReSharper disable once InconsistentNaming
    public string instanceId;
    // ReSharper disable once InconsistentNaming
    public string version => PubSubVersion.Latest;

#pragma warning restore IDE1006
}