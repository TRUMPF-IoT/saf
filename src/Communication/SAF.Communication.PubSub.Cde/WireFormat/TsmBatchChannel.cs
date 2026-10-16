// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

/// <summary>
/// The channel name that marks a batch TSM.
/// </summary>
internal static class TsmBatchChannel
{
    private const string Prefix = "$$batch";

    public static string Create(int size) => $"{Prefix}:size={size}$$";

    public static bool Matches(string channel) => channel.StartsWith(Prefix, StringComparison.Ordinal);
}
