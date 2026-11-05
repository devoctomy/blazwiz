using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2eLockIndicatorViewModelTests
{
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly TestText testText = new();
    private readonly List<E2eKeyScope> unlocked = [];

    public E2eLockIndicatorViewModelTests()
    {
        mockManager.SetupGet(x => x.Unlocked).Returns(() => unlocked);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eLockIndicatorViewModel(null!, testText.Plurals, testText.Sync)).ParamName);
        Assert.Equal("plurals", Assert.Throws<ArgumentNullException>(() => new E2eLockIndicatorViewModel(mockManager.Object, null!, testText.Sync)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2eLockIndicatorViewModel(mockManager.Object, testText.Plurals, null!)).ParamName);
    }

    [Fact]
    public void GivenNothingUnlocked_WhenShown_ThenHidden()
    {
        // Act
        using var sut = CreateSut();

        // Assert
        Assert.False(sut.IsVisible);
    }

    [Theory]
    [InlineData(1, "1 unlocked")]
    [InlineData(2, "2 unlocked")]
    public void GivenProfilesUnlocked_WhenShown_ThenSaysHowMany(int count, string expected)
    {
        // Arrange
        for (var i = 0; i < count; i++)
        {
            unlocked.Add(new E2eKeyScope(Guid.NewGuid().ToString("D")));
        }

        // Act
        using var sut = CreateSut();

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Equal(expected, sut.Text);
    }

    [Fact]
    public void GivenTheMenuOpen_WhenLockAll_ThenEveryProfileIsLockedAndTheMenuCloses()
    {
        // Arrange
        unlocked.Add(new E2eKeyScope(Guid.NewGuid().ToString("D")));
        using var sut = CreateSut();
        sut.ToggleMenuCommand.Execute(null);

        // Act
        sut.LockAllCommand.Execute(null);

        // Assert
        mockManager.Verify(x => x.LockAll(), Times.Once);
        Assert.False(sut.IsMenuOpen);
    }

    [Fact]
    public void GivenTheMenuOpen_WhenTheLastProfileLocks_ThenHiddenAndTheMenuCloses()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        unlocked.Add(scope);
        using var sut = CreateSut();
        sut.ToggleMenuCommand.Execute(null);

        // Act
        unlocked.Clear();
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Assert
        Assert.False(sut.IsVisible);
        Assert.False(sut.IsMenuOpen);
    }

    private E2eLockIndicatorViewModel CreateSut() => new(mockManager.Object, testText.Plurals, testText.Sync);
}
