# Toolbox Services

The SAF Toolbox (`SAF.Toolbox`) is a collection of ready-made helpers that plug-in authors can register in their plugin's DI container to simplify common tasks.

---

## Heartbeat

A testable, configurable timer that fires a `Beat` event at a fixed interval. Prefer this over `System.Threading.Timer` inside plug-ins because it is easier to control in tests.

### Registration

```csharp
// Single heartbeat
pluginServices.AddHeartbeat(heartbeatMillis: 1000);

// Pool (lazily creates heartbeats at different rates)
pluginServices.AddHeartbeatPool();
```

### Usage

```csharp
public class PollingService(IHeartbeat heartbeat) : IServicePlugin, IDisposable
{
    public Task StartAsync(CancellationToken token)
    {
        heartbeat.Beat += OnBeat;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        heartbeat.Beat -= OnBeat;
        return Task.CompletedTask;
    }

    private void OnBeat(object? sender, HeartbeatEventArgs e)
    {
        // called every BeatCycleTimeMillis
        Console.WriteLine($"Beat #{e.BeatCount}");
    }

    public void Dispose() { /* unsubscribe if needed */ }
}
```

### IHeartbeat Members

| Member | Description |
|---|---|
| `Beat` | Event fired each cycle |
| `BeatCycleTimeMillis` | Configured cycle time |
| `CurrentBeat` | Monotonically increasing beat counter |
| `WallClockTimeAgo(n)` | Convert `n` beats to a `TimeSpan` |

### Heartbeat Pool

When different services need different rates, use `IHeartbeatPool`:

```csharp
public class MultiRateService(IHeartbeatPool pool)
{
    public void Start()
    {
        var fast = pool.GetOrCreateHeartbeat(500);   // 500 ms
        var slow = pool.GetOrCreateHeartbeat(5000);  // 5 000 ms

        fast.Beat += (_, _) => PollSensor();
        slow.Beat += (_, _) => ReportStatus();
    }
}
```

---

## Request Client

Implements the request/reply pattern on top of `IMessagingInfrastructure`. The caller publishes a request message and awaits a single reply on a dynamically generated reply topic.

### Registration

```csharp
pluginServices.AddRequestClient();
```

### Usage

Define request and response types (typically in a shared contracts assembly):

```csharp
// Contracts
public class GetOrderRequest : MessageRequestBase
{
    public string OrderId { get; set; } = default!;
}

public class GetOrderResponse
{
    public string OrderId { get; set; } = default!;
    public string Status  { get; set; } = default!;
}
```

Send a request and await the first matching reply:

```csharp
public class OrderClient(IRequestClient client)
{
    public async Task<GetOrderResponse?> GetOrderAsync(string orderId)
    {
        return await client.SendRequestAwaitFirstAnswer<GetOrderRequest, GetOrderResponse>(
            topic: "orders/get",
            request: new GetOrderRequest { OrderId = orderId },
            millisecondsTimeoutTarget: 5000);
    }
}
```

Respond from another plugin:

```csharp
public class OrderResponder(IMessagingInfrastructure messaging) : IMessageHandler
{
    public bool CanHandle(Message message) =>
        message.Topic == "orders/get";

    public void Handle(Message message)
    {
        var request  = JsonSerializer.Deserialize<GetOrderRequest>(message.Payload!);
        var response = new GetOrderResponse
        {
            OrderId = request!.OrderId,
            Status  = "Confirmed"
        };

        // Reply to the reply channel carried in the request
        messaging.Publish(new Message
        {
            Topic   = request.ReplyTo!,
            Payload = JsonSerializer.Serialize(response)
        });
    }
}
```

---

## File Handling

Provides an `IDirectoryInfo` rooted at `IServiceHostInfo.FileSystemUserBasePath` — a safe, host-managed scratch area for file I/O.

### Registration

```csharp
pluginServices.AddFileHandling();
```

### Usage

```csharp
public class ConfigLoader(IDirectoryInfo baseDir)
{
    public string LoadConfig(string name)
    {
        var file = baseDir.FileSystem.FileInfo.New(
            Path.Combine(baseDir.FullName, name));
        return file.Exists ? file.FileSystem.File.ReadAllText(file.FullName) : string.Empty;
    }
}
```

---

## File Transfer

Transfers files between SAF hosts by chunking them over the messaging infrastructure. Suitable for sending firmware updates, log bundles, or configuration packages between distributed nodes.

### Components

| Type | Description |
|---|---|
| `IFileSender` | Chunks a file and transfers it to a topic (`SendAsync(topic, fullFilePath, timeoutMs)`) |
| `IStatefulFileReceiverFactory` | Creates a stateful receiver bound to a destination folder (`CreateForFolder(folderPath)`) |
| `IStatefulFileReceiver` | Reassembles incoming chunks; raises `TargetFilePathResolved`, `BeforeFileReceived`, `FileReceived` |
| `IFileReceiver` | Wires a stateful receiver to a topic (`Subscribe(topic, receiver)` / `Unsubscribe`) |

### Registration

```csharp
pluginServices.AddFileHandling();
pluginServices.AddFileSender();
pluginServices.AddFileReceiver();  // also registers IStatefulFileReceiverFactory
```

### Sending a File

`SendAsync` takes the destination topic, the full path of the file, and a timeout in milliseconds. It returns a `FileTransferStatus`.

```csharp
public class FirmwareUpdater(IFileSender sender)
{
    public async Task<FileTransferStatus> SendFirmwareAsync(string filePath, string targetTopic)
    {
        return await sender.SendAsync(targetTopic, filePath, timeoutMs: 60_000)
            .ConfigureAwait(false);
    }
}
```

### Receiving a File (Stateful)

Create a receiver for a destination folder, subscribe it to a topic via `IFileReceiver`, and handle the `FileReceived` event.

```csharp
public class FirmwareReceiver(
    IFileReceiver fileReceiver,
    IStatefulFileReceiverFactory receiverFactory) : IServicePlugin
{
    private const string Topic = "firmware/update";
    private IStatefulFileReceiver? _receiver;

    public Task StartAsync(CancellationToken token)
    {
        _receiver = receiverFactory.CreateForFolder("./firmware");

        _receiver.FileReceived += (_, e) =>
            Console.WriteLine($"Firmware received: {e.LocalFileFullName}");

        fileReceiver.Subscribe(Topic, _receiver);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken token)
    {
        fileReceiver.Unsubscribe(Topic);
        _receiver?.Dispose();
        return Task.CompletedTask;
    }
}
```

---

## Serialization Helpers

`SAF.Toolbox` includes `IJsonObjectConverter` implementations used by `IRequestClient` for custom JSON serialization. Inject an array of converters into the `SendRequestAwaitFirstAnswer` overload when your payload types require non-default serialization.

---

## Full Registration Reference

```csharp
// In IPluginManifest.ConfigureServices:

pluginServices.AddHeartbeat(1000);         // single heartbeat, 1 second
pluginServices.AddHeartbeatPool();          // pool for multiple rates

pluginServices.AddRequestClient();          // request/reply helper

pluginServices.AddFileHandling();           // IDirectoryInfo

pluginServices.AddFileSender();             // IFileSender
pluginServices.AddFileReceiver();           // IFileReceiver + IStatefulFileReceiverFactory
```
