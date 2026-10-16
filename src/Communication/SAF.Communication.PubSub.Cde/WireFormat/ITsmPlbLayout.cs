// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

/// <summary>
/// Lays out the binary payloads of the messages in one TSM in its <c>PLB</c>.
/// </summary>
internal interface ITsmPlbLayout
{
    /// <returns><c>null</c> if there are no bytes to send.</returns>
    byte[]? Pack(IReadOnlyList<byte[]> binaryPayloads);

    /// <returns>The binary payloads with the given lengths, in order, or <c>null</c> if the lengths do not match <paramref name="plb"/>.</returns>
    IReadOnlyList<byte[]>? Unpack(byte[]? plb, IReadOnlyList<int> lengths);
}
