// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Nats.WireFormat;

using NATS.Client.Core;

/// <summary>
/// Writes and reads the <see cref="NatsHeaderNames.Metadata"/> header that the formats from version 2 on share.
/// </summary>
internal interface INatsMetadataHeader
{
    string Write(MessageMetadataDtoV2 metadata);

    /// <param name="metadata"><c>null</c> if there is no such header or it is JSON <c>null</c>.</param>
    /// <returns><c>false</c> if the header cannot be read.</returns>
    bool TryRead(NatsHeaders? headers, out MessageMetadataDtoV2? metadata);
}
