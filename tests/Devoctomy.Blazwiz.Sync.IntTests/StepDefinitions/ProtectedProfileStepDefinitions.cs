using System.Text;
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
        Assert.DoesNotContain(profiles[profile].Id, await cloud.Index.GetIndexAsync() ?? string.Empty);
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

    private string Name(string profile) => profiles[profile].Id;

    private string Key(string profile, string file) => $"profiles/{profiles[profile].Id}/{file}";
}
