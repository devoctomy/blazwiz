using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Components;
using Devoctomy.Blazwiz.UI.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.Components;

public class ModalDialogTests
{
    [Fact]
    public void GivenNewDialog_WhenReadingFocusMode_ThenFirstInput()
    {
        // Arrange
        var sut = new ModalDialog();

        // Act
        var focusMode = sut.FocusMode;

        // Assert
        Assert.Equal(ModalDialogFocusMode.FirstInput, focusMode);
    }

    [Fact]
    public async Task GivenParameters_WhenParametersSet_ThenViewModelTakesThem()
    {
        // Arrange
        var mockInterop = new Mock<IBrowserInteropService>();
        var viewModel = new ModalDialogViewModel(mockInterop.Object) { Element = new ElementReference("dialog") };
        var closed = 0;
        var sut = new TestModalDialog(viewModel, ModalDialogFocusMode.Dialog, EventCallback.Factory.Create(this, () => closed++));

        // Act
        sut.SetParameters();
        await viewModel.OnAfterRenderAsync(true);
        await viewModel.OnKeyDownAsync(new KeyboardEventArgs { Key = "Escape" });

        // Assert
        mockInterop.Verify(x => x.FocusAsync(It.Is<ElementReference>(e => e.Id == "dialog")), Times.Once);
        Assert.Equal(1, closed);
    }

    private sealed class TestModalDialog : ModalDialog
    {
        public TestModalDialog(ModalDialogViewModel viewModel, ModalDialogFocusMode focusMode, EventCallback onClose)
        {
            ViewModel = viewModel;
            FocusMode = focusMode;
            OnClose = onClose;
        }

        public void SetParameters()
        {
            OnParametersSet();
        }
    }
}
