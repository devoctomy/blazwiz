using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Events.Channels.Interfaces;
using Devoctomy.Blazwiz.Events.Models;
using Devoctomy.Blazwiz.Events.Sessions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Events.UnitTests.Sessions;

public class UserEventsSignOutHandlerTests
{
    private readonly Mock<IUserEventChannel> mockChannel = new();
    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
    private UserEvent? published;

    public UserEventsSignOutHandlerTests()
    {
        mockChannel.Setup(x => x.InstanceId).Returns("me");
        mockChannel.Setup(x => x.PublishAsync(It.IsAny<UserSession>(), It.IsAny<UserEvent>()))
            .Callback<UserSession, UserEvent>((_, e) => published = e)
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("channel", Assert.Throws<ArgumentNullException>(() => new UserEventsSignOutHandler(null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new UserEventsSignOutHandler(mockChannel.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(SignOutReason.UserRequested, UserEventReasons.UserRequested)]
    [InlineData(SignOutReason.KeysRotated, UserEventReasons.KeysRotated)]
    public async Task GivenReason_WhenSigningOutEverywhere_ThenTheEventIsPublishedWithReasonDeviceTimeAndInstance(SignOutReason reason, string expected)
    {
        // Arrange
        var sut = new UserEventsSignOutHandler(mockChannel.Object, time);

        // Act
        await sut.OnSigningOutEverywhereAsync(new UserSession { Username = "bob" }, reason, "Android-Edge-624834");

        // Assert
        Assert.Equal(UserEventTypes.SignedOutEverywhere, published?.Type);
        Assert.Equal(expected, published?.Reason);
        Assert.Equal("Android-Edge-624834", published?.Device);
        Assert.Equal(time.GetUtcNow(), published?.At);
        Assert.Equal("me", published?.From);
    }

    [Fact]
    public async Task GivenNoDeviceName_WhenSigningOutEverywhere_ThenNoDeviceIsSent()
    {
        // Arrange
        var sut = new UserEventsSignOutHandler(mockChannel.Object, time);

        // Act
        await sut.OnSigningOutEverywhereAsync(new UserSession { Username = "bob" }, SignOutReason.UserRequested, string.Empty);

        // Assert
        Assert.Null(published?.Device);
    }
}
