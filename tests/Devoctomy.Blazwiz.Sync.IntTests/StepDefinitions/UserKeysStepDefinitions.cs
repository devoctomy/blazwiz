using Devoctomy.Blazwiz.Core.Offline.Interfaces;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.Sync.S3;
using Devoctomy.Blazwiz.TestSupport.Fakes;
using Moq;

namespace Devoctomy.Blazwiz.Sync.IntTests.StepDefinitions;

/// <summary>
/// The user's keys documents and index in the real data bucket, through <see cref="S3UserKeysStore"/>, as every device
/// of the user reaches them.
/// </summary>
[Binding]
public sealed class UserKeysStepDefinitions(TestContext testContext)
{
    private static readonly Argon2idOptions LightArgon2 = new()
    {
        Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 },
    };

    private readonly Dictionary<string, E2eKeyScope> profiles = [];
    private readonly Dictionary<string, string> readETags = [];
    private readonly Dictionary<string, Exception?> refusals = [];
    private readonly List<E2eKeyScope> created = [];
    private S3UserKeysStore? store;
    private bool secondCreationRefused;

    private S3UserKeysStore Store => store ?? throw new InvalidOperationException("The keys store was not set up.");

    [Given("the user's keys kept in the data bucket")]
    public void GivenTheUsersKeysKeptInTheDataBucket()
    {
        if (testContext.S3Client == null)
        {
            // Not configured: the @aws hook skips the scenario.
            return;
        }

        var targets = new Mock<IS3SyncTargetProvider>();
        targets.Setup(x => x.IsConfigured).Returns(true);
        targets.Setup(x => x.GetTargetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new S3SyncTarget(testContext.S3Client, testContext.AwsBucketName!, testContext.RootPrefix!));
        store = new S3UserKeysStore(targets.Object, Mock.Of<IOfflineModeService>(), TimeProvider.System);
    }

    [When("keys {string} are created for profile {string}")]
    public async Task WhenKeysAreCreated(string value, string profile)
    {
        secondCreationRefused = await Store.TryCreateAsync(Scope(profile), value) == null;
    }

    [Given("keys {string} for profile {string}, read by devices {string} and {string}")]
    public async Task GivenKeysReadByTwoDevices(string value, string profile, string first, string second)
    {
        Assert.NotNull(await Store.TryCreateAsync(Scope(profile), value));
        foreach (var device in new[] { first, second })
        {
            readETags[device] = (await Store.GetAsync(Scope(profile)))!.ETag;
        }
    }

    [When("device {string} changes the keys of profile {string} to {string}")]
    public async Task WhenDeviceChangesTheKeys(string device, string profile, string value)
    {
        refusals[device] = await Record.ExceptionAsync(() => Store.SetAsync(Scope(profile), value, readETags[device]));
    }

    [When("the keys of profile {string} are deleted")]
    public Task WhenTheKeysAreDeleted(string profile) => Store.DeleteAsync(Scope(profile));

    [When("device {string} creates the index")]
    public async Task WhenDeviceCreatesTheIndex(string device)
    {
        refusals[device] = await Record.ExceptionAsync(() => Store.SetIndexAsync($"{{\"from\":\"{device}\"}}", null));
    }

    [When("devices {string} and {string} each create a protected profile at the same time")]
    public async Task WhenTwoDevicesEachCreateAProfile(string first, string second)
    {
        var managers = new[] { first, second }.Select(_ => CreateManager()).ToList();
        var scopes = await Task.WhenAll(managers.Select(m => m.CreateAsync("correct horse", new E2eProfileSettings())));
        created.AddRange(scopes);
    }

    [Then("the second creation is refused")]
    public void ThenTheSecondCreationIsRefused()
    {
        Assert.True(secondCreationRefused);
    }

    [Then("the keys of profile {string} are {string}")]
    public async Task ThenTheKeysAre(string profile, string value)
    {
        Assert.Equal(value, (await Store.GetAsync(Scope(profile)))?.Value);
    }

    [Then("device {string} is told the keys changed")]
    public void ThenDeviceIsToldTheKeysChanged(string device)
    {
        Assert.IsType<E2eDocumentChangedException>(refusals[device]);
    }

    [Then("profile {string} has no keys")]
    public async Task ThenProfileHasNoKeys(string profile)
    {
        Assert.Null(await Store.GetAsync(Scope(profile)));
    }

    [Then("device {string} changing the keys of profile {string} to {string} is told the keys changed")]
    public async Task ThenDeviceChangingTheKeysIsToldTheyChanged(string device, string profile, string value)
    {
        await Assert.ThrowsAsync<E2eDocumentChangedException>(() => Store.SetAsync(Scope(profile), value, readETags[device]));
    }

    [Then("the index lists both profiles")]
    public async Task ThenTheIndexListsBothProfiles()
    {
        var listed = (await CreateManager().GetProfilesAsync()).Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(2, created.Count);
        Assert.All(created, scope => Assert.Contains(scope.Id, listed));
    }

    private E2eKeyScope Scope(string profile)
    {
        if (!profiles.TryGetValue(profile, out var scope))
        {
            scope = new E2eKeyScope(Guid.NewGuid().ToString("D"));
            profiles[profile] = scope;
        }

        return scope;
    }

    private E2eProtectedProfileManager CreateManager()
    {
        var keyWrap = new AeadKeyWrapService(new BouncyCastleAesGcmAeadCipher());
        var documents = new E2eKeysDocumentService(new BouncyCastleArgon2idKeyDerivationService(), keyWrap, LightArgon2, TimeProvider.System);
        var derivedKeys = new InMemoryDerivedKeyStore();
        return new E2eProtectedProfileManager(
            Store,
            new FakeE2eLocalKeyStore(),
            Store,
            new FakeE2eLocalProfileIndexStore(),
            new E2eKeyRings(derivedKeys),
            documents,
            TimeProvider.System,
            derivedKeys,
            keyWrap);
    }
}
