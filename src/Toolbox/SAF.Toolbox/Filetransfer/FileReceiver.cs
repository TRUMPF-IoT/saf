// SPDX-FileCopyrightText: 2017-2020 TRUMPF Laser GmbH
//
// SPDX-License-Identifier: MPL-2.0


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SAF.Common;
using SAF.Toolbox.Serialization;

namespace SAF.Toolbox.FileTransfer;

internal class FileReceiver : IFileReceiver
{
    private const string Ok = "OK";
    private const string Nok = "NOK";
    private readonly IMessagingInfrastructure _messaging;
    private readonly ILogger<FileReceiver> _log;
    private readonly Dictionary<string, object> _subscriptions = new();

    public FileReceiver(IMessagingInfrastructure messaging, ILogger<FileReceiver>? log)
    {
        _messaging = messaging ?? throw new ArgumentNullException(nameof(messaging));
        _log = log ?? NullLogger<FileReceiver>.Instance;
    }

    public void Subscribe(string topic, Action<TransportFileDelivery> callback)
    {
        if(string.IsNullOrEmpty(topic)) throw new ArgumentException("Topic must not be empty", nameof(topic));
        if(callback == null) throw new ArgumentNullException(nameof(callback));
            
        var subscription = _messaging.Subscribe(topic, message =>
        {
            HandleMessage(callback, message);
        });

        _subscriptions.Add(topic, subscription);
    }

    public void Unsubscribe(string topic)
    {
        if(string.IsNullOrEmpty(topic)) throw new ArgumentException("Topic must not be empty", nameof(topic));

        var isKnown = _subscriptions.TryGetValue(topic, out var subscription);
        if(!isKnown || subscription == null) return;

        _subscriptions.Remove(topic);
        _messaging.Unsubscribe(subscription);
    }

    public void Unsubscribe()
    {
        foreach(var entry in _subscriptions)
        {
            _messaging.Unsubscribe(entry.Value);
        }

        _subscriptions.Clear();
    }

    private void HandleMessage(Action<TransportFileDelivery> callback, Message message)
    {
        var envelope = ReadEnvelope(message);
        if(envelope == null)
        {
            return;
        }

        var properties = ReadTransportFile(envelope);
        if(properties.Count == 0)
        {
            SendAcknowledgement(envelope, false);
            return;
        }

        var transportFile = ConvertToTransportFile(properties);
        var isConsistent = transportFile.Verify();
        var delivery = new TransportFileDelivery
        {
            IsConsistent = isConsistent,
            Timestamp = DateTimeOffset.Now,
            Channel = message.Topic,
            TransportFile = transportFile
        };

        SendAcknowledgement(envelope, isConsistent);
        callback.Invoke(delivery);
    }

    private void SendAcknowledgement(TransportFileEnvelope envelope, bool isConsistent)
    {
        var payload = isConsistent ? Ok : Nok;

        envelope.TransportFile.TryGetValue("Name", out var fileName);
        _log.LogDebug($"Send ack to {envelope.ReplyTo}, payload={payload}, file={fileName}");

        var message = new Message
        {
            Topic = envelope.ReplyTo,
            Payload = payload
        };
        _messaging.Publish(message);
    }

    private static TransportFile ConvertToTransportFile(IDictionary<string, string> properties)
    {
        properties.TryGetValue("Name", out var name);
        properties.TryGetValue("MimeType", out var mimeType);
        properties.TryGetValue("Content", out var content);
        properties.TryGetValue("OriginalLength", out var originalLengthAsString);

        var originalLength = 0L;
        if (!string.IsNullOrEmpty(originalLengthAsString))
            long.TryParse(originalLengthAsString, out originalLength);

        var transportFile = new TransportFile(name!, mimeType, properties) { Content = content!, OriginalLength = originalLength };

        return transportFile;
    }

    private static IDictionary<string, string> ReadTransportFile(TransportFileEnvelope envelope) => envelope.TransportFile;

    private static TransportFileEnvelope? ReadEnvelope(Message message)
    {
        TransportFileEnvelope? envelope = null;
        try
        {
            if(string.IsNullOrEmpty(message.Payload)) return null;
            envelope = JsonSerializer.Deserialize<TransportFileEnvelope>(message.Payload);
        }
        catch
        {
            return envelope;
        }

        return envelope;
    }
}