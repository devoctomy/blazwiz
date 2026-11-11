using System.Text;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.IntTests.Support;

namespace Devoctomy.Blazwiz.Sync.IntTests.StepDefinitions;

[Binding]
[Scope(Feature = "Protected profiles")]
public sealed class ProtectedProfileStepDefinitions
{
    private readonly SharedCloud cloud = new();
    private readonly Dictionary<string, ProtectedDevice> devices = [];
    private readonly Dictionary<string, E2eKeyScope> profiles = [];

    [Given("two devices with protected profiles, signed in as the same user")]
    public void GivenTwoDevicesWithProtectedProfiles()
    {
        devices["A"] = new ProtectedDevice(cloud);
        devices["B"] = new ProtectedDevice(cloud);
    }

    [Given("device {string} creates a protected profile {string} with the passphrase {string}")]
    public async Task GivenDeviceCreatesAProtectedProfile(string device, string profile, string passphrase)
    {
        profiles[profile] = await devices[device].Profiles.CreateAsync(passphrase);
    }

    [Given("device {string} writes {string} containing {string} in {string}")]
    [When("device {string} writes {string} containing {string} in {string}")]
    public Task WhenDeviceWrites(string device, string file, string content, string profile)
    {
        return devices[device].Registry.GetFileStore(Name(profile)).WriteAsync(Key(profile, file), Encoding.UTF8.GetBytes(content));
    }

    [Given("device {string} syncs")]
    [When("device {string} syncs")]
    public Task WhenDeviceSyncs(string device)
    {
        return devices[device].SyncAsync();
    }

    [Given("device {string} loads its protected profiles")]
    [When("device {string} loads its protected profiles")]
    public Task WhenDeviceLoadsItsProtectedProfiles(string device)
    {
        return devices[device].Profiles.LoadAsync();
    }

    [Given("device {string} unlocks {string} with the passphrase {string}")]
    [When("device {string} unlocks {string} with the passphrase {string}")]
    public Task WhenDeviceUnlocks(string device, string profile, string passphrase)
    {
        return devices[device].Manager.UnlockAsync(profiles[profile], passphrase);
    }

    [When("device {string} locks {string}")]
    public void WhenDeviceLocks(string device, string profile)
    {
        devices[device].Manager.Lock(profiles[profile]);
    }

    [When("device {string} deletes {string}")]
    public Task WhenDeviceDeletes(string device, string profile)
    {
        return devices[device].Profiles.DeleteAsync(profiles[profile]);
    }

    [When("device {string} changes the passphrase of {string} from {string} to {string}")]
    public Task WhenDeviceChangesThePassphrase(string device, string profile, string current, string replacement)
    {
        return devices[device].Profiles.ChangePassphraseAsync(profiles[profile], current, replacement);
    }

    [When("device {string} changes the passphrase of {string} from {string} to {string} and encrypts its files again")]
    public Task WhenDeviceChangesThePassphraseAndEncryptsItsFilesAgain(string device, string profile, string current, string replacement)
    {
        return devices[device].Rotation.RotateAsync(profiles[profile], current, replacement);
    }

    [When("device {string} finishes the passphrase change of {string}")]
    public Task WhenDeviceFinishesThePassphraseChange(string device, string profile)
    {
        return devices[device].Rotation.FinishAsync(profiles[profile]);
    }

    [When("device {string} completes its passphrase changes")]
    public Task WhenDeviceCompletesItsPassphraseChanges(string device)
    {
        // What signing out everywhere does, once every session is revoked.
        return devices[device].Rotation.CompleteAsync();
    }

    [When("the stored {string} in {string} is copied on device {string} to {string} in {string}")]
    public async Task WhenTheStoredFileIsCopied(string file, string profile, string device, string toFile, string toProfile)
    {
        var stored = devices[device].StoredFiles;
        var content = (await stored[Name(profile)].ReadAsync(Key(profile, file)))!.Content;
        await stored[Name(toProfile)].WriteAsync(Key(toProfile, toFile), content);
    }

    [Then("device {string} reads {string} in {string} as {string}")]
    public async Task ThenDeviceReads(string device, string file, string profile, string expected)
    {
        var read = await devices[device].Registry.GetFileStore(Name(profile)).ReadAsync(Key(profile, file));
        Assert.NotNull(read);
        Assert.Equal(expected, Encoding.UTF8.GetString(read.Content));
    }

    [Then("device {string} cannot read {string} in {string} while it is locked")]
    public async Task ThenDeviceCannotReadWhileLocked(string device, string file, string profile)
    {
        Assert.False(devices[device].Manager.IsUnlocked(profiles[profile]));
        await Assert.ThrowsAsync<E2eLockedException>(() => devices[device].Registry.GetFileStore(Name(profile)).ReadAsync(Key(profile, file)));
    }

    [Then("device {string} stores {string} in {string} encrypted")]
    public async Task ThenDeviceStoresEncrypted(string device, string file, string profile)
    {
        var stored = await devices[device].StoredFiles[Name(profile)].ReadAsync(Key(profile, file));
        Assert.NotNull(stored);
        Assert.Equal("E2EE", Encoding.ASCII.GetString(stored.Content, 0, 4));
    }

