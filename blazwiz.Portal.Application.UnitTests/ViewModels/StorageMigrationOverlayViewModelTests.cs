using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services.Interfaces;
using blazwiz.Portal.Application.ViewModels;
using Moq;

namespace blazwiz.Portal.Application.UnitTests.ViewModels;

public class StorageMigrationOverlayViewModelTests
{
    private readonly Mock<ISyncStorageChannel> mockStorageChannel = new();
    private readonly Mock<INavigationService> mockNavigation = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("storageChannel", Assert.Throws<ArgumentNullException>(() => new StorageMigrationOverlayViewModel(null!, mockNavigation.Object)).ParamName);
        Assert.Equal("navigationService", Assert.Throws<ArgumentNullException>(() => new StorageMigrationOverlayViewModel(mockStorageChannel.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenAnotherTabMovesFiles_WhenMigratingMessage_ThenWaitsWithoutReloading()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Migrating);

        // Assert
        Assert.True(sut.IsVisible);
        Assert.Contains(nameof(StorageMigrationOverlayViewModel.IsVisible), changed);
        mockNavigation.Verify(x => x.Reload(), Times.Never);
    }

    [Fact]
    public void GivenAnotherTabMovedFiles_WhenChangedMessage_ThenReloads()
    {
        // Arrange
        CreateSut();

        // Act
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Changed);

        // Assert
        mockNavigation.Verify(x => x.Reload(), Times.Once);
    }

    [Fact]
    public void GivenDisposed_WhenStorageMessage_ThenIgnored()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.Dispose();
        mockStorageChannel.Raise(x => x.MessageReceived += null, mockStorageChannel.Object, SyncStorageChannelMessage.Changed);

        // Assert
        mockNavigation.Verify(x => x.Reload(), Times.Never);
    }

    private StorageMigrationOverlayViewModel CreateSut() => new(mockStorageChannel.Object, mockNavigation.Object);
}
