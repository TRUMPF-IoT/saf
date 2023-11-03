// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


namespace SAF.Communication.PubSub.Interfaces;

public static class MessageToken
{
    private const string Prefix = "cdepubsub";

    public static readonly string Publish = $"{Prefix}:publish";

    public static readonly string DiscoveryRequest = $"{Prefix}:discover:request";
    public static readonly string DiscoveryResponse = $"{Prefix}:discover:response";

    public static readonly string RegistryAlive = $"{Prefix}:registry:alive";
    public static readonly string RegistryShutdown = $"{Prefix}:registry:shutdown";
    public static readonly string SubscriberAlive = $"{Prefix}:subscriber:alive";
    public static readonly string SubscriberShutdown = $"{Prefix}:subscriber:shutdown";

    public static readonly string SubscribeRequest = $"{Prefix}:subscribe:request";
    public static readonly string SubscribeResponse = $"{Prefix}:subscribe:response";
    public static readonly string Unsubscribe = $"{Prefix}:unsubscribe";
    public static readonly string SubscribeTrigger = $"{Prefix}:subscribe:trigger";

    public static readonly string Error = $"{Prefix}:error";
}