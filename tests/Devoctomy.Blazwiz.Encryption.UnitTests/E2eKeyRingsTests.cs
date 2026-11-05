using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eKeyRingsTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly E2eKeyRings sut;

    public E2eKeyRingsTests()
    {
        sut = new E2eKeyRings(keyStore);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new E2eKeyRings(null!)).ParamName);
    }

    [Fact]
    public void GivenNullScope_WhenGet_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Get(null!));
    }

    [Fact]
    public void GivenAScope_WhenGetTwice_ThenTheSameRingForThatScope()
    {
        // Act
        var first = sut.Get(new E2eKeyScope("s1"));
        var second = sut.Get(new E2eKeyScope("s1"));

        // Assert
        Assert.Same(first, second);
        Assert.Equal(new E2eKeyScope("s1"), first.Scope);
        Assert.Equal(E2eWriteMode.Plaintext, first.WriteMode);
    }

    [Fact]
    public void GivenTwoScopes_WhenOneIsLoadedAndCleared_ThenTheOtherKeepsItsKeys()
    {
        // Arrange
        var account = sut.Get(E2eKeyScope.Account);
        var other = sut.Get(new E2eKeyScope("s1"));
        account.Load([1], new Dictionary<string, byte[]> { ["k_1"] = [2] }, "k_1");
        other.Load([3], new Dictionary<string, byte[]> { ["k_1"] = [4] }, "k_1");

        // Act
        account.Clear(E2eWriteMode.Refuse);

        // Assert
        Assert.False(account.TryGetFileKey("k_1", out _));
        Assert.True(other.TryGetFileKey("k_1", out var key));
        Assert.Equal(new byte[] { 4 }, key);
        Assert.True(other.TryGetMasterKey(out var master));
        Assert.Equal(new byte[] { 3 }, master);
        Assert.Equal(E2eWriteMode.Encrypt, other.WriteMode);
        Assert.True(keyStore.TryGetKey("e2e:s1:file:k_1", out _));
        Assert.False(keyStore.TryGetKey("e2e:account:file:k_1", out _));
    }
}
