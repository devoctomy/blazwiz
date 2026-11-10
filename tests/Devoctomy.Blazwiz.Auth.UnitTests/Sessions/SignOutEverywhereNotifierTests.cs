using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Sessions;

public class SignOutEverywhereNotifierTests
{
    private readonly Mock<IDeviceNameService> mockDeviceName = new();
    private readonly UserSession session = new() { Username = "bob" };

    public SignOutEverywhereNotifierTests()
    {
        mockDeviceName.Setup(x => x.GetNameAsync()).ReturnsAsync("Android-Edge-624834");
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("handlers", Assert.Throws<ArgumentNullException>(() => new SignOutEverywhereNotifier(null!, mockDeviceName.Object)).ParamName);
        Assert.Equal("deviceNameService", Assert.Throws<ArgumentNullException>(() => new SignOutEverywhereNotifier([], null!)).ParamName);
    }

    [Fact]
    public async Task GivenNoHandlers_WhenNotifyAsync_ThenHasHandlersFalse_AndTheDeviceNameIsNotRead()
    {
        // Arrange
        var sut = new SignOutEverywhereNotifier([], mockDeviceName.Object);

        // Act
        await sut.NotifyAsync(session, SignOutReason.UserRequested);

        // Assert
        Assert.False(sut.HasHandlers);
        mockDeviceName.Verify(x => x.GetNameAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenHandlers_WhenNotifyAsync_ThenEachIsCalledWithTheReasonAndThisDevicesName_EvenIfOneThrows()
    {
        // Arrange
        var failing = new Mock<ISignOutEverywhereHandler>();
        failing.Setup(x => x.OnSigningOutEverywhereAsync(It.IsAny<UserSession>(), It.IsAny<SignOutReason>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("offline"));
        var working = new Mock<ISignOutEverywhereHandler>();
        var sut = new SignOutEverywhereNotifier([failing.Object, working.Object], mockDeviceName.Object);

        // Act
        await sut.NotifyAsync(session, SignOutReason.KeysRotated);

        // Assert
        Assert.True(sut.HasHandlers);
        failing.Verify(x => x.OnSigningOutEverywhereAsync(session, SignOutReason.KeysRotated, "Android-Edge-624834"), Times.Once);
        working.Verify(x => x.OnSigningOutEverywhereAsync(session, SignOutReason.KeysRotated, "Android-Edge-624834"), Times.Once);
    }

    [Fact]
    public async Task GivenTheDeviceNameCannotBeRead_WhenNotifyAsync_ThenHandlersAreStillCalled()
    {
        // Arrange
        mockDeviceName.Setup(x => x.GetNameAsync()).ThrowsAsync(new InvalidOperationException("no storage"));
        var handler = new Mock<ISignOutEverywhereHandler>();
        var sut = new SignOutEverywhereNotifier([handler.Object], mockDeviceName.Object);

        // Act
        await sut.NotifyAsync(session, SignOutReason.UserRequested);

        // Assert
        handler.Verify(x => x.OnSigningOutEverywhereAsync(session, SignOutReason.UserRequested, string.Empty), Times.Once);
    }
}
