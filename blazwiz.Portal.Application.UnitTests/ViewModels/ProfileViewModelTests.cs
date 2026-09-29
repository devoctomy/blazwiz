using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using blazwiz.Portal.Models.Auth;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class ProfileViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();

    [Fact]
    public void GivenNullAuthService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new ProfileViewModel(null!));
        Assert.Equal("authService", exception.ParamName);
    }

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenExposesUserAndInitials()
    {
        // Arrange
        var session = new UserSession { DisplayName = "Bob Hoskins" };
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        var sut = new ProfileViewModel(mockAuth.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Same(session, sut.User);
        Assert.Equal("BH", sut.Initials);
        Assert.Contains(nameof(ProfileViewModel.User), changed);
        Assert.Contains(nameof(ProfileViewModel.Initials), changed);
    }

    [Fact]
    public async Task GivenNoUser_WhenInitialized_ThenUserIsNull()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = new ProfileViewModel(mockAuth.Object);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Null(sut.User);
        Assert.Equal("?", sut.Initials);
    }
}
