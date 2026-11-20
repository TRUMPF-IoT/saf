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
/// The file store calls the overloads that take the secret's name. By default they ignore the name and
/// call <see cref="Protect(byte[])"/> and <see cref="Unprotect(byte[])"/>, so a protector that does not
/// care about names implements only those two. A protector that binds a payload to its name, such as
/// the signing decorator, implements the named overloads; a decorator around another protector must
/// forward them, or the name never reaches the protector it wraps.
/// </remarks>
public interface ISecretProtector
{
    /// <summary>
    /// The stable, case-insensitive identifier of this protector (e.g. <c>"pkcs"</c> or <c>"dpapi"</c>).
    /// It can be stamped alongside a protected payload so a reader knows which protector produced it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns the self-describing protected payload.
    /// </summary>
    /// <param name="plaintext">The raw secret bytes to protect.</param>
    /// <returns>The encrypted payload, safe to persist at rest.</returns>
    byte[] Protect(byte[] plaintext);

    /// <summary>
    /// Decrypts a payload previously produced by <see cref="Protect(byte[])"/> and returns the plaintext bytes.
    /// </summary>
    /// <param name="protectedData">The protected payload to decrypt.</param>
    /// <returns>The recovered plaintext bytes.</returns>
    byte[] Unprotect(byte[] protectedData);

    /// <summary>
    /// Protects the value of the secret <paramref name="name"/>. The default ignores the name and calls
    /// <see cref="Protect(byte[])"/>.
    /// </summary>
    /// <param name="name">The physical name the secret is stored under.</param>
    /// <param name="plaintext">The raw secret bytes to protect.</param>
    /// <returns>The protected payload, safe to persist at rest.</returns>
    byte[] Protect(string name, byte[] plaintext) => Protect(plaintext);

    /// <summary>
    /// Recovers the value of the secret <paramref name="name"/> from a payload produced by
    /// <see cref="Protect(string, byte[])"/>. The default ignores the name and calls
    /// <see cref="Unprotect(byte[])"/>.
    /// </summary>
    /// <param name="name">The physical name the payload was found under.</param>
    /// <param name="protectedData">The protected payload to decrypt.</param>
    /// <returns>The recovered plaintext bytes.</returns>
    byte[] Unprotect(string name, byte[] protectedData) => Unprotect(protectedData);
}
