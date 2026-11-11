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

    private static readonly E2eProfileSettings Settings = new();

    // What every device of the user shares: the keys documents and the index (the data bucket).
    private readonly FakeUserKeysStore userKeys = new();
    private readonly FakeE2eProfileIndexStore index = new();
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly AeadKeyWrapService keyWrap = new(new BouncyCastleAesGcmAeadCipher());
    private readonly E2eKeysDocumentService documents;
    private readonly Device device;

    public E2eProtectedProfileManagerTests()
    {
        var options = new Argon2idOptions { Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 } };
        documents = new E2eKeysDocumentService(new BouncyCastleArgon2idKeyDerivationService(), keyWrap, options, clock);
        device = new Device(this);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var local = new FakeE2eLocalKeyStore();
        var localIndex = new FakeE2eLocalProfileIndexStore();
        var derived = new InMemoryDerivedKeyStore();
        var rings = new E2eKeyRings(derived);

        // Act & Assert
        Assert.Equal("userKeysStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(null!, local, index, localIndex, rings, documents, clock, derived, keyWrap)).ParamName);
        Assert.Equal("localKeyStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, null!, index, localIndex, rings, documents, clock, derived, keyWrap)).ParamName);
        Assert.Equal("indexStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, null!, localIndex, rings, documents, clock, derived, keyWrap)).ParamName);
        Assert.Equal("localIndexStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, null!, rings, documents, clock, derived, keyWrap)).ParamName);
        Assert.Equal("keyRings", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, null!, documents, clock, derived, keyWrap)).ParamName);
        Assert.Equal("keysDocuments", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, null!, clock, derived, keyWrap)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, documents, null!, derived, keyWrap)).ParamName);
        Assert.Equal("derivedKeyStore", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, documents, clock, null!, keyWrap)).ParamName);
        Assert.Equal("keyWrapService", Assert.Throws<ArgumentNullException>(() => new E2eProtectedProfileManager(userKeys, local, index, localIndex, rings, documents, clock, derived, null!)).ParamName);
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
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);

        // Assert
        Assert.True(Guid.TryParseExact(scope.Id, "D", out _));
        Assert.True(userKeys.Values.ContainsKey(scope.Id));
        Assert.Single(userKeys.Values);
        var entry = Assert.Single(await device.Manager.GetProfilesAsync());
        Assert.Equal(scope.Id, entry.Id);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, entry.Created);
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.Equal([scope], device.Manager.Unlocked);
        Assert.NotNull(device.Rings.Get(scope).ActiveKeyId);
        Assert.Equal(userKeys.Values[scope.Id], device.LocalKeys.Get(scope)!.CachedDocument);
        Assert.Equal(index.Value, device.LocalIndex.Value);
        Assert.Equal([scope], changed);
    }

    [Fact]
    public async Task GivenTwoProfiles_WhenCreated_ThenEachHasItsOwnKeys()
    {
        // Act
        var first = await device.Manager.CreateAsync(Passphrase, Settings);
        var second = await device.Manager.CreateAsync("another passphrase", Settings);

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
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);

        // Act
        var profiles = await other.Manager.GetProfilesAsync();

        // Assert
        Assert.Equal(scope.Id, Assert.Single(profiles).Id);
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.Null(other.Rings.Get(scope).ActiveKeyId);
    }

    [Fact]
    public async Task GivenTheRightPassphrase_WhenUnlockedOnAnotherDevice_ThenItHasTheSameKeys()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
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
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(scope, "wrong"));
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.Null(other.Rings.Get(scope).ActiveKeyId);
    }

    [Fact]
    public async Task GivenTwoProfiles_WhenOneIsUnlockedWithTheOthersPassphrase_ThenThrows()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase, Settings);
        var second = await device.Manager.CreateAsync("another passphrase", Settings);
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
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
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
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);
        userKeys.Failure = new HttpRequestException("offline");

        // Act & Assert
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => other.Manager.UnlockAsync(scope, Passphrase));
    }

    [Fact]
    public async Task GivenOffline_WhenListed_ThenThisDevicesCopyOfTheIndexIsUsed()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
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
        var first = await device.Manager.CreateAsync(Passphrase, Settings);
        var second = await device.Manager.CreateAsync("another passphrase", Settings);
        var changed = new List<E2eKeyScope>();
        device.Manager.LockStateChanged += (_, scope) => changed.Add(scope);

        // Act
        device.Manager.Lock(first);

        // Assert
        Assert.False(device.Manager.IsUnlocked(first));
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        Assert.True(device.Manager.IsUnlocked(second));
        Assert.NotNull(device.Rings.Get(second).ActiveKeyId);
        Assert.Equal([first], changed);
    }

    [Fact]
    public async Task GivenUnlockedProfiles_WhenAllLocked_ThenNoneAreUnlocked()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase, Settings);
        var second = await device.Manager.CreateAsync("another passphrase", Settings);

        // Act
        device.Manager.LockAll();

        // Assert
        Assert.Empty(device.Manager.Unlocked);
        Assert.Null(device.Rings.Get(first).ActiveKeyId);
        Assert.Null(device.Rings.Get(second).ActiveKeyId);
    }

    [Fact]
    public async Task GivenANewPassphrase_WhenChanged_ThenOnlyItUnlocksNewKeysThatStillReadTheOldFiles()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var oldKeyId = device.Rings.Get(scope).ActiveKeyId!;
        Assert.True(device.Rings.Get(scope).TryGetFileKey(oldKeyId, out var oldFileKey));
        var reported = new List<E2eStep>();

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings, new Progress(p => reported.Add(p.Step)));

        // Assert
        var other = new Device(this);
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(scope, Passphrase));
        await other.Manager.UnlockAsync(scope, "battery staple");
        var ring = other.Rings.Get(scope);
        Assert.NotEqual(oldKeyId, ring.ActiveKeyId);
        Assert.Equal(device.Rings.Get(scope).ActiveKeyId, ring.ActiveKeyId);
        Assert.True(ring.TryGetFileKey(oldKeyId, out var stillThere));
        Assert.Equal(oldFileKey, stillThere);
        Assert.True(device.Manager.IsRotationInProgress(scope));
        Assert.True(other.Manager.IsRotationInProgress(scope));
        Assert.Equal(userKeys.Values[scope.Id], device.LocalKeys.Get(scope)!.CachedDocument);
        Assert.Equal([E2eStep.DeriveKey, E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey], reported.Distinct());
    }

    [Fact]
    public async Task GivenThePassphraseChanged_WhenTheOldMasterKeyIsUsed_ThenItUnwrapsNoneOfTheNewKeys()
    {
        // Arrange: someone who once held the master key.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        Assert.True(device.Rings.Get(scope).TryGetMasterKey(out var oldMasterKey));

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        var document = documents.Parse(userKeys.Values[scope.Id]);
        Assert.Throws<InvalidDataException>(() => documents.LoadFileKeys(new E2eKeyRing(new InMemoryDerivedKeyStore(), scope), document, oldMasterKey));
        Assert.Single(document.Keys, k => k.Value.Status == FileKeyEntry.ActiveStatus);
        Assert.Single(document.Keys, k => k.Value.Status == FileKeyEntry.RetiredStatus);
    }

    [Fact]
    public async Task GivenTheKeysDocumentChangedElsewhere_WhenThePassphraseIsChanged_ThenItStartsAgainWithNewKeysAndKeepsNoneOfTheFirstAttempt()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        userKeys.BeforeWrite = s =>
        {
            userKeys.BeforeWrite = null;
            userKeys.Values[s.Id] += " ";
        };

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        Assert.Equal(1, userKeys.ConflictCount);
        var document = documents.Parse(userKeys.Values[scope.Id]);
        Assert.Equal(2, document.Keys.Count);
        Assert.Equal(device.Rings.Get(scope).ActiveKeyId, Assert.Single(document.Keys, k => k.Value.Status == FileKeyEntry.ActiveStatus).Key);
    }

    [Fact]
    public async Task GivenDeviceUnlockOnWithoutThePasskeysKey_WhenThePassphraseIsChanged_ThenNoCopyOfTheOldMasterKeyStaysOnThisDevice()
    {
        // Arrange: device unlock was turned on, but this session signed in with a password.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        device.DerivedKeys.RemoveKey(E2eKeyNames.KeyEncryptionKey);

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        Assert.False(await device.Manager.HasDeviceUnlockAsync(scope));
        Assert.True(string.IsNullOrEmpty(device.LocalKeys.Get(scope)!.WrappedMasterKey.Wrapped));
    }

    [Fact]
    public async Task GivenDeviceUnlockOnAnotherDevice_WhenThePassphraseIsChangedHere_ThenThatDeviceNeedsTheNewPassphrase()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, Passphrase);
        await other.Manager.EnableDeviceUnlockAsync(scope);
        other.Manager.Lock(scope);

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        var ex = await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => other.Manager.UnlockWithDeviceAsync(scope));
        Assert.Equal(E2eErrorCodes.KeysChanged, ex.Code);
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.False(await other.Manager.HasDeviceUnlockAsync(scope));
        Assert.Equal(userKeys.Values[scope.Id], other.LocalKeys.Get(scope)!.CachedDocument);
        await other.Manager.UnlockAsync(scope, "battery staple");
        Assert.True(other.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenTheKeysDocumentChangedWithTheSameMasterKey_WhenUnlockedWithThePasskey_ThenThisDevicesCopyIsUpdatedAndItUnlocks()
    {
        // Arrange: written elsewhere without a rotation (e.g. a rotation mark cleared).
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        device.Manager.Lock(scope);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, Passphrase);
        var document = documents.Parse(userKeys.Values[scope.Id]);
        document.RotationInProgress = true;
        userKeys.Values[scope.Id] = documents.Serialize(document);

        // Act
        await device.Manager.UnlockWithDeviceAsync(scope);

        // Assert
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.True(device.Manager.IsRotationInProgress(scope));
        Assert.Equal(FakeUserKeysStore.ETagOf(userKeys.Values[scope.Id]), device.LocalKeys.Get(scope)!.CachedDocumentETag);
        Assert.True(await device.Manager.HasDeviceUnlockAsync(scope));
    }

    [Fact]
    public async Task GivenTheKeysDocumentUnchanged_WhenUnlockedWithThePasskey_ThenItUnlocksFromThisDevicesCopy()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        device.Manager.Lock(scope);
        var etag = device.LocalKeys.Get(scope)!.CachedDocumentETag;

        // Act
        await device.Manager.UnlockWithDeviceAsync(scope);

        // Assert
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.False(device.Manager.IsRotationInProgress(scope));
        Assert.Equal(etag, device.LocalKeys.Get(scope)!.CachedDocumentETag);
        Assert.True(await device.Manager.HasDeviceUnlockAsync(scope));
    }

    [Fact]
    public async Task GivenOfflineAfterARotationElsewhere_WhenUnlockedWithThePasskey_ThenItUnlocksFromThisDevicesCopy()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        var oldKeyId = device.Rings.Get(scope).ActiveKeyId;
        device.Manager.Lock(scope);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, Passphrase);
        await other.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);
        userKeys.Failure = new HttpRequestException("offline");

        // Act
        await device.Manager.UnlockWithDeviceAsync(scope);

        // Assert: edits made now are under the old key, which the rotated document keeps.
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.Equal(oldKeyId, device.Rings.Get(scope).ActiveKeyId);
    }

    [Fact]
    public async Task GivenARotationInProgress_WhenCompleted_ThenTheMarkIsClearedForEveryDevice()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Act
        await device.Manager.CompleteRotationAsync(scope);

        // Assert
        Assert.False(device.Manager.IsRotationInProgress(scope));
        Assert.False(documents.Parse(userKeys.Values[scope.Id]).RotationInProgress);
        Assert.Equal(userKeys.Values[scope.Id], device.LocalKeys.Get(scope)!.CachedDocument);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, "battery staple");
        Assert.False(other.Manager.IsRotationInProgress(scope));
    }

    [Fact]
    public async Task GivenTheKeysRotatedAgainElsewhere_WhenCompleted_ThenTheirMarkStays()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, "battery staple");
        await other.Manager.ChangePassphraseAsync(scope, "battery staple", "tr0ub4dor and more", Settings);

        // Act
        await device.Manager.CompleteRotationAsync(scope);

        // Assert
        Assert.True(documents.Parse(userKeys.Values[scope.Id]).RotationInProgress);
    }

    [Fact]
    public async Task GivenALockedProfile_WhenTheRotationIsCompleted_ThenThrowsLocked()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);
        device.Manager.Lock(scope);

        // Act & Assert
        Assert.False(device.Manager.IsRotationInProgress(scope));
        await Assert.ThrowsAsync<E2eLockedException>(() => device.Manager.CompleteRotationAsync(scope));
        Assert.True(documents.Parse(userKeys.Values[scope.Id]).RotationInProgress);
    }

    [Fact]
    public async Task GivenTheWrongCurrentPassphrase_WhenChanged_ThenThrowsAndNothingIsWritten()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var before = userKeys.Values[scope.Id];

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => device.Manager.ChangePassphraseAsync(scope, "wrong", "battery staple", Settings));
        Assert.Equal(before, userKeys.Values[scope.Id]);
    }

    [Fact]
    public async Task GivenAProfile_WhenDeleted_ThenItsKeysAndIndexEntryAreGoneAndItIsLocked()
    {
        // Arrange
        var first = await device.Manager.CreateAsync(Passphrase, Settings);
        var second = await device.Manager.CreateAsync("another passphrase", Settings);

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
    public async Task GivenAProfile_WhenItsIdleTimeoutIsSet_ThenEveryDeviceSeesIt()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);
        Assert.False((await other.Manager.GetProfilesAsync()).Single().TryGetIdleTimeout(out _));

        // Act
        await device.Manager.SetIdleTimeoutAsync(scope, TimeSpan.FromMinutes(5));

        // Assert
        var entry = Assert.Single(await other.Manager.GetProfilesAsync());
        Assert.Equal(300, entry.IdleTimeoutSeconds);
        Assert.True(entry.TryGetIdleTimeout(out var timeout));
        Assert.Equal(TimeSpan.FromMinutes(5), timeout);
        Assert.Contains("\"idleTimeoutSeconds\":300", index.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenAProfile_WhenSetToNeverLockForBeingIdle_ThenItIsRecordedAsZero()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);

        // Act
        await device.Manager.SetIdleTimeoutAsync(scope, null);

        // Assert
        var entry = Assert.Single(await device.Manager.GetProfilesAsync());
        Assert.Equal(0, entry.IdleTimeoutSeconds);
        Assert.True(entry.TryGetIdleTimeout(out var timeout));
        Assert.Null(timeout);
    }

    [Fact]
    public async Task GivenNoIdleTimeoutChosen_WhenTheIndexIsWritten_ThenNoneIsRecorded()
    {
        // Act
        await device.Manager.CreateAsync(Passphrase, Settings);

        // Assert
        Assert.DoesNotContain("idleTimeoutSeconds", index.Value, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNoSuchProfile_WhenItsIdleTimeoutIsSet_ThenThrowsNotFoundAndNothingIsWritten()
    {
        // Arrange
        await device.Manager.CreateAsync(Passphrase, Settings);
        var writes = index.SetCount;

        // Act & Assert
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => device.Manager.SetIdleTimeoutAsync(new E2eKeyScope(Guid.NewGuid().ToString("D")), TimeSpan.FromMinutes(1)));
        Assert.Equal(writes, index.SetCount);
    }

    [Fact]
    public async Task GivenAnIdleTimeoutUnderASecond_WhenSet_ThenThrowsArgumentOutOfRange()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => device.Manager.SetIdleTimeoutAsync(scope, TimeSpan.FromMilliseconds(500)));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.SetIdleTimeoutAsync(new E2eKeyScope("notes"), TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public async Task GivenAProfileDeletedOnAnotherDevice_WhenForgotten_ThenThisDevicesCopyIsGoneAndItIsLocked()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, Passphrase);
        await device.Manager.DeleteAsync(scope);

        // Act
        await other.Manager.ForgetAsync(scope);

        // Assert
        Assert.Null(other.LocalKeys.Get(scope));
        Assert.False(other.Manager.IsUnlocked(scope));
        Assert.Null(other.Rings.Get(scope).ActiveKeyId);
        await Assert.ThrowsAsync<ArgumentException>(() => other.Manager.ForgetAsync(new E2eKeyScope("notes")));
    }

    [Fact]
    public async Task GivenAShortPassphrase_WhenCreatedOrChanged_ThenThrowsTooShortAndNothingIsWritten()
    {
        // Arrange
        var settings = new E2eProfileSettings { MinimumPassphraseLength = 20 };
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var before = userKeys.Values[scope.Id];

        // Act & Assert
        var ex = await Assert.ThrowsAsync<E2ePassphraseTooShortException>(() => device.Manager.CreateAsync(Passphrase, settings));
        Assert.Equal(E2eErrorCodes.PassphraseTooShort, ex.Code);
        await Assert.ThrowsAsync<E2ePassphraseTooShortException>(() => device.Manager.ChangePassphraseAsync(scope, Passphrase, "short", Settings));
        Assert.Equal(before, userKeys.Values[scope.Id]);
        Assert.Single(userKeys.Values);
    }

    [Fact]
    public async Task GivenAProfilesOwnArgon2Costs_WhenCreated_ThenTheyAreInItsKeysDocument()
    {
        // Arrange
        var settings = new E2eProfileSettings { Argon2 = new Argon2idParameters { MemoryKib = 128, Iterations = 2, Parallelism = 1, KeyLength = 32 } };

        // Act
        var scope = await device.Manager.CreateAsync(Passphrase, settings);

        // Assert
        var kdf = documents.Parse(userKeys.Values[scope.Id]).Kdf;
        Assert.Equal(128, kdf.MemoryKib);
        Assert.Equal(2, kdf.Iterations);
    }

    [Fact]
    public async Task GivenAKnownId_WhenItsKeysAreCreated_ThenItIsListedAndUnlockedAndCannotBeCreatedAgain()
    {
        // Arrange
        var scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));

        // Act
        await device.Manager.CreateKeysAsync(scope, Passphrase, Settings);

        // Assert
        Assert.Equal(scope.Id, Assert.Single(await device.Manager.GetProfilesAsync()).Id);
        Assert.True(device.Manager.IsUnlocked(scope));
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.Manager.CreateKeysAsync(scope, Passphrase, Settings));
    }

    [Fact]
    public async Task GivenDeviceUnlockTurnedOn_WhenLockedAndUnlockedWithThePasskey_ThenUnlockedWithoutThePassphrase()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        device.Manager.Lock(scope);

        // Act
        var can = await device.Manager.CanUnlockWithDeviceAsync(scope);
        await device.Manager.UnlockWithDeviceAsync(scope);

        // Assert
        Assert.True(can);
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.NotNull(device.Rings.Get(scope).ActiveKeyId);
    }

    [Fact]
    public async Task GivenDeviceUnlockTurnedOn_WhenThePasskeysKeyIsGoneFromMemory_ThenItIsStillOnHere_ButCannotUnlockNow()
    {
        // Arrange: e.g. after a reload, until the passkey is used again.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        device.Manager.Lock(scope);
        device.DerivedKeys.RemoveKey(E2eKeyNames.KeyEncryptionKey);

        // Act
        var hasIt = await device.Manager.HasDeviceUnlockAsync(scope);
        var can = await device.Manager.CanUnlockWithDeviceAsync(scope);

        // Assert
        Assert.True(hasIt);
        Assert.False(can);
    }

    [Fact]
    public async Task GivenDeviceUnlockNeverOrNoLongerOn_WhenChecked_ThenNotOnHere()
    {
        // Arrange
        var never = await device.Manager.CreateAsync(Passphrase, Settings);
        var turnedOff = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(turnedOff);
        await device.Manager.DisableDeviceUnlockAsync(turnedOff);

        // Act & Assert
        Assert.False(await device.Manager.HasDeviceUnlockAsync(never));
        Assert.False(await device.Manager.HasDeviceUnlockAsync(turnedOff));
    }

    [Fact]
    public async Task GivenDeviceUnlockOff_WhenUnlockedWithThePasskey_ThenThePassphraseIsRequired()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        await device.Manager.DisableDeviceUnlockAsync(scope);
        device.Manager.Lock(scope);

        // Act & Assert
        Assert.False(await device.Manager.CanUnlockWithDeviceAsync(scope));
        await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => device.Manager.UnlockWithDeviceAsync(scope));
        Assert.False(device.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenDeviceUnlockOn_WhenAnotherProfilesWrappedKeyIsPutInItsPlace_ThenThePassphraseIsRequired()
    {
        // Arrange: the wrapped master key is bound to its profile, so one moved between entries does not unwrap.
        var a = await device.Manager.CreateAsync(Passphrase, Settings);
        var b = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(a);
        await device.Manager.EnableDeviceUnlockAsync(b);
        device.Manager.Lock(a);
        device.Manager.Lock(b);
        var localA = (await device.LocalKeys.GetAsync(a))!;
        var localB = (await device.LocalKeys.GetAsync(b))!;
        localB.WrappedMasterKey = localA.WrappedMasterKey;
        await device.LocalKeys.SaveAsync(b, localB);

        // Act & Assert
        await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => device.Manager.UnlockWithDeviceAsync(b));
        Assert.False(device.Manager.IsUnlocked(b));
        await device.Manager.UnlockWithDeviceAsync(a);
        Assert.True(device.Manager.IsUnlocked(a));
    }

    [Fact]
    public async Task GivenDeviceUnlockOnOneDevice_WhenAnotherDeviceTries_ThenThePassphraseIsRequired()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);
        var other = new Device(this);
        await other.Manager.UnlockAsync(scope, Passphrase);
        other.Manager.Lock(scope);

        // Act & Assert
        Assert.False(await other.Manager.CanUnlockWithDeviceAsync(scope));
        await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => other.Manager.UnlockWithDeviceAsync(scope));
    }

    [Fact]
    public async Task GivenALockedProfile_WhenDeviceUnlockIsTurnedOn_ThenThrowsLocked()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        device.Manager.Lock(scope);

        // Act & Assert
        await Assert.ThrowsAsync<E2eLockedException>(() => device.Manager.EnableDeviceUnlockAsync(scope));
    }

    [Fact]
    public async Task GivenDeviceUnlockOn_WhenThePassphraseIsChangedOrTheProfileUnlockedAgain_ThenItStillWorks()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        await device.Manager.EnableDeviceUnlockAsync(scope);

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);
        device.Manager.Lock(scope);
        await device.Manager.UnlockAsync(scope, "battery staple");
        device.Manager.Lock(scope);
        await device.Manager.UnlockWithDeviceAsync(scope);

        // Assert
        Assert.True(device.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenAProfileLostFromTheIndex_WhenRestored_ThenItIsInTheIndexAgain()
    {
        // Arrange: the index lost the entry (two devices writing it at once), but the keys document is still there.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        index.Value = System.Text.Json.JsonSerializer.Serialize(new E2eProfileIndex());

        // Act
        var restored = await device.Manager.TryRestoreToIndexAsync(scope);

        // Assert
        Assert.True(restored);
        Assert.Equal(scope.Id, Assert.Single(await device.Manager.GetProfilesAsync()).Id);
    }

    [Fact]
    public async Task GivenAProfileWhoseKeysAreGone_WhenRestored_ThenItIsNot()
    {
        // Arrange: deleted on another device, keys and all.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        index.Value = System.Text.Json.JsonSerializer.Serialize(new E2eProfileIndex());
        userKeys.Values.Remove(scope.Id);

        // Act
        var restored = await device.Manager.TryRestoreToIndexAsync(scope);

        // Assert
        Assert.False(restored);
        Assert.Empty(await device.Manager.GetProfilesAsync());
    }

    [Fact]
    public async Task GivenTheKeysStoreCannotBeReached_WhenRestored_ThenTheProfileIsKeptAndTheIndexLeftAlone()
    {
        // Arrange: offline, so whether the keys are still there is not known.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var before = index.SetCount;
        userKeys.Failure = new HttpRequestException("offline");

        // Act
        var restored = await device.Manager.TryRestoreToIndexAsync(scope);

        // Assert
        Assert.True(restored);
        Assert.Equal(before, index.SetCount);
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
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.UnlockAsync(new E2eKeyScope("notes"), Passphrase));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.UnlockAsync(new E2eKeyScope("index"), Passphrase));
        Assert.Throws<ArgumentException>(() => device.Manager.Lock(new E2eKeyScope("notes")));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.DeleteAsync(new E2eKeyScope("notes")));
        await Assert.ThrowsAsync<ArgumentException>(() => device.Manager.ChangePassphraseAsync(new E2eKeyScope("notes"), "a", "b", Settings));
        await Assert.ThrowsAsync<ArgumentNullException>(() => device.Manager.UnlockAsync(null!, Passphrase));
        Assert.Throws<ArgumentNullException>(() => device.Manager.IsUnlocked(null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => device.Manager.CreateAsync(string.Empty, Settings));
    }

    [Fact]
    public async Task GivenAnotherDeviceAddsAProfileAtTheSameTime_WhenCreated_ThenBothAreInTheIndex()
    {
        // Arrange: the other device's profile lands in the index between this device reading and writing it.
        var other = new Device(this);
        var theirs = await other.Manager.CreateAsync(Passphrase, Settings);
        var withTheirs = index.Value;
        index.Value = null;
        index.BeforeWrite = () =>
        {
            index.BeforeWrite = null;
            index.Value = withTheirs;
        };

        // Act
        var mine = await device.Manager.CreateAsync(Passphrase, Settings);

        // Assert
        Assert.Equal(1, index.ConflictCount);
        Assert.Equal([theirs.Id, mine.Id], (await device.Manager.GetProfilesAsync()).Select(p => p.Id));
        Assert.Equal(index.Value, device.LocalIndex.Value);
    }

    [Fact]
    public async Task GivenAProfileDeletedOnAnotherDeviceAtTheSameTime_WhenItsIdleTimeoutIsSet_ThenThrowsNotFoundAndTheDeletionStands()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var other = new Device(this);
        index.BeforeWrite = () =>
        {
            index.BeforeWrite = null;
            other.Manager.DeleteAsync(scope).GetAwaiter().GetResult();
        };

        // Act & Assert
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => device.Manager.SetIdleTimeoutAsync(scope, TimeSpan.FromMinutes(5)));
        Assert.Equal(1, index.ConflictCount);
        Assert.Empty(await device.Manager.GetProfilesAsync());
    }

    [Fact]
    public async Task GivenTheIndexKeepsChangingElsewhere_WhenItsIdleTimeoutIsSet_ThenThrowsDocumentChangedAfterTheLastAttempt()
    {
        // Arrange: every write meets an index changed since it was read.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var before = index.SetCount;
        index.BeforeWrite = () => index.Value += " ";

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => device.Manager.SetIdleTimeoutAsync(scope, TimeSpan.FromMinutes(5)));
        Assert.Equal(E2eProtectedProfileManager.MaxWriteAttempts, index.SetCount - before);
        Assert.Equal(E2eProtectedProfileManager.MaxWriteAttempts, index.ConflictCount);
        Assert.Null(Assert.Single(await device.Manager.GetProfilesAsync()).IdleTimeoutSeconds);
    }

    [Fact]
    public async Task GivenTheKeysDocumentRewrittenElsewhereWithTheSamePassphrase_WhenThePassphraseIsChanged_ThenTheChangeIsMadeToTheNewVersion()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        userKeys.BeforeWrite = s =>
        {
            userKeys.BeforeWrite = null;
            userKeys.Values[s.Id] += " ";
        };

        // Act
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        Assert.Equal(1, userKeys.ConflictCount);
        var other = new Device(this);
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.UnlockAsync(scope, Passphrase));
        await other.Manager.UnlockAsync(scope, "battery staple");
        Assert.True(other.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenThePassphraseChangedOnAnotherDeviceAtTheSameTime_WhenChanged_ThenThrowsWrongPassphraseAndTheirsStands()
    {
        // Arrange: the other device's change lands between this device reading and writing the keys document.
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var original = userKeys.Values[scope.Id];
        var other = new Device(this);
        await other.Manager.ChangePassphraseAsync(scope, Passphrase, "their passphrase", Settings);
        var theirs = userKeys.Values[scope.Id];
        userKeys.Values[scope.Id] = original;
        userKeys.BeforeWrite = s =>
        {
            userKeys.BeforeWrite = null;
            userKeys.Values[s.Id] = theirs;
        };

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => device.Manager.ChangePassphraseAsync(scope, Passphrase, "my passphrase", Settings));
        Assert.Equal(theirs, userKeys.Values[scope.Id]);
        device.Manager.Lock(scope);
        await device.Manager.UnlockAsync(scope, "their passphrase");
        Assert.True(device.Manager.IsUnlocked(scope));
    }

    [Fact]
    public async Task GivenTheKeysDocumentKeepsChangingElsewhere_WhenThePassphraseIsChanged_ThenThrowsDocumentChangedAfterTheLastAttempt()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        userKeys.BeforeWrite = s => userKeys.Values[s.Id] += " ";

        // Act & Assert
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings));
        Assert.Equal(E2eProtectedProfileManager.MaxWriteAttempts, userKeys.ConflictCount);
    }

    [Fact]
    public async Task GivenAProfile_WhenCreatedUnlockedOrItsPassphraseChanged_ThenThisDevicesCopyKeepsTheStoresVersion()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var created = device.LocalKeys.Get(scope)!.CachedDocumentETag;
        var other = new Device(this);

        // Act
        await other.Manager.UnlockAsync(scope, Passphrase);
        var unlocked = other.LocalKeys.Get(scope)!.CachedDocumentETag;
        await device.Manager.ChangePassphraseAsync(scope, Passphrase, "battery staple", Settings);

        // Assert
        Assert.NotNull(created);
        Assert.Equal(created, unlocked);
        Assert.Equal(FakeUserKeysStore.ETagOf(userKeys.Values[scope.Id]), device.LocalKeys.Get(scope)!.CachedDocumentETag);
        Assert.NotEqual(created, device.LocalKeys.Get(scope)!.CachedDocumentETag);
        Assert.Equal(created, other.LocalKeys.Get(scope)!.CachedDocumentETag);
    }

    [Fact]
    public async Task GivenOffline_WhenUnlockedFromThisDevicesCopy_ThenItsVersionIsKept()
    {
        // Arrange
        var scope = await device.Manager.CreateAsync(Passphrase, Settings);
        var etag = device.LocalKeys.Get(scope)!.CachedDocumentETag;
        device.Manager.Lock(scope);
        userKeys.Failure = new HttpRequestException("offline");

        // Act
        await device.Manager.UnlockAsync(scope, Passphrase);

        // Assert
        Assert.True(device.Manager.IsUnlocked(scope));
        Assert.Equal(etag, device.LocalKeys.Get(scope)!.CachedDocumentETag);
    }

    /// <summary>
    /// Reports progress straight away, on the caller's thread.
    /// </summary>
    private sealed class Progress(Action<E2eProgress> report) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => report(value);
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
            DerivedKeys = new InMemoryDerivedKeyStore();
            DerivedKeys.SetKey(E2eKeyNames.KeyEncryptionKey, System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            Rings = new E2eKeyRings(DerivedKeys);
            Manager = new E2eProtectedProfileManager(owner.userKeys, LocalKeys, owner.index, LocalIndex, Rings, owner.documents, owner.clock, DerivedKeys, owner.keyWrap);
        }

        public FakeE2eLocalKeyStore LocalKeys { get; }

        public FakeE2eLocalProfileIndexStore LocalIndex { get; }

        public InMemoryDerivedKeyStore DerivedKeys { get; }

        public E2eKeyRings Rings { get; }

        public E2eProtectedProfileManager Manager { get; }
    }
}
