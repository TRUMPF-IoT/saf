// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Cde;
using Common;
using Communication.PubSub.Interfaces;

internal class CdeMessagingConfiguration
{
    private readonly IDictionary<string, string> _config;

    public CdeMessagingConfiguration()
        : this(new Dictionary<string, string>())
    { }
    public CdeMessagingConfiguration(MessagingConfiguration config)
        : this(config?.Config ?? new Dictionary<string, string>())
    { }

    public CdeMessagingConfiguration(IDictionary<string, string> config)
    {
        _config = config;
    }

    public RoutingOptions RoutingOptions
    {
        get
        {
            if (!_config.TryGetValue("routingOptions", out var options)) return RoutingOptions.All;
            return Enum.TryParse<RoutingOptions>(options, out var routingOptions) ? routingOptions : RoutingOptions.All;
        }
    }
}