// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

﻿using System;
using Microsoft.Extensions.DependencyInjection;
using SAF.Toolbox.FileHandling;

namespace SAF.Toolbox
{
    public static class ServiceProviderExtensions
    {
        public static IFileSystemDirectory GetFileSystemDirectory(this IServiceProvider provider, string path)
        {
            var factoryFunc = provider.GetService<Func<string, IFileSystemDirectory>>();
            return factoryFunc(path);
        }
    }
}