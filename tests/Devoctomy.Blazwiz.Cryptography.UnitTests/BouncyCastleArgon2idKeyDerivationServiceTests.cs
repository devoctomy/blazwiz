using System.Text;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Models;

namespace Devoctomy.Blazwiz.Cryptography.UnitTests;

public class BouncyCastleArgon2idKeyDerivationServiceTests
{
    private static readonly Argon2idParameters Small = new() { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 };

    private readonly BouncyCastleArgon2idKeyDerivationService sut = new();

    [Fact]
    public async Task GivenReferenceVector_WhenDeriveKeyAsync_ThenMatchesArgon2Reference()
    {
        // Arrange: the Argon2id (version 1.3) vector from the reference implementation's tests.
        var parameters = new Argon2idParameters { MemoryKib = 65536, Iterations = 2, Parallelism = 1, KeyLength = 32 };

        // Act
        var key = await sut.DeriveKeyAsync("password", Encoding.UTF8.GetBytes("somesalt"), parameters);

        // Assert
        Assert.Equal("09316115d5cf24ed5a15a31a3ba326e5cf32edc24702987c02b6566f61913cf7", Convert.ToHexStringLower(key));
    }

    [Fact]
    public async Task GivenSameInputs_WhenDeriveKeyAsyncTwice_ThenSameKey()
    {
        // Arrange
        var salt = new byte[16];

        // Act & Assert
        Assert.Equal(await sut.DeriveKeyAsync("apple", salt, Small), await sut.DeriveKeyAsync("apple", salt, Small));
    }

    [Fact]
    public async Task GivenDifferentPassphraseOrSalt_WhenDeriveKeyAsync_ThenDifferentKeys()
    {
        // Arrange
        var salt = new byte[16];
        var otherSalt = new byte[16];
        otherSalt[0] = 1;

        // Act
        var key = await sut.DeriveKeyAsync("apple", salt, Small);

        // Assert
        Assert.NotEqual(key, await sut.DeriveKeyAsync("orange", salt, Small));
        Assert.NotEqual(key, await sut.DeriveKeyAsync("apple", otherSalt, Small));
    }

    [Fact]
    public async Task GivenKeyLength_WhenDeriveKeyAsync_ThenKeyHasThatLength()
    {
        // Arrange
        var parameters = Small.Clone();

        // Act
        parameters.KeyLength = 48;

        // Assert
        Assert.Equal(48, (await sut.DeriveKeyAsync("apple", new byte[16], parameters)).Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task GivenNoPassphrase_WhenDeriveKeyAsync_ThenThrowsArgumentException(string? passphrase)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.DeriveKeyAsync(passphrase!, new byte[16], Small));
    }

    [Fact]
    public async Task GivenShortSalt_WhenDeriveKeyAsync_ThenThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => sut.DeriveKeyAsync("apple", new byte[7], Small));
    }

    [Fact]
    public async Task GivenInvalidParameters_WhenDeriveKeyAsync_ThenThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.DeriveKeyAsync("apple", new byte[16], new Argon2idParameters { MemoryKib = 64, Iterations = 0, Parallelism = 1, KeyLength = 32 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.DeriveKeyAsync("apple", new byte[16], new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 0, KeyLength = 32 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.DeriveKeyAsync("apple", new byte[16], new Argon2idParameters { MemoryKib = 4, Iterations = 1, Parallelism = 1, KeyLength = 32 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.DeriveKeyAsync("apple", new byte[16], new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 8 }));
    }

    [Fact]
    public async Task GivenCancelledToken_WhenDeriveKeyAsync_ThenThrowsOperationCanceledException()
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.DeriveKeyAsync("apple", new byte[16], Small, new CancellationToken(true)));
    }

    [Fact]
    public void GivenDefaultOptions_WhenGettingParameters_ThenBuildDefaults()
    {
        // Act
        var parameters = new Argon2idOptions().Parameters;

        // Assert
#if DEBUG
        Assert.Equal(Argon2idOptions.DebugMemoryKib, parameters.MemoryKib);
#else
        Assert.Equal(Argon2idOptions.ReleaseMemoryKib, parameters.MemoryKib);
#endif
        Assert.Equal(2, parameters.Iterations);
        Assert.Equal(1, parameters.Parallelism);
        Assert.Equal(32, parameters.KeyLength);
    }
}
