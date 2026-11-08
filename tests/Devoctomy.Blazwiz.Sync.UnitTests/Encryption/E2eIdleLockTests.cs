using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class E2eIdleLockTests
{
    private readonly Mock<IE2eProtectedProfileManager> mockManager = new();
    private readonly SyncOptions options = new();
    private readonly FakeSyncProfileRegistry registry;
    private readonly FakeTimeProvider time = new();
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));
    private readonly HashSet<E2eKeyScope> unlocked = [];

    public E2eIdleLockTests()
    {
        options.Profiles.Add(new SyncProfileOptions
        {
            Name = scope.Id,
            RemotePattern = "profiles/x/**/*",
            KeyScope = scope,
            Settings = new E2eProfileSettings { IdleTimeout = TimeSpan.FromMinutes(5) },
        });
        registry = FakeSyncProfileRegistry.Of(options);
        mockManager.Setup(x => x.IsUnlocked(It.IsAny<E2eKeyScope>())).Returns<E2eKeyScope>(s => unlocked.Contains(s));
        mockManager.SetupGet(x => x.Unlocked).Returns(() => [.. unlocked]);
        mockManager.Setup(x => x.Lock(It.IsAny<E2eKeyScope>())).Callback<E2eKeyScope>(s =>
        {
            unlocked.Remove(s);
            mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, s);
        });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("manager", Assert.Throws<ArgumentNullException>(() => new E2eIdleLock(null!, registry, time)).ParamName);
        Assert.Equal("registry", Assert.Throws<ArgumentNullException>(() => new E2eIdleLock(mockManager.Object, null!, time)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new E2eIdleLock(mockManager.Object, registry, null!)).ParamName);
    }

    [Fact]
    public void GivenAnUnlockedProfile_WhenItsIdleTimeoutPasses_ThenItIsLocked()
    {
        // Arrange
        using var sut = Started();
        Unlock();

        // Act
        time.Advance(TimeSpan.FromMinutes(5));

        // Assert
        mockManager.Verify(x => x.Lock(scope), Times.Once);
    }

    [Fact]
    public void GivenAnUnlockedProfile_WhenItsFilesAreUsed_ThenTheTimerRestarts()
    {
        // Arrange
        using var sut = Started();
        Unlock();

        // Act
        time.Advance(TimeSpan.FromMinutes(4));
        sut.Touch(scope);
        time.Advance(TimeSpan.FromMinutes(4));

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
        time.Advance(TimeSpan.FromMinutes(1));
        mockManager.Verify(x => x.Lock(scope), Times.Once);
    }

    [Fact]
    public void GivenAProfileLockedByHand_WhenItsTimeoutWouldHavePassed_ThenNothingHappens()
    {
        // Arrange
        using var sut = Started();
        Unlock();
        unlocked.Remove(scope);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);

        // Act
        time.Advance(TimeSpan.FromMinutes(10));

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public void GivenNoIdleTimeout_WhenUnlocked_ThenItStaysUnlocked()
    {
        // Arrange
        options.Profiles[0].Settings.IdleTimeout = null;
        using var sut = Started();
        Unlock();

        // Act
        time.Advance(TimeSpan.FromDays(1));

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public void GivenANewProfileUnlockedBeforeItIsAdded_WhenAdded_ThenItIsTimed()
    {
        // Arrange
        using var sut = Started();
        var created = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        unlocked.Add(created);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, created);

        // Act
        registry.Add(new SyncProfileOptions { Name = created.Id, RemotePattern = "profiles/y/**/*", KeyScope = created, Settings = new E2eProfileSettings { IdleTimeout = TimeSpan.FromMinutes(1) } });
        time.Advance(TimeSpan.FromMinutes(1));

        // Assert
        mockManager.Verify(x => x.Lock(created), Times.Once);
    }

    [Fact]
    public void GivenAProfileSetUpInCode_WhoseNameIsNotItsScopeId_WhenUnlocked_ThenItIsTimed()
    {
        // Arrange: e.g. the template's "files" profile, whose key scope is a GUID.
        var files = new E2eKeyScope(Guid.NewGuid().ToString("D"));
        options.Profiles.Add(new SyncProfileOptions
        {
            Name = "files",
            RemotePattern = "files/**/*",
            KeyScope = files,
            Settings = new E2eProfileSettings { IdleTimeout = TimeSpan.FromMinutes(3) },
        });
        using var sut = Started();
        unlocked.Add(files);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, files);

        // Act
        time.Advance(TimeSpan.FromMinutes(3));

        // Assert
        mockManager.Verify(x => x.Lock(files), Times.Once);
    }

    [Fact]
    public void GivenAProfileAlreadyUnlocked_WhenStarted_ThenItIsTimed()
    {
        // Arrange
        unlocked.Add(scope);
        using var sut = Started();

        // Act
        time.Advance(TimeSpan.FromMinutes(5));

        // Assert
        mockManager.Verify(x => x.Lock(scope), Times.Once);
    }

    [Fact]
    public void GivenAnUnlockedProfile_WhenItsTimeoutIsChangedAndRefreshed_ThenTheNewTimeoutCountsFromNow()
    {
        // Arrange
        using var sut = Started();
        Unlock();
        time.Advance(TimeSpan.FromMinutes(4));

        // Act
        registry.Find(scope.Id)!.Settings = new E2eProfileSettings { IdleTimeout = TimeSpan.FromMinutes(2) };
        sut.Refresh(scope);

        // Assert
        time.Advance(TimeSpan.FromMinutes(1));
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
        time.Advance(TimeSpan.FromMinutes(1));
        mockManager.Verify(x => x.Lock(scope), Times.Once);
    }

    [Fact]
    public void GivenAnUnlockedProfile_WhenItsTimeoutIsRemovedAndRefreshed_ThenItStaysUnlocked()
    {
        // Arrange
        using var sut = Started();
        Unlock();

        // Act
        registry.Find(scope.Id)!.Settings = new E2eProfileSettings { IdleTimeout = null };
        sut.Refresh(scope);
        time.Advance(TimeSpan.FromHours(1));

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
    }

    [Fact]
    public void GivenALockedProfile_WhenRefreshed_ThenItIsNotTimed()
    {
        // Arrange
        using var sut = Started();

        // Act
        sut.Refresh(scope);
        time.Advance(TimeSpan.FromHours(1));

        // Assert
        mockManager.Verify(x => x.Lock(It.IsAny<E2eKeyScope>()), Times.Never);
        Assert.Throws<ArgumentNullException>(() => sut.Refresh(null!));
    }

    private E2eIdleLock Started()
    {
        var sut = new E2eIdleLock(mockManager.Object, registry, time);
        sut.Start();
        sut.Start();
        return sut;
    }

    private void Unlock()
    {
        unlocked.Add(scope);
        mockManager.Raise(x => x.LockStateChanged += null, mockManager.Object, scope);
    }
}
