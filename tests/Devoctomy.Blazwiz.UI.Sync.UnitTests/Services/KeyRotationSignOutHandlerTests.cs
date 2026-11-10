using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class KeyRotationSignOutHandlerTests
{
    private readonly Mock<ISyncKeyRotationService> mockRotation = new();
    private readonly KeyRotationSignOutHandler sut;

    public KeyRotationSignOutHandlerTests()
    {
        var services = new ServiceCollection().AddSingleton(mockRotation.Object).BuildServiceProvider();
        sut = new KeyRotationSignOutHandler(services);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => new KeyRotationSignOutHandler(null!)).ParamName);
    }

    [Fact]
    public async Task GivenKeysRotated_WhenSigningOutEverywhere_ThenTheRotationsAreCompleted()
    {
        // Act
        await sut.OnSigningOutEverywhereAsync(new UserSession(), SignOutReason.KeysRotated, "Laptop");

        // Assert
        mockRotation.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenTheUserAsked_WhenSigningOutEverywhere_ThenNoRotationIsCompleted()
    {
        // Act
        await sut.OnSigningOutEverywhereAsync(new UserSession(), SignOutReason.UserRequested, "Laptop");

        // Assert
        mockRotation.Verify(x => x.CompleteAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
