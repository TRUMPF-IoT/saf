// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Contracts;
/// <summary>
/// Protects secret payloads at rest, decoupling <em>how</em> a secret is encrypted from <em>where</em>
/// it is stored. The file-based secret store delegates its encryption to an implementation of this
/// interface, so new protection mechanisms (e.g. a Windows DPAPI-backed protector) can be added
/// additively without modifying the store (open/closed principle). The default, cross-platform
/// implementation uses PKCS#7/CMS enveloping.
/// </summary>
/// <remarks>
/// Every call carries the physical name of the secret. A protector that does not need it ignores it; one
/// that binds a payload to its name, such as the signing decorator, refuses a payload found under another
/// name. A decorator passes the name on to the protector it wraps.
/// </remarks>
public interface ISecretProtector
{
    /// <summary>
    /// The stable, case-insensitive identifier of this protector (e.g. <c>"pkcs"</c> or <c>"dpapi"</c>).
    /// It can be stamped alongside a protected payload so a reader knows which protector produced it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/>, the value of the secret <paramref name="name"/>, and returns
    /// the self-describing protected payload.
    /// </summary>
    /// <param name="name">The physical name the secret is stored under.</param>
    /// <param name="plaintext">The raw secret bytes to protect.</param>
    /// <returns>The encrypted payload, safe to persist at rest.</returns>
    byte[] Protect(string name, byte[] plaintext);

    /// <summary>
    /// Decrypts a payload previously produced by <see cref="Protect"/> and returns the plaintext bytes.
    /// </summary>
    /// <param name="name">The physical name the payload was found under.</param>
    /// <param name="protectedData">The protected payload to decrypt.</param>
    /// <returns>The recovered plaintext bytes.</returns>
    byte[] Unprotect(string name, byte[] protectedData);
}
