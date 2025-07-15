// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.LegacyFileTransfer;

public interface IFileReceiver
{
    void Subscribe(string topic, Action<TransportFileDelivery> callback);
    void Unsubscribe(string topic);
    void Unsubscribe();
}