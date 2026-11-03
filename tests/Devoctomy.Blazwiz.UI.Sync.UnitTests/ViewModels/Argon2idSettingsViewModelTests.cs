using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class Argon2idSettingsViewModelTests
{
    private readonly TestText testText = new();

    private readonly Mock<IArgon2idKeyDerivationService> mockArgon2 = new();
    private readonly Argon2idOptions options = new() { Parameters = new Argon2idParameters { MemoryKib = 512 * 1024, Iterations = 2, Parallelism = 1, KeyLength = 32 } };
    private readonly FakeTimeProvider time = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("argon2", Assert.Throws<ArgumentNullException>(() => new Argon2idSettingsViewModel(null!, options, time, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new Argon2idSettingsViewModel(mockArgon2.Object, null!, time, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new Argon2idSettingsViewModel(mockArgon2.Object, options, null!, testText.Sync, testText.Errors)).ParamName);
    }

    [Fact]
    public void GivenOptions_WhenGettingSettings_ThenShownFromOptions()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        Assert.Equal("512 MiB", sut.MemoryText);
        Assert.Same(options.Parameters, sut.Parameters);
        options.Parameters.MemoryKib = 1000;
        Assert.Equal("1000 KiB", sut.MemoryText);
    }

    [Fact]
    public async Task GivenBenchmark_WhenBenchmarkAsync_ThenDerivesWithTheSettingsAndShowsTime()
    {
        // Arrange
        Argon2idParameters? used = null;
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], Argon2idParameters, CancellationToken>((_, salt, p, _) =>
            {
                Assert.Equal(16, salt.Length);
                used = p;
                time.Advance(TimeSpan.FromSeconds(2.4));
            })
            .ReturnsAsync(new byte[32]);
        var sut = CreateSut();

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.Equal("Took 2.4 s", sut.Result);
        Assert.Equal(options.Parameters.MemoryKib, used!.MemoryKib);
        Assert.NotSame(options.Parameters, used);
        Assert.False(sut.IsRunning);
        Assert.Null(sut.Error);
    }

    [Fact]
    public async Task GivenDerivationFails_WhenBenchmarkAsync_ThenErrorShown()
    {
        // Arrange
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("no memory"));
        var sut = CreateSut();

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.Equal("The benchmark failed: Something went wrong.", sut.Error);
        Assert.Null(sut.Result);
        Assert.False(sut.IsRunning);
    }

    private Argon2idSettingsViewModel CreateSut() => new(mockArgon2.Object, options, time, testText.Sync, testText.Errors);
}
