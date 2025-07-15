// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0

using Microsoft.Extensions.Options;
using NSubstitute;
using SAF.Common;
using SAF.Toolbox.Serialization;
using Xunit;

namespace SAF.Toolbox.Tests.FileTransfer;

public class FileSenderTests
{
    [Theory]
    [InlineData(1)] // 1 byte
    [InlineData(1024)] // 1 kByte
    [InlineData(1024 * 3)] // 3 kByte
    [InlineData(1024 * 1024)] // 1 MByte
    [InlineData(1024 * 1024 * 3)] // 3 MByte
    [InlineData(1024 * 200 - 1)] // excact chunk size - 1
    [InlineData(1024 * 200)] // excact chunk size
    [InlineData(1024 * 200 + 1)] // excact chunk size + 1
    [InlineData(1024 * 200 * 3 - 1)] // multiple of chunk size - 1
    [InlineData(1024 * 200 * 3)] // multiple of chunk size
    [InlineData(1024 * 200 * 3 + 1)] // multiple of chunk size + 1
    [InlineData(134217728)] // 128 MByte
    public async Task SendInChunksCallsPublishOk(int fileSizeInBytes)
    {
        Action<Message>? senderHandler = null;
        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.When(m => m.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()))
            .Do(args => senderHandler = args.Arg<Action<Message>>());
        var options = Substitute.For<IOptions<FileSenderConfiguration>>();
        options.Value.Returns(new FileSenderConfiguration());

        var testChannel = $"tests/fileSender/{fileSizeInBytes}";
        var buffer = new byte[fileSizeInBytes];
        messaging.When(m => m.Publish(Arg.Is<Message>(msg => msg.Topic == testChannel)))
            .Do(args =>
            {
                var msg = args.Arg<Message>();
                var req = JsonSerializer.Deserialize<TransportFileEnvelope>(msg.Payload!)!;
                senderHandler?.Invoke(new Message { Topic = req.ReplyTo, Payload = "OK" });
            });
        using (var tempFile = new TemporaryFile($"file{fileSizeInBytes}.tmp", buffer))
        {
            var fileSender = new FileSender(messaging, null, options);
            var sendResult = await fileSender.SendInChunks(testChannel, tempFile.TempFilePath);
            Assert.Equal(FileTransferStatus.Delivered, sendResult);
        }

