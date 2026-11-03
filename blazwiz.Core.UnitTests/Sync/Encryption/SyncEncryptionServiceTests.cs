using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Encryption;

public class SyncEncryptionServiceTests
{
    private const string Profile = "notes";
    private const string Passphrase = "correct horse battery staple";

    private readonly Mock<IE2eKeyManager> mockKeys = new();
    private readonly Mock<IE2eKeyRing> mockRing = new();
    private readonly Mock<ISyncEncryptionSweeper> mockSweeper = new();
    private readonly Mock<ISyncService> mockSync = new();
    private readonly Mock<ISyncScheduler> mockScheduler = new();
    private readonly Mock<IConnectivityService> mockConnectivity = new();
    private readonly Mock<ISyncLockService> mockLock = new();
    private readonly Mock<ISyncStorageChannel> mockChannel = new();
    private readonly List<string> calls = [];
    private readonly EncryptedSyncProfile profile = new(Profile, Mock.Of<ISyncFileStore>(), Mock.Of<ISyncIndexStore>());
    private bool online = true;
    private bool forcedOffline;
    private E2eStatus status = E2eStatus.Disabled;

    public SyncEncryptionServiceTests()
    {
        mockKeys.SetupGet(x => x.Status).Returns(() => status);
        mockKeys.SetupGet(x => x.HasKeyEncryptionKey).Returns(true);
        mockKeys.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("initialize")).ReturnsAsync(() => status);
        mockKeys.Setup(x => x.IsEnabledAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("is-enabled")).ReturnsAsync(false);
        mockKeys.Setup(x => x.EnableAsync(Passphrase, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                calls.Add("enable");
                status = E2eStatus.Unlocked;
            })
            .Returns(Task.CompletedTask);
        mockKeys.Setup(x => x.VerifyPassphraseAsync(Passphrase, It.IsAny<CancellationToken>())).Callback(() => calls.Add("verify")).Returns(Task.CompletedTask);
        mockKeys.Setup(x => x.RemoveAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("remove")).Returns(Task.CompletedTask);
        mockKeys.Setup(x => x.UnlockLocalCopyAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, CancellationToken>((p, _) => calls.Add(p == null ? "unlock-local" : $"unlock-local-{p}"))
            .Returns(Task.CompletedTask);
        mockKeys.Setup(x => x.ForgetDeviceAsync()).Callback(() => calls.Add("forget")).Returns(Task.CompletedTask);
        mockKeys.Setup(x => x.SetUpDeviceAsync(Passphrase, It.IsAny<CancellationToken>())).Callback(() =>
        {
            calls.Add("setup");
            status = E2eStatus.Unlocked;
        }).Returns(Task.CompletedTask);
        mockRing.SetupGet(x => x.WriteMode).Returns(E2eWriteMode.Encrypt);
        mockRing.Setup(x => x.SetWriteMode(It.IsAny<E2eWriteMode>())).Callback<E2eWriteMode>(m => calls.Add($"write-{m}"));
        mockSweeper.Setup(x => x.CountEncryptedAsync(It.IsAny<CancellationToken>())).Callback(() => calls.Add("count")).ReturnsAsync(0);
        mockSweeper.Setup(x => x.EncryptAsync(It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Callback(() => calls.Add("encrypt")).ReturnsAsync(0);
        mockSweeper.Setup(x => x.DecryptAsync(It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<IProgress<E2eProgress>?, bool, CancellationToken>((_, pendingOnly, _) => calls.Add(pendingOnly ? "decrypt-pending" : "decrypt"))
            .ReturnsAsync(0);
        mockSync.Setup(x => x.SyncAsync(Profile, false, It.IsAny<CancellationToken>())).Callback(() => calls.Add("sync")).ReturnsAsync(new SyncResult());
        mockConnectivity.Setup(x => x.CheckAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => new ConnectivityStatus { IsOnline = online && !forcedOffline });
        mockConnectivity.SetupGet(x => x.IsSyncEnabled).Returns(() => !forcedOffline);
        mockConnectivity.Setup(x => x.SetSyncEnabledAsync(It.IsAny<bool>())).Callback<bool>(v =>
        {
            calls.Add(v ? "online" : "offline");
            forcedOffline = !v;
        }).Returns(Task.CompletedTask);
        mockLock.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        mockChannel.Setup(x => x.PostAsync(It.IsAny<SyncStorageChannelMessage>())).Callback<SyncStorageChannelMessage>(m => calls.Add($"post-{m}")).Returns(Task.CompletedTask);
        mockScheduler.Setup(x => x.SyncNowAsync()).Callback(() => calls.Add("sync-now")).Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("keyManager", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(null!, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("keyRing", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, null!, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("sweeper", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, null!, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, null!, mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("syncService", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], null!, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("scheduler", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, null!, mockConnectivity.Object, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("connectivityService", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, null!, mockLock.Object, mockChannel.Object)).ParamName);
        Assert.Equal("lockService", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, null!, mockChannel.Object)).ParamName);
        Assert.Equal("channel", Assert.Throws<ArgumentNullException>(() => new SyncEncryptionService(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, null!)).ParamName);
    }

    [Fact]
    public async Task GivenUnlocked_WhenInitializeAsync_ThenEncryptsFilesThatArrivedUnencrypted()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        using var sut = CreateSut();

        // Act
        var result = await sut.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.Unlocked, result);
        Assert.Equal(["initialize", "encrypt"], calls);
    }

    [Theory]
    [InlineData(E2eStatus.Disabled)]
    [InlineData(E2eStatus.Blocked)]
    [InlineData(E2eStatus.NeedsSetup)]
    public async Task GivenNotUnlocked_WhenInitializeAsync_ThenNoSweep(E2eStatus initial)
    {
        status = initial;
        using var sut = CreateSut();
        await sut.InitializeAsync();
        Assert.Equal(["initialize"], calls);
    }

    [Fact]
    public async Task GivenSweepFails_WhenInitializeAsync_ThenStillReturnsStatus()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        mockSweeper.Setup(x => x.EncryptAsync(It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        using var sut = CreateSut();

        // Act & Assert
        Assert.Equal(E2eStatus.Unlocked, await sut.InitializeAsync());
    }

    [Fact]
    public async Task GivenOnline_WhenEnableAsync_ThenRunsTheStepsInOrderAndGoesBackOnline()
    {
        // Arrange
        using var sut = CreateSut();
        var reported = new List<E2eProgress>();

        // Act
        await sut.EnableAsync(Passphrase, new ListProgress(reported));

        // Assert
        Assert.Equal(["sync", "offline", "is-enabled", "count", "enable", "encrypt", "online", $"post-{SyncStorageChannelMessage.Changed}"], calls);
        Assert.Equal(
            [E2eStep.Sync, E2eStep.GoOffline, E2eStep.CheckExistingKeys, E2eStep.CheckIndex, E2eStep.GoOnline],
            reported.Select(p => p.Step).Distinct());
        Assert.All(reported.GroupBy(p => p.Step), g => Assert.True(g.Last().Completed));
        mockLock.Verify(x => x.AcquireAsync("sync-" + Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAlreadyInOfflineMode_WhenEnableAsync_ThenSkipsSyncAndStaysOffline()
    {
        // Arrange
        forcedOffline = true;
        using var sut = CreateSut();

        // Act
        await sut.EnableAsync(Passphrase);

        // Assert
        Assert.DoesNotContain("sync", calls);
        Assert.DoesNotContain("online", calls);
        Assert.True(forcedOffline);
    }

    [Fact]
    public async Task GivenEnabledElsewhere_WhenEnableAsync_ThenThrowsAlreadyEnabledAndGoesBackOnline()
    {
        // Arrange
        mockKeys.Setup(x => x.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<E2eAlreadyEnabledException>(() => sut.EnableAsync(Passphrase));
        Assert.Contains("initialize", calls);
        Assert.DoesNotContain("enable", calls);
        Assert.Equal("online", calls[^1]);
    }

    [Fact]
    public async Task GivenFilesAlreadyMarkedEncrypted_WhenEnableAsync_ThenStopsBeforeMakingKeys()
    {
        // Arrange
        mockSweeper.Setup(x => x.CountEncryptedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        using var sut = CreateSut();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnableAsync(Passphrase));
        Assert.Contains("3", ex.Message);
        Assert.DoesNotContain("enable", calls);
        Assert.False(forcedOffline);
    }

    [Fact]
    public async Task GivenEnableRunning_WhenEnableAsyncAgain_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var gate = new TaskCompletionSource();
        mockKeys.Setup(x => x.EnableAsync(Passphrase, It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<CancellationToken>())).Returns(gate.Task);
        using var sut = CreateSut();
        var first = sut.EnableAsync(Passphrase);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.EnableAsync(Passphrase));
        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task GivenUnlockedAndOnline_WhenDisableAsync_ThenRunsTheStepsInOrder()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        using var sut = CreateSut();

        // Act
        await sut.DisableAsync(Passphrase);

        // Assert
        Assert.Equal(["verify", "sync", "offline", "write-Plaintext", "decrypt", "remove", "online", $"post-{SyncStorageChannelMessage.Changed}"], calls);
    }

    [Fact]
    public async Task GivenOffline_WhenDisableAsync_ThenThrowsBeforeDecrypting()
    {
        // Arrange
        online = false;
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DisableAsync(Passphrase));
        Assert.Equal(["verify"], calls);
    }

    [Fact]
    public async Task GivenSyncFails_WhenDisableAsync_ThenThrowsBeforeDecrypting()
    {
        // Arrange
        var failed = new SyncResult { Failed = 1 };
        mockSync.Setup(x => x.SyncAsync(Profile, false, It.IsAny<CancellationToken>())).ReturnsAsync(failed);
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.DisableAsync(Passphrase));
        Assert.DoesNotContain("decrypt", calls);
        Assert.DoesNotContain("remove", calls);
    }

    [Fact]
    public async Task GivenDecryptFails_WhenDisableAsync_ThenKeysKeptEncryptingAgainAndBackOnline()
    {
        // Arrange
        mockSweeper.Setup(x => x.DecryptAsync(It.IsAny<IProgress<E2eProgress>?>(), false, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("disk"));
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<IOException>(() => sut.DisableAsync(Passphrase));
        Assert.DoesNotContain("remove", calls);
        Assert.Contains("write-Encrypt", calls);
        Assert.Equal("online", calls[^1]);
    }

    [Fact]
    public async Task GivenWrongPassphrase_WhenDisableAsync_ThenThrowsAndNothingChanges()
    {
        // Arrange
        mockKeys.Setup(x => x.VerifyPassphraseAsync("wrong", It.IsAny<CancellationToken>())).ThrowsAsync(new E2eWrongPassphraseException());
        using var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => sut.DisableAsync("wrong"));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task GivenNeedsSetup_WhenSetUpDeviceAsync_ThenSetsUpEncryptsArrivalsAndSyncs()
    {
        status = E2eStatus.NeedsSetup;
        using var sut = CreateSut();
        await sut.SetUpDeviceAsync(Passphrase);
        Assert.Equal(["setup", "encrypt", "sync-now"], calls);
    }

    [Fact]
    public async Task GivenDisabledElsewhereWithPendingChanges_WhenResolveDisabledElsewhereAsync_ThenDecryptsPendingForgetsAndSyncs()
    {
        status = E2eStatus.DisabledElsewhere;
        mockSweeper.Setup(x => x.CountPendingEncryptedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        using var sut = CreateSut();
        await sut.ResolveDisabledElsewhereAsync();
        Assert.Equal(["unlock-local", "decrypt-pending", "forget", "sync-now"], calls);
    }

    [Fact]
    public async Task GivenDisabledElsewhereWithNoPendingChanges_WhenResolveDisabledElsewhereAsync_ThenForgetsAndSyncsWithoutUnlocking()
    {
        status = E2eStatus.DisabledElsewhere;
        using var sut = CreateSut();
        await sut.ResolveDisabledElsewhereAsync();
        Assert.Equal(["forget", "sync-now"], calls);
    }

    [Fact]
    public async Task GivenPassphrase_WhenResolveDisabledElsewhereAsync_ThenUnlocksWithIt()
    {
        status = E2eStatus.DisabledElsewhere;
        mockSweeper.Setup(x => x.CountPendingEncryptedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var sut = CreateSut();
        await sut.ResolveDisabledElsewhereAsync(Passphrase);
        Assert.Equal([$"unlock-local-{Passphrase}", "decrypt-pending", "forget", "sync-now"], calls);
    }

    [Fact]
    public async Task GivenPassphraseRequired_WhenResolveDisabledElsewhereAsync_ThenThrowsAndKeepsTheKeys()
    {
        // Arrange
        status = E2eStatus.DisabledElsewhere;
        mockSweeper.Setup(x => x.CountPendingEncryptedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        mockKeys.Setup(x => x.UnlockLocalCopyAsync(null, It.IsAny<CancellationToken>())).ThrowsAsync(new E2ePassphraseRequiredException());
        using var sut = CreateSut();

        // Act
        await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => sut.ResolveDisabledElsewhereAsync());

        // Assert
        Assert.Empty(calls);
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    public async Task GivenPendingAndPasskey_WhenResolveNeedsPassphraseAsync_ThenTrueOnlyWhenChangesNeedTheKeysAndThePasskeyCannotUnlock(int pending, bool passkeyUnlocks, bool expected)
    {
        mockSweeper.Setup(x => x.CountPendingEncryptedAsync(It.IsAny<CancellationToken>())).ReturnsAsync(pending);
        mockKeys.Setup(x => x.CanUnlockLocalCopyWithPasskeyAsync()).ReturnsAsync(passkeyUnlocks);
        using var sut = CreateSut();
        Assert.Equal(expected, await sut.ResolveNeedsPassphraseAsync());
    }

    [Fact]
    public async Task GivenPassphrase_WhenChangePassphraseAsync_ThenKeyManagerChangesIt()
    {
        using var sut = CreateSut();
        await sut.ChangePassphraseAsync("old passphrase", "new passphrase");
        mockKeys.Verify(x => x.ChangePassphraseAsync("old passphrase", "new passphrase", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GivenUnlocked_WhenFilesDownloaded_ThenThatProfileIsSwept()
    {
        // Arrange
        status = E2eStatus.Unlocked;
        using var sut = CreateSut();

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs(Profile, ["notes/a.md"]));

        // Assert
        mockSweeper.Verify(x => x.EncryptAsync(null, Profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GivenNotUnlockedOrOtherProfile_WhenFilesDownloaded_ThenNoSweep()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs(Profile, ["notes/a.md"]));
        status = E2eStatus.Unlocked;
        mockSync.Raise(x => x.FilesChanged += null, mockSync.Object, new SyncFilesChangedEventArgs("photos", ["a.jpg"]));

        // Assert
        mockSweeper.Verify(x => x.EncryptAsync(It.IsAny<IProgress<E2eProgress>?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GivenStatusUnknown_WhenBackOnline_ThenInitializesAgainAndSyncs()
    {
        // Arrange
        status = E2eStatus.Unknown;
        mockKeys.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).Callback(() => status = E2eStatus.Disabled).ReturnsAsync(E2eStatus.Disabled);
        using var sut = CreateSut();

        // Act
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, new ConnectivityStatus { IsOnline = true });

        // Assert
        mockKeys.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockScheduler.Verify(x => x.SyncNowAsync(), Times.Once);
    }

    [Fact]
    public void GivenStatusKnown_WhenBackOnline_ThenNothingHappens()
    {
        using var sut = CreateSut();
        mockConnectivity.Raise(x => x.StatusChanged += null, mockConnectivity.Object, new ConnectivityStatus { IsOnline = true });
        mockKeys.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GivenKeyManagerStatusChanges_WhenSubscribed_ThenRaisesStatusChanged()
    {
        // Arrange
        using var sut = CreateSut();
        var raised = new List<E2eStatus>();
        sut.StatusChanged += (_, s) => raised.Add(s);

        // Act
        mockKeys.Raise(x => x.StatusChanged += null, mockKeys.Object, E2eStatus.Unlocked);
        sut.Dispose();
        mockKeys.Raise(x => x.StatusChanged += null, mockKeys.Object, E2eStatus.Disabled);

        // Assert
        Assert.Equal([E2eStatus.Unlocked], raised);
    }

    [Fact]
    public void GivenKeyRing_WhenCanDecrypt_ThenTrueOnlyForHeldKeys()
    {
        // Arrange
        byte[]? key = [1];
        mockRing.Setup(x => x.TryGetFileKey("k_1", out key)).Returns(true);
        using var sut = CreateSut();

        // Act & Assert
        Assert.True(sut.CanDecrypt("k_1"));
        Assert.False(sut.CanDecrypt("k_2"));
        Assert.True(sut.HasPasskeyKeys);
        Assert.Equal(E2eStatus.Disabled, sut.Status);
    }

    private SyncEncryptionService CreateSut() =>
        new(mockKeys.Object, mockRing.Object, mockSweeper.Object, [profile], mockSync.Object, mockScheduler.Object, mockConnectivity.Object, mockLock.Object, mockChannel.Object);

    private sealed class ListProgress(List<E2eProgress> reported) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => reported.Add(value);
    }
}
