# Smart Application Framework (SAF)

[![License](https://img.shields.io/github/license/trumpf-iot/saf)](https://github.com/TRUMPF-IoT/saf/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/SAF.Common)](https://www.nuget.org/packages/SAF.Common)
[![.NET](https://github.com/trumpf-iot/saf/actions/workflows/dotnet-core.yml/badge.svg?branch=master)](https://github.com/trumpf-iot/saf/actions/workflows/dotnet-core.yml)

SAF is an open-source, cross-platform framework for building distributed applications across cloud and edge.
It is designed around isolated plug-ins that communicate through messaging, so application components stay loosely coupled, independently deployable, and easy to evolve.

## Project Summary

SAF builds on top of the .NET Generic Host and provides a production-focused foundation for modular systems:

- **Plugin system** with assembly isolation and dedicated DI containers per plug-in
- **SAF host integration** for plug-in discovery, host identity, and consistent startup wiring
- **Exchangeable messaging infrastructure** (In-Process, Redis, NATS, C-DEngine, Routing)
- **Exchangeable storage infrastructure** (LiteDB, SQLite, Redis, C-DEngine)
- **Toolbox services** such as Heartbeat, Request/Reply, and File Transfer helpers

This combination allows teams to run the same architectural model in both edge and cloud scenarios while keeping infrastructure choices flexible.

## Read the Full Documentation

For architecture details, package-level guidance, and end-to-end setup instructions, see the generated GitHub Pages documentation:

- **https://trumpf-iot.github.io/saf/**

Start with the overview and continue with Getting Started, Plugin System, Messaging, Storage, and Toolbox sections.

## How to Engage, Contribute, and Give Feedback

Contributions are welcome. Try SAF in your own projects, open issues, join design discussions, and submit pull requests.
