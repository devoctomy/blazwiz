using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Sync;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Moq;

namespace Devoctomy.Blazwiz.Sync.IntTests.Support;

/// <summary>
/// One device: its own in-memory files and index, tracked like the app's, syncing with the real engine through the
/// real S3 remote store.
/// </summary>
public sealed class SimulatedDevice
{
    public const string Profile = "notes";
    public const string Owner = "inttest-user";

    private readonly Mock<TimeProvider> clock = new();

    public SimulatedDevice(S3SyncTarget target)
    {
        var user = new Mock<ISyncUserContext>();
        user.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        clock.Setup(x => x.GetUtcNow()).Returns(() => DateTimeOffset.UtcNow);

        var targets = new Mock<IS3SyncTargetProvider>();
        targets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(target);

        var matcher = new GlobSyncPathMatcher("notes/**/*", includeEmptyFolders: true);
        Files = new InMemorySyncFileStore(user.Object, clock.Object);
        TrackedFiles = new TrackingSyncFileStore(Profile, Files, Index, user.Object, Mock.Of<ISyncScheduler>(), clock.Object, new EncryptedFileFormat(new BouncyCastleAesGcmAeadCipher()));
        SyncProfile = new SyncProfile(Profile, Files, Index, new S3SyncRemoteStore(targets.Object, matcher), matcher);
        Engine = new SyncEngine(new SyncPlanner(), new NewestWinsConflictResolver(), new Sha256ContentHasher(), user.Object, Mock.Of<ISyncEventLog>(), clock.Object, new EncryptedFileFormat(new BouncyCastleAesGcmAeadCipher()), new SyncOptions());
    }

    /// <summary>
    /// Gets the device's files, without change tracking (what the engine sees).
    /// </summary>
    public InMemorySyncFileStore Files { get; }

    /// <summary>
    /// Gets the device's files as the app sees them: every change is tracked in the index.
    /// </summary>
    public ISyncFileStore TrackedFiles { get; }

    /// <summary>
    /// Gets the device's sync index.
    /// </summary>
    public InMemorySyncIndexStore Index { get; } = new();

    public SyncProfile SyncProfile { get; }

    public SyncEngine Engine { get; }

    public Task<SyncResult> SyncAsync() => Engine.SyncAsync(SyncProfile, verify: false);
}
