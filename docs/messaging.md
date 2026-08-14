# Messaging Infrastructure

SAF's messaging infrastructure provides an **exchangeable pub/sub message bus** used by plug-ins to communicate with each other — both within the same host process and across distributed host instances.

## Interface

```csharp
namespace SAF.Messaging.Contracts;

public interface IMessagingInfrastructure
{
    void   Publish(Message message);
    object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler;
    object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler;
    object Subscribe(Action<Message> handler);
    object Subscribe(string routeFilterPattern, Action<Message> handler);
    void   Unsubscribe(object subscription);
}
```

`routeFilterPattern` is a **regular expression** applied to `Message.Topic`.

## The Message Type

```csharp
public class Message
{
    public string  Topic            { get; set; }   // routing key / topic
    public string? Payload          { get; set; }   // usually JSON
    public List<MessageCustomProperty>? CustomProperties { get; set; }
}
```

---

## Architecture: Two-Layer Registration

SAF uses a **factory pattern** so the same messaging backend can be used both as a directly-injected `IMessagingInfrastructureFactory` (keyed) and as the primary `IMessagingInfrastructure` (resolved by `SAF.Messaging.Runtime`):

```mermaid
graph LR
    MPLUG["Messaging plug-in\n(e.g. SAF.Messaging.InProcess)"] -->|keyed singleton| F["IMessagingInfrastructureFactory\n(key = 'InProcess')"]
    RUNTIME["SAF.Messaging.Runtime\n(PluginManifest)"] -->|reads Messaging:PrimaryKey| F
    RUNTIME -->|registers| I[IMessagingInfrastructure]
    I -->|imported into| PA[Plugin A]
    I -->|imported into| PB[Plugin B]
```

Each messaging implementation is itself a **plug-in**: its `PluginManifest` registers a keyed `IMessagingInfrastructureFactory`. The separate `SAF.Messaging.Runtime` plug-in reads `Messaging:PrimaryKey` from configuration, selects the matching factory, and registers the resulting `IMessagingInfrastructure` (plus `IServiceMessageDispatcher`). Because these types live in `SAF.Messaging.Contracts.dll` — a public contract assembly — they are imported into every plugin container.

