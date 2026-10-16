// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Communication.PubSub.Cde.Tests.WireFormat;

using SAF.Communication.PubSub.Cde.WireFormat;
using SAF.Messaging.Contracts;
using TestUtilities.WireFormat;
using Xunit;

public class TsmMessageFormatV5Tests
{
    private readonly TsmMessageFormatV5 _v5 = new(new TsmPlbLayout());

    [Fact]
    public void Encode_PutsTheBinaryPayloadIntoPlbWithoutCopying()
    {
        var message = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextAndBinary);

        var payload = _v5.Encode(message);

        Assert.Equal("""{"Topic":"saf/wire/text-and-binary","Payload":"{\"name\":\"chunk\"}","CustomProperties":[{"Name":"index","Value":"3"}],"BinaryPayloadLength":5}""", payload.Pls);
        Assert.Same(message.BinaryPayload, payload.Plb);
    }

    /// <summary>
    /// C-DEngine loses a PLB whose length is a whole multiple of its (even) chunk size, so PLB never has an even length.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(250_000)]
    [InlineData(1_500_000)]
    public void Encode_PadsAnEvenLengthToAnOddOne(int length)
    {
        var binary = new byte[length];
        Array.Fill(binary, (byte)7);

        var payload = _v5.Encode(new Message { Topic = "t", BinaryPayload = binary });

        Assert.Equal(length + 1, payload.Plb!.Length);
        Assert.True(payload.Plb.AsSpan(0, length).SequenceEqual(binary));
        Assert.Equal(binary, _v5.Decode("t", payload)!.BinaryPayload);
    }

    [Fact]
    public void Encode_WritesTheV2JsonForATextMessage()
    {
        var message = WireFormatReferenceMessages.Create(WireFormatReferenceMessages.CustomProperties);

        Assert.Equal(new TsmMessageFormatV2().Encode(message), _v5.Encode(message));
    }

    /// <summary>
    /// C-DEngine drops an empty PLB, so the length alone tells an empty binary payload from none.
    /// </summary>
    [Fact]
    public void Encode_SendsAnEmptyBinaryPayloadAsItsLengthOnly()
    {
        var payload = _v5.Encode(WireFormatReferenceMessages.Create(WireFormatReferenceMessages.EmptyBinary));

        Assert.Equal("""{"Topic":"saf/wire/empty-binary","BinaryPayloadLength":0}""", payload.Pls);
        Assert.Null(payload.Plb);
    }

    [Theory]
    [InlineData(WireFormatReferenceMessages.Binary)]
    [InlineData(WireFormatReferenceMessages.TextAndBinary)]
    [InlineData(WireFormatReferenceMessages.EmptyBinary)]
    [InlineData(WireFormatReferenceMessages.TextPayload)]
    public void EncodeAndDecode_RoundTripTheMessage(string id)
    {
        var message = WireFormatReferenceMessages.Create(id);

        var decoded = _v5.Decode("ignored", _v5.Encode(message))!;

        Assert.Equal(message.Topic, decoded.Topic);
        Assert.Equal(message.Payload, decoded.Payload);
        Assert.Equal(message.BinaryPayload, decoded.BinaryPayload);
        Assert.Equal(message.CustomProperties?.Count, decoded.CustomProperties?.Count);
    }

    [Fact]
    public void Decode_TakesPlbWithoutCopying()
    {
        byte[] plb = [1, 2, 3];

        var decoded = _v5.Decode("t", new TsmPayload("""{"Topic":"t","BinaryPayloadLength":3}""", plb))!;

        Assert.Same(plb, decoded.BinaryPayload);
    }

    [Fact]
    public void Decode_IgnoresThePaddingByte()
        => Assert.Equal(new byte[] { 1, 2 }, _v5.Decode("t", new TsmPayload("""{"Topic":"t","BinaryPayloadLength":2}""", [1, 2, 0]))!.BinaryPayload);

    /// <summary>
    /// A 9.x registry writes the subscriber's version 5.0.0 into the TSM text, but the V2 JSON into PLS.
    /// </summary>
    [Fact]
    public void Decode_ReadsV2JsonWithoutPlb()
    {
        var decoded = _v5.Decode("t", new TsmPayload("""{"Topic":"t","Payload":"p"}"""))!;

        Assert.Equal("p", decoded.Payload);
        Assert.Null(decoded.BinaryPayload);
    }

    [Fact]
    public void EncodeBatch_JoinsTheBinaryPayloadsInMessageOrder()
    {
        List<Message> messages =
        [
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.Binary),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextPayload),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.EmptyBinary),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextAndBinary)
        ];

        var payload = _v5.EncodeBatch(messages);

        Assert.Equal([0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF, 0x53, 0x41, 0x46, 0x00, 0xFF], payload.Plb);
        Assert.Equal([6, null, 0, 5], ReadLengths(payload.Pls));
    }

    [Fact]
    public void EncodeBatch_PadsAnEvenTotalLength()
    {
        var payload = _v5.EncodeBatch([new Message { Topic = "a", BinaryPayload = [1] }, new Message { Topic = "b", BinaryPayload = [2] }]);

        Assert.Equal(3, payload.Plb!.Length);
        Assert.Equal([1, 2], _v5.DecodeBatch(payload)!.Select(m => m.BinaryPayload![0]));
    }

    [Fact]
    public void EncodeBatch_UsesASingleBinaryPayloadWithoutCopying()
    {
        var binary = new Message { Topic = "b", BinaryPayload = [1, 2, 3] };

        var payload = _v5.EncodeBatch([new Message { Topic = "a" }, binary, new Message { Topic = "c", BinaryPayload = [] }]);

        Assert.Same(binary.BinaryPayload, payload.Plb);
    }

    [Fact]
    public void EncodeBatch_SendsNoPlbForTextMessages()
        => Assert.Null(_v5.EncodeBatch([new Message { Topic = "a", Payload = "p" }]).Plb);

    [Fact]
    public void DecodeBatch_CutsPlbIntoTheMessages()
    {
        List<Message> messages =
        [
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.Binary),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextPayload),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.EmptyBinary),
            WireFormatReferenceMessages.Create(WireFormatReferenceMessages.TextAndBinary)
        ];

        var decoded = _v5.DecodeBatch(_v5.EncodeBatch(messages))!;

        Assert.Equal(messages.Select(m => m.Topic), decoded.Select(m => m.Topic));
        for (var i = 0; i < messages.Count; i++)
        {
            Assert.Equal(messages[i].Payload, decoded[i].Payload);
            Assert.Equal(messages[i].BinaryPayload, decoded[i].BinaryPayload);
        }
    }

    [Theory]
    [InlineData("""{"Topic":"t","BinaryPayloadLength":4}""", new byte[] { 1, 2, 3 })]
    [InlineData("""{"Topic":"t","BinaryPayloadLength":1}""", new byte[] { 1, 2, 3 })]
    [InlineData("""{"Topic":"t","BinaryPayloadLength":2}""", new byte[] { 1, 2 })]
    [InlineData("""{"Topic":"t","BinaryPayloadLength":-1}""", new byte[] { 1, 2, 3 })]
    [InlineData("""{"Topic":"t","BinaryPayloadLength":1}""", null)]
    [InlineData("""{"Topic":"t"}""", new byte[] { 1 })]
    [InlineData("null", null)]
    public void Decode_ReturnsNull_WhenPlbDoesNotMatch(string pls, byte[]? plb)
        => Assert.Null(_v5.Decode("t", new TsmPayload(pls, plb)));

    [Theory]
    [InlineData("""[{"Topic":"a","BinaryPayloadLength":2},{"Topic":"b","BinaryPayloadLength":2}]""", new byte[] { 1, 2, 3 })]
    [InlineData("""[{"Topic":"a","BinaryPayloadLength":1},{"Topic":"b"}]""", new byte[] { 1, 2 })]
    [InlineData("null", null)]
    public void DecodeBatch_ReturnsNull_WhenPlbDoesNotMatch(string pls, byte[]? plb)
        => Assert.Null(_v5.DecodeBatch(new TsmPayload(pls, plb)));

    private static IEnumerable<int?> ReadLengths(string? pls)
        => nsCDEngine.BaseClasses.TheCommonUtils.DeserializeJSONStringToObject<List<LengthOnly>>(pls).Select(m => m.BinaryPayloadLength);

    private sealed class LengthOnly
    {
        public int? BinaryPayloadLength { get; set; }
    }
}
