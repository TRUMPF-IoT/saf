// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde
{
    internal class RegistryIdentity
    {
        public RegistryIdentity(string address, string instanceId)
        {
            this.address = address;
            this.instanceId = instanceId;
        }

#pragma warning disable IDE1006 // naming convention

        public string address;
        public string instanceId;
        // ReSharper disable once InconsistentNaming
        public string version => "1.0.0";

#pragma warning restore IDE1006
    }
}
