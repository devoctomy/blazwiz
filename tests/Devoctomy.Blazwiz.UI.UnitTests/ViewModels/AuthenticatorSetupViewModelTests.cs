using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;
using Devoctomy.Blazwiz.UI.ViewModels;
using Moq;

namespace Devoctomy.Blazwiz.UI.UnitTests.ViewModels;

public class AuthenticatorSetupViewModelTests
{
    private readonly Mock<IQrCodeService> mockQrCode = new();
    private readonly Mock<IClipboardService> mockClipboard = new();
    private readonly AppSettings appSettings = new() { ApplicationTitle = "My App" };

    public AuthenticatorSetupViewModelTests()
    {
        mockQrCode.Setup(x => x.CreateImageUrl(It.IsAny<string>())).Returns<string>(text => "qr:" + text);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("qrCodeService", Assert.Throws<ArgumentNullException>(() => new AuthenticatorSetupViewModel(null!, mockClipboard.Object, appSettings)).ParamName);
        Assert.Equal("clipboardService", Assert.Throws<ArgumentNullException>(() => new AuthenticatorSetupViewModel(mockQrCode.Object, null!, appSettings)).ParamName);
        Assert.Equal("appSettings", Assert.Throws<ArgumentNullException>(() => new AuthenticatorSetupViewModel(mockQrCode.Object, mockClipboard.Object, null!)).ParamName);
    }

    [Fact]
    public void GivenNew_WhenReadingIt_ThenNothingShown()
    {
        // Act
        var sut = CreateSut();

        // Assert
        Assert.False(sut.IsShown);
        Assert.Equal(string.Empty, sut.GroupedSecret);
    }

    [Fact]
    public void GivenSecret_WhenShow_ThenTheLinkQrCodeAndGroupedSecret()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        sut.Show("JBSWY3DPEHPK3PXPAB", " bob+mfa@example.com ");

        // Assert
        Assert.True(sut.IsShown);
        Assert.Equal("JBSWY3DPEHPK3PXPAB", sut.Secret);
        Assert.Equal("JBSW Y3DP EHPK 3PXP AB", sut.GroupedSecret);
        Assert.Equal("otpauth://totp/My%20App:bob%2Bmfa%40example.com?secret=JBSWY3DPEHPK3PXPAB&issuer=My%20App", sut.AppLink);
        Assert.Equal("qr:" + sut.AppLink, sut.QrCodeImageUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenNoSecret_WhenShow_ThenThrowsArgumentException(string? secret)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => CreateSut().Show(secret!, "bob@example.com"));
    }

    [Fact]
    public void GivenNoAccount_WhenShow_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => CreateSut().Show("JBSWY3DPEHPK3PXP", null!));
    }

    [Fact]
    public async Task GivenShown_WhenCopySecretAsync_ThenCopiedForAWhile()
    {
        // Arrange
        var sut = CreateSut();
        sut.Show("JBSWY3DPEHPK3PXP", "bob@example.com");

        // Act
        await sut.CopySecretAsync();

        // Assert
        Assert.True(sut.IsCopied);
        Assert.False(sut.CopyFailed);
        mockClipboard.Verify(x => x.CopyAsync("JBSWY3DPEHPK3PXP", AuthenticatorSetupViewModel.ClipboardLifetime), Times.Once);
    }

    [Fact]
    public async Task GivenBrowserRefuses_WhenCopySecretAsync_ThenCopyFailed()
    {
        // Arrange
        mockClipboard.Setup(x => x.CopyAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>())).ThrowsAsync(new InvalidOperationException("denied"));
        var sut = CreateSut();
        sut.Show("JBSWY3DPEHPK3PXP", "bob@example.com");

        // Act
        await sut.CopySecretAsync();

        // Assert
        Assert.False(sut.IsCopied);
        Assert.True(sut.CopyFailed);
    }

    [Fact]
    public async Task GivenNothingShown_WhenCopySecretAsync_ThenNothingCopied()
    {
        // Act
        await CreateSut().CopySecretAsync();

        // Assert
        mockClipboard.Verify(x => x.CopyAsync(It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task GivenCopied_WhenClear_ThenNothingShown()
    {
        // Arrange
        var sut = CreateSut();
        sut.Show("JBSWY3DPEHPK3PXP", "bob@example.com");
        await sut.CopySecretAsync();

        // Act
        sut.Clear();

        // Assert
        Assert.False(sut.IsShown);
        Assert.False(sut.IsCopied);
        Assert.False(sut.CopyFailed);
        Assert.Equal(string.Empty, sut.AppLink);
        Assert.Equal(string.Empty, sut.QrCodeImageUrl);
    }

    private AuthenticatorSetupViewModel CreateSut() => new(mockQrCode.Object, mockClipboard.Object, appSettings);
}