        var expectedCalls = fileSizeInBytes / FileSender.MaxChunkSize;
        expectedCalls += (fileSizeInBytes % FileSender.MaxChunkSize) != 0 ? 1 : 0;
        messaging.Received(Convert.ToInt32(expectedCalls)).Publish(Arg.Is<Message>(msg => msg.Topic == testChannel));
    }

    [Theory]
    [InlineData(1)] // 1 byte
    [InlineData(1024)] // 1 kByte
    [InlineData(1024 * 3)] // 3 kByte
    [InlineData(1024 * 1024)] // 1 MByte
    [InlineData(1024 * 1024 * 3)] // 3 MByte
    [InlineData(FileSender.MaxChunkSize - 1)] // excact chunk size - 1
    [InlineData(FileSender.MaxChunkSize)] // excact chunk size
    [InlineData(FileSender.MaxChunkSize + 1)] // excact chunk size + 1
    [InlineData(FileSender.MaxChunkSize * 3 - 1)] // multiple of chunk size - 1
    [InlineData(FileSender.MaxChunkSize * 3)] // multiple of chunk size
    [InlineData(FileSender.MaxChunkSize * 3 + 1)] // multiple of chunk size + 1
    public async Task SendInChunksAllowsWriteAccessToFileAfterSendingLastChunkOk(int fileSizeInBytes)
    {
        Action<Message>? senderHandler = null;
        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.When(m => m.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()))
            .Do(args => senderHandler = args.Arg<Action<Message>>());
        var options = Substitute.For<IOptions<FileSenderConfiguration>>();
        options.Value.Returns(new FileSenderConfiguration());

        var testChannel = $"tests/fileSender/{fileSizeInBytes}";
        var buffer = new byte[fileSizeInBytes];
        using (var tempFile = new TemporaryFile($"file{fileSizeInBytes}.tst", buffer))
        {
            var tempFilePath = tempFile.TempFilePath;
            messaging.When(m => m.Publish(Arg.Is<Message>(msg => msg.Topic == testChannel)))
                .Do(args =>
                {
                    var msg = args.Arg<Message>();
                    var req = JsonSerializer.Deserialize<TransportFileEnvelope>(msg.Payload!)!;
                    var props = req.TransportFile.FromDictionary();
                    if (props.LastChunk)
                    {
                        if (File.Exists(tempFilePath))
                            File.Delete(tempFilePath);
                        File.WriteAllText(tempFilePath, "Allowed to overwrite the file!");
                    }

                    senderHandler?.Invoke(new Message { Topic = req.ReplyTo, Payload = "OK" });
                });

            var fileSender = new FileSender(messaging, null, options);
            var sendResult = await fileSender.SendInChunks(testChannel, tempFile.TempFilePath);
            Assert.Equal(FileTransferStatus.Delivered, sendResult);
        }

        var expectedCalls = fileSizeInBytes / FileSender.MaxChunkSize;
        expectedCalls += (fileSizeInBytes % FileSender.MaxChunkSize) != 0 ? 1 : 0;
        messaging.Received(Convert.ToInt32(expectedCalls)).Publish(Arg.Is<Message>(msg => msg.Topic == testChannel));
    }

    [Theory]
    [InlineData(1)] // 1 byte
    [InlineData(1024)] // 1 kByte
    [InlineData(1024 * 3)] // 3 kByte
    [InlineData(1024 * 1024)] // 1 MByte
    [InlineData(1024 * 1024 * 3)] // 3 MByte
    [InlineData(FileSender.MaxChunkSize - 1)] // excact chunk size - 1
    [InlineData(FileSender.MaxChunkSize)] // excact chunk size
    [InlineData(FileSender.MaxChunkSize + 1)] // excact chunk size + 1
    [InlineData(FileSender.MaxChunkSize * 3 - 1)] // multiple of chunk size - 1
    [InlineData(FileSender.MaxChunkSize * 3)] // multiple of chunk size
    [InlineData(FileSender.MaxChunkSize * 3 + 1)] // multiple of chunk size + 1
    public async Task SendInChunksUsesSameUniqueTransferIdForEachChunkOk(int fileSizeInBytes)
    {
        Action<Message>? senderHandler = null;
        var messaging = Substitute.For<IMessagingInfrastructure>();
        messaging.When(m => m.Subscribe(Arg.Any<string>(), Arg.Any<Action<Message>>()))
            .Do(args => senderHandler = args.Arg<Action<Message>>());
        var options = Substitute.For<IOptions<FileSenderConfiguration>>();
        options.Value.Returns(new FileSenderConfiguration());

        var testChannel = $"tests/fileSender/{fileSizeInBytes}";
        var buffer = new byte[fileSizeInBytes];
        messaging.When(m => m.Publish(Arg.Is<Message>(msg => msg.Topic == testChannel)))
            .Do(args =>
            {
                var msg = args.Arg<Message>();
                var req = JsonSerializer.Deserialize<TransportFileEnvelope>(msg.Payload!)!;
                var props = req.TransportFile.FromDictionary();

                Assert.NotNull(props.TransferId);
                // each test uses a new FileSender instance, which always generates 1 as unique id for the first file being transferred
                Assert.Equal(1, props.TransferId);

                senderHandler?.Invoke(new Message { Topic = req.ReplyTo, Payload = "OK" });
            });
        using (var tempFile = new TemporaryFile($"file{fileSizeInBytes}.tmp", buffer))
        {
            var fileSender = new FileSender(messaging, null, options);
            var sendResult = await fileSender.SendInChunks(testChannel, tempFile.TempFilePath);
            Assert.Equal(FileTransferStatus.Delivered, sendResult);
        }

        var expectedCalls = fileSizeInBytes / FileSender.MaxChunkSize;
        expectedCalls += fileSizeInBytes % FileSender.MaxChunkSize != 0 ? 1 : 0;
        messaging.Received(Convert.ToInt32(expectedCalls)).Publish(Arg.Is<Message>(msg => msg.Topic == testChannel));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task SendInChunksWithMissingAnswerReturnsTimedOutOk(int expectedCalls)
    {
        const int fileSizeInBytes = 1024;

        var messaging = Substitute.For<IMessagingInfrastructure>();
        var options = Substitute.For<IOptions<FileSenderConfiguration>>();
        options.Value.Returns(new FileSenderConfiguration
        {
            RetryAttemptsForFailedChunks = expectedCalls - 1
        });

        var testChannel = $"tests/fileSender/{fileSizeInBytes}";
        var buffer = new byte[fileSizeInBytes];
        using (var tempFile = new TemporaryFile($"file{fileSizeInBytes}.tst", buffer))
        {
            var fileSender = new FileSender(messaging, null, options);
            fileSender.Timeout = 2000;
            var sendResult = await fileSender.SendInChunks(testChannel, tempFile.TempFilePath);
            Assert.Equal(FileTransferStatus.TimedOut, sendResult);
        }

        messaging.Received(expectedCalls).Publish(Arg.Is<Message>(msg => msg.Topic == testChannel));
    }
}