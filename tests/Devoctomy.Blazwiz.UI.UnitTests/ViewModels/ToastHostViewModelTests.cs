using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class ToastHostViewModelTests
{
    private readonly Mock<IToastService> mockToasts = new();

    [Fact]
    public void GivenNullToastService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("toastService", Assert.Throws<ArgumentNullException>(() => new ToastHostViewModel(null!)).ParamName);
    }

    [Fact]
    public void GivenToasts_WhenShown_ThenTheyAreTheServicesToasts_AndTheViewIsToldWhenTheyChange()
    {
        // Arrange
        var toast = new Toast { Message = "Copied" };
        mockToasts.SetupGet(x => x.Toasts).Returns([toast]);
        using var sut = new ToastHostViewModel(mockToasts.Object);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockToasts.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Same(toast, Assert.Single(sut.Toasts));
        Assert.Equal([nameof(ToastHostViewModel.Toasts)], changed);
    }

    [Fact]
    public void GivenAToast_WhenDismissed_ThenTheServiceDismissesIt()
    {
        // Arrange
        using var sut = new ToastHostViewModel(mockToasts.Object);
        var id = Guid.NewGuid();

        // Act
        sut.DismissCommand.Execute(id);

        // Assert
        mockToasts.Verify(x => x.Dismiss(id), Times.Once);
    }

    [Fact]
    public void GivenDisposed_WhenToastsChange_ThenTheViewIsNotTold()
    {
        // Arrange
        var sut = new ToastHostViewModel(mockToasts.Object);
        var changed = 0;
        sut.PropertyChanged += (_, _) => changed++;

        // Act
        sut.Dispose();
        mockToasts.Raise(x => x.Changed += null, EventArgs.Empty);

        // Assert
        Assert.Equal(0, changed);
    }
}
