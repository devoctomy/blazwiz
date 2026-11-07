using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services;
using Microsoft.Extensions.Time.Testing;

namespace Devoctomy.Blazwiz.UI.UnitTests.Services;

public class ToastServiceTests
{
    private readonly FakeTimeProvider time = new();

    [Fact]
    public void GivenNullTimeProvider_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new ToastService(null!)).ParamName);
    }

    [Fact]
    public void GivenAMessage_WhenShown_ThenItIsListedAndChangedIsRaised()
    {
        // Arrange
        using var sut = new ToastService(time);
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        var toast = sut.Show("Password copied", ToastTone.Success);

        // Assert
        Assert.Equal(1, changes);
        var shown = Assert.Single(sut.Toasts);
        Assert.Equal(toast.Id, shown.Id);
        Assert.Equal("Password copied", shown.Message);
        Assert.Equal(ToastTone.Success, shown.Tone);
        Assert.Equal("bi-check-circle-fill", shown.Icon);
    }

    [Fact]
    public async Task GivenAToast_WhenItsTimePasses_ThenItGoes()
    {
        // Arrange
        using var sut = new ToastService(time);
        sut.Show("Saved");
        sut.Show("Longer", duration: TimeSpan.FromSeconds(10));

        // Act
        time.Advance(ToastService.DefaultDuration);
        await WaitForAsync(() => sut.Toasts.Count == 1);

        // Assert
        Assert.Equal("Longer", Assert.Single(sut.Toasts).Message);
        time.Advance(TimeSpan.FromSeconds(10));
        await WaitForAsync(() => sut.Toasts.Count == 0);
        Assert.Empty(sut.Toasts);
    }

    [Fact]
    public void GivenTooManyToasts_WhenAnotherIsShown_ThenTheOldestGoes()
    {
        // Arrange
        using var sut = new ToastService(time);
        for (var i = 0; i < ToastService.MaximumToasts; i++)
        {
            sut.Show($"Toast {i}");
        }

        // Act
        sut.Show("Newest");

        // Assert
        Assert.Equal(ToastService.MaximumToasts, sut.Toasts.Count);
        Assert.Equal("Toast 1", sut.Toasts[0].Message);
        Assert.Equal("Newest", sut.Toasts[^1].Message);
    }

    [Fact]
    public void GivenAToast_WhenDismissed_ThenItGoes_AndDismissingItAgainChangesNothing()
    {
        // Arrange
        using var sut = new ToastService(time);
        var toast = sut.Show("Saved");
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        sut.Dismiss(toast.Id);
        sut.Dismiss(toast.Id);

        // Assert
        Assert.Empty(sut.Toasts);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenNoMessage_WhenShown_ThenThrowsArgumentException(string message)
    {
        // Arrange
        using var sut = new ToastService(time);

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.Show(message));
    }

    [Fact]
    public void GivenADurationThatIsNotPositive_WhenShown_ThenThrowsArgumentOutOfRangeException()
    {
        // Arrange
        using var sut = new ToastService(time);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => sut.Show("Saved", duration: TimeSpan.Zero));
    }

    [Fact]
    public async Task GivenDisposed_WhenATimePasses_ThenNothingIsRaised()
    {
        // Arrange
        var sut = new ToastService(time);
        sut.Show("Saved");
        var changes = 0;
        sut.Changed += (_, _) => changes++;

        // Act
        sut.Dispose();
        time.Advance(ToastService.DefaultDuration);
        await Task.Delay(20);

        // Assert
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData(ToastTone.Info, "bi-info-circle-fill")]
    [InlineData(ToastTone.Warning, "bi-exclamation-triangle-fill")]
    [InlineData(ToastTone.Danger, "bi-x-circle-fill")]
    public void GivenATone_WhenGettingTheIcon_ThenItsOwn(ToastTone tone, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, new Toast { Tone = tone }.Icon);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(5);
        }
    }
}
