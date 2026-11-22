using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Components;
using Devoctomy.Blazwiz.UI.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class ModalDialogViewModelTests
{
    private readonly Mock<IBrowserInteropService> mockInterop = new();
    private readonly ElementReference element = new("dialog");

    [Fact]
    public void GivenNullBrowserInterop_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("browserInterop", Assert.Throws<ArgumentNullException>(() => new ModalDialogViewModel(null!)).ParamName);
    }

    [Theory]
    [InlineData(ModalDialogFocusMode.FirstInput)]
    [InlineData(ModalDialogFocusMode.FirstButton)]
    [InlineData(ModalDialogFocusMode.Autofocus)]
    public async Task GivenModeFindsTarget_WhenFirstRendered_ThenTargetFocusedAndNotDialog(ModalDialogFocusMode mode)
    {
        // Arrange
        SetupFind(mode, true);
        var sut = CreateSut(mode);

        // Act
        await sut.OnAfterRenderAsync(true);

        // Assert
        VerifyFind(mode, Times.Once());
        mockInterop.Verify(x => x.FocusAsync(It.IsAny<ElementReference>()), Times.Never);
    }

    [Theory]
    [InlineData(ModalDialogFocusMode.FirstInput)]
    [InlineData(ModalDialogFocusMode.FirstButton)]
    [InlineData(ModalDialogFocusMode.Autofocus)]
    public async Task GivenModeFindsNothing_WhenFirstRendered_ThenDialogFocused(ModalDialogFocusMode mode)
    {
        // Arrange
        SetupFind(mode, false);
        var sut = CreateSut(mode);

        // Act
        await sut.OnAfterRenderAsync(true);

        // Assert
        VerifyFind(mode, Times.Once());
        mockInterop.Verify(x => x.FocusAsync(It.Is<ElementReference>(e => e.Id == "dialog")), Times.Once);
    }

    [Fact]
    public async Task GivenDialogMode_WhenFirstRendered_ThenOnlyDialogFocused()
    {
        // Arrange
        var sut = CreateSut(ModalDialogFocusMode.Dialog);

        // Act
        await sut.OnAfterRenderAsync(true);

        // Assert
        mockInterop.Verify(x => x.FocusAsync(It.Is<ElementReference>(e => e.Id == "dialog")), Times.Once);
        mockInterop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenNoneMode_WhenFirstRendered_ThenNothingFocused()
    {
        // Arrange
        var sut = CreateSut(ModalDialogFocusMode.None);

        // Act
        await sut.OnAfterRenderAsync(true);

        // Assert
        mockInterop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenAlreadyRendered_WhenRenderedAgain_ThenNothingFocused()
    {
        // Arrange
        var sut = CreateSut(ModalDialogFocusMode.FirstInput);

        // Act
        await sut.OnAfterRenderAsync(false);

        // Assert
        mockInterop.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GivenEscape_WhenOnKeyDownAsync_ThenOnCloseInvoked()
    {
        // Arrange
        var closed = 0;
        var sut = CreateSut(ModalDialogFocusMode.FirstInput, () => closed++);

        // Act
        await sut.OnKeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        Assert.Equal(1, closed);
    }

    [Fact]
    public async Task GivenOtherKey_WhenOnKeyDownAsync_ThenOnCloseNotInvoked()
    {
        // Arrange
        var closed = 0;
        var sut = CreateSut(ModalDialogFocusMode.FirstInput, () => closed++);

        // Act
        await sut.OnKeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        // Assert
        Assert.Equal(0, closed);
    }

    [Fact]
    public async Task GivenNullKeyboardEventArgs_WhenOnKeyDownAsync_ThenThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut(ModalDialogFocusMode.FirstInput);

        // Act & Assert
        Assert.Equal("e", (await Assert.ThrowsAsync<ArgumentNullException>(() => sut.OnKeyDownAsync(null!))).ParamName);
    }

    private ModalDialogViewModel CreateSut(ModalDialogFocusMode mode, Action? onClose = null)
    {
        var sut = new ModalDialogViewModel(mockInterop.Object) { Element = element };
        sut.SetParameters(mode, onClose == null ? default : EventCallback.Factory.Create(this, onClose));
        return sut;
    }

    private void SetupFind(ModalDialogFocusMode mode, bool found)
    {
        switch (mode)
        {
            case ModalDialogFocusMode.FirstInput:
                mockInterop.Setup(x => x.FocusFirstInputAsync(It.Is<ElementReference>(e => e.Id == "dialog"))).ReturnsAsync(found);
                break;
            case ModalDialogFocusMode.FirstButton:
                mockInterop.Setup(x => x.FocusFirstButtonAsync(It.Is<ElementReference>(e => e.Id == "dialog"))).ReturnsAsync(found);
                break;
            default:
                mockInterop.Setup(x => x.FocusAutofocusAsync(It.Is<ElementReference>(e => e.Id == "dialog"))).ReturnsAsync(found);
                break;
        }
    }

    private void VerifyFind(ModalDialogFocusMode mode, Times times)
    {
        switch (mode)
        {
            case ModalDialogFocusMode.FirstInput:
                mockInterop.Verify(x => x.FocusFirstInputAsync(It.IsAny<ElementReference>()), times);
                break;
            case ModalDialogFocusMode.FirstButton:
                mockInterop.Verify(x => x.FocusFirstButtonAsync(It.IsAny<ElementReference>()), times);
                break;
            default:
                mockInterop.Verify(x => x.FocusAutofocusAsync(It.IsAny<ElementReference>()), times);
                break;
        }
    }
}
