using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.ViewModels;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.ViewModels;

public class E2ePassphraseFormViewModelTests
{
    private const string Pass = "correct horse battery";

    private readonly TestText testText = new();
    private readonly Mock<ISyncProtectedProfileService> mockProfiles = new();
    private readonly Mock<IArgon2idKeyDerivationService> mockArgon2 = new();
    private readonly Argon2idBenchmarkViewModel benchmark;
    private readonly E2eKeyScope scope = new(Guid.NewGuid().ToString("D"));

    public E2ePassphraseFormViewModelTests()
    {
        var options = new Argon2idOptions { Parameters = new Argon2idParameters { MemoryKib = 64 * 1024, Iterations = 3, Parallelism = 1, KeyLength = 48 } };
        benchmark = new Argon2idBenchmarkViewModel(mockArgon2.Object, options, new FakeTimeProvider(), testText.Sync, testText.Errors);
        mockArgon2.Setup(x => x.DeriveKeyAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<Argon2idParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new byte[32]);
        mockProfiles.Setup(x => x.GetArgon2ParametersAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Argon2idParameters { MemoryKib = 19 * 1024, Iterations = 2, Parallelism = 1, KeyLength = 32 });
        mockProfiles.Setup(x => x.GetArgon2ParametersAsync(scope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Argon2idParameters { MemoryKib = 256 * 1024, Iterations = 9, Parallelism = 2, KeyLength = 32 });
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("profiles", Assert.Throws<ArgumentNullException>(() => new E2ePassphraseFormViewModel(null!, benchmark, testText.Sync)).ParamName);
        Assert.Equal("argon2", Assert.Throws<ArgumentNullException>(() => new E2ePassphraseFormViewModel(mockProfiles.Object, null!, testText.Sync)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new E2ePassphraseFormViewModel(mockProfiles.Object, benchmark, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAProfileAboutToBeCreated_WhenReset_ThenTheSettingsStartAtThoseOfNewProfilesWithTheKeyLengthFixed()
    {
        // Arrange
        using var sut = CreateSut();

        // Act
        await sut.ResetAsync();

        // Assert
        Assert.Same(benchmark, sut.Argon2);
        Assert.Equal(19, sut.Argon2.MemoryMib);
        Assert.Equal(2, sut.Argon2.Iterations);
        Assert.Equal(32, sut.Argon2.KeyLength);
        Assert.True(sut.Argon2.IsKeyLengthFixed);
        Assert.False(sut.AsksForCurrentPassphrase);
        Assert.Equal(E2ePassphraseFormTab.Passphrase, sut.ActiveTab);
    }

    [Fact]
    public async Task GivenAProfileWithEntriesAndTheAdvancedTabShown_WhenResetForIt_ThenEmptyOnTheFirstTabAtItsOwnSettings()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        EnterPassphrases(sut);
        sut.CurrentPassphrase = "old passphrase";
        sut.ShowTabCommand.Execute(E2ePassphraseFormTab.Advanced);
        sut.Argon2.Iterations = 50;
        var shown = sut.ActiveTab;

        // Act
        await sut.ResetAsync(scope, asksForCurrentPassphrase: true);

        // Assert
        Assert.Equal(E2ePassphraseFormTab.Advanced, shown);
        Assert.Equal(E2ePassphraseFormTab.Passphrase, sut.ActiveTab);
        Assert.True(sut.AsksForCurrentPassphrase);
        Assert.Empty(sut.CurrentPassphrase);
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.ConfirmPassphrase);
        Assert.False(sut.AcknowledgedNoRecovery);
        Assert.Equal(256, sut.Argon2.MemoryMib);
        Assert.Equal(9, sut.Argon2.Iterations);
        Assert.Equal(2, sut.Argon2.Parallelism);
        Assert.False(sut.Argon2.NeedsBenchmark);
    }

    [Theory]
    [InlineData("", "", true, "Enter your passphrase.")]
    [InlineData(Pass, "different", true, "The passphrases do not match.")]
    [InlineData(Pass, Pass, false, "Confirm you understand there is no passphrase recovery.")]
    public async Task GivenInvalidPassphraseFields_WhenValidated_ThenTheErrorOnTheFirstTab(string passphrase, string confirmation, bool acknowledged, string error)
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        sut.Passphrase = passphrase;
        sut.ConfirmPassphrase = confirmation;
        sut.AcknowledgedNoRecovery = acknowledged;
        sut.ShowTab(E2ePassphraseFormTab.Advanced);

        // Act
        var result = sut.Validate();

        // Assert
        Assert.Equal(error, result);
        Assert.Equal(E2ePassphraseFormTab.Passphrase, sut.ActiveTab);
    }

    [Fact]
    public async Task GivenTheCurrentPassphraseIsAskedForAndMissing_WhenValidated_ThenItIsAskedForFirst()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync(scope, asksForCurrentPassphrase: true);
        EnterPassphrases(sut);

        // Act
        var missing = sut.Validate();
        sut.CurrentPassphrase = "old passphrase";
        var entered = sut.Validate();

        // Assert
        Assert.Equal("Enter your current passphrase.", missing);
        Assert.Null(entered);
    }

    [Fact]
    public async Task GivenSettingsArgon2idCannotUse_WhenValidated_ThenWhyOnTheAdvancedTab()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        EnterPassphrases(sut);
        sut.Argon2.Iterations = 0;

        // Act
        var result = sut.Validate();

        // Assert
        Assert.Equal(testText.Sync["Sync.Argon2id.InvalidIterations"].Value, result);
        Assert.Equal(E2ePassphraseFormTab.Advanced, sut.ActiveTab);
    }

