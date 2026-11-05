using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.TestSupport.Fakes;
using Moq;

namespace Devoctomy.Blazwiz.Sync.IntTests.Support;

/// <summary>
/// One device of the user, with protected profiles: the real profile service, key manager, encrypting stores and sync
/// engine, with in-memory local storage, and the user's keys, index and bucket shared with the other devices.
/// </summary>
public sealed class ProtectedDevice
{
    public const string Owner = "inttest-user";

    private static readonly Argon2idOptions LightArgon2 = new()
    {
        Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 },
    };

    private readonly EncryptedFileFormat format = new(new BouncyCastleAesGcmAeadCipher());
    private readonly Mock<ISyncUserContext> user = new();
    private readonly SyncEngine engine;

    public ProtectedDevice(SharedCloud cloud)
    {
        user.Setup(x => x.GetUserIdAsync()).ReturnsAsync(Owner);
        var keyWrap = new AeadKeyWrapService(new BouncyCastleAesGcmAeadCipher());
        var documents = new E2eKeysDocumentService(new BouncyCastleArgon2idKeyDerivationService(), keyWrap, LightArgon2, TimeProvider.System);
        var derivedKeys = new InMemoryDerivedKeyStore();
        Rings = new E2eKeyRings(derivedKeys);
        Manager = new E2eProtectedProfileManager(cloud.Keys, LocalKeys, cloud.Index, new FakeE2eLocalProfileIndexStore(), Rings, documents, TimeProvider.System, derivedKeys, keyWrap);
        Registry = new DeviceProfileRegistry(this, cloud);

        var resolver = new Mock<ISyncStorageResolver>();
        resolver.Setup(x => x.GetStateAsync(It.IsAny<string>())).ReturnsAsync(new SyncStorageState { Kind = SyncStorageKind.IndexedDb });
        resolver.Setup(x => x.GetKinds(It.IsAny<string>())).Returns([SyncStorageKind.IndexedDb]);
        resolver.Setup(x => x.GetStore(It.IsAny<string>(), SyncStorageKind.IndexedDb)).Returns((string name, SyncStorageKind _) => StoredFiles[name]);
        var locks = new Mock<ISyncLockService>();
        locks.Setup(x => x.AcquireAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());

        Profiles = new SyncProtectedProfileService(
            Manager,
            Registry,
            resolver.Object,
            user.Object,
            locks.Object,
            Mock.Of<IE2eLockChannel>(),
            new SyncOptions(),
            Mock.Of<IE2eIdleLock>(),
            Labels.Object);
        engine = new SyncEngine(new SyncPlanner(), new NewestWinsConflictResolver(), new Sha256ContentHasher(), user.Object, Mock.Of<ISyncEventLog>(), TimeProvider.System, format);
    }

    public FakeE2eLocalKeyStore LocalKeys { get; } = new();

    public Mock<IE2eProfileLabelStore> Labels { get; } = CreateLabels();

    public E2eKeyRings Rings { get; }

    public E2eProtectedProfileManager Manager { get; }

    public SyncProtectedProfileService Profiles { get; }

    public DeviceProfileRegistry Registry { get; }

    /// <summary>
    /// Gets each profile's files as stored on the device (ciphertext), by profile name. Kept after a profile is
    /// removed, so tests can see what is left behind.
    /// </summary>
    public Dictionary<string, InMemorySyncFileStore> StoredFiles { get; } = [];

    /// <summary>
    /// Syncs every profile on the device.
    /// </summary>
    public async Task SyncAsync()
    {
        foreach (var profile in Registry.Profiles)
        {
            await engine.SyncAsync(Registry.GetSyncProfile(profile.Name), verify: false);
        }
    }

    private static Mock<IE2eProfileLabelStore> CreateLabels()
    {
        var labels = new Mock<IE2eProfileLabelStore>();
        labels.Setup(x => x.GetAllAsync()).ReturnsAsync(new Dictionary<string, string>());
        return labels;
    }

    internal InMemorySyncFileStore CreateStoredFiles(string name)
    {
        var files = new InMemorySyncFileStore(user.Object, TimeProvider.System);
        StoredFiles[name] = files;
        return files;
    }

    internal ISyncFileStore CreateAppFiles(SyncProfileOptions profile, ISyncFileStore stored, ISyncIndexStore index)
    {
        var tracked = new TrackingSyncFileStore(profile.Name, stored, index, user.Object, Mock.Of<ISyncScheduler>(), TimeProvider.System, format);
        return new EncryptingSyncFileStore(tracked, Rings.Get(profile.KeyScope!), format);
    }
}
