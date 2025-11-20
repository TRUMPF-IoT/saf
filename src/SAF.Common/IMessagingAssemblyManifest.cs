// SPDX-FileCopyrightText: 2017-2022 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Common;
using Microsoft.Extensions.DependencyInjection;

public class MessagingConfiguration
{
    public string Type { get; set; } = string.Empty;
    public IDictionary<string, string>? Config { get; set; }
}

public interface IMessagingAssemblyManifest
{
    void RegisterDependencies(IServiceCollection services, MessagingConfiguration config);
}