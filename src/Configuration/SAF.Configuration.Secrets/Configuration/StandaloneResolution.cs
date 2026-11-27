// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Configuration;

using Microsoft.Extensions.Logging;
using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// What the standalone <c>AddResolvedSecrets(IConfigurationBuilder, ...)</c> overload passes instead of a host
/// container.
/// </summary>
internal sealed record StandaloneResolution(ISecretReferencePolicy? Policy, ILoggerFactory? LoggerFactory);
