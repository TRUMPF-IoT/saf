// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Messaging.Redis
{
    public class RedisConfiguration
    {
        public string ConnectionString { get; set; }
        public int Timeout { get; set; }
    }
}