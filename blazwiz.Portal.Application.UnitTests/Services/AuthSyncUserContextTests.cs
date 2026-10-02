using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Models.Auth;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class AuthSyncUserContextTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullAuthService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new AuthSyncUserContext(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSignedIn_WhenGetUserIdAsync_ThenUsername()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });

        Assert.Equal("bob@example.com", await new AuthSyncUserContext(mockAuth.Object).GetUserIdAsync());
    }

    [Fact]
    public async Task GivenSignedOut_WhenGetUserIdAsync_ThenNull()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);

        Assert.Null(await new AuthSyncUserContext(mockAuth.Object).GetUserIdAsync());
    }
}
