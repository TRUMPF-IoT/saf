// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.WireFormat;

using nsCDEngine.BaseClasses;
using SAF.Messaging.Contracts;
using Interfaces;

/// <summary>
/// The whole message as PascalCase JSON, used from <see cref="PubSubVersion.V2"/> on.
/// <see cref="PubSubVersion.V4"/> sends the same shape, several messages at once as a JSON array.
/// </summary>
internal sealed class TsmMessageFormatV2 : ITsmBatchFormat
{
    public Version MinimumVersion { get; } = Version.Parse(PubSubVersion.V2);

    // The JSON carries text only.
    public bool CanEncode(Message message) => message.BinaryPayload is null;

    public string Encode(Message message)
        => TheCommonUtils.SerializeObjectToJSONString(MessageDtoV2.FromMessage(message));

    public Message? Decode(string channel, string? pls)
        => TheCommonUtils.DeserializeJSONStringToObject<MessageDtoV2>(pls)?.ToMessage();

    public string EncodeBatch(IEnumerable<Message> messages)
        => TheCommonUtils.SerializeObjectToJSONString(messages.Select(MessageDtoV2.FromMessage).ToList());

    public List<Message>? DecodeBatch(string? pls)
        => TheCommonUtils.DeserializeJSONStringToObject<List<MessageDtoV2>>(pls)?
            .Select(m => m.ToMessage())
            .ToList();
}
