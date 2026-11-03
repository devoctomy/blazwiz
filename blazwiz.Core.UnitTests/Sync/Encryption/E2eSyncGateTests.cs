using System.Security.Cryptography;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Models;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Encryption;
using blazwiz.Core.UnitTests.Encryption.Fakes;
using Moq;

namespace blazwiz.Core.UnitTests.Sync.Encryption;

public class E2eSyncGateTests
{
    private readonly Mock<IE2eKeyManager> mockKeys = new();
    private readonly SyncOptions options = new();

    public E2eSyncGateTests()
    {
        options.Profiles.Add(new SyncProfileOptions { Name = "notes", IsEncrypted = true });
        options.Profiles.Add(new SyncProfileOptions { Name = "photos" });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("keyManager", Assert.Throws<ArgumentNullException>(() => new E2eSyncGate(null!, options)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new E2eSyncGate(mockKeys.Object, null!)).ParamName);
    }

    [Theory]
    [InlineData(E2eStatus.Unsupported, true)]
    [InlineData(E2eStatus.Unlocked, true)]
    [InlineData(E2eStatus.Blocked, false)]
    [InlineData(E2eStatus.NeedsSetup, false)]
    [InlineData(E2eStatus.DisabledElsewhere, false)]
    public async Task GivenStatusNotNeedingACheck_WhenCanSyncAsyncEncryptedProfile_ThenOnlyWhenNothingPlainCanReachEncryptedFiles(E2eStatus status, bool expected)
    {
        // Arrange
        mockKeys.SetupGet(x => x.Status).Returns(status);
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        Assert.Equal(expected, await sut.CanSyncAsync("notes"));
        mockKeys.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(E2eStatus.Disabled, E2eStatus.Disabled, true)]
    [InlineData(E2eStatus.Disabled, E2eStatus.NeedsSetup, false)]
    [InlineData(E2eStatus.Disabled, E2eStatus.Blocked, false)]
    [InlineData(E2eStatus.Disabled, E2eStatus.Unknown, false)]
    [InlineData(E2eStatus.Unavailable, E2eStatus.Unavailable, true)]
    [InlineData(E2eStatus.Unavailable, E2eStatus.Blocked, false)]
    [InlineData(E2eStatus.Unknown, E2eStatus.Unknown, false)]
    [InlineData(E2eStatus.Unknown, E2eStatus.Disabled, true)]
    [InlineData(E2eStatus.Unknown, E2eStatus.Unlocked, true)]
    public async Task GivenStatusWritingPlaintextOrUnknown_WhenCanSyncAsyncEncryptedProfile_ThenKeysAreCheckedAgainFirst(E2eStatus status, E2eStatus rechecked, bool expected)
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        mockKeys.SetupGet(x => x.Status).Returns(status);
        mockKeys.Setup(x => x.InitializeAsync(cts.Token)).ReturnsAsync(rechecked);
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        Assert.Equal(expected, await sut.CanSyncAsync("notes", cts.Token));
        mockKeys.Verify(x => x.InitializeAsync(cts.Token), Times.Once);
    }

    [Fact]
    public async Task GivenKeysCannotBeRead_WhenCanSyncAsyncEncryptedProfile_ThenHeldBack()
    {
        // Arrange
        mockKeys.SetupGet(x => x.Status).Returns(E2eStatus.Disabled);
        mockKeys.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidDataException("corrupt"));
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        Assert.False(await sut.CanSyncAsync("notes"));
    }

    [Fact]
    public async Task GivenCancelledDuringCheck_WhenCanSyncAsyncEncryptedProfile_ThenThrowsOperationCanceledException()
    {
        // Arrange
        mockKeys.SetupGet(x => x.Status).Returns(E2eStatus.Disabled);
        mockKeys.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.CanSyncAsync("notes"));
    }

    [Theory]
    [InlineData(E2eStatus.Unknown)]
    [InlineData(E2eStatus.Disabled)]
    [InlineData(E2eStatus.NeedsSetup)]
    public async Task GivenAnyStatus_WhenCanSyncAsyncUnencryptedProfile_ThenSyncsWithoutCheckingKeys(E2eStatus status)
    {
        // Arrange
        mockKeys.SetupGet(x => x.Status).Returns(status);
        var sut = new E2eSyncGate(mockKeys.Object, options);

        // Act & Assert
        Assert.True(await sut.CanSyncAsync("photos"));
        mockKeys.Verify(x => x.InitializeAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenDeviceWritingPlaintext_WhenEncryptionEnabledOnAnotherDevice_ThenItsNextPassIsHeldBackAndWritesRefused()
    {
        // Arrange
        var userKeys = new FakeUserKeysStore();
        var a = new Device(userKeys, options);
        var b = new Device(userKeys, options);
        Assert.Equal(E2eStatus.Disabled, await b.Manager.InitializeAsync());
        Assert.True(await b.Gate.CanSyncAsync("notes"));
        await a.Manager.InitializeAsync();

        // Act
        await a.Manager.EnableAsync("correct horse battery staple");
        var canSync = await b.Gate.CanSyncAsync("notes");

        // Assert
        Assert.False(canSync);
        Assert.Equal(E2eStatus.NeedsSetup, b.Manager.Status);
        Assert.Equal(E2eWriteMode.Refuse, b.Ring.WriteMode);
    }

    /// <summary>
    /// One device with real keys, sharing keys.json with the others.
    /// </summary>
    private sealed class Device
    {
        public Device(FakeUserKeysStore userKeys, SyncOptions options)
        {
            var derived = new InMemoryDerivedKeyStore();
            derived.SetKey(E2eKeyNames.KeyEncryptionKey, RandomNumberGenerator.GetBytes(32));
            var argon2Options = new Argon2idOptions { Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 } };
            var clock = new Mock<TimeProvider>();
            clock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
            Ring = new E2eKeyRing(derived);
            Manager = new E2eKeyManager(userKeys, new FakeE2eLocalKeyStore(), derived, Ring, new BouncyCastleArgon2idKeyDerivationService(), new AeadKeyWrapService(new BouncyCastleAesGcmAeadCipher()), argon2Options, clock.Object);
            Gate = new E2eSyncGate(Manager, options);
        }

        public E2eKeyRing Ring { get; }

        public E2eKeyManager Manager { get; }

        public E2eSyncGate Gate { get; }
    }
}
