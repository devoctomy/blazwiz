using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eKeyRingTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly E2eKeyRing sut;

    public E2eKeyRingTests()
    {
        sut = new E2eKeyRing(keyStore, E2eKeyScope.Account);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyRing(null!, E2eKeyScope.Account)).ParamName);
        Assert.Equal("scope", Assert.Throws<ArgumentNullException>(() => new E2eKeyRing(keyStore, null!)).ParamName);
    }

    [Fact]
    public void GivenAScope_WhenConstructed_ThenTheRingIsForThatScope()
    {
        // Act & Assert
        Assert.Same(E2eKeyScope.Account, sut.Scope);
    }

    [Fact]
    public void GivenAnotherScope_WhenNew_ThenItRefusesWritesUntilUnlocked()
    {
        // Act
        var ring = new E2eKeyRing(keyStore, new E2eKeyScope("s1"));

        // Assert
        Assert.Equal(E2eWriteMode.Refuse, ring.WriteMode);
    }

    [Fact]
    public void GivenAnotherScope_WhenAskedToWritePlaintext_ThenThrows()
    {
        // Arrange
        var ring = new E2eKeyRing(keyStore, new E2eKeyScope("s1"));
        ring.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => ring.SetWriteMode(E2eWriteMode.Plaintext));
        Assert.Throws<InvalidOperationException>(() => ring.Clear(E2eWriteMode.Plaintext));
        Assert.Equal(E2eWriteMode.Encrypt, ring.WriteMode);
    }

    [Fact]
    public void GivenNewRing_WhenGettingState_ThenPlaintextWithNoKeys()
    {
        // Act & Assert
        Assert.Equal(E2eWriteMode.Plaintext, sut.WriteMode);
        Assert.Null(sut.ActiveKeyId);
        Assert.False(sut.TryGetMasterKey(out _));
        Assert.False(sut.TryGetFileKey("k_1", out _));
    }

    [Fact]
    public void GivenKeys_WhenLoad_ThenHeldInDerivedKeyStoreAndEncrypting()
    {
        // Act
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2], ["k_2"] = [3] }, "k_2");

        // Assert
        Assert.Equal(E2eWriteMode.Encrypt, sut.WriteMode);
        Assert.Equal("k_2", sut.ActiveKeyId);
        Assert.True(sut.TryGetMasterKey(out var master));
        Assert.Equal(new byte[] { 1 }, master);
        Assert.True(sut.TryGetFileKey("k_1", out var first));
        Assert.Equal(new byte[] { 2 }, first);
        Assert.True(keyStore.TryGetKey("e2e:account:file:k_2", out var second));
        Assert.Equal(new byte[] { 3 }, second);
        Assert.True(keyStore.TryGetKey("e2e:account:master", out _));
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
        Assert.False(keyStore.TryGetKey("e2e:account:file:k_1", out _));
    }

    [Fact]
    public void GivenLoadedKeys_WhenClear_ThenKeysRemovedFromStoreButOtherKeysKept()
    {
        // Arrange
        keyStore.SetKey(E2eKeyNames.KeyEncryptionKey, [9]);
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act
        sut.Clear(E2eWriteMode.Refuse);

        // Assert
        Assert.Equal(E2eWriteMode.Refuse, sut.WriteMode);
        Assert.Null(sut.ActiveKeyId);
        Assert.False(sut.TryGetFileKey("k_1", out _));
        Assert.False(keyStore.TryGetKey("e2e:account:master", out _));
        Assert.True(keyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out _));
    }

    [Fact]
    public void GivenEncryptMode_WhenClear_ThenThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => sut.Clear(E2eWriteMode.Encrypt));
    }

    [Fact]
    public void GivenLoadedKeys_WhenSetWriteModePlaintext_ThenKeysStillReadable()
    {
        // Arrange
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");

        // Act
        sut.SetWriteMode(E2eWriteMode.Plaintext);

        // Assert
        Assert.Equal(E2eWriteMode.Plaintext, sut.WriteMode);
        Assert.True(sut.TryGetFileKey("k_1", out _));
    }

    [Fact]
    public void GivenNoKeys_WhenSetWriteModeEncrypt_ThenThrowsInvalidOperationException()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sut.SetWriteMode(E2eWriteMode.Encrypt));
    }

    [Fact]
    public void GivenDerivedKeyStoreCleared_WhenTryGetFileKey_ThenFalse()
    {
        // Arrange: e.g. signed out, which clears every session key.
        sut.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");
        keyStore.Clear();

        // Act & Assert
        Assert.False(sut.TryGetFileKey("k_1", out _));
        Assert.False(sut.TryGetMasterKey(out _));
    }
}
