using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class Argon2idBenchmarkViewModelTests
{
    private readonly TestText testText = new();

    private readonly Mock<IArgon2idKeyDerivationService> mockArgon2 = new();
    private readonly Argon2idOptions options = new() { Parameters = new Argon2idParameters { MemoryKib = 512 * 1024, Iterations = 2, Parallelism = 1, KeyLength = 32 } };
    private readonly FakeTimeProvider time = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("argon2", Assert.Throws<ArgumentNullException>(() => new Argon2idBenchmarkViewModel(null!, options, time, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new Argon2idBenchmarkViewModel(mockArgon2.Object, null!, time, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new Argon2idBenchmarkViewModel(mockArgon2.Object, options, null!, testText.Sync, testText.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new Argon2idBenchmarkViewModel(mockArgon2.Object, options, time, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new Argon2idBenchmarkViewModel(mockArgon2.Object, options, time, testText.Sync, null!)).ParamName);
    }

    [Fact]
    public async Task GivenABenchmarkRunning_WhenBenchmarkAsyncAgain_ThenOnlyOneKeyIsDerived()
    {
        // Arrange
        var release = new TaskCompletionSource<byte[]>();
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .Returns(release.Task);
        var sut = CreateSut();
        var running = sut.BenchmarkAsync();

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.True(sut.IsRunning);
        Assert.False(sut.CanBenchmark);
        release.SetResult(new byte[32]);
        await running;
        Assert.False(sut.IsRunning);
        Assert.NotNull(sut.Result);
        mockArgon2.Verify(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenAnEarlierFailure_WhenTheBenchmarkSucceeds_ThenTheErrorIsCleared()
    {
        // Arrange
        mockArgon2.SetupSequence(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OutOfMemoryException("no memory"))
            .ReturnsAsync(new byte[32]);
        var sut = CreateSut();
        await sut.BenchmarkAsync();
        var error = sut.Error;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.NotNull(error);
        Assert.Null(sut.Error);
        Assert.Equal(testText.Sync["Sync.Argon2id.Took", 0.0].Value, sut.Result);
    }

    [Fact]
    public void GivenOptions_WhenConstructing_ThenBenchmarkSettingsStartAtTheOptions()
    {
        // Arrange
        options.Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 3, Parallelism = 2, KeyLength = 48 };

        // Act
        var sut = CreateSut();

        // Assert
        Assert.Same(options.Parameters, sut.Parameters);
        Assert.Equal(0.0625, sut.MemoryMib);
        Assert.Equal(3, sut.Iterations);
        Assert.Equal(2, sut.Parallelism);
        Assert.Equal(48, sut.KeyLength);
        Assert.Null(sut.Invalid);
        Assert.True(sut.CanBenchmark);
    }

    [Fact]
    public void GivenTheViewModel_WhenGettingCommonMemorySizes_ThenListsThemInMib()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var sizes = sut.CommonMemoryMib;

        // Assert
        Assert.Equal([16, 19, 32, 64, 128, 256, 512], sizes);
    }

    [Fact]
    public async Task GivenChangedSettings_WhenBenchmarkAsync_ThenDerivesWithTheChangedSettingsAndLeavesTheOptions()
    {
        // Arrange
        Argon2idParameters? used = null;
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], Argon2idParameters, CancellationToken>((_, _, p, _) => used = p)
            .ReturnsAsync(new byte[64]);
        var sut = CreateSut();
        sut.MemoryMib = 64;
        sut.Iterations = 4;
        sut.Parallelism = 2;
        sut.KeyLength = 64;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.Equal(64 * 1024, used!.MemoryKib);
        Assert.Equal(4, used.Iterations);
        Assert.Equal(2, used.Parallelism);
        Assert.Equal(64, used.KeyLength);
        Assert.Equal(512 * 1024, options.Parameters.MemoryKib);
        Assert.Equal(2, options.Parameters.Iterations);
        Assert.Equal(1, options.Parameters.Parallelism);
        Assert.Equal(32, options.Parameters.KeyLength);
    }

    [Fact]
    public async Task GivenAFractionOfAMib_WhenBenchmarkAsync_ThenDerivesWithTheMemoryRoundedToKib()
    {
        // Arrange
        Argon2idParameters? used = null;
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], Argon2idParameters, CancellationToken>((_, _, p, _) => used = p)
            .ReturnsAsync(new byte[32]);
        var sut = CreateSut();
        sut.MemoryMib = 0.5;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.Equal(512, used!.MemoryKib);
    }

    [Theory]
    [InlineData(1, 0, 1, 32, "Sync.Argon2id.InvalidIterations")]
    [InlineData(1, 1, 0, 32, "Sync.Argon2id.InvalidParallelism")]
    [InlineData(0.005, 1, 1, 32, "Sync.Argon2id.InvalidMemory")]
    [InlineData(0.01, 1, 2, 32, "Sync.Argon2id.InvalidMemory")]
    [InlineData(1, 1, 1, 15, "Sync.Argon2id.InvalidKeyLength")]
    public async Task GivenInvalidSettings_WhenBenchmarkAsync_ThenSaysWhyAndDerivesNothing(double memoryMib, int iterations, int parallelism, int keyLength, string expectedKey)
    {
        // Arrange
        var sut = CreateSut();
        sut.MemoryMib = memoryMib;
        sut.Iterations = iterations;
        sut.Parallelism = parallelism;
        sut.KeyLength = keyLength;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.StartsWith(testText.Sync[expectedKey].Value.Split('{')[0], sut.Invalid);
        Assert.False(sut.CanBenchmark);
        Assert.Null(sut.Result);
        mockArgon2.Verify(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GivenTheMemoryMenuClosed_WhenToggled_ThenItOpensAndClosesAgain()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.ToggleMemoryMenu();
        var opened = sut.IsMemoryMenuOpen;
        sut.ToggleMemoryMenu();

        // Assert
        Assert.True(opened);
        Assert.False(sut.IsMemoryMenuOpen);
    }

    [Fact]
    public void GivenTheMemoryMenuOpen_WhenClosed_ThenItIsClosed()
    {
        // Arrange
        var sut = CreateSut();
        sut.ToggleMemoryMenu();

        // Act
        sut.CloseMemoryMenu();

        // Assert
        Assert.False(sut.IsMemoryMenuOpen);
    }

    [Fact]
    public void GivenTheMemoryMenuOpen_WhenChoosingASize_ThenTheMemoryIsSetAndTheMenuCloses()
    {
        // Arrange
        var sut = CreateSut();
        sut.ToggleMemoryMenu();

        // Act
        sut.ChooseMemory(256);

        // Assert
        Assert.Equal(256, sut.MemoryMib);
        Assert.False(sut.IsMemoryMenuOpen);
    }

    [Fact]
    public void GivenTwoLanes_WhenGettingInvalid_ThenSaysTheMinimumMemory()
    {
        // Arrange
        var sut = CreateSut();
        sut.Parallelism = 2;

        // Act
        sut.MemoryMib = 0.01;

        // Assert
        Assert.Equal("Memory must be at least 16 KiB (8 KiB for each lane).", sut.Invalid);
    }

    [Fact]
    public async Task GivenLargeMemory_WhenBenchmarkAsync_ThenDerivesWithIt()
    {
        // Arrange
        Argon2idParameters? used = null;
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], Argon2idParameters, CancellationToken>((_, _, p, _) => used = p)
            .ReturnsAsync(new byte[32]);
        var sut = CreateSut();
        sut.MemoryMib = 8192;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.Null(sut.Invalid);
        Assert.Equal(8192 * 1024, used!.MemoryKib);
        Assert.NotNull(sut.Result);
    }

    [Fact]
    public async Task GivenMemoryTooLargeForArgon2idParameters_WhenBenchmarkAsync_ThenErrorShownAndNothingDerived()
    {
        // Arrange
        var sut = CreateSut();
        sut.MemoryMib = 3_000_000;

        // Act
        await sut.BenchmarkAsync();

        // Assert
        Assert.NotNull(sut.Error);
        Assert.Null(sut.Result);
        Assert.False(sut.IsRunning);
        mockArgon2.Verify(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GivenASettingChanged_WhenChanging_ThenRaisesInvalidAndCanBenchmark()
    {
        // Arrange
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        // Act
        sut.Iterations = 0;

        // Assert
        Assert.Contains(nameof(sut.Iterations), raised);
        Assert.Contains(nameof(sut.Invalid), raised);
        Assert.Contains(nameof(sut.CanBenchmark), raised);
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

    private Argon2idBenchmarkViewModel CreateSut() => new(mockArgon2.Object, options, time, testText.Sync, testText.Errors);
}
