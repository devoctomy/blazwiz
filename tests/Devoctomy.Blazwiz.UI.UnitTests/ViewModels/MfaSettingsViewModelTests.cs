using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class MfaSettingsViewModelTests
{
    private readonly FrameworkText testText = new();
    private readonly Mock<IMfaService> mockMfa = new();
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IQrCodeService> mockQrCode = new();
    private readonly Mock<IClipboardService> mockClipboard = new();
    private readonly AuthenticatorSetupViewModel setup;

    public MfaSettingsViewModelTests()
    {
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "5f2c", Email = "bob@example.com" });
        mockQrCode.Setup(x => x.CreateImageUrl(It.IsAny<string>())).Returns("qr");
        setup = new AuthenticatorSetupViewModel(mockQrCode.Object, mockClipboard.Object, new AppSettings());
    }

    public static TheoryData<UserSession?> NotOnline => new()
    {
        null,
        new UserSession { Username = "bob@example.com", IsOffline = true },
    };

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("mfaService", Assert.Throws<ArgumentNullException>(() => new MfaSettingsViewModel(null!, mockAuth.Object, setup, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("authService", Assert.Throws<ArgumentNullException>(() => new MfaSettingsViewModel(mockMfa.Object, null!, setup, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("setup", Assert.Throws<ArgumentNullException>(() => new MfaSettingsViewModel(mockMfa.Object, mockAuth.Object, null!, testText.Ui, testText.Errors)).ParamName);
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new MfaSettingsViewModel(mockMfa.Object, mockAuth.Object, setup, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new MfaSettingsViewModel(mockMfa.Object, mockAuth.Object, setup, testText.Ui, null!)).ParamName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenOnlineSession_WhenOnInitializedAsync_ThenAvailable_WithTheState(bool enabled)
    {
        // Arrange
        mockMfa.Setup(x => x.IsEnabledAsync()).ReturnsAsync(enabled);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.True(sut.IsAvailable);
        Assert.Equal(enabled, sut.IsEnabled);
    }

    [Theory]
    [MemberData(nameof(NotOnline))]
    public async Task GivenNoOnlineSession_WhenOnInitializedAsync_ThenNotAvailable(UserSession? session)
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(session);
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsAvailable);
        mockMfa.Verify(x => x.IsEnabledAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenStateUnreadable_WhenOnInitializedAsync_ThenNotAvailable()
    {
        // Arrange
        mockMfa.Setup(x => x.IsEnabledAsync()).ThrowsAsync(new BlazwizException(AuthErrorCodes.Unreachable));
        var sut = CreateSut();

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.False(sut.IsAvailable);
    }

    [Fact]
    public async Task GivenOff_WhenBeginSetupAsync_ThenTheSecretIsShownForTheEmail()
    {
        // Arrange
        mockMfa.Setup(x => x.BeginSetupAsync()).ReturnsAsync("JBSWY3DPEHPK3PXP");
        var sut = await CreateInitializedSutAsync();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        await sut.BeginSetupAsync();

        // Assert
        Assert.True(sut.Setup.IsShown);
        Assert.Contains("bob%40example.com", sut.Setup.AppLink, StringComparison.Ordinal);
        Assert.Contains(nameof(MfaSettingsViewModel.Setup), changed);
    }

    [Fact]
    public async Task GivenNoEmail_WhenBeginSetupAsync_ThenTheUsernameIsTheAccount()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { Username = "bob" });
        mockMfa.Setup(x => x.BeginSetupAsync()).ReturnsAsync("JBSWY3DPEHPK3PXP");
        var sut = await CreateInitializedSutAsync();

        // Act
        await sut.BeginSetupAsync();

        // Assert
        Assert.Contains(":bob?", sut.Setup.AppLink, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenSetupShown_WhenConfirmSetupAsync_ThenOn_AndTheSetupGone()
    {
        // Arrange
        mockMfa.Setup(x => x.BeginSetupAsync()).ReturnsAsync("JBSWY3DPEHPK3PXP");
        var sut = await CreateInitializedSutAsync();
        await sut.BeginSetupAsync();
        sut.Code = "123456";

        // Act
        await sut.ConfirmSetupAsync();

        // Assert
        mockMfa.Verify(x => x.CompleteSetupAsync("123456"), Times.Once);
        Assert.True(sut.IsEnabled);
        Assert.False(sut.Setup.IsShown);
        Assert.Equal(string.Empty, sut.Code);
        Assert.Equal(testText.Ui["Profile.Mfa.TurnedOn"].Value, sut.SuccessMessage);
        Assert.False(sut.IsBusy);
    }

    [Fact]
    public async Task GivenWrongCode_WhenConfirmSetupAsync_ThenTheError_AndStillSettingUp()
    {
        // Arrange
        mockMfa.Setup(x => x.BeginSetupAsync()).ReturnsAsync("JBSWY3DPEHPK3PXP");
        mockMfa.Setup(x => x.CompleteSetupAsync(It.IsAny<string>())).ThrowsAsync(new BlazwizException(AuthErrorCodes.CodeMismatch));
        var sut = await CreateInitializedSutAsync();
        await sut.BeginSetupAsync();

        // Act
        await sut.ConfirmSetupAsync();

        // Assert
        Assert.Equal(testText.Errors.Describe(AuthErrorCodes.CodeMismatch, []), sut.ErrorMessage);
        Assert.False(sut.IsEnabled);
        Assert.True(sut.Setup.IsShown);
    }

    [Fact]
    public async Task GivenBusy_WhenAnotherChange_ThenIgnored()
    {
        // Arrange
        var pending = new TaskCompletionSource<string>();
        mockMfa.Setup(x => x.BeginSetupAsync()).Returns(pending.Task);
        var sut = await CreateInitializedSutAsync();
        var first = sut.BeginSetupAsync();

        // Act
        await sut.DisableAsync();
        pending.SetResult("JBSWY3DPEHPK3PXP");
        await first;

        // Assert
        mockMfa.Verify(x => x.DisableAsync(), Times.Never);
    }

    [Fact]
    public async Task GivenSetupShown_WhenCancelSetup_ThenNothingShown()
    {
        // Arrange
        mockMfa.Setup(x => x.BeginSetupAsync()).ReturnsAsync("JBSWY3DPEHPK3PXP");
        var sut = await CreateInitializedSutAsync();
        await sut.BeginSetupAsync();
        sut.Code = "12";

        // Act
        sut.CancelSetup();

        // Assert
        Assert.False(sut.Setup.IsShown);
        Assert.Equal(string.Empty, sut.Code);
        Assert.Null(sut.ErrorMessage);
    }

    [Fact]
    public async Task GivenOn_WhenAskThenCancelDisable_ThenStillOn()
    {
        // Arrange
        mockMfa.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var sut = await CreateInitializedSutAsync();

        // Act
        sut.AskDisable();
        var asked = sut.IsConfirmingDisable;
        sut.CancelDisable();

        // Assert
        Assert.True(asked);
        Assert.False(sut.IsConfirmingDisable);
        Assert.True(sut.IsEnabled);
    }

    [Fact]
    public async Task GivenOn_WhenDisableAsync_ThenOff()
    {
        // Arrange
        mockMfa.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        var sut = await CreateInitializedSutAsync();
        sut.AskDisable();

        // Act
        await sut.DisableAsync();

        // Assert
        Assert.False(sut.IsEnabled);
        Assert.False(sut.IsConfirmingDisable);
        Assert.Equal(testText.Ui["Profile.Mfa.TurnedOff"].Value, sut.SuccessMessage);
    }

    [Fact]
    public async Task GivenRefused_WhenDisableAsync_ThenTheError_AndStillOn()
    {
        // Arrange
        mockMfa.Setup(x => x.IsEnabledAsync()).ReturnsAsync(true);
        mockMfa.Setup(x => x.DisableAsync()).ThrowsAsync(new BlazwizException(AuthErrorCodes.MfaChangeFailed));
        var sut = await CreateInitializedSutAsync();
        sut.AskDisable();

        // Act
        await sut.DisableAsync();

        // Assert
        Assert.True(sut.IsEnabled);
        Assert.False(sut.IsConfirmingDisable);
        Assert.Equal(testText.Errors.Describe(AuthErrorCodes.MfaChangeFailed, []), sut.ErrorMessage);
    }

    [Fact]
    public void GivenSut_WhenDisposed_ThenNoLongerListensToTheSetup()
    {
        // Arrange
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // Act
        sut.Dispose();
        setup.Show("JBSWY3DPEHPK3PXP", "bob@example.com");

        // Assert
        Assert.Empty(changed);
    }

    private async Task<MfaSettingsViewModel> CreateInitializedSutAsync()
    {
        var sut = CreateSut();
        await sut.OnInitializedAsync();
        return sut;
    }

    private MfaSettingsViewModel CreateSut() => new(mockMfa.Object, mockAuth.Object, setup, testText.Ui, testText.Errors);
}
