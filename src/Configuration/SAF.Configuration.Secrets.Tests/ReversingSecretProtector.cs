// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Configuration.Secrets.Tests;

using SAF.Configuration.Secrets.Contracts;

/// <summary>
/// A deterministic, reversible stand-in for a real <see cref="ISecretProtector"/>, so store behaviour
/// can be asserted without depending on certificate crypto. Reversing the bytes keeps plaintext out of
/// the persisted payload while remaining trivially invertible.
/// </summary>
internal sealed class ReversingSecretProtector(string name = "fake") : ISecretProtector
{
    public string Name => name;

    public byte[] Protect(byte[] plaintext)
    {
        var copy = (byte[])plaintext.Clone();
        Array.Reverse(copy);
        return copy;
    }

    public byte[] Unprotect(byte[] protectedData)
    {
        var copy = (byte[])protectedData.Clone();
        Array.Reverse(copy);
        return copy;
    }
}
