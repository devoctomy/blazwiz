using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eKeyRingTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly E2eKeyScope scope = new("s1");
    private readonly E2eKeyRing sut;

    public E2eKeyRingTests()
    {
        sut = new E2eKeyRing(keyStore, scope);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyRing(null!, scope)).ParamName);
        Assert.Equal("scope", Assert.Throws<ArgumentNullException>(() => new E2eKeyRing(keyStore, null!)).ParamName);
    }

    [Fact]
    public void GivenNewRing_WhenGettingState_ThenLockedWithNoKeys()
    {
        // Act & Assert
        Assert.Same(scope, sut.Scope);
        Assert.Null(sut.ActiveKeyId);
        Assert.False(sut.TryGetMasterKey(out _));
        Assert.False(sut.TryGetFileKey("k_1", out _));
    }

    [Fact]
    public void GivenKeys_WhenLoad_ThenHeldInDerivedKeyStoreUnderTheScope()
    {
        // Act
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2], ["k_2"] = [3] }, "k_2");

        // Assert
        Assert.Equal("k_2", sut.ActiveKeyId);
        Assert.True(sut.TryGetMasterKey(out var master));
        Assert.Equal(new byte[] { 1 }, master);
        Assert.True(sut.TryGetFileKey("k_1", out var first));
        Assert.Equal(new byte[] { 2 }, first);
        Assert.True(keyStore.TryGetKey("e2e:s1:file:k_2", out var second));
        Assert.Equal(new byte[] { 3 }, second);
        Assert.True(keyStore.TryGetKey("e2e:s1:master", out _));
    }

    [Fact]
    public void GivenActiveKeyNotInKeys_WhenLoad_ThenThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_9"));
    }

    [Fact]
    public void GivenLoadedKeys_WhenLoadAgain_ThenOldFileKeysAreRemoved()
    {
        // Arrange
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act
        sut.Load([4], new Dictionary<string, byte[]> { ["k_2"] = [5] }, "k_2");

        // Assert
        Assert.False(sut.TryGetFileKey("k_1", out _));
        Assert.False(keyStore.TryGetKey("e2e:s1:file:k_1", out _));
    }

    [Fact]
    public void GivenLoadedKeys_WhenClear_ThenLockedAndKeysRemovedFromStoreButOtherKeysKept()
    {
        // Arrange
        keyStore.SetKey(E2eKeyNames.KeyEncryptionKey, [9]);
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act
        sut.Clear();

        // Assert
        Assert.Null(sut.ActiveKeyId);
        Assert.False(sut.TryGetFileKey("k_1", out _));
        Assert.False(sut.TryGetMasterKey(out _));
        Assert.False(keyStore.TryGetKey("e2e:s1:master", out _));
        Assert.True(keyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out _));
    }

    [Fact]
    public void GivenInvalidArguments_WhenUsed_ThenThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Load(null!, new Dictionary<string, byte[]>(), "k_1"));
        Assert.Throws<ArgumentNullException>(() => sut.Load([1], null!, "k_1"));
        Assert.ThrowsAny<ArgumentException>(() => sut.TryGetFileKey(" ", out _));
    }
}