**You do not register messaging in host code.** Instead you:
1. Make the implementation DLL discoverable (add it to a plugin folder container's `IncludePatterns`, e.g. `SAF.Messaging.InProcess.dll`).
2. Set `Messaging:PrimaryKey` to that implementation's key.
3. Provide the implementation's configuration section (e.g. `Redis`, `Nats`) where required.

When you use `builder.AddSafHost()`, `SAF.Messaging.Runtime.dll` is loaded automatically and `SAF.Messaging.Contracts.dll` is added to `PluginContractsSearchPattern` for you. If you use the plugin system without `SAF.Hosting`, you must include both yourself.

---

## Available Implementations

For each implementation, add its DLL to your plugin discovery `IncludePatterns` and set `Messaging:PrimaryKey`. The `Add*Infrastructure` extension methods shown are what each plug-in's own `PluginManifest` calls internally — you normally only supply configuration.

### In-Process (Development / Tests)

Messages are dispatched synchronously within the same process. No external dependencies.

**Package / plug-in DLL:** `SAF.Messaging.InProcess` (`SAF.Messaging.InProcess.dll`)

```json
{
  "Messaging": { "PrimaryKey": "InProcess" }
}
```

### Redis

Backed by [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis). Suitable for multi-process or multi-machine deployments. The Redis plug-in registers **both** a messaging factory and `IStorageInfrastructure`.

**Package / plug-in DLL:** `SAF.Messaging.Redis` (`SAF.Messaging.Redis.dll`)

```json
{
  "Messaging": { "PrimaryKey": "Redis" },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Timeout": 60000
  }
}
```

> The plug-in reads the `Redis` section from the plugin settings file, falling back to host configuration.

### NATS

Backed by [NATS.Net](https://nats.io). High-performance, cloud-native messaging. Also provides NATS-backed storage.

**Package / plug-in DLL:** `SAF.Messaging.NATS` (`SAF.Messaging.NATS.dll`)

```json
{
  "Messaging": { "PrimaryKey": "Nats" },
  "Nats": { "Url": "nats://localhost:4222" }
}
```

### C-DEngine

Backed by [C-DEngine](https://github.com/TRUMPF-IoT/C-DEngine), a mesh-network framework for industrial IoT.

**Package / plug-in DLL:** `SAF.Messaging.Cde` (`SAF.Messaging.Cde.dll`)

```json
{
  "Messaging": { "PrimaryKey": "Cde" },
  "Cde": { /* C-DEngine options */ }
}
```

### Routing (Multiple Brokers)

Routes messages across multiple messaging infrastructures based on topic patterns. Load the routing plug-in **and** each backend plug-in it references (e.g. `SAF.Messaging.InProcess.dll;SAF.Messaging.Redis.dll;SAF.Messaging.Routing.dll`), then configure the routes under `MessageRouting`.

**Package / plug-in DLL:** `SAF.Messaging.Routing` (`SAF.Messaging.Routing.dll`)

```json
{
  "Messaging": { "PrimaryKey": "Routing" },
  "MessageRouting": {
    "Routings": [
      {
        "Messaging": { "Key": "InProcess" },
        "PublishPatterns": [ "local/.*" ],
        "SubscriptionPatterns": [ "local/.*" ]
      },
      {
        "Messaging": { "Key": "Redis" },
        "PublishPatterns": [ "remote/.*" ],
        "SubscriptionPatterns": [ "remote/.*" ]
      }
    ]
  },
  "Redis": { "ConnectionString": "localhost:6379" }
}
```

---

## How-To: Publish a Message

```csharp
public class OrderService(IMessagingInfrastructure messaging)
{
    public void PlaceOrder(Order order)
    {
        var payload = JsonSerializer.Serialize(order);
        messaging.Publish(new Message
        {
            Topic   = "orders/placed",
            Payload = payload
        });
    }
}
```

---

## How-To: Subscribe with a Lambda

```csharp
public class OrderNotifier(IMessagingInfrastructure messaging, ILogger<OrderNotifier> logger)
{
    private object? _subscription;

    public void Start()
    {
        _subscription = messaging.Subscribe(
            routeFilterPattern: @"orders/.*",
            handler: msg =>
            {
                var order = JsonSerializer.Deserialize<Order>(msg.Payload!);
                logger.LogInformation("Order received: {Id}", order?.Id);
            });
    }

    public void Stop() => messaging.Unsubscribe(_subscription!);
}
```

---

## How-To: Subscribe with a Typed Message Handler

Typed handlers implement `IMessageHandler` and are resolved from the plugin's DI container — useful when the handler itself has dependencies.

```csharp
public class OrderMessageHandler(IOrderRepository repository) : IMessageHandler
{
    public bool CanHandle(Message message) =>
        message.Topic.StartsWith("orders/", StringComparison.Ordinal);

    public void Handle(Message message)
    {
        var order = JsonSerializer.Deserialize<Order>(message.Payload!);
        repository.Save(order!);
    }
}
```

Register in the plugin manifest:

```csharp
public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
{
    pluginServices.AddSingleton<IOrderRepository, OrderRepository>();
    pluginServices.AddSingleton<OrderMessageHandler>();
    pluginServices.AddMessageHandlerResolver();  // from SAF.Messaging.Extensions
}
```

Subscribe in `IServicePlugin.StartAsync`:

```csharp
_subscription = messaging.Subscribe<OrderMessageHandler>(@"orders/.*");
```

---

## How-To: Request / Reply Pattern

Use the `IRequestClient` from `SAF.Toolbox` for request/reply over messaging. See [Toolbox Services → Request Client](./toolbox.md#request-client).

---

## How-To: Implement a Custom Messaging Infrastructure

Create a class that implements `IMessagingInfrastructure`:

```csharp
public class MyCustomMessaging : IMessagingInfrastructure
{
    public void Publish(Message message) { /* publish via your broker */ }

    public object Subscribe<TMessageHandler>() where TMessageHandler : IMessageHandler
        => Subscribe<TMessageHandler>(pattern: ".*");

    public object Subscribe<TMessageHandler>(string routeFilterPattern) where TMessageHandler : IMessageHandler
    {
        // Subscribe and return an opaque subscription handle
        return new object();
    }

    public object Subscribe(Action<Message> handler) => Subscribe(".*", handler);

    public object Subscribe(string routeFilterPattern, Action<Message> handler)
    {
        // Store handler with pattern, return handle
        return new object();
    }

    public void Unsubscribe(object subscription) { /* remove subscription */ }
}
```

Expose it through a plug-in. Provide an extension method that registers a keyed factory, then call it from your plug-in's `PluginManifest` (mirroring how the built-in implementations work):

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMyBrokerMessagingInfrastructure(this IServiceCollection services)
        => services.AddKeyedSingleton<IMessagingInfrastructureFactory>("MyBroker",
            (sp, _) => new DelegatingMessagingInfrastructureFactory(
                "MyBroker",
                cfg => new MyCustomMessaging()));
}

public class PluginManifest : IPluginManifest
{
    public void ConfigureServices(IPluginSystemHostContext context, IServiceCollection pluginServices)
        => pluginServices.AddMyBrokerMessagingInfrastructure();
}
```

Deploy the plug-in DLL (add it to your plugin discovery `IncludePatterns`) and select it:

```json
{ "Messaging": { "PrimaryKey": "MyBroker" } }
```

The `SAF.Messaging.Runtime` plugin resolves your keyed factory and registers it as `IMessagingInfrastructure`. With `AddSafHost()`, the runtime plugin is loaded automatically; otherwise include `SAF.Messaging.Runtime.dll` in your own plugin discovery setup.

---

## Well-Known Keys

```csharp
public static class MessagingInfrastructureKeys
{
    public const string Routing   = "Routing";
    public const string InProcess = "InProcess";
    public const string Redis     = "Redis";
    public const string Cde       = "Cde";
    public const string Nats      = "Nats";
}
```
