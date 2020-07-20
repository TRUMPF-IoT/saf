// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common
{
    public interface IServiceHostEnvironment
    {
        string ApplicationName { get; }
        string EnvironmentName { get; }
    }
}