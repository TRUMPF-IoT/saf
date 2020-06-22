// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using System.Collections.Generic;

namespace SAF.Toolbox.FileTransfer
{
    internal class TransportFileEnvelope
    {
        public IDictionary<string, string> TransportFile;
        public string ReplyTo;
    }
}