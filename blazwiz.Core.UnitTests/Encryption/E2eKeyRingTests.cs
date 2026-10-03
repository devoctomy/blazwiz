using blazwiz.Core.Cryptography;
using blazwiz.Core.Encryption;
using blazwiz.Core.Encryption.Models;

namespace blazwiz.Core.UnitTests.Encryption;

public class E2eKeyRingTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly E2eKeyRing sut;

    public E2eKeyRingTests()
    {
        sut = new E2eKeyRing(keyStore);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyRing(null!)).ParamName);
    }

    [Fact]
    public void GivenNewRing_WhenGettingState_ThenPlaintextWithNoKeys()
    {
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
        Assert.True(keyStore.TryGetKey(E2eKeyNames.FileKeyPrefix + "k_2", out var second));
        Assert.Equal(new byte[] { 3 }, second);
        Assert.True(keyStore.TryGetKey(E2eKeyNames.MasterKey, out _));
    }

    [Fact]
    public void GivenActiveKeyNotInKeys_WhenLoad_ThenThrowsArgumentException()
    {
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
        Assert.False(keyStore.TryGetKey(E2eKeyNames.FileKeyPrefix + "k_1", out _));
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
        Assert.False(keyStore.TryGetKey(E2eKeyNames.MasterKey, out _));
        Assert.True(keyStore.TryGetKey(E2eKeyNames.KeyEncryptionKey, out _));
    }

    [Fact]
    public void GivenEncryptMode_WhenClear_ThenThrowsArgumentException()
    {
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
