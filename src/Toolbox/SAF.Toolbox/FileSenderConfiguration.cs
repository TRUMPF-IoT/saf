// SPDX-FileCopyrightText: 2017-2024 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox;

public class FileSenderConfiguration
{
    public int RetryAttemptsForFailedChunks { get; set; } = 0;
}