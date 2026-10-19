// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.WireFormat;

/// <summary>
/// Reports a received message that is dropped because its wire format version is unknown.
/// </summary>
public interface IUnknownVersionWarning
{
    void Report(string version, string topic);
}
