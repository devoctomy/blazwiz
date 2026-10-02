using System.Globalization;
using System.Text;
using blazwiz.Core.IntTests.Support;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.IntTests.StepDefinitions;

[Binding]
public sealed class SyncStepDefinitions(TestContext testContext)
{
    private static readonly DateTimeOffset Today = DateTimeOffset.UtcNow.Date;

    [Given("two devices signed in as the same user")]
    public void GivenTwoDevicesSignedInAsTheSameUser()
    {
        if (testContext.S3Client == null)
        {
            return;
        }

        var target = new S3SyncTarget(testContext.S3Client, testContext.AwsBucketName!, testContext.RootPrefix!);
        testContext.Devices["A"] = new SimulatedDevice(target);
        testContext.Devices["B"] = new SimulatedDevice(target);
    }

    [Given("device {string} has a file {string} containing {string}")]
    public Task GivenDeviceHasAFileContaining(string device, string key, string content)
    {
        return Device(device).TrackedFiles.WriteAsync(key, Encoding.UTF8.GetBytes(content));
    }

    [Given("device {string} has an empty folder {string}")]
    public Task GivenDeviceHasAnEmptyFolder(string device, string key)
    {
        return Device(device).TrackedFiles.CreateFolderAsync(key);
    }

    [Given("device {string} has a binary file {string} of {int} bytes")]
    public Task GivenDeviceHasABinaryFile(string device, string key, int size)
    {
        var bytes = new byte[size];
        new Random(42).NextBytes(bytes);
        return Device(device).TrackedFiles.WriteAsync(key, bytes);
    }

    [Given("both devices have synced")]
    public async Task GivenBothDevicesHaveSynced()
    {
        await SyncAsync("A");
        await SyncAsync("B");
    }

    [Given("device {string} has synced")]
    [When("device {string} syncs")]
    public Task WhenDeviceSyncs(string device)
    {
        return SyncAsync(device);
    }

    [When("device {string} changes {string} to {string}")]
    public Task WhenDeviceChanges(string device, string key, string content)
    {
        return Device(device).TrackedFiles.WriteAsync(key, Encoding.UTF8.GetBytes(content));
    }

    [When("device {string} changes {string} to {string} at {word}")]
    public Task WhenDeviceChangesAt(string device, string key, string content, string time)
    {
        return Device(device).TrackedFiles.WriteAsync(key, Encoding.UTF8.GetBytes(content), At(time));
    }

    [When("device {string} deletes {string}")]
    public Task WhenDeviceDeletes(string device, string key)
    {
        return Device(device).TrackedFiles.DeleteAsync(key);
    }

    [When("device {string} deletes {string} at {word}")]
    public async Task WhenDeviceDeletesAt(string device, string key, string time)
    {
        var simulated = Device(device);
        await simulated.TrackedFiles.DeleteAsync(key);
        var record = (await simulated.Index.GetAsync(SimulatedDevice.Profile, SimulatedDevice.Owner, key))!;
        record.DeletedAt = At(time);
        await simulated.Index.SaveAsync(record);
    }

    [When("device {string} renames {string} to {string}")]
    public async Task WhenDeviceRenames(string device, string from, string to)
    {
        // S3 has no rename: write the new key, then delete the old one.
        var files = Device(device).TrackedFiles;
        var file = (await files.ReadAsync(from))!;
        await files.WriteAsync(to, file.Content, file.ModifiedAt);
        await files.DeleteAsync(from);
    }

    [When("device {string} loses its sync index")]
    public void WhenDeviceLosesItsSyncIndex(string device)
    {
        Device(device).Index.Clear();
    }

    [Then("device {string} has a file {string} containing {string}")]
    public async Task ThenDeviceHasAFileContaining(string device, string key, string content)
    {
        var file = await Device(device).Files.ReadAsync(key);
        Assert.NotNull(file);
        Assert.Equal(content, Encoding.UTF8.GetString(file.Content));
    }

    [Then("device {string} has no file {string}")]
    public async Task ThenDeviceHasNoFile(string device, string key)
    {
        Assert.Null(await Device(device).Files.ReadAsync(key));
    }

    [Then("device {string} has an empty folder {string}")]
    public async Task ThenDeviceHasAnEmptyFolder(string device, string key)
    {
        var folder = await Device(device).Files.ReadAsync(key);
        Assert.NotNull(folder);
        Assert.Empty(folder.Content);
    }

    [Then("device {string} has the same bytes for {string} as device {string}")]
    public async Task ThenDeviceHasTheSameBytes(string device, string key, string other)
    {
        var mine = await Device(device).Files.ReadAsync(key);
        var theirs = await Device(other).Files.ReadAsync(key);
        Assert.Equal(theirs!.Content, mine!.Content);
    }

    [Then("the bucket has no object {string}")]
    public async Task ThenTheBucketHasNoObject(string key)
    {
        var listing = await testContext.S3Client!.ListObjectsV2Async(new Amazon.S3.Model.ListObjectsV2Request
        {
            BucketName = testContext.AwsBucketName,
            Prefix = testContext.RootPrefix + key,
        });
        Assert.DoesNotContain(listing.S3Objects ?? [], o => o.Key == testContext.RootPrefix + key);
    }

    [Then("the last sync of device {string} resolved {int} conflict")]
    public void ThenTheLastSyncResolvedConflicts(string device, int count)
    {
        Assert.Equal(count, testContext.LastResults[device].ConflictsResolved);
    }

    [Then("the last sync of device {string} uploaded {int} files and adopted {int}")]
    public void ThenTheLastSyncUploadedAndAdopted(string device, int uploaded, int adopted)
    {
        var result = testContext.LastResults[device];
        Assert.Equal(uploaded, result.Uploaded);
        Assert.Equal(adopted, result.Adopted);
    }

    private static DateTimeOffset At(string time)
    {
        return Today.Add(TimeSpan.ParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture));
    }

    private async Task SyncAsync(string device)
    {
        var result = await Device(device).SyncAsync();
        Assert.Empty(result.Errors);
        testContext.LastResults[device] = result;
    }

    private SimulatedDevice Device(string name) => testContext.Devices[name];
}
