// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Toolbox.Tests.FileTransfer;

using SAF.Messaging.Contracts;
using SAF.Toolbox.FileTransfer;
using SAF.Toolbox.FileTransfer.Messages;
using SAF.Toolbox.Serialization;
using Xunit;
using static FileTransferGolden;

public class FileTransferWireFormatTests
{
    private static readonly FileReceiverState State = new() { TransmittedChunks = [0, 2] };

    [Fact]
    public void RequestsAndChunkResponse_AreWrittenAsBySaf10()
    {
        Assert.Equal(StateRequest, JsonSerializer.Serialize(new GetReceiverStateRequest { File = File, ReplyTo = "reply/1" }));
        Assert.Equal(TextChunkRequest, JsonSerializer.Serialize(
            new SendFileChunkRequest { File = File, FileChunk = new FileChunk { Index = 1, Data = ChunkData }, ReplyTo = "reply/1" }));
        Assert.Equal(ChunkResponse, JsonSerializer.Serialize(new SendFileChunkResponse { Status = FileReceiverStatus.Ok }));
    }

    [Fact]
    public void StateResponse_CarriesTheChunkFormatsOnlyIfSet()
    {
        Assert.Equal(Saf10StateResponse, JsonSerializer.Serialize(new GetReceiverStateResponse { State = State }));
        Assert.Equal(StateResponse, JsonSerializer.Serialize(
            new GetReceiverStateResponse { State = State, AcceptedChunkFormats = MessageFormats.Text | MessageFormats.Binary }));
    }

    [Fact]
    public void Saf10Senders_ReadTheStateResponse()
    {
        var response = JsonSerializer.Deserialize<Saf10GetReceiverStateResponse>(StateResponse)!;

        Assert.Equal([0u, 2u], response.State.TransmittedChunks);
    }

    [Fact]
    public void StateResponsesOfSaf10Receivers_AcceptNoChunkFormats()
    {
        var response = JsonSerializer.Deserialize<GetReceiverStateResponse>(Saf10StateResponse)!;

        Assert.Null(response.AcceptedChunkFormats);
        Assert.Equal([0u, 2u], response.State.TransmittedChunks);
    }

    [Fact]
    public void BinaryChunkRequest_CarriesTheFileAndTheIndex()
    {
        Assert.Equal(BinaryChunkRequest, JsonSerializer.Serialize(new SendBinaryFileChunkRequest { File = File, Index = 1 }));
    }

    // GetReceiverStateResponse as in SAF 10.x.
    private sealed class Saf10GetReceiverStateResponse
    {
        public required FileReceiverState State { get; set; }
    }
}
