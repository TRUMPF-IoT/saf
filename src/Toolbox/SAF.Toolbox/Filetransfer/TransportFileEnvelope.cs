// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Toolbox.FileTransfer;

internal class TransportFileEnvelope
{
    public IDictionary<string, string> TransportFile { get; set; } = new Dictionary<string, string>();
    public string ReplyTo { get; set; } = default!;
}