using System.Text;
using blazwiz.Core.Cryptography;
using blazwiz.Core.Cryptography.Configuration;
using blazwiz.Core.Cryptography.Interfaces;
using Moq;

namespace blazwiz.Core.UnitTests.Cryptography;

public class PrfKeyDerivatorTests
{
    private const string ApplicationName = "testapp";
    private const string ApplicationId = "6f0c6b1e-4b5a-4d8e-9a43-2c1d3e5f7a90";

    private readonly BouncyCastleHkdfKeyDerivationService keyDerivationService = new();
    private readonly InMemoryDerivedKeyStore keyStore = new();

    [Fact]
    public void GivenNullOptions_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PrfKeyDerivator(null!, keyDerivationService, keyStore));
        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void GivenNullKeyDerivationService_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PrfKeyDerivator(new PrfKeyDerivationOptions(), null!, keyStore));
        Assert.Equal("keyDerivationService", exception.ParamName);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PrfKeyDerivator(new PrfKeyDerivationOptions(), keyDerivationService, null!));
        Assert.Equal("keyStore", exception.ParamName);
    }

    [Fact]
    public void GivenConfiguredKeys_WhenDeriveKeys_ThenEachKeyIsStoredWithItsLength()
    {
        // Arrange
        var sut = CreateSut(b => b.AddKey("apple").AddKey("orange", 16));

        // Act
        sut.DeriveKeys(new byte[32]);

        // Assert
        Assert.True(keyStore.TryGetKey("apple", out var apple));
        Assert.True(keyStore.TryGetKey("orange", out var orange));
        Assert.Equal(32, apple.Length);
        Assert.Equal(16, orange.Length);
        Assert.NotEqual(apple.Take(16), orange);
    }

    [Fact]
    public void GivenConfiguredKey_WhenDeriveKeys_ThenKeyIsHkdfOfPrfWithApplicationIdSaltAndNamedInfo()
    {
        // Arrange
        var prfOutput = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var sut = CreateSut(b => b.AddKey("apple"));

        // Act
        sut.DeriveKeys(prfOutput);

        // Assert
        var expected = keyDerivationService.DeriveKey(
            prfOutput,
            Encoding.UTF8.GetBytes(ApplicationId),
            Encoding.UTF8.GetBytes($"{ApplicationName}:apple"),
            32);
        keyStore.TryGetKey("apple", out var apple);
        Assert.Equal(expected, apple);
    }

    [Fact]
    public void GivenSamePrfOutput_WhenDeriveKeysTwice_ThenKeysAreTheSame()
    {
        // Arrange
        var sut = CreateSut(b => b.AddKey("apple"));
        sut.DeriveKeys(new byte[32]);
        keyStore.TryGetKey("apple", out var first);

        // Act
        sut.DeriveKeys(new byte[32]);

        // Assert
        keyStore.TryGetKey("apple", out var second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GivenDifferentPrfOutput_WhenDeriveKeys_ThenKeysDiffer()
    {
        // Arrange
        var sut = CreateSut(b => b.AddKey("apple"));
        sut.DeriveKeys(new byte[32]);
        keyStore.TryGetKey("apple", out var first);

        // Act
        sut.DeriveKeys(Enumerable.Repeat((byte)1, 32).ToArray());

        // Assert
        keyStore.TryGetKey("apple", out var second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GivenDifferentApplicationId_WhenDeriveKeys_ThenKeysDiffer()
    {
        // Arrange
        CreateSut(b => b.AddKey("apple")).DeriveKeys(new byte[32]);
        keyStore.TryGetKey("apple", out var first);
        var otherStore = new InMemoryDerivedKeyStore();
        var otherOptions = new PrfKeyDerivationOptions();
        new PrfKeyDerivationBuilder(otherOptions).UseApplication(ApplicationName, Guid.NewGuid().ToString()).AddKey("apple");

        // Act
        new PrfKeyDerivator(otherOptions, keyDerivationService, otherStore).DeriveKeys(new byte[32]);

        // Assert
        otherStore.TryGetKey("apple", out var second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GivenDifferentApplicationName_WhenDeriveKeys_ThenKeysDiffer()
    {
        // Arrange
        CreateSut(b => b.AddKey("apple")).DeriveKeys(new byte[32]);
        keyStore.TryGetKey("apple", out var first);
        var otherStore = new InMemoryDerivedKeyStore();
        var otherOptions = new PrfKeyDerivationOptions();
        new PrfKeyDerivationBuilder(otherOptions).UseApplication("otherapp", ApplicationId).AddKey("apple");

        // Act
        new PrfKeyDerivator(otherOptions, keyDerivationService, otherStore).DeriveKeys(new byte[32]);

        // Assert
        otherStore.TryGetKey("apple", out var second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void GivenPrfOutput_WhenDeriveKeys_ThenCallersPrfOutputIsLeftAlone()
    {
        // Arrange
        var prfOutput = Enumerable.Repeat((byte)7, 32).ToArray();
        var sut = CreateSut(b => b.AddKey("apple"));

        // Act
        sut.DeriveKeys(prfOutput);

        // Assert
        Assert.All(prfOutput, b => Assert.Equal(7, b));
    }

    [Fact]
    public void GivenDerivedKey_WhenDeriveKeys_ThenDerivedArrayIsWipedAfterStoring()
    {
        // Arrange
        var derived = new byte[] { 1, 2, 3 };
        var mockKdf = new Mock<IKeyDerivationService>();
        mockKdf.Setup(x => x.DeriveKey(It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<byte[]>(), It.IsAny<int>())).Returns(derived);
        var options = new PrfKeyDerivationOptions();
        new PrfKeyDerivationBuilder(options).UseApplication(ApplicationName, ApplicationId).AddKey("apple", 3);
        var sut = new PrfKeyDerivator(options, mockKdf.Object, keyStore);

        // Act
        sut.DeriveKeys(new byte[32]);

        // Assert
        Assert.Equal(new byte[3], derived);
        keyStore.TryGetKey("apple", out var stored);
        Assert.Equal([1, 2, 3], stored);
    }

    [Fact]
    public void GivenNullPrfOutput_WhenDeriveKeys_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut(b => b.AddKey("apple")).DeriveKeys(null!));
    }

    [Fact]
    public void GivenEmptyPrfOutput_WhenDeriveKeys_ThenThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CreateSut(b => b.AddKey("apple")).DeriveKeys([]));
    }

    [Fact]
    public void GivenNoApplication_WhenDeriveKeys_ThenThrowsInvalidOperationException()
    {
        // Arrange
        var options = new PrfKeyDerivationOptions();
        new PrfKeyDerivationBuilder(options).AddKey("apple");
        var sut = new PrfKeyDerivator(options, keyDerivationService, keyStore);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sut.DeriveKeys(new byte[32]));
        Assert.False(keyStore.TryGetKey("apple", out _));
    }

    private PrfKeyDerivator CreateSut(Action<PrfKeyDerivationBuilder> addKeys)
    {
        var options = new PrfKeyDerivationOptions();
        var builder = new PrfKeyDerivationBuilder(options).UseApplication(ApplicationName, ApplicationId);
        addKeys(builder);
        return new PrfKeyDerivator(options, keyDerivationService, keyStore);
    }
}
