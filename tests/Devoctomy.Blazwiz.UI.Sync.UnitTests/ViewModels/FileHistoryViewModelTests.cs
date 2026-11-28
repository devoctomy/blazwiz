using System.Globalization;
using System.Security.Cryptography;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class FileHistoryViewModelTests
{
    private const string Profile = "documents";
    private const string Key = "docs/plan.md";
    private static readonly DateTimeOffset Uploaded = new(2026, 11, 26, 14, 2, 0, TimeSpan.Zero);

    private readonly TestText testText = new();
    private readonly Mock<ISyncFileHistoryService> mockHistory = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<IFileHistoryHost> mockHost = new();
    private readonly FakeTimeProvider time = new();
    private readonly SyncFileVersion newer = Version("v2", "\"e2\"", Uploaded.AddHours(1));
    private readonly SyncFileVersion older = Version("v1", "\"e1\"", Uploaded);

    public FileHistoryViewModelTests()
    {
        mockConnectivity.SetupGet(x => x.Status).Returns(new ConnectivityStatus { IsOnline = true });
        mockHistory.SetupGet(x => x.IsAvailable).Returns(true);
        mockHistory.Setup(x => x.GetVersionsAsync(Profile, Key, It.IsAny<CancellationToken>())).ReturnsAsync([newer, older]);
        mockHistory.Setup(x => x.ReadVersionAsync(It.IsAny<SyncFileVersion>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SyncFileVersion v, CancellationToken _) => new SyncFileVersionContent { Version = v, Content = [1] });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("history", Assert.Throws<ArgumentNullException>(() => new FileHistoryViewModel(null!, mockConnectivity.Object, testText.Sync, testText.Errors, time)).ParamName);
        Assert.Equal("connectivity", Assert.Throws<ArgumentNullException>(() => new FileHistoryViewModel(mockHistory.Object, null!, testText.Sync, testText.Errors, time)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new FileHistoryViewModel(mockHistory.Object, mockConnectivity.Object, null!, testText.Errors, time)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new FileHistoryViewModel(mockHistory.Object, mockConnectivity.Object, testText.Sync, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new FileHistoryViewModel(mockHistory.Object, mockConnectivity.Object, testText.Sync, testText.Errors, null!)).ParamName);
    }

    [Theory]
    [InlineData(OfflineReason.OfflineMode)]
    [InlineData(OfflineReason.NoNetwork)]
    public async Task GivenOffline_WhenExpanded_ThenTheDropdownIsDisabledAndNothingIsListed(OfflineReason reason)
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.Status).Returns(new ConnectivityStatus { IsOnline = false, Reason = reason });
        var sut = CreateSetSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsOffline);
        Assert.Equal(testText.Sync["Sync.History.Offline"].Value, sut.OfflineText);
        Assert.False(sut.IsOpen);
        Assert.Equal(0, CountListings());
    }

    [Fact]
    public void GivenConnectivityNotCheckedYet_WhenAsked_ThenTheDropdownIsNotDisabled()
    {
        // Arrange
        mockConnectivity.SetupGet(x => x.Status).Returns(new ConnectivityStatus());

        // Act
        var sut = CreateSetSut();

        // Assert
        Assert.False(sut.IsOffline);
        Assert.Null(sut.OfflineText);
    }

    [Fact]
    public async Task GivenTheDropdownExpanded_WhenTheAppGoesOfflineAndBackOnline_ThenItClosesIsDisabledAndIsEnabledAgain()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.ToggleCommand.ExecuteAsync(null);
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var offline = new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.OfflineMode };
        var online = new ConnectivityStatus { IsOnline = true };

        // Act
        mockConnectivity.SetupGet(x => x.Status).Returns(offline);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, offline);
        var closedOffline = !sut.IsOpen && sut.IsOffline;
        mockConnectivity.SetupGet(x => x.Status).Returns(online);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, online);

        // Assert
        Assert.True(closedOffline);
        Assert.False(sut.IsOffline);
        Assert.Contains(nameof(FileHistoryViewModel.IsOffline), changed);
        Assert.Contains(nameof(FileHistoryViewModel.OfflineText), changed);
    }

    [Fact]
    public void GivenADisposedViewModel_WhenConnectivityChanges_ThenNothingIsRaised()
    {
        // Arrange
        var sut = CreateSetSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, new ConnectivityStatus { Reason = OfflineReason.NoNetwork });

        // Assert
        Assert.Empty(changed);
    }

    [Fact]
    public async Task GivenAVersionShown_WhenTheAppGoesOffline_ThenItCanStillBeRestored()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        var offline = new ConnectivityStatus { IsOnline = false, Reason = OfflineReason.OfflineMode };
        mockConnectivity.SetupGet(x => x.Status).Returns(offline);
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, offline);
        sut.OpenRestoreCommand.Execute(null);

        // Act
        await sut.RestoreCommand.ExecuteAsync(null);

        // Assert
        mockHistory.Verify(x => x.RestoreAsync(It.Is<SyncFileVersionContent>(c => c.Version == older), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsViewingVersion);
    }

    public static TheoryData<Exception> DecryptionFailures() => new()
    {
        new E2eLockedException(E2eErrorCodes.CannotDecrypt, Key),
        new AuthenticationTagMismatchException(),
        new CryptographicException("altered"),
        new InvalidDataException("not an encrypted file of this profile"),
    };

    [Theory]
    [MemberData(nameof(DecryptionFailures))]
    public async Task GivenAVersionThisDeviceCannotDecrypt_WhenPicked_ThenItSaysSoAndTheCurrentOneStays(Exception failure)
    {
        // Arrange: e.g. encrypted with a key this device does not hold.
        mockHistory.Setup(x => x.ReadVersionAsync(older, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sut = CreateSetSut();
        var told = 0;
        sut.ShownChanged += (_, _) => told++;

        // Act
        await sut.SelectCommand.ExecuteAsync(older);

        // Assert
        Assert.Equal(testText.Sync["Sync.History.CannotDecrypt"].Value, sut.Error);
        Assert.False(sut.IsViewingVersion);
        Assert.False(sut.IsReading);
        Assert.Equal(0, told);
        sut.OpenRestoreCommand.Execute(null);
        Assert.False(sut.IsRestoreDialogOpen);
    }

    [Fact]
    public async Task GivenNoFileSet_WhenExpanded_ThenItIsNotAvailableAndNothingIsListed()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsAvailable);
        Assert.False(sut.IsOpen);
        mockHistory.Verify(x => x.GetVersionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GivenNoBucket_WhenAFileIsSet_ThenItIsNotAvailable()
    {
        // Arrange
        mockHistory.SetupGet(x => x.IsAvailable).Returns(false);

        // Act
        var sut = CreateSetSut();

        // Assert
        Assert.False(sut.IsAvailable);
    }

    [Fact]
    public async Task GivenAFile_WhenTheDropdownIsExpandedCollapsedAndExpandedAgain_ThenTheVersionsAreListedEachTimeItIsExpandedOnly()
    {
        // Arrange
        var sut = CreateSetSut();
        var listedBefore = CountListings();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);
        var listedOpen = CountListings();
        await sut.ToggleCommand.ExecuteAsync(null);
        var openAfterCollapse = sut.IsOpen;
        var listedCollapsed = CountListings();
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(0, listedBefore);
        Assert.Equal(1, listedOpen);
        Assert.False(openAfterCollapse);
        Assert.Equal(1, listedCollapsed);
        Assert.Equal(2, CountListings());
        Assert.True(sut.IsOpen);
        Assert.False(sut.IsLoading);
        Assert.False(sut.IsEmpty);
        Assert.Equal([newer, older], sut.Versions);
        mockHistory.Verify(x => x.ReadVersionAsync(It.IsAny<SyncFileVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenAFileNeverUploaded_WhenExpanded_ThenItSaysThereAreNoVersions()
    {
        // Arrange
        mockHistory.Setup(x => x.GetVersionsAsync(Profile, Key, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var sut = CreateSetSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.True(sut.IsEmpty);
        Assert.Null(sut.ListError);
    }

    [Fact]
    public async Task GivenOffline_WhenExpanded_ThenItSaysWhyAndListsNothing()
    {
        // Arrange
        mockHistory.Setup(x => x.GetVersionsAsync(Profile, Key, It.IsAny<CancellationToken>())).ThrowsAsync(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable));
        var sut = CreateSetSut();

        // Act
        await sut.ToggleCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(testText.Errors.Describe(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable)), sut.ListError);
        Assert.Empty(sut.Versions);
        Assert.False(sut.IsEmpty);
        Assert.False(sut.IsLoading);
        Assert.True(sut.IsOpen);
    }

    [Fact]
    public async Task GivenAnEarlierListingStillRunning_WhenItFinishesAfterALaterOne_ThenTheLaterOneIsKept()
    {
        // Arrange
        var first = new TaskCompletionSource<IReadOnlyList<SyncFileVersion>>();
        var failing = new TaskCompletionSource<IReadOnlyList<SyncFileVersion>>();
        mockHistory.SetupSequence(x => x.GetVersionsAsync(Profile, Key, It.IsAny<CancellationToken>()))
            .Returns(first.Task)
            .Returns(failing.Task)
            .ReturnsAsync([older]);
        var sut = CreateSetSut();
        var firstListing = sut.ToggleAsync();
        sut.CloseCommand.Execute(null);
        var failingListing = sut.ToggleAsync();
        sut.CloseCommand.Execute(null);
        await sut.ToggleCommand.ExecuteAsync(null);

        // Act
        first.SetResult([newer, older]);
        failing.SetException(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable));
        await firstListing;
        await failingListing;

        // Assert
        Assert.Equal([older], sut.Versions);
        Assert.Null(sut.ListError);
        Assert.False(sut.IsLoading);
    }

    [Fact]
    public async Task GivenAVersionPicked_WhenItIsRead_ThenItIsShownAndThePageIsTold()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.ToggleCommand.ExecuteAsync(null);
        var told = 0;
        sut.ShownChanged += (_, _) => told++;

        // Act
        await sut.SelectCommand.ExecuteAsync(older);

        // Assert
        Assert.False(sut.IsOpen);
        Assert.True(sut.IsViewingVersion);
        Assert.Same(older, sut.Selected);
        Assert.Equal([1], sut.SelectedContent!.Content);
        Assert.False(sut.IsReading);
        Assert.Equal(1, told);
        Assert.Equal(testText.Sync["Sync.History.Viewing", sut.Format(older)].Value, sut.ViewingText);
        Assert.Equal(testText.Sync["Sync.History.Restore.Text", sut.Format(older)].Value, sut.RestoreText);
    }

    [Fact]
    public void GivenAVersion_WhenFormatted_ThenItIsItsETagAndItsUploadTimeInLocalTime()
    {
        // Arrange
        time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Plus2", TimeSpan.FromHours(2), "Plus2", "Plus2"));
        var sut = CreateSut();

        // Act
        var text = sut.Format(older);

        // Assert
        Assert.Equal($"e1 ({Uploaded.ToOffset(TimeSpan.FromHours(2)).ToString("g", CultureInfo.CurrentCulture)})", text);
        Assert.Throws<ArgumentNullException>(() => sut.Format(null!));
    }

    [Fact]
    public async Task GivenAPastVersionShown_WhenCurrentIsPickedOrBackToCurrent_ThenTheCurrentOneIsShownAndThePageIsToldOnce()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        var told = 0;
        sut.ShownChanged += (_, _) => told++;

        // Act
        await sut.SelectCommand.ExecuteAsync(null);
        sut.BackToCurrentCommand.Execute(null);

        // Assert
        Assert.False(sut.IsViewingVersion);
        Assert.Null(sut.Selected);
        Assert.Null(sut.ViewingText);
        Assert.Equal(string.Empty, sut.RestoreText);
        Assert.Equal(1, told);
    }

    [Fact]
    public async Task GivenAVersionNoLongerInTheBucket_WhenPicked_ThenItSaysSoAndTheCurrentOneStays()
    {
        // Arrange
        mockHistory.Setup(x => x.ReadVersionAsync(older, It.IsAny<CancellationToken>())).ReturnsAsync((SyncFileVersionContent?)null);
        var sut = CreateSetSut();
        var told = 0;
        sut.ShownChanged += (_, _) => told++;

        // Act
        await sut.SelectCommand.ExecuteAsync(older);

        // Assert
        Assert.Equal(testText.Sync["Sync.History.Gone"].Value, sut.Error);
        Assert.False(sut.IsViewingVersion);
        Assert.False(sut.IsReading);
        Assert.Equal(0, told);
    }

    [Fact]
    public async Task GivenTheBucketUnreachable_WhenAVersionIsPicked_ThenItSaysWhy()
    {
        // Arrange
        var failure = new SyncNetworkException(SyncErrorCodes.RemoteUnreachable);
        mockHistory.Setup(x => x.ReadVersionAsync(older, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var sut = CreateSetSut();

        // Act
        await sut.SelectCommand.ExecuteAsync(older);

        // Assert
        Assert.Equal(testText.Errors.Describe(failure), sut.Error);
        Assert.False(sut.IsViewingVersion);
        sut.DismissErrorCommand.Execute(null);
        Assert.Null(sut.Error);
    }

    [Fact]
    public async Task GivenUnsavedChanges_WhenAVersionIsPicked_ThenItAsksFirstAndReadsNothing()
    {
        // Arrange
        mockHost.SetupGet(x => x.HasUnsavedChanges).Returns(true);
        var sut = CreateSetSut();

        // Act
        await sut.SelectCommand.ExecuteAsync(older);

        // Assert
        Assert.True(sut.IsUnsavedDialogOpen);
        Assert.False(sut.IsViewingVersion);
        mockHistory.Verify(x => x.ReadVersionAsync(It.IsAny<SyncFileVersion>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenUnsavedChanges_WhenCancelChosen_ThenTheyAreKeptAndNothingIsShown()
    {
        // Arrange
        mockHost.SetupGet(x => x.HasUnsavedChanges).Returns(true);
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);

        // Act
        sut.CancelViewCommand.Execute(null);
        await sut.SaveAndViewCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsUnsavedDialogOpen);
        Assert.False(sut.IsViewingVersion);
        mockHost.Verify(x => x.SaveChangesAsync(), Times.Never);
        mockHost.Verify(x => x.DiscardChanges(), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenUnsavedChanges_WhenSaveChosen_ThenTheyAreSavedAndTheVersionShownOnlyIfTheyWere(bool saved)
    {
        // Arrange
        mockHost.SetupGet(x => x.HasUnsavedChanges).Returns(true);
        mockHost.Setup(x => x.SaveChangesAsync()).ReturnsAsync(saved);
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);

        // Act
        await sut.SaveAndViewCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsUnsavedDialogOpen);
        Assert.Equal(saved, sut.IsViewingVersion);
        mockHost.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GivenUnsavedChanges_WhenDontSaveChosen_ThenTheyAreDroppedAndTheVersionShown()
    {
        // Arrange
        mockHost.SetupGet(x => x.HasUnsavedChanges).Returns(true);
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);

        // Act
        await sut.DiscardAndViewCommand.ExecuteAsync(null);

        // Assert
        mockHost.Verify(x => x.DiscardChanges(), Times.Once);
        mockHost.Verify(x => x.SaveChangesAsync(), Times.Never);
        Assert.Same(older, sut.Selected);
    }

    [Fact]
    public async Task GivenNothingWaiting_WhenDontSaveChosen_ThenNothingIsDropped()
    {
        // Arrange
        var sut = CreateSetSut();

        // Act
        await sut.DiscardAndViewCommand.ExecuteAsync(null);

        // Assert
        mockHost.Verify(x => x.DiscardChanges(), Times.Never);
        Assert.False(sut.IsViewingVersion);
    }

    [Fact]
    public async Task GivenAPastVersionShown_WhenRestoreConfirmed_ThenItIsRestoredAndTheCurrentOneShown()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        var shown = sut.SelectedContent;
        var told = 0;
        sut.ShownChanged += (_, _) => told++;

        // Act
        sut.OpenRestoreCommand.Execute(null);
        var asked = sut.IsRestoreDialogOpen;
        await sut.RestoreCommand.ExecuteAsync(null);

        // Assert
        Assert.True(asked);
        mockHistory.Verify(x => x.RestoreAsync(shown!, It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsRestoreDialogOpen);
        Assert.False(sut.IsRestoring);
        Assert.False(sut.IsViewingVersion);
        Assert.Equal(1, told);
    }

    [Fact]
    public async Task GivenARestoreThatFails_WhenRestored_ThenItSaysWhyAndTheVersionStaysShown()
    {
        // Arrange
        mockHistory.Setup(x => x.RestoreAsync(It.IsAny<SyncFileVersionContent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new E2eLockedException());
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        sut.OpenRestoreCommand.Execute(null);

        // Act
        await sut.RestoreCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(testText.Errors.Describe(new E2eLockedException()), sut.Error);
        Assert.False(sut.IsRestoreDialogOpen);
        Assert.True(sut.IsViewingVersion);
    }

    [Fact]
    public async Task GivenTheCurrentVersionShown_WhenRestoreIsAskedFor_ThenNothingHappens()
    {
        // Arrange
        var sut = CreateSetSut();

        // Act
        sut.OpenRestoreCommand.Execute(null);
        await sut.RestoreCommand.ExecuteAsync(null);

        // Assert
        Assert.False(sut.IsRestoreDialogOpen);
        mockHistory.Verify(x => x.RestoreAsync(It.IsAny<SyncFileVersionContent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenARestoreRunning_WhenCancelledOrRestoredAgain_ThenItRunsOnceAndTheDialogStaysUntilItEnds()
    {
        // Arrange
        var release = new TaskCompletionSource();
        mockHistory.Setup(x => x.RestoreAsync(It.IsAny<SyncFileVersionContent>(), It.IsAny<CancellationToken>())).Returns(release.Task);
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        sut.OpenRestoreCommand.Execute(null);
        var restoring = sut.RestoreAsync();

        // Act
        sut.CancelRestoreCommand.Execute(null);
        await sut.RestoreAsync();
        var openWhileRestoring = sut.IsRestoreDialogOpen;
        release.SetResult();
        await restoring;

        // Assert
        Assert.True(openWhileRestoring);
        mockHistory.Verify(x => x.RestoreAsync(It.IsAny<SyncFileVersionContent>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(sut.IsRestoreDialogOpen);
    }

    [Fact]
    public async Task GivenTheRestoreDialogOpen_WhenCancelled_ThenItClosesAndTheVersionStaysShown()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        sut.OpenRestoreCommand.Execute(null);

        // Act
        sut.CancelRestoreCommand.Execute(null);

        // Assert
        Assert.False(sut.IsRestoreDialogOpen);
        Assert.True(sut.IsViewingVersion);
    }

    [Fact]
    public async Task GivenAVersionShownAndListed_WhenAnotherFileIsSet_ThenItStartsAgainFromTheCurrentVersion()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.ToggleCommand.ExecuteAsync(null);
        await sut.SelectCommand.ExecuteAsync(older);
        await sut.ToggleCommand.ExecuteAsync(null);

        // Act
        sut.SetFile(Profile, "docs/other.md", mockHost.Object);

        // Assert
        Assert.False(sut.IsOpen);
        Assert.Empty(sut.Versions);
        Assert.False(sut.IsViewingVersion);
        Assert.True(sut.IsAvailable);
    }

    [Fact]
    public async Task GivenAVersionShown_WhenTheSameFileIsSetAgain_ThenItStaysShownWithTheNewHost()
    {
        // Arrange
        var sut = CreateSetSut();
        await sut.SelectCommand.ExecuteAsync(older);
        var otherHost = new Mock<IFileHistoryHost>();
        otherHost.SetupGet(x => x.HasUnsavedChanges).Returns(true);

        // Act
        sut.SetFile(Profile, Key, otherHost.Object);
        await sut.SelectCommand.ExecuteAsync(newer);

        // Assert
        Assert.Same(older, sut.Selected);
        Assert.True(sut.IsUnsavedDialogOpen);
    }

    [Fact]
    public void GivenNoProfileKeyOrHost_WhenSetFile_ThenThrowsArgument()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.SetFile(" ", Key, mockHost.Object));
        Assert.ThrowsAny<ArgumentException>(() => sut.SetFile(Profile, string.Empty, mockHost.Object));
        Assert.Throws<ArgumentNullException>(() => sut.SetFile(Profile, Key, null!));
    }

    private static SyncFileVersion Version(string versionId, string etag, DateTimeOffset uploaded) => new()
    {
        Profile = Profile,
        Key = Key,
        VersionId = versionId,
        ETag = etag,
        UploadedAt = uploaded,
    };

    private int CountListings() => mockHistory.Invocations.Count(i => i.Method.Name == nameof(ISyncFileHistoryService.GetVersionsAsync));

    private FileHistoryViewModel CreateSut() => new(mockHistory.Object, mockConnectivity.Object, testText.Sync, testText.Errors, time);

    private FileHistoryViewModel CreateSetSut()
    {
        var sut = CreateSut();
        sut.SetFile(Profile, Key, mockHost.Object);
        return sut;
    }
}
