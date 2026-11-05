using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.S3;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

public class SyncProfileRegistryTests
{
    private readonly SyncOptions options = new();
    private readonly E2eKeyRings keyRings = new(new InMemoryDerivedKeyStore());
    private readonly ServiceProvider provider;

    public SyncProfileRegistryTests()
    {
        options.Profiles.Add(Profile("notes"));
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(Mock.Of<ISyncUserContext>());
        services.AddSingleton(Mock.Of<ISyncScheduler>());
        services.AddSingleton(Mock.Of<IS3SyncTargetProvider>());
        services.AddSingleton<IEncryptedFileFormat>(new EncryptedFileFormat(new BouncyCastleAesGcmAeadCipher()));
        services.AddSingleton<IE2eKeyRings>(keyRings);
        provider = services.BuildServiceProvider();
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("services", Assert.Throws<ArgumentNullException>(() => new SyncProfileRegistry(null!, options)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new SyncProfileRegistry(provider, null!)).ParamName);
    }

    [Fact]
    public void GivenConfiguredProfiles_WhenRead_ThenTheyAreListed()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.Equal(["notes"], sut.Profiles.Select(p => p.Name));
        Assert.Same(options.Profiles[0], sut.Find("notes"));
        Assert.Null(sut.Find("photos"));
    }

    [Fact]
    public void GivenANewProfile_WhenAdded_ThenItIsListedAfterTheConfiguredOnesAndAnnounced()
    {
        // Arrange
        var sut = CreateSut();
        var added = new List<string>();
        sut.ProfileAdded += (_, name) => added.Add(name);

        // Act
        sut.Add(Profile("photos"));

        // Assert
        Assert.Equal(["notes", "photos"], sut.Profiles.Select(p => p.Name));
        Assert.Equal(["photos"], added);
    }

    [Fact]
    public void GivenAnExistingName_WhenAdded_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();
        sut.Add(Profile("photos"));

        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.Add(Profile("notes")));
        Assert.Throws<ArgumentException>(() => sut.Add(Profile("photos")));
    }

    [Fact]
    public void GivenAnIncompleteProfile_WhenAdded_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Add(null!));
        Assert.Throws<ArgumentException>(() => sut.Add(new SyncProfileOptions { RemotePattern = "x/**" }));
        Assert.Throws<ArgumentException>(() => sut.Add(new SyncProfileOptions { Name = "x" }));
    }

    [Fact]
    public void GivenAnAddedProfile_WhenRemoved_ThenItIsGoneAndAnnounced()
    {
        // Arrange
        var sut = CreateSut();
        sut.Add(Profile("photos"));
        var removed = new List<string>();
        sut.ProfileRemoved += (_, name) => removed.Add(name);

        // Act
        var result = sut.Remove("photos");

        // Assert
        Assert.True(result);
        Assert.Equal(["notes"], sut.Profiles.Select(p => p.Name));
        Assert.Equal(["photos"], removed);
        Assert.False(sut.Remove("photos"));
        Assert.Throws<ArgumentException>(() => sut.GetFileStore("photos"));
    }

    [Fact]
    public void GivenAConfiguredProfile_WhenRemoved_ThenThrows()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sut.Remove("notes"));
    }

    [Fact]
    public void GivenAProfile_WhenItsStoresAreAskedForTwice_ThenTheSameOnesAreReturned()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var engine = sut.GetSyncProfile("notes");

        // Assert
        Assert.Same(engine, sut.GetSyncProfile("notes"));
        Assert.Same(sut.GetUntrackedFileStore("notes"), engine.FileStore);
        Assert.Same(sut.GetIndexStore("notes"), engine.IndexStore);
        Assert.Same(sut.GetMatcher("notes"), engine.Matcher);
        Assert.Same(sut.GetFileStore("notes"), sut.GetFileStore("notes"));
        Assert.IsType<S3SyncRemoteStore>(engine.RemoteStore);
        Assert.Throws<ArgumentException>(() => sut.GetSyncProfile("unknown"));
    }

    [Fact]
    public async Task GivenAProfile_WhenAdded_ThenItsStoreUsesItsOwnKeysAndRefusesWritesWhileLocked()
    {
        // Arrange
        var sut = CreateSut();
        var profile = Profile("photos");
        var scope = profile.KeyScope!;
        var name = profile.Name;

        // Act
        sut.Add(profile);

        // Assert
        Assert.IsType<EncryptingSyncFileStore>(sut.GetFileStore(name));
        await Assert.ThrowsAsync<E2eLockedException>(() => sut.GetFileStore(name).WriteAsync("a.txt", [1]));
        Assert.Null(keyRings.Get(scope).ActiveKeyId);
    }

    private static SyncProfileOptions Profile(string name) => new()
    {
        Name = name,
        RemotePattern = name + "/**/*",
        KeyScope = new E2eKeyScope(Guid.NewGuid().ToString("D")),
        IndexStoreFactory = _ => new InMemorySyncIndexStore(),
        FileStoreFactory = sp => new InMemorySyncFileStore(sp.GetRequiredService<ISyncUserContext>(), TimeProvider.System),
    };

    private SyncProfileRegistry CreateSut() => new(provider, options);
}