    [Fact]
    public async Task GivenSettingsChanged_WhenValidated_ThenRefusedUntilABenchmarkCompletesWithExactlyThoseValues()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        EnterPassphrases(sut);
        sut.Argon2.MemoryMib = 50_000;
        sut.Argon2.Iterations = 300;

        // Act
        var before = sut.Validate();
        var tab = sut.ActiveTab;
        await sut.Argon2.BenchmarkAsync();
        var after = sut.Validate();
        sut.Argon2.Iterations = 301;
        var changedAgain = sut.Validate();

        // Assert
        Assert.Equal(testText.Sync["Sync.Argon2id.NeedsBenchmark"].Value, before);
        Assert.Equal(E2ePassphraseFormTab.Advanced, tab);
        Assert.Null(after);
        Assert.Equal(testText.Sync["Sync.Argon2id.NeedsBenchmark"].Value, changedAgain);
    }

    [Fact]
    public async Task GivenSettingsChanged_WhenAskedWhetherTheDialogCanBeConfirmed_ThenNotUntilTheyAreBenchmarked()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var asTheyStart = sut.CanConfirm;

        // Act
        sut.Argon2.Iterations = 300;
        var afterChange = sut.CanConfirm;
        await sut.Argon2.BenchmarkAsync();

        // Assert
        Assert.True(asTheyStart);
        Assert.False(afterChange);
        Assert.True(sut.CanConfirm);
        Assert.Contains(nameof(E2ePassphraseFormViewModel.CanConfirm), changed);
    }

    [Fact]
    public async Task GivenDisposed_WhenTheSettingsChange_ThenNothingIsRaised()
    {
        // Arrange
        var sut = CreateSut();
        await sut.ResetAsync();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        sut.Argon2.Iterations = 300;

        // Assert
        Assert.DoesNotContain(nameof(E2ePassphraseFormViewModel.CanConfirm), changed);
    }

    [Fact]
    public async Task GivenSettingsBenchmarked_WhenTheParametersAreRead_ThenTheValuesEntered()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync();
        sut.Argon2.MemoryMib = 4096;
        sut.Argon2.Parallelism = 32;
        await sut.Argon2.BenchmarkAsync();

        // Act
        var parameters = sut.GetArgon2Parameters();

        // Assert
        Assert.Equal(4096 * 1024, parameters.MemoryKib);
        Assert.Equal(2, parameters.Iterations);
        Assert.Equal(32, parameters.Parallelism);
        Assert.Equal(32, parameters.KeyLength);
    }

    [Fact]
    public async Task GivenPassphrasesEntered_WhenCleared_ThenTheyAreForgottenAndTheSettingsKept()
    {
        // Arrange
        using var sut = CreateSut();
        await sut.ResetAsync(scope, asksForCurrentPassphrase: true);
        EnterPassphrases(sut);
        sut.CurrentPassphrase = "old passphrase";
        sut.Argon2.Iterations = 12;

        // Act
        sut.ClearPassphrases();

        // Assert
        Assert.Empty(sut.CurrentPassphrase);
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.ConfirmPassphrase);
        Assert.False(sut.AcknowledgedNoRecovery);
        Assert.Equal(12, sut.Argon2.Iterations);
        Assert.True(sut.AsksForCurrentPassphrase);
    }

    [Fact]
    public void GivenNullPassphrases_WhenSet_ThenTheyAreEmpty()
    {
        // Arrange
        using var sut = CreateSut();
        EnterPassphrases(sut);
        sut.CurrentPassphrase = Pass;

        // Act
        sut.CurrentPassphrase = null!;
        sut.Passphrase = null!;
        sut.ConfirmPassphrase = null!;

        // Assert
        Assert.Empty(sut.CurrentPassphrase);
        Assert.Empty(sut.Passphrase);
        Assert.Empty(sut.ConfirmPassphrase);
    }

    private static void EnterPassphrases(E2ePassphraseFormViewModel sut)
    {
        sut.Passphrase = Pass;
        sut.ConfirmPassphrase = Pass;
        sut.AcknowledgedNoRecovery = true;
    }

    private E2ePassphraseFormViewModel CreateSut() => new(mockProfiles.Object, benchmark, testText.Sync);
}
