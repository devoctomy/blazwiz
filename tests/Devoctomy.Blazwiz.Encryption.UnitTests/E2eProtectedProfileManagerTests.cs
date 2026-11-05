using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.TestSupport.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eProtectedProfileManagerTests
{
    private const string Passphrase = "correct horse";

    // What every device of the user shares: the keys documents and the index (Secrets Manager).
    private readonly FakeUserKeysStore userKeys = new();
    private readonly FakeE2eProfileIndexStore index = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly E2eKeysDocumentService documents;
    private readonly Device device;

    public E2eProtectedProfileManagerTests()
    {
        var options = new Argon2idOptions { Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 } };
        documents = new E2eKeysDocumentService(new BouncyCastleArgon2idKeyDerivationService(), new AeadKeyWrapService(new BouncyCastleAesGcmAeadCipher()), options, clock);
        device = new Device(this);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var local = new FakeE2eLocalKeyStore();
        var localIndex = new FakeE2eLocalProfileIndexStore();
        var rings = new E2eKeyRings(new InMemoryDerivedKeyStore());

        // Act & Assert
        Assert.Equal("userKeysStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(null!, local, index, localIndex, rings, documents, clock)).ParamName);
        Assert.Equal("localKeyStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, null!, index, localIndex, rings, documents, clock)).ParamName);
        Assert.Equal("indexStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, null!, localIndex, rings, documents, clock)).ParamName);
        Assert.Equal("localIndexStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, null!, rings, documents, clock)).ParamName);
        Assert.Equal("keyRings", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, null!, documents, clock)).ParamName);
        Assert.Equal("keysDocuments", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, null!, clock)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, documents, null!)).ParamName);
    }

    [Fact]
    public async Task GivenNoProfiles_WhenListed_ThenEmpty()
    {
        // Act
        var profiles = await device.Manager.GetProfilesAsync();

        // Assert
        Assert.Empty(profiles);
    }

    [Fact]
    public async Task GivenAPassphrase_WhenCreated_ThenAGuidProfileWithItsOwnSecretIsListedAndUnlocked()
    {
        // Arrange
        var changed = new List<E2eKeyScope>();
        device.Manager.LockStateChanged += (_, scope) => changed.Add(scope);

        // Act
        var scope = await device.Manager.CreateAsync(Passphrase);

        // Assert
        Assert.True(Guid.TryParseExact(scope.Id, "D", out _));
        Assert.True(userKeys.Values.ContainsKey(scope.Id));
        Assert.False(userKeys.Values.ContainsKey(E2eKeyScope.AccountId));
        var entry = Assert.Single(await device.Manager.GetProfilesAsync());
        Assert.Equal(scope.Id, entry.Id);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, entry.Created);
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.Equal([scope], device.Manager.Unlocked);
        Assert.Equal(E2eWriteMode.Encrypt, device.Rings.Get(scope).WriteMode);
        Assert.Equal(userKeys.Values[scope.Id], device.LocalKeys.Get(scope)!.CachedDocument);
        Assert.Equal(index.Value, device.LocalIndex.Value);
        Assert.Equal([scope], changed);
    }

    [Fact]
    public async Task GivenTwoProfiles_WhenCreated_ThenEachHasItsOwnKeys()
    {
        // Act
        var first = await device.Manager.CreateAsync(Passphrase);
        var second = await device.Manager.CreateAsync("another passphrase");

        // Assert
        Assert.NotEqual(first, second);
        Assert.Equal([first.Id, second.Id], (await device.Manager.GetProfilesAsync()).Select(p => p.Id));
        Assert.True(device.Rings.Get(first).TryGetFileKey(device.Rings.Get(first).ActiveKeyId!, out var firstKey));
        Assert.True(device.Rings.Get(second).TryGetFileKey(device.Rings.Get(second).ActiveKeyId!, out var secondKey));
        Assert.NotEqual(firstKey, secondKey);
    }

    [Fact]
    public async Task GivenAProfileCreatedElsewhere_WhenANewDeviceSignsIn_ThenItIsListedButLocked()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var other = new Device(this);

        // Act
        var profiles = await other.Manager.GetProfilesAsync();

        // Assert
        Assert.Equal(scope.Id, Assert.Single(profiles).Id);
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.Equal(E2eWriteMode.Refuse, other.Rings.Get(scope).WriteMode);
    }

    [Fact]
    public async Task GivenTheRightPassphrase_WhenUnlockedOnAnotherDevice_ThenItHasTheSameKeys()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var other = new Device(this);

        // Act
        await other.Manager.UnlockAsync(scope, Passphrase);

        // Assert
        Assert.True(other.Manager.IsUnlocked(scope));
        var ring = device.Rings.Get(scope);
        Assert.Equal(ring.ActiveKeyId, other.Rings.Get(scope).ActiveKeyId);
        Assert.True(ring.TryGetFileKey(ring.ActiveKeyId!, out var expected));
        Assert.True(other.Rings.Get(scope).TryGetFileKey(ring.ActiveKeyId!, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(userKeys.Values[scope.Id], other.LocalKeys.Get(scope)!.CachedDocument);
    }

    [Fact]
    public async Task GivenTheWrongPassphrase_WhenUnlocked_ThenThrowsAndStaysLocked()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var other = new Device(this);

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(scope, "wrong"));
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.Equal(E2eWriteMode.Refuse, other.Rings.Get(scope).WriteMode);
    }

    [Fact]
    public async Task GivenTwoProfiles_WhenOneIsUnlockedWithTheOthersPassphrase_ThenThrows()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase);
        var second = await device.Manager.CreateAsync("another passphrase");
        var other = new Device(this);

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(second, Passphrase));
        await other.Manager.UnlockAsync(first, Passphrase);
        Assert.Equal([first], other.Manager.Unlocked);
    }

    [Fact]
    public async Task GivenAnUnknownProfile_WhenUnlocked_ThenThrowsNotFound()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => device.Manager.UnlockAsync(scope, Passphrase));
        Assert.Equal(E2eErrorCodes.ProfileNotFound, ex.Code);
    }

    [Fact]
    public async Task GivenOfflineWithACopy_WhenUnlocked_ThenUnlocksFromThisDevicesCopy()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        device.Manager.Lock(scope);
        userKeys.Failure = new HttpRequestException("offline");

        // Act
        await device.Manager.UnlockAsync(scope, Passphrase);

        // Assert
        Assert.True(device.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenOfflineWithoutACopy_WhenUnlocked_ThenThrowsNotFound()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var other = new Device(this);
        userKeys.Failure = new HttpRequestException("offline");

        // Act & Assert
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => other.Manager.UnlockAsync(scope, Passphrase));
    }

    [Fact]
    public async Task GivenOffline_WhenListed_ThenThisDevicesCopyOfTheIndexIsUsed()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        index.Failure = new HttpRequestException("offline");

        // Act
        var profiles = await device.Manager.GetProfilesAsync();

        // Assert
        Assert.Equal(scope.Id, Assert.Single(profiles).Id);
    }

    [Fact]
    public async Task GivenUnlockedProfiles_WhenOneIsLocked_ThenOnlyItsKeysAreWiped()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase);
        var second = await device.Manager.CreateAsync("another passphrase");
        var changed = new List<E2eKeyScope>();
        device.Manager.LockStateChanged += (_, scope) => changed.Add(scope);

        // Act
        device.Manager.Lock(first);

        // Assert
        Assert.False(device.Manager.IsUnlocked(first));
        Assert.Equal(E2eWriteMode.Refuse, device.Rings.Get(first).WriteMode);
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        Assert.True(device.Manager.IsUnlocked(second));
        Assert.Equal(E2eWriteMode.Encrypt, device.Rings.Get(second).WriteMode);
        Assert.Equal([first], changed);
    }

    [Fact]
    public async Task GivenUnlockedProfiles_WhenAllLocked_ThenNoneAreUnlocked()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase);
        var second = await device.Manager.CreateAsync("another passphrase");

        // Act
        device.Manager.LockAll();

        // Assert
        Assert.Empty(device.Manager.Unlocked);
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        Assert.Null(device.Rings.Get(second).ActiveKeyId);
    }

    [Fact]
    public async Task GivenANewPassphrase_WhenChanged_ThenOnlyItUnlocksTheSameKeys()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var activeKeyId = device.Rings.Get(scope).ActiveKeyId;

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple");

        // Assert
        var other = new Device(this);
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(scope, Passphrase));
        await other.Manager.UnlockAsync(scope, "battery staple");
        Assert.Equal(activeKeyId, other.Rings.Get(scope).ActiveKeyId);
        Assert.Equal(userKeys.Values[scope.Id], device.LocalKeys.Get(scope)!.CachedDocument);
    }

    [Fact]
    public async Task GivenTheWrongCurrentPassphrase_WhenChanged_ThenThrowsAndNothingIsWritten()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase);
        var before = userKeys.Values[scope.Id];

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => device.Manager.ChangePassphraseAsync(scope, "wrong", "battery staple"));
        Assert.Equal(before, userKeys.Values[scope.Id]);
    }

    [Fact]
    public async Task GivenAProfile_WhenDeleted_ThenItsKeysAndIndexEntryAreGoneAndItIsLocked()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase);
        var second = await device.Manager.CreateAsync("another passphrase");

        // Act
        await device.Manager.DeleteAsync(first);

        // Assert
        Assert.False(userKeys.Values.ContainsKey(first.Id));
        Assert.Null(device.LocalKeys.Get(first));
        Assert.Equal(second.Id, Assert.Single(await device.Manager.GetProfilesAsync()).Id);
        Assert.False(device.Manager.IsUnlocked(first));
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => new Device(this).Manager.UnlockAsync(first, Passphrase));
    }

    [Fact]
    public async Task GivenAnInvalidIndex_WhenListed_ThenThrowsInvalidData()
    {
        // Arrange
        index.Value = "not json";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => device.Manager.GetProfilesAsync());
    }

    [Fact]
    public async Task GivenAScopeThatIsNotAProtectedProfile_WhenUsed_ThenThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.UnlockAsync(E2eKeyScope.Account, Passphrase));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.UnlockAsync(new E2eKeyScope("index"), Passphrase));
        Assert.Throws<ArgumentException>(() => device.Manager.Lock(E2eKeyScope.Account));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.DeleteAsync(E2eKeyScope.Account));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.ChangePassphraseAsync(E2eKeyScope.Account, "a", "b"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => device.Manager.UnlockAsync(null!, Passphrase));
        Assert.Throws<ArgumentNullException>(() => device.Manager.IsUnlocked(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => device.Manager.CreateAsync(string.Empty));
    }

    /// <summary>
    /// One device: its local storage and its memory, sharing the user's keys and index with every other device.
    /// </summary>
    private sealed class Device
    {
        public Device(E2eProtectedProfileManagerTests owner)
        {
            LocalKeys = new FakeE2eLocalKeyStore();
            LocalIndex = new FakeE2eLocalProfileIndexStore();
            Rings = new E2eKeyRings(new InMemoryDerivedKeyStore());
            Manager = new E2eProtectedProfileManager(owner.userKeys, LocalKeys, owner.index, LocalIndex, Rings, owner.documents, owner.clock);
        }

        public FakeE2eLocalKeyStore LocalKeys { get; }

        public FakeE2eLocalProfileIndexStore LocalIndex { get; }

        public E2eKeyRings Rings { get; }

        public E2eProtectedProfileManager Manager { get; }
    }
}
