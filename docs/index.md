# Smart Application Framework (SAF)

The Smart Application Framework (SAF) is an open-source and cross-plattform framework for building distributed applications across cloud and edge. It allows developers to build resilient, stateless and stateful plug-ins that run on cloud and edge.

SAF runs on [.NET Core](https://dotnet) and can easily be integrated into [ASP.NET Core](https://docs.microsoft.com/aspnet/core) applications. It utilizes Microsoft's .NET Core [Dependency Injection](https://docs.microsoft.com/aspnet/core/fundamentals/dependency-injection) to provide a main Dependency Injection (DI) container that loads various plug-ins. At its base, it provides exchangeable messaging and storage infrastructure services as well as a logging interface.

## Overview

A distributed application built upon the Service Application Framework (SAF) consists at least of two things: A [SAF Host](#saf-host) and a [SAF Plug-in](#saf-plug-in). The SAF Host is responsible for loading and initializing the SAF Infrastruture Services and for loading and initializing the SAF Plug-ins. It provides the SAF Infrastrutcure Services to the SAF Plug-ins using Microsofts's .NET Core Dependency Injection.

![SAF Application Overview](/diagrams/saf-overview.svg).

Additionaly SAF provides the SAF Toolbox Services which are a set of useful tools to support you with common tasks when implementing your SAF Plug-in functionality.

### SAF Host

A Smart Application Framework Host or `ServiceHost` loads one or more SAF Plug-ins during runtime. It is also possible to run several SAF Hosts with different plug-ins in certain IT infrastructures. 

It initializes its own main Dependency Injection (DI) container that contains the [SAF Infrastructure Services](./infrastructureAndToolboxServices.md#saf-infrastructure-services). The `ServiceHost` is responsible to create one DI container for every loaded Plug-in. So every loaded SAF Plug-in gets it's own DI container. It then  redirects the SAF Infrastructure Services to every plug-in specific DI containers.

For a better understanding of the Dependency Injection (DI) container concept of SAF see [SAF DI Container Overview](./diContainerOverview.md).

### SAF Plug-in

When implementing a Smart Application Framework Plug-in you are able to extend your Smart Application with whatever functionality you need. Plug-ins are losely coupled through SAFs messaging infrastructure.

A SAF Plug-in will be loaded from the SAF Host that creates a Dependency Injection (DI) container the contains the [SAF Infrastructure Services](./infrastructureAndToolboxServices.md#saf-infrastructure-services). It must provide excactly one public implementation of `IServiceAssemblyManifest` that describes the Plug-in and registers the plug-ins dependencies in the plug-in specific DI container provided by the host.

Your are able to register [SAF Toolbox Services](./infrastructureAndToolboxServices.md#saf-toolbox-services) in the Plug-in specific DI container that may help you to rapidly implement your plug-in functionality.

For a better understanding of the Dependency Injection (DI) container concept of SAF see [SAF DI Container Overview](./diContainerOverview.md).