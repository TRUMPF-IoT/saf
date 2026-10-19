// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Redis.Tests.WireFormat;

using System.Text;
using SAF.Messaging.Redis.WireFormat;
using Xunit;

public class RedisEnvelopeHeaderReaderTests
{
    private readonly RedisEnvelopeHeaderReader _reader = new();

    [Theory]
    [InlineData("""{"version":"2.0.0","message":{"topic":"t"}}""", "2.0.0")]
    [InlineData("""{"message":{"topic":"t"},"version":"2.0.0"}""", "2.0.0")]
    [InlineData("""{"extra":[1,{"a":2}],"Version":"3.0.0","MESSAGE":{}}""", "3.0.0")]
    [InlineData("""{"version":null,"message":{}}""", null)]
    [InlineData("""{"message":{}}""", null)]
    public void Read_ReturnsTheVersionOfAnEnvelope(string json, string? expectedVersion)
        => Assert.Equal(new RedisEnvelopeHeader(expectedVersion), Read(json));

    [Theory]
    [InlineData("""{"version":"2.0.0","message":{"topic":1""")]
    [InlineData("""{"version":"2.0.0","message":{"payload":"a"},"trailing":""")]
    [InlineData("""{"message":{"topic":"t"},"version":"2.0.0","trailing":""")]
    public void Read_StopsOnceVersionAndMessageAreFound(string json)
        => Assert.Equal(new RedisEnvelopeHeader("2.0.0"), Read(json));

    [Theory]
    [InlineData("""{"version":"2.0.0"}""")]
    [InlineData("""{"version":"2.0.0","message":"opaque"}""")]
    [InlineData("""{"version":"2.0.0","message":null}""")]
    [InlineData("""{"version":2,"message":{}}""")]
    [InlineData("""{"message":{"topic":1""")]
    [InlineData("""[{"version":"2.0.0","message":{}}]""")]
    [InlineData("not json")]
    [InlineData("")]
    public void Read_ReturnsNull_ForAnythingButAnEnvelope(string json)
        => Assert.Null(Read(json));

    private RedisEnvelopeHeader? Read(string json) => _reader.Read(Encoding.UTF8.GetBytes(json));
}
