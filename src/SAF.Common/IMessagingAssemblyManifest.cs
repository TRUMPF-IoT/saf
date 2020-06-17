// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace SAF.Common
{
    public class MessagingConfiguration
    {
        public string Type { get; set; }
        public IDictionary<string, string> Config { get; set; }
    }

    public interface IMessagingAssemblyManifest
    {
        void RegisterDependencies(IServiceCollection services, MessagingConfiguration config);
    }
}
