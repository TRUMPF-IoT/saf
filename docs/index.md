# Smart Application Framework (SAF)

The Smart Application Framework (SAF) is an open-source and cross-plattform framework for building distributed applications across cloud and edge. It allows developers to build resilient, stateless and stateful plug-ins that run on cloud and edge.

SAF runs on [.NET Core](https://dotnet) and can easily be integrated into [ASP.NET Core](https://docs.microsoft.com/aspnet/core) applications. It utilizes Microsoft's .NET Core [Dependency Injection](https://docs.microsoft.com/aspnet/core/fundamentals/dependency-injection) to provide a main DI container that loads various plug-ins. At its base, it provides exchangeable messaging and storage infrastructure as well as a logging interface.

## Overview

To build a distributed application using SAF you need at least two things. A [SAF Host](#-SAF-Host) and a [SAF Plug-in](#-SAF-Plug-in).

### SAF Host

A Smart Application Framework Host or `ServiceHost` loads one or more SAF Plug-ins. It is also possible to run several SAF Hosts with different plug-ins in certain IT infrastructures. 

It initializes its own main DI container that contains the [SAF Infrastructure Services](./infrastructureAndToolboxServices.md). The `ServiceHost` is responsible to create one DI container for every loaded Plug-in. So every loaded SAF Plug-in gets it's own DI container. It then  redirects the SAF Infrastructure Services to every plug-in specific DI containers.

### SAF Plug-in

When implementing a Smart Application Framework Plug-in you are able to extend your Smart Application with whatever functionality you need. Plug-ins are losely coupled through SAFs messaging infrastructure.

A SAF Plug-in will be loaded from the SAF Host that creates a DI container the contains the [SAF Infrastructure Services](./infrastructureAndToolboxServices.md). It must provide excactly one public implementation of `IServiceAssemblyManifest` that describes the Plug-in and registers the plug-ins dependencies in the DI container provided by the host.

Your are able to register [SAF Toolbox Services](./infrastructureAndToolboxServices.md) that may helps you to rapidly implement your plug-in functionality.