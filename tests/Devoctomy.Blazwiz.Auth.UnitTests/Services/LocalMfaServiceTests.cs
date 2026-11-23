using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Services;

public class LocalMfaServiceTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private List<string>? stored;

    public LocalMfaServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com" });
        mockLocalStorage.Setup(x => x.GetObjectAsync<List<string>>(LocalMfaService.StorageKey)).ReturnsAsync(() => stored == null ? null : [.. stored]);
        mockLocalStorage
            .Setup(x => x.SetObjectAsync(LocalMfaService.StorageKey, It.IsAny<List<string>>()))
            .Callback<string, List<string>>((_, users) => stored = [.. users])
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("authenticationService", Assert.Throws<ArgumentNullException>(() => new LocalMfaService(null!, mockLocalStorage.Object)).ParamName);
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalMfaService(mockAuth.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNothingStored_WhenIsEnabledAsync_ThenFalse()
    {
        // Act
        var result = await CreateSut().IsEnabledAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GivenSignedIn_WhenBeginSetupAsync_ThenThePlaceholderSecret()
    {
        // Act
        var secret = await CreateSut().BeginSetupAsync();

        // Assert
        Assert.Equal(LocalMfaService.PlaceholderSecret, secret);
    }

    [Fact]
    public async Task GivenSixDigits_WhenCompleteSetupAsync_ThenOnForThisUser_AndNotForOthers()
    {
        // Arrange
        stored = ["alice@example.com"];
        var sut = CreateSut();

        // Act
        await sut.CompleteSetupAsync(" 123456 ");

        // Assert
        Assert.True(await sut.IsEnabledAsync());
        Assert.Equal(["alice@example.com", "bob@example.com"], stored);
    }

    [Fact]
    public async Task GivenAlreadyOn_WhenCompleteSetupAsync_ThenNotAddedTwice()
    {
        // Arrange
        stored = ["BOB@example.com"];

        // Act
        await CreateSut().CompleteSetupAsync("123456");

        // Assert
        Assert.Equal(["BOB@example.com"], stored);
    }

    [Theory]
    [InlineData(null, AuthErrorCodes.MfaCodeRequired)]
    [InlineData("", AuthErrorCodes.MfaCodeRequired)]
    [InlineData("  ", AuthErrorCodes.MfaCodeRequired)]
    [InlineData("12345", AuthErrorCodes.CodeMismatch)]
    [InlineData("1234567", AuthErrorCodes.CodeMismatch)]
    [InlineData("12345a", AuthErrorCodes.CodeMismatch)]
    public async Task GivenNotSixDigits_WhenCompleteSetupAsync_ThenRefused(string? code, string expected)
    {
        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().CompleteSetupAsync(code!));

        // Assert
        Assert.Equal(expected, exception.Code);
        Assert.Null(stored);
    }

    [Fact]
    public async Task GivenOn_WhenDisableAsync_ThenOffForThisUserOnly()
    {
        // Arrange
        stored = ["alice@example.com", "Bob@Example.com"];
        var sut = CreateSut();

        // Act
        await sut.DisableAsync();

        // Assert
        Assert.False(await sut.IsEnabledAsync());
        Assert.Equal(["alice@example.com"], stored);
    }

    [Fact]
    public async Task GivenNobodySignedIn_WhenAnyCall_ThenNotSignedIn()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync((UserSession?)null);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(AuthErrorCodes.NotSignedIn, (await Assert.ThrowsAsync<BlazwizException>(sut.IsEnabledAsync)).Code);
        Assert.Equal(AuthErrorCodes.NotSignedIn, (await Assert.ThrowsAsync<BlazwizException>(sut.BeginSetupAsync)).Code);
        Assert.Equal(AuthErrorCodes.NotSignedIn, (await Assert.ThrowsAsync<BlazwizException>(() => sut.CompleteSetupAsync("123456"))).Code);
        Assert.Equal(AuthErrorCodes.NotSignedIn, (await Assert.ThrowsAsync<BlazwizException>(sut.DisableAsync)).Code);
    }

    private LocalMfaService CreateSut() => new(mockAuth.Object, mockLocalStorage.Object);
}
