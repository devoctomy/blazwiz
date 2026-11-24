using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Offline;
using Devoctomy.Blazwiz.Core.Offline.Models;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class CognitoRememberedDeviceServiceTests
{
    private readonly Mock<ICognitoUserPoolClient> mockClient = new(MockBehavior.Strict);
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly OfflineModeService offlineMode = new();
    private readonly CognitoCurrentDevice currentDevice = new();

    public CognitoRememberedDeviceServiceTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob@example.com", AccessToken = "access" });
    }

    public static TheoryData<Exception, string> Failures => new()
    {
        { new CognitoException("TooManyRequestsException", "slow down"), AuthErrorCodes.TooManyAttempts },
        { new CognitoException("LimitExceededException", "slow down"), AuthErrorCodes.TooManyAttempts },
        { new CognitoException("NotAuthorizedException", "revoked"), AuthErrorCodes.SignInAgain },
        { new CognitoException("ResourceNotFoundException", "gone"), AuthErrorCodes.DevicesFailed },
        { new HttpRequestException("offline"), AuthErrorCodes.Unreachable },
        { new TaskCanceledException("timed out"), AuthErrorCodes.Unreachable },
    };

    public static TheoryData<UserSession?> NoOnlineSession => new()
    {
        null,
        new UserSession { Username = "bob@example.com", IsOffline = true },
        new UserSession { Username = "bob@example.com" },
    };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("userPoolClient", Assert.Throws<ArgumentNullException>(() => new CognitoRememberedDeviceService(null!, mockAuth.Object, offlineMode, currentDevice)).ParamName);
        Assert.Equal("authenticationService", Assert.Throws<ArgumentNullException>(() => new CognitoRememberedDeviceService(mockClient.Object, null!, offlineMode, currentDevice)).ParamName);
        Assert.Equal("offlineModeService", Assert.Throws<ArgumentNullException>(() => new CognitoRememberedDeviceService(mockClient.Object, mockAuth.Object, null!, currentDevice)).ParamName);
        Assert.Equal("currentDevice", Assert.Throws<ArgumentNullException>(() => new CognitoRememberedDeviceService(mockClient.Object, mockAuth.Object, offlineMode, null!)).ParamName);
    }

    [Fact]
    public async Task GivenDevices_WhenListAsync_ThenMostRecentFirst_WithThisDeviceMarked()
    {
        // Arrange
        currentDevice.DeviceKey = "k2";
        mockClient.Setup(x => x.ListDevicesAsync("access")).ReturnsAsync(
        [
            new RememberedDevice { Key = "k1", LastSignedInAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero) },
            new RememberedDevice { Key = "k3" },
            new RememberedDevice { Key = "k2", LastSignedInAt = new DateTimeOffset(2026, 11, 20, 0, 0, 0, TimeSpan.Zero) },
        ]);

        // Act
        var devices = await CreateSut().ListAsync();

        // Assert
        Assert.Equal(["k2", "k1", "k3"], devices.Select(d => d.Key));
        Assert.Equal([true, false, false], devices.Select(d => d.IsThisDevice));
    }

    [Fact]
    public async Task GivenNotSignedInFromARememberedDevice_WhenListAsync_ThenNoneIsThisDevice()
    {
        // Arrange
        mockClient.Setup(x => x.ListDevicesAsync("access")).ReturnsAsync([new RememberedDevice { Key = "k1" }]);

        // Act
        var devices = await CreateSut().ListAsync();

        // Assert
        Assert.False(Assert.Single(devices).IsThisDevice);
    }

    [Fact]
    public async Task GivenAnotherDevice_WhenForgetAsync_ThenForgotten_AndThisDeviceKept()
    {
        // Arrange
        currentDevice.DeviceKey = "k2";
        mockClient.Setup(x => x.ForgetDeviceAsync("access", "k1")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().ForgetAsync("k1");

        // Assert
        mockClient.Verify(x => x.ForgetDeviceAsync("access", "k1"), Times.Once);
        Assert.Equal("k2", currentDevice.DeviceKey);
    }

    [Fact]
    public async Task GivenThisDevice_WhenForgetAsync_ThenNoLongerThisSessionsDevice()
    {
        // Arrange
        currentDevice.DeviceKey = "k2";
        mockClient.Setup(x => x.ForgetDeviceAsync("access", "k2")).Returns(Task.CompletedTask);

        // Act
        await CreateSut().ForgetAsync("k2");

        // Assert
        Assert.Null(currentDevice.DeviceKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GivenNoKey_WhenForgetAsync_ThenThrowsArgumentException(string? key)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().ForgetAsync(key!));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task GivenCognitoFails_WhenListAsync_ThenTheMatchingError(Exception failure, string expected)
    {
        // Arrange
        mockClient.Setup(x => x.ListDevicesAsync("access")).ThrowsAsync(failure);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().ListAsync());

        // Assert
        Assert.Equal(expected, exception.Code);
    }

    [Fact]
    public async Task GivenCognitoFails_WhenForgetAsyncThisDevice_ThenStillThisSessionsDevice()
    {
        // Arrange
        currentDevice.DeviceKey = "k2";
        mockClient.Setup(x => x.ForgetDeviceAsync("access", "k2")).ThrowsAsync(new HttpRequestException("offline"));

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().ForgetAsync("k2"));

        // Assert
        Assert.Equal(AuthErrorCodes.Unreachable, exception.Code);
        Assert.Equal("k2", currentDevice.DeviceKey);
    }

    [Fact]
    public async Task GivenOfflineMode_WhenListOrForget_ThenAccountChangeOffline_WithoutCallingCognito()
    {
        // Arrange (the strict client mock fails on any call)
        offlineMode.Enter(OfflineEntryReason.Chosen);
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(sut.ListAsync)).Code);
        Assert.Equal(AuthErrorCodes.AccountChangeOffline, (await Assert.ThrowsAsync<BlazwizException>(() => sut.ForgetAsync("k1"))).Code);
    }

    [Theory]
    [MemberData(nameof(NoOnlineSession))]
    public async Task GivenNoOnlineSession_WhenListAsync_ThenNotSignedIn(UserSession? session)
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);

        // Act
        var exception = await Assert.ThrowsAsync<BlazwizException>(() => CreateSut().ListAsync());

        // Assert
        Assert.Equal(AuthErrorCodes.NotSignedIn, exception.Code);
    }

    private CognitoRememberedDeviceService CreateSut() => new(mockClient.Object, mockAuth.Object, offlineMode, currentDevice);
}