    [Then("device {string} cannot unlock {string} with the passphrase {string}")]
    public async Task ThenDeviceCannotUnlock(string device, string profile, string passphrase)
    {
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => devices[device].Manager.UnlockAsync(profiles[profile], passphrase));
        Assert.False(devices[device].Manager.IsUnlocked(profiles[profile]));
    }

    [Then("device {string} cannot unlock {string} with the passphrase {string} because it does not exist")]
    public async Task ThenDeviceCannotUnlockBecauseItDoesNotExist(string device, string profile, string passphrase)
    {
        await Assert.ThrowsAsync<E2eProfileNotFoundException>(() => devices[device].Manager.UnlockAsync(profiles[profile], passphrase));
    }

    [Then("device {string} cannot decrypt {string} in {string}")]
    public async Task ThenDeviceCannotDecrypt(string device, string file, string profile)
    {
        Assert.True(devices[device].Manager.IsUnlocked(profiles[profile]));
        var store = devices[device].Registry.GetFileStore(Name(profile));
        Assert.NotNull(await Record.ExceptionAsync(() => store.ReadAsync(Key(profile, file))));
    }

    [Then("the bucket has no files of {string}")]
    public void ThenTheBucketHasNoFilesOf(string profile)
    {
        Assert.DoesNotContain(cloud.Objects.Keys, k => k.StartsWith(Key(profile, string.Empty), StringComparison.Ordinal));
    }

    [Then("the user's keys of {string} are gone")]
    public async Task ThenTheUsersKeysAreGone(string profile)
    {
        Assert.False(cloud.Keys.Values.ContainsKey(profiles[profile].Id));
        Assert.DoesNotContain(profiles[profile].Id, (await cloud.Index.GetIndexAsync())?.Value ?? string.Empty);
    }

    [Then("device {string} has no keys or files of {string}")]
    public async Task ThenDeviceHasNoKeysOrFiles(string device, string profile)
    {
        var scope = profiles[profile];
        Assert.Null(devices[device].LocalKeys.Get(scope));
        Assert.False(devices[device].Manager.IsUnlocked(scope));
        Assert.Null(devices[device].Rings.Get(scope).ActiveKeyId);
        Assert.Null(devices[device].Registry.Find(scope.Id));
        Assert.Empty(await devices[device].StoredFiles[scope.Id].ListAsync());
    }

    [Then("every file of {string} in the bucket is encrypted with the active key of device {string}")]
    public void ThenEveryFileInTheBucketIsEncryptedWithTheActiveKey(string profile, string device)
    {
        var activeKeyId = devices[device].Rings.Get(profiles[profile]).ActiveKeyId;
        Assert.NotNull(activeKeyId);
        var files = cloud.Objects.Where(o => o.Key.StartsWith(Key(profile, string.Empty), StringComparison.Ordinal) && !o.Key.EndsWith('/')).ToList();
        Assert.NotEmpty(files);
        Assert.All(files, o =>
        {
            Assert.True(devices[device].Format.TryGetKeyId(o.Value.Content, out var keyId));
            Assert.Equal(activeKeyId, keyId);
        });
    }

    [Then("the old master key of {string} on device {string} unwraps none of its keys")]
    public void ThenTheOldMasterKeyUnwrapsNoneOfItsKeys(string profile, string device)
    {
        // The device was unlocked before the change, so it still holds the old master key, as anyone who had it would.
        Assert.True(devices[device].Rings.Get(profiles[profile]).TryGetMasterKey(out var oldMasterKey));
        var json = cloud.Keys.Values[profiles[profile].Id];
        var documents = new E2eKeysDocumentService(
            new Cryptography.BouncyCastleArgon2idKeyDerivationService(),
            new Cryptography.AeadKeyWrapService(new Cryptography.BouncyCastleAesGcmAeadCipher()),
            new Cryptography.Configuration.Argon2idOptions(),
            TimeProvider.System);
        var ring = new E2eKeyRing(new Cryptography.InMemoryDerivedKeyStore(), profiles[profile]);
        Assert.Throws<InvalidDataException>(() => documents.LoadFileKeys(ring, documents.Parse(json), oldMasterKey));
    }

    [Then("the passphrase change of {string} is finished")]
    public void ThenThePassphraseChangeIsFinished(string profile)
    {
        Assert.DoesNotContain("\"rotation_in_progress\":true", cloud.Keys.Values[profiles[profile].Id], StringComparison.Ordinal);
    }

    [Then("the passphrase change of {string} is unfinished on device {string}")]
    public void ThenThePassphraseChangeIsUnfinishedOnDevice(string profile, string device)
    {
        Assert.True(devices[device].Manager.IsRotationInProgress(profiles[profile]));
    }

    [Then("the passphrase change of {string} is not unfinished on device {string}")]
    public void ThenThePassphraseChangeIsNotUnfinishedOnDevice(string profile, string device)
    {
        Assert.False(devices[device].Manager.IsRotationInProgress(profiles[profile]));
    }

    private string Name(string profile) => profiles[profile].Id;

    private string Key(string profile, string file) => $"profiles/{profiles[profile].Id}/{file}";
}
