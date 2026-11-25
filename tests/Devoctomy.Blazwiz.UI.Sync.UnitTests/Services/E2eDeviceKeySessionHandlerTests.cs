using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class E2eDeviceKeySessionHandlerTests
{
    private readonly Mock<IPasskeyKeysService> mockPasskeys = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("passkeys", Assert.Throws<ArgumentNullException>(() => new E2eDeviceKeySessionHandler(null!)).ParamName);
    }

    [Fact]
    public async Task GivenASignInWithThePasskey_WhenTheSessionStarts_ThenTheDeviceKeyIsWiped_AndNothingElse()
    {
        // Arrange
        ISessionLifecycleHandler sut = new E2eDeviceKeySessionHandler(mockPasskeys.Object);

        // Act
        await sut.OnSessionStartedAsync(new UserSession { Username = "bob@example.com" });

        // Assert
        mockPasskeys.Verify(x => x.ForgetKey(E2eKeyNames.KeyEncryptionKey), Times.Once);
        mockPasskeys.VerifyNoOtherCalls();
    }
}
