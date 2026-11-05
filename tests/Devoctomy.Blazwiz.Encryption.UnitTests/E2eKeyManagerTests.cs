using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.TestSupport.Fakes;
using Moq;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eKeyManagerTests
{
    private const string Passphrase = "correct horse battery staple";

    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 45, 0, TimeSpan.Zero);

    private readonly FakeUserKeysStore userKeys = new();
    private readonly Argon2idOptions options = new() { Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 } };
    private readonly CountingArgon2 argon2 = new();
    private readonly AeadKeyWrapService keyWrap = new(new BouncyCastleAesGcmAeadCipher());
    private readonly Mock<TimeProvider> mockClock = new();
    private readonly Device device;

    public E2eKeyManagerTests()
    {
        mockClock.Setup(x => x.GetUtcNow()).Returns(Now);
        device = NewDevice();
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var ring = new E2eKeyRing(device.DerivedKeys, E2eKeyScope.Account);

        // Act & Assert
        Assert.Equal("userKeysStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(null!, device.LocalKeys, device.DerivedKeys, ring, argon2, keyWrap, options, mockClock.Object)).ParamName);
        Assert.Equal("localKeyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, null!, device.DerivedKeys, ring, argon2, keyWrap, options, mockClock.Object)).ParamName);
        Assert.Equal("derivedKeyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, null!, ring, argon2, keyWrap, options, mockClock.Object)).ParamName);
        Assert.Equal("keyRing", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, device.DerivedKeys, null!, argon2, keyWrap, options, mockClock.Object)).ParamName);
        Assert.Equal("argon2", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, device.DerivedKeys, ring, null!, keyWrap, options, mockClock.Object)).ParamName);
        Assert.Equal("keyWrapService", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, device.DerivedKeys, ring, argon2, null!, options, mockClock.Object)).ParamName);
        Assert.Equal("argon2Options", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, device.DerivedKeys, ring, argon2, keyWrap, null!, mockClock.Object)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new E2eKeyManager(userKeys, device.LocalKeys, device.DerivedKeys, ring, argon2, keyWrap, options, null!)).ParamName);
    }

    [Fact]
    public async Task GivenKeysStoreUnusable_WhenInitializeAsync_ThenUnsupportedAndPlaintext()
    {
        // Arrange
        userKeys.Failure = new InvalidOperationException("placeholder sign in");

        // Act
        var status = await device.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.Unsupported, status);
        Assert.Equal(E2eWriteMode.Plaintext, device.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenNoKeysAndPasskeyKeys_WhenInitializeAsync_ThenDisabled()
    {
        // Act & Assert
        Assert.Equal(E2eStatus.Disabled, await device.Manager.InitializeAsync());
        Assert.True(device.Manager.HasKeyEncryptionKey);
    }

    [Fact]
    public async Task GivenNoKeysAndNoPasskeyKeys_WhenInitializeAsync_ThenUnavailable()
    {
        // Arrange
        var other = NewDevice(withKek: false);

        // Act & Assert
        Assert.Equal(E2eStatus.Unavailable, await other.Manager.InitializeAsync());
        Assert.False(other.Manager.HasKeyEncryptionKey);
    }

    [Fact]
    public async Task GivenOfflineAndNoLocalCopy_WhenInitializeAsync_ThenUnknownAndPlaintext()
    {
        // Arrange
        userKeys.Failure = new HttpRequestException();

        // Act
        var status = await device.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.Unknown, status);
        Assert.Equal(E2eWriteMode.Plaintext, device.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenPasskeyKeys_WhenEnableAsync_ThenKeysDocumentHasTheSpecifiedShape()
    {
        // Act
        await device.Manager.EnableAsync(Passphrase);

        // Assert
        var json = JsonNode.Parse(userKeys.Value!)!;
        Assert.Equal(1, json["version"]!.GetValue<int>());
        var kdf = json["kdf"]!;
        Assert.Equal("argon2id", kdf["alg"]!.GetValue<string>());
        Assert.Equal(16, Convert.FromBase64String(kdf["salt"]!.GetValue<string>()).Length);
        Assert.Equal(64, kdf["memory_kib"]!.GetValue<int>());
        Assert.Equal(1, kdf["iterations"]!.GetValue<int>());
        Assert.Equal(1, kdf["parallelism"]!.GetValue<int>());
        Assert.Equal(32, kdf["key_len"]!.GetValue<int>());
        Assert.Equal("aes-256-gcm", json["master_key"]!["alg"]!.GetValue<string>());
        Assert.Equal(12, Convert.FromBase64String(json["master_key"]!["nonce"]!.GetValue<string>()).Length);
        Assert.Equal(48, Convert.FromBase64String(json["master_key"]!["wrapped"]!.GetValue<string>()).Length);
        var (keyId, entry) = Assert.Single(json["keys"]!.AsObject());
        Assert.StartsWith(E2eKeyManager.KeyIdPrefix, keyId);
        Assert.Equal(18, keyId.Length);
        Assert.Equal("active", entry!["status"]!.GetValue<string>());
        Assert.Equal(Now.UtcDateTime, entry["created"]!.GetValue<DateTime>());
        Assert.NotEqual(json["master_key"]!["nonce"]!.GetValue<string>(), entry["nonce"]!.GetValue<string>());
    }

    [Fact]
    public async Task GivenPasskeyKeys_WhenEnableAsync_ThenUnlockedWithDeviceCopyAndKeysInMemoryOnly()
    {
        // Act
        await device.Manager.EnableAsync(Passphrase);

        // Assert
        Assert.Equal(E2eStatus.Unlocked, device.Manager.Status);
        Assert.Equal(E2eWriteMode.Encrypt, device.Ring.WriteMode);
        var keyId = device.Ring.ActiveKeyId!;
        Assert.True(device.Ring.TryGetFileKey(keyId, out var fileKey));
        Assert.Equal(32, fileKey.Length);
        var local = device.LocalKeys.Saved!;
        Assert.Equal(userKeys.Value, local.CachedDocument);
        Assert.True(device.Ring.TryGetMasterKey(out var master));
        Assert.Equal(master, keyWrap.Unwrap(device.Kek!, local.WrappedMasterKey));
        Assert.DoesNotContain(Convert.ToBase64String(master), userKeys.Value);
        Assert.DoesNotContain(Convert.ToBase64String(fileKey), userKeys.Value);
        Assert.Equal(1, argon2.Count);
    }

    [Fact]
    public async Task GivenPasskeyKeys_WhenEnableAsync_ThenReportsStepsInOrder()
    {
        // Arrange
        var reported = new List<E2eProgress>();

        // Act
        await device.Manager.EnableAsync(Passphrase, new SyncProgress(reported));

        // Assert
        Assert.Equal(
            [E2eStep.DeriveKey, E2eStep.DeriveKey, E2eStep.CreateKeys, E2eStep.CreateKeys, E2eStep.SaveKeys, E2eStep.SaveKeys, E2eStep.StoreDeviceKey, E2eStep.StoreDeviceKey],
            reported.Select(p => p.Step));
        Assert.Equal([false, true, false, true, false, true, false, true], reported.Select(p => p.Completed));
    }

    [Fact]
    public async Task GivenNoPasskeyKeys_WhenEnableAsync_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var other = NewDevice(withKek: false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => other.Manager.EnableAsync(Passphrase));
        Assert.Null(userKeys.Value);
    }

    [Fact]
    public async Task GivenKeysCreatedMeanwhile_WhenEnableAsync_ThenThrowsAlreadyEnabledAndLeavesThem()
    {
        // Arrange
        userKeys.Value = "existing";

        // Act & Assert
        await Assert.ThrowsAsync<E2eAlreadyEnabledException>(() => device.Manager.EnableAsync(Passphrase));
        Assert.Equal("existing", userKeys.Value);
        Assert.Equal(E2eStatus.NeedsSetup, device.Manager.Status);
        Assert.Equal(E2eWriteMode.Refuse, device.Ring.WriteMode);
        Assert.Null(device.LocalKeys.Saved);
    }

    [Fact]
    public async Task GivenEnabledDevice_WhenInitializeAsyncAfterReload_ThenUnlocksWithoutPassphrase()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var reloaded = device.Reload();
        argon2.Count = 0;

        // Act
        var status = await reloaded.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.Unlocked, status);
        Assert.Equal(0, argon2.Count);
        Assert.Equal(device.Ring.ActiveKeyId, reloaded.Ring.ActiveKeyId);
        Assert.True(reloaded.Ring.TryGetFileKey(reloaded.Ring.ActiveKeyId!, out _));
    }

    [Fact]
    public async Task GivenEnabledDeviceOffline_WhenInitializeAsync_ThenUnlocksFromLocalCopy()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var reloaded = device.Reload();
        userKeys.Failure = new HttpRequestException();

        // Act & Assert
        Assert.Equal(E2eStatus.Unlocked, await reloaded.Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenEnabledAndNoPasskeyKeys_WhenInitializeAsync_ThenBlockedAndRefusingWrites()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var reloaded = device.Reload(withKek: false);

        // Act
        var status = await reloaded.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.Blocked, status);
        Assert.Equal(E2eWriteMode.Refuse, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenEnabledElsewhere_WhenInitializeAsyncOnNewDevice_ThenNeedsSetup()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var other = NewDevice();

        // Act
        var status = await other.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.NeedsSetup, status);
        Assert.Equal(E2eWriteMode.Refuse, other.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenLocalCopyWrappedByAnotherPasskey_WhenInitializeAsync_ThenNeedsSetup()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var reloaded = device.Reload();
        reloaded.DerivedKeys.SetKey(E2eKeyNames.KeyEncryptionKey, RandomNumberGenerator.GetBytes(32));

        // Act & Assert
        Assert.Equal(E2eStatus.NeedsSetup, await reloaded.Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenKeysDeletedElsewhere_WhenInitializeAsync_ThenDisabledElsewhereAndRefusingWrites()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var reloaded = device.Reload();
        userKeys.Value = null;

        // Act
        var status = await reloaded.Manager.InitializeAsync();

        // Assert
        Assert.Equal(E2eStatus.DisabledElsewhere, status);
        Assert.Equal(E2eWriteMode.Refuse, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenEnabledElsewhere_WhenSetUpDeviceAsync_ThenUnlockedWithItsOwnDeviceCopy()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var other = NewDevice();
        await other.Manager.InitializeAsync();

        // Act
        await other.Manager.SetUpDeviceAsync(Passphrase);

        // Assert
        Assert.Equal(E2eStatus.Unlocked, other.Manager.Status);
        Assert.Equal(device.Ring.ActiveKeyId, other.Ring.ActiveKeyId);
        device.Ring.TryGetFileKey(device.Ring.ActiveKeyId!, out var expected);
        other.Ring.TryGetFileKey(other.Ring.ActiveKeyId!, out var actual);
        Assert.Equal(expected, actual);
        Assert.Equal(other.Ring.TryGetMasterKey(out var master) ? master : null, keyWrap.Unwrap(other.Kek!, other.LocalKeys.Saved!.WrappedMasterKey));
        Assert.Equal(E2eStatus.Unlocked, await other.Reload().Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenWrongPassphrase_WhenSetUpDeviceAsync_ThenThrowsAndCanTryAgain()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var other = NewDevice();
        await other.Manager.InitializeAsync();

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.SetUpDeviceAsync("wrong passphrase"));
        Assert.Equal(E2eStatus.NeedsSetup, other.Manager.Status);
        Assert.Null(other.LocalKeys.Saved);
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => other.Manager.SetUpDeviceAsync("still wrong"));
        await other.Manager.SetUpDeviceAsync(Passphrase);
        Assert.Equal(E2eStatus.Unlocked, other.Manager.Status);
    }

    [Fact]
    public async Task GivenDocumentFromOtherBuildSettings_WhenSetUpDeviceAsync_ThenUsesTheDocumentKdf()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        options.Parameters = new Argon2idParameters { MemoryKib = 128, Iterations = 2, Parallelism = 1, KeyLength = 32 };
        var other = NewDevice();

        // Act
        await other.Manager.SetUpDeviceAsync(Passphrase);

        // Assert
        Assert.Equal(64, argon2.LastParameters!.MemoryKib);
        Assert.Equal(1, argon2.LastParameters.Iterations);
    }

    [Fact]
    public async Task GivenNoKeys_WhenSetUpDeviceAsync_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.Manager.SetUpDeviceAsync(Passphrase));
    }

    [Fact]
    public async Task GivenFileKeyEntriesSwapped_WhenUnlocking_ThenNotUnlocked()
    {
        // Arrange: a second entry with the first entry's wrapped key, under another id.
        await device.Manager.EnableAsync(Passphrase);
        var document = JsonNode.Parse(userKeys.Value!)!.AsObject();
        var keys = document["keys"]!.AsObject();
        var first = keys.First();
        var copy = first.Value!.DeepClone();
        copy["status"] = "retired";
        keys.Add("k_SWAPPED00000000", copy);
        userKeys.Value = document.ToJsonString();
        var other = NewDevice();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => other.Manager.SetUpDeviceAsync(Passphrase));
        Assert.Equal(E2eStatus.NeedsSetup, await device.Reload().Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenEnabled_WhenChangePassphraseAsync_ThenOnlyNewPassphraseWorksAndDevicesKeepWorking()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var before = JsonNode.Parse(userKeys.Value!)!;

        // Act
        await device.Manager.ChangePassphraseAsync(Passphrase, "a whole new passphrase");

        // Assert
        var after = JsonNode.Parse(userKeys.Value!)!;
        Assert.NotEqual(before["kdf"]!["salt"]!.GetValue<string>(), after["kdf"]!["salt"]!.GetValue<string>());
        Assert.Equal(before["keys"]!.ToJsonString(), after["keys"]!.ToJsonString());
        Assert.Equal(userKeys.Value, device.LocalKeys.Saved!.CachedDocument);
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => device.Manager.VerifyPassphraseAsync(Passphrase));
        await device.Manager.VerifyPassphraseAsync("a whole new passphrase");
        Assert.Equal(E2eStatus.Unlocked, await device.Reload().Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenWrongCurrentPassphrase_WhenChangePassphraseAsync_ThenThrowsAndNothingIsSaved()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var before = userKeys.Value;

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => device.Manager.ChangePassphraseAsync("wrong", "new passphrase"));
        Assert.Equal(before, userKeys.Value);
        Assert.Equal(0, userKeys.SetCount);
    }

    [Fact]
    public async Task GivenSaveFails_WhenChangePassphraseAsync_ThenThrows()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var store = new FailingSetUserKeysStore(userKeys.Value!);
        var manager = new E2eKeyManager(store, device.LocalKeys, device.DerivedKeys, device.Ring, argon2, keyWrap, options, mockClock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => manager.ChangePassphraseAsync(Passphrase, "new passphrase"));
    }

    [Fact]
    public async Task GivenEnabled_WhenRemoveAsync_ThenEverythingIsForgotten()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var keyId = device.Ring.ActiveKeyId!;

        // Act
        await device.Manager.RemoveAsync();

        // Assert
        Assert.Null(userKeys.Value);
        Assert.Null(device.LocalKeys.Saved);
        Assert.False(device.Ring.TryGetFileKey(keyId, out _));
        Assert.Equal(E2eWriteMode.Plaintext, device.Ring.WriteMode);
        Assert.Equal(E2eStatus.Disabled, device.Manager.Status);
        Assert.True(device.DerivedKeys.TryGetKey(E2eKeyNames.KeyEncryptionKey, out _));
    }

    [Fact]
    public async Task GivenKeysDeletedElsewhere_WhenUnlockLocalCopyAsync_ThenKeysHeldWritingPlaintext()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        var keyId = device.Ring.ActiveKeyId!;
        var reloaded = device.Reload();
        userKeys.Value = null;
        await reloaded.Manager.InitializeAsync();

        // Act
        await reloaded.Manager.UnlockLocalCopyAsync();

        // Assert
        Assert.True(reloaded.Ring.TryGetFileKey(keyId, out _));
        Assert.Equal(E2eWriteMode.Plaintext, reloaded.Ring.WriteMode);
        Assert.Equal(E2eStatus.DisabledElsewhere, reloaded.Manager.Status);
    }

    [Fact]
    public async Task GivenNoLocalCopy_WhenUnlockLocalCopyAsync_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => device.Manager.UnlockLocalCopyAsync());
    }

    [Fact]
    public async Task GivenPasskeyRegisteredAgain_WhenUnlockLocalCopyAsyncWithoutPassphrase_ThenThrowsPassphraseRequiredAndKeepsRefusing()
    {
        // Arrange
        var reloaded = await DisableElsewhereAndReloadWithOtherPasskeyAsync();

        // Act & Assert
        await Assert.ThrowsAsync<E2ePassphraseRequiredException>(() => reloaded.Manager.UnlockLocalCopyAsync());
        Assert.Equal(E2eWriteMode.Refuse, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenPasskeyRegisteredAgain_WhenUnlockLocalCopyAsyncWithPassphrase_ThenKeysHeldWritingPlaintext()
    {
        // Arrange
        var reloaded = await DisableElsewhereAndReloadWithOtherPasskeyAsync();

        // Act
        await reloaded.Manager.UnlockLocalCopyAsync(Passphrase);

        // Assert
        Assert.NotNull(reloaded.Ring.ActiveKeyId);
        Assert.Equal(E2eWriteMode.Plaintext, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenNoPasskeyKeys_WhenUnlockLocalCopyAsyncWithPassphrase_ThenKeysHeldWritingPlaintext()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        userKeys.Value = null;
        var reloaded = device.Reload(withKek: false);
        await reloaded.Manager.InitializeAsync();

        // Act
        await reloaded.Manager.UnlockLocalCopyAsync(Passphrase);

        // Assert
        Assert.NotNull(reloaded.Ring.ActiveKeyId);
        Assert.Equal(E2eWriteMode.Plaintext, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenPasskeyRegisteredAgain_WhenUnlockLocalCopyAsyncWithWrongPassphrase_ThenThrowsWrongPassphrase()
    {
        // Arrange
        var reloaded = await DisableElsewhereAndReloadWithOtherPasskeyAsync();

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => reloaded.Manager.UnlockLocalCopyAsync("not the passphrase"));
        Assert.Equal(E2eWriteMode.Refuse, reloaded.Ring.WriteMode);
    }

    [Fact]
    public async Task GivenSamePasskey_WhenCanUnlockLocalCopyWithPasskeyAsync_ThenTrueWithoutUnlocking()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        userKeys.Value = null;
        var reloaded = device.Reload();
        await reloaded.Manager.InitializeAsync();

        // Act & Assert
        Assert.True(await reloaded.Manager.CanUnlockLocalCopyWithPasskeyAsync());
        Assert.Null(reloaded.Ring.ActiveKeyId);
    }

    [Fact]
    public async Task GivenPasskeyRegisteredAgain_WhenCanUnlockLocalCopyWithPasskeyAsync_ThenFalse()
    {
        // Act
        var reloaded = await DisableElsewhereAndReloadWithOtherPasskeyAsync();

        // Assert
        Assert.False(await reloaded.Manager.CanUnlockLocalCopyWithPasskeyAsync());
    }

    [Fact]
    public async Task GivenNoLocalCopy_WhenCanUnlockLocalCopyWithPasskeyAsync_ThenFalse()
    {
        // Act & Assert
        Assert.False(await device.Manager.CanUnlockLocalCopyWithPasskeyAsync());
    }

    [Fact]
    public async Task GivenKeysDeletedElsewhere_WhenForgetDeviceAsync_ThenLocalKeysGoneAndDisabled()
    {
        // Arrange
        await device.Manager.EnableAsync(Passphrase);
        userKeys.Value = null;

        // Act
        await device.Manager.ForgetDeviceAsync();

        // Assert
        Assert.Null(device.LocalKeys.Saved);
        Assert.Equal(E2eStatus.Disabled, device.Manager.Status);
        Assert.Equal(0, userKeys.DeleteCount);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"version":2}""")]
    [InlineData("""{"version":1,"kdf":{"alg":"scrypt"}}""")]
    [InlineData("""{"version":1,"kdf":{"alg":"argon2id"},"keys":{}}""")]
    public async Task GivenInvalidDocument_WhenInitializeAsync_ThenThrowsInvalidDataException(string json)
    {
        // Arrange
        userKeys.Value = json;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => device.Manager.InitializeAsync());
    }

    [Fact]
    public async Task GivenStatusChanges_WhenInitializeAsync_ThenRaisesStatusChangedOnce()
    {
        // Arrange
        var raised = new List<E2eStatus>();
        device.Manager.StatusChanged += (_, s) => raised.Add(s);

        // Act
        await device.Manager.InitializeAsync();
        await device.Manager.InitializeAsync();

        // Assert
        Assert.Equal([E2eStatus.Disabled], raised);
    }

    [Fact]
    public async Task GivenEnabled_WhenIsEnabledAsync_ThenTrue()
    {
        // Act & Assert
        Assert.False(await device.Manager.IsEnabledAsync());
        await device.Manager.EnableAsync(Passphrase);
        Assert.True(await device.Manager.IsEnabledAsync());
    }

    // Encryption on with this device set up, then turned off elsewhere, and the passkey registered again here: the new
    // passkey's key cannot unwrap this device's master key.
    private async Task<Device> DisableElsewhereAndReloadWithOtherPasskeyAsync()
    {
        await device.Manager.EnableAsync(Passphrase);
        userKeys.Value = null;
        var reloaded = device.Reload(withKek: false);
        reloaded.DerivedKeys.SetKey(E2eKeyNames.KeyEncryptionKey, RandomNumberGenerator.GetBytes(32));
        Assert.Equal(E2eStatus.DisabledElsewhere, await reloaded.Manager.InitializeAsync());
        return reloaded;
    }

    private Device NewDevice(bool withKek = true)
    {
        var derived = new InMemoryDerivedKeyStore();
        byte[]? kek = null;
        if (withKek)
        {
            kek = RandomNumberGenerator.GetBytes(32);
            derived.SetKey(E2eKeyNames.KeyEncryptionKey, kek);
        }

        return new Device(this, new FakeE2eLocalKeyStore(), derived, kek);
    }

    /// <summary>
    /// One device: its local storage and its passkey (PRF) key encryption key.
    /// </summary>
    private sealed class Device
    {
        private readonly E2eKeyManagerTests owner;

        public Device(E2eKeyManagerTests owner, FakeE2eLocalKeyStore localKeys, InMemoryDerivedKeyStore derivedKeys, byte[]? kek)
        {
            this.owner = owner;
            LocalKeys = localKeys;
            DerivedKeys = derivedKeys;
            Kek = kek;
            Ring = new E2eKeyRing(derivedKeys, E2eKeyScope.Account);
            Manager = new E2eKeyManager(owner.userKeys, localKeys, derivedKeys, Ring, owner.argon2, owner.keyWrap, owner.options, owner.mockClock.Object);
        }

        public FakeE2eLocalKeyStore LocalKeys { get; }

        public InMemoryDerivedKeyStore DerivedKeys { get; }

        public byte[]? Kek { get; }

        public E2eKeyRing Ring { get; }

        public E2eKeyManager Manager { get; }

        // The same device after a reload: same local storage and passkey, but nothing in memory.
        public Device Reload(bool withKek = true)
        {
            var derived = new InMemoryDerivedKeyStore();
            if (withKek && Kek != null)
            {
                derived.SetKey(E2eKeyNames.KeyEncryptionKey, Kek);
            }

            return new Device(owner, LocalKeys, derived, withKek ? Kek : null);
        }
    }

    private sealed class CountingArgon2 : IArgon2idKeyDerivationService
    {
        private readonly BouncyCastleArgon2idKeyDerivationService inner = new();

        public int Count { get; set; }

        public Argon2idParameters? LastParameters { get; private set; }

        public Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, Argon2idParameters parameters, CancellationToken ct = default)
        {
            Count++;
            LastParameters = parameters;
            return inner.DeriveKeyAsync(passphrase, salt, parameters, ct);
        }
    }

    private sealed class SyncProgress(List<E2eProgress> reported) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => reported.Add(value);
    }

    private sealed class FailingSetUserKeysStore(string value) : IUserKeysStore
    {
        public Task<string?> GetAsync(E2eKeyScope scope, CancellationToken ct = default) => Task.FromResult<string?>(value);

        public Task SetAsync(E2eKeyScope scope, string value, CancellationToken ct = default) => throw new HttpRequestException();

        public Task<bool> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default) => throw new HttpRequestException();

        public Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default) => throw new HttpRequestException();
    }
}
