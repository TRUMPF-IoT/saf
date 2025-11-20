// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.Cde;
using nsCDEngine.BaseClasses;

public static class TsmExtensions
{
    private static string? _localNodeId;
    private static string? LocalNodeId => _localNodeId ??= TheBaseAssets.MyServiceHostInfo?.MyDeviceInfo?.DeviceID.ToString();

    public static bool IsLocalHost(this TSM tsm)
    {
        // This is a performance improved re-implementation of TheCommonUtils.IsLocalHost(tsm.GetOriginator()).
        // It avoids guid creation, parsing and comparison. Instead it simply uses a string comparison.
        return !string.IsNullOrEmpty(LocalNodeId) && tsm.ORG.StartsWith(LocalNodeId, StringComparison.InvariantCultureIgnoreCase);
    }
}