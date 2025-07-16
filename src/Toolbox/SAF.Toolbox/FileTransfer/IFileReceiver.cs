// SPDX-FileCopyrightText: 2017-2025 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.FileTransfer;

public interface IFileReceiver
{
    void Subscribe(string topic, IStatefulFileReceiver statefulFileReceiver, string folderPath);
    void Unsubscribe(string topic);
    void Unsubscribe();
}