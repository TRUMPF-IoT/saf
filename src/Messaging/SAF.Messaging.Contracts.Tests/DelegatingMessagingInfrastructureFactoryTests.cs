// SPDX-FileCopyrightText: 2017-2026 TRUMPF Laser SE
//
// SPDX-License-Identifier: MPL-2.0

namespace SAF.Messaging.Contracts.Tests;

using NSubstitute;
using Xunit;

public class DelegatingMessagingInfrastructureFactoryTests
{
    [Fact]
    public void Constructor_WhenKeyIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegatingMessagingInfrastructureFactory(null!, _ => Substitute.For<IMessagingInfrastructure>()));
    }

    [Fact]
    public void Constructor_WhenFactoryIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DelegatingMessagingInfrastructureFactory("key", null!));
    }

    [Fact]
    public void Create_WhenConfigurationIsNull_ThrowsArgumentNullException()
    {
        var sut = new DelegatingMessagingInfrastructureFactory("key", _ => Substitute.For<IMessagingInfrastructure>());

        Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
    }

    [Fact]
    public void Create_WhenConfigurationIsValid_UsesDelegateAndReturnsInfrastructure()
    {
        var expectedInfrastructure = Substitute.For<IMessagingInfrastructure>();
        var configuration = new MessagingConfiguration { Key = "routing" };
        MessagingConfiguration? capturedConfiguration = null;

        var sut = new DelegatingMessagingInfrastructureFactory("key", input =>
        {
            capturedConfiguration = input;
            return expectedInfrastructure;
        });

        var result = sut.Create(configuration);

        Assert.Same(expectedInfrastructure, result);
        Assert.Same(configuration, capturedConfiguration);
    }
}
