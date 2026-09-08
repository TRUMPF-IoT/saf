// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Provides read and write access to a secure secret store. This is the consumer-facing service
/// that is registered in dependency injection and forwarded into plugin containers.
/// </summary>
public interface ISecretStore : ISecretReader, ISecretWriter;
