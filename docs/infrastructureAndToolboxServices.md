# Infrastructure and Toolbox Services

To provide commonly used services to the plug-in assemblies, two different types of infrastructure services are known:

- [SAF Infrastructure Services](#saf-infrastructure-services)
- [SAF Toolbox Services](#saf-toolbox-services)

As a service consumer (typically a SAF Plug-in), it doesn't really matter how or better where the service is implemented. But in case the framework needs to be extended with commonly used services, they should be placed in the right place.

## SAF Infrastructure Services

The SAF infrastructure services are based on the underlying host. The host is responsible to add and initialize them. Usually, these are replaceable based on the underlying system and your requirements.

They mainly provides interfaces for message based communication and also for storing some application relevant data.

Communication between plug-ins running in the same host instance as well as the communication between plug-ins running in different SAF Host instances is done through SAFs messaging infrastructure, which mainly provides a pub/sub messaging service.

Storing data is done using SAFs storage infrastructure which allows the storage of key/value pairs.

### Messaging Infrastructure

SAFs messaging infrastructure provides a pub/sub messaging service used from SAF Plug-ins to communicate with each other. To use it inject `IMessagingInfrastructure` into your class.

Currently the following implementations are available:

* [C-DEngine](https://github.com/TRUMPF-IoT/C-DEngine): Allows the usage of the C-DEngine as base messaging infrastructure. SAF extends the C-DEngine messaging possibilities with cross-network and cross-infrastrucure pub/sub communication. This allows to build highly distributed applications based on a mesh network across cloud and edge.

* [Redis](https://redis.io): Uses [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) as Redis client.

* In-memory/In-process: Typically used for local-only applications or test environments.

* Message routing: Allows the usage of multiple message infrastructures and configures routing of messages between them.

You are allowed to choose whatever implementation you need for your use case. Also feel free to use your own pub/sub messaging infrastructure implementation.

### Storage Infrastructure

SAFs storage infrastructure provides a service to store key/value pairs. The storage is local to one single SAF Host and can be used by all Plug-ins loaded from that host instance. To use it inject `IStorageInfrastructure` into your class.

Currently the following implementations are available:

* [C-DEngine](https://github.com/TRUMPF-IoT/C-DEngine): Uses the C-DEngines storage mechanism to locally store data.

* [LiteDb](https://www.litedb.org): Embedded NoSQL database for .NET

You are allowed to choose whatever implementation you need for your use case. Also feel free to use your own storage infrastructure implementation.

## SAF Toolbox Services

SAF Toolbox Services are just tools to support and streamline SAF Plug-in development. They are initialized by the plug-in and contain useful stuff to simplify your life. They can be found in the `SAF.Toolbox` namespace.

Examples of such services are:

- Heartbeat
- Request Client
- File Handling
- File Transfer (including File Sender and File Receiver)
- ...

Sometimes, toolbox services have a dependencies on infrastructure services.