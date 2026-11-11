using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class SessionCheckKeyMissingHandlerTests
{
    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new SessionCheckKeyMissingHandler(null!)).ParamName);
    }

    [Fact]
    public async Task GivenAFileUnderAnUnknownKey_WhenTold_ThenTheSessionIsChecked()
    {
        // Arrange: if every device was signed out after a rotation, the check ends this session.
        var mockAuth = new Mock<IAuthenticationService>();
        var sut = new SessionCheckKeyMissingHandler(mockAuth.Object);

        // Act
        await sut.OnKeyMissingAsync(new E2eKeyScope(Guid.NewGuid().ToString("D")), "k_new");

        // Assert
        mockAuth.Verify(x => x.CheckSessionAsync(), Times.Once);
    }
}
