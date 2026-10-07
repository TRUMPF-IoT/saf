// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Storage.Cde.Tests;
using Xunit;

/// <summary>
/// The tests that run on the C-DEngine node of the test process. They share the <see cref="CdeFixture"/> and
/// run one after another, so the lease counts they check are their own.
/// </summary>
[CollectionDefinition(Name)]
public class CdeCollection : ICollectionFixture<CdeFixture>
{
    public const string Name = "Cde";
}