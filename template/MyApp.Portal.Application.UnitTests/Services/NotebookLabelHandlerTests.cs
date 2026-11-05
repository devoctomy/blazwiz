using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Moq;
using MyApp.Portal.Application.Services;
using MyApp.Portal.Application.Services.Interfaces;

namespace MyApp.Portal.Application.UnitTests.Services;

public class NotebookLabelHandlerTests
{
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly Mock<INotebookService> mockNotebooks = new();
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));
    private bool unlocked;

    public NotebookLabelHandlerTests()
    {
        mockManager.Setup(x => x.IsUnlocked(scope)).Returns(() => unlocked);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new NotebookLabelHandler(null!, mockNotebooks.Object)).ParamName);
        Assert.Equal("notebookService", Assert.Throws<ArgumentNullException>(() => new NotebookLabelHandler(mockManager.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheSessionStarted_WhenANotebookIsUnlocked_ThenItIsLabelled()
    {
        // Arrange
        using var sut = new NotebookLabelHandler(mockManager.Object, mockNotebooks.Object);
        await sut.OnSessionStartedAsync(new UserSession());

        // Act
        unlocked = true;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockNotebooks.Verify(x => x.FindAsync(scope.Id), Times.Once);
    }

    [Fact]
    public async Task GivenTheSessionStarted_WhenANotebookIsLocked_ThenNothingIsRead()
    {
        // Arrange
        using var sut = new NotebookLabelHandler(mockManager.Object, mockNotebooks.Object);
        await sut.OnSessionStartedAsync(new UserSession());

        // Act
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockNotebooks.Verify(x => x.FindAsync(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task GivenSigningOut_WhenANotebookIsUnlocked_ThenNothingIsRead()
    {
        // Arrange
        using var sut = new NotebookLabelHandler(mockManager.Object, mockNotebooks.Object);
        await sut.OnSessionStartedAsync(new UserSession());
        await sut.OnSessionStartedAsync(new UserSession());

        // Act
        await sut.OnSigningOutAsync();
        unlocked = true;
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        mockNotebooks.Verify(x => x.FindAsync(It.IsAny<string?>()), Times.Never);
    }
}
