using System.Text.Json;
using Devoctomy.Blazwiz.Browser.Services;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Microsoft.JSInterop;
using Moq;

namespace Devoctomy.Blazwiz.Browser.UnitTests.Services;

public class WebAuthnServiceTests
{
    private const string Prf = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=";

    private readonly FakeJsRuntime js = new();
    private readonly Mock<IPrfKeyDerivator> mockDerivator = new();
    private readonly WebAuthnService sut;

    public WebAuthnServiceTests()
    {
        sut = new WebAuthnService(js, mockDerivator.Object);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("jsRuntime", Assert.Throws<ArgumentNullException>(() => new WebAuthnService(null!, mockDerivator.Object)).ParamName);
        Assert.Equal("prfKeyDerivator", Assert.Throws<ArgumentNullException>(() => new WebAuthnService(js, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAVerifiedAssertionWithPrf_WhenAuthenticating_ThenTheKeysAreDerivedAndTheOutputWiped()
    {
        // Arrange
        byte[]? seen = null;
        mockDerivator.Setup(x => x.DeriveKeys(It.IsAny<byte[]>())).Callback<byte[]>(b => seen = (byte[])b.Clone());
        js.Results["webAuthnWrapper.authenticate"] = $$"""{"success":true,"userPresent":true,"userVerified":true,"prfOutput":"{{Prf}}"}""";

        // Act
        var result = await sut.AuthenticateAsync("cred");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.UserVerified);
        Assert.True(result.KeysDerived);
        Assert.Equal(Convert.FromBase64String(Prf), seen);
    }

    [Fact]
    public async Task GivenAnAssertionWithoutUserVerification_WhenAuthenticating_ThenNoKeysAreDerived()
    {
        // Arrange: a touch proves possession of the key, not the user.
        js.Results["webAuthnWrapper.authenticate"] = $$"""{"success":true,"userPresent":true,"userVerified":false,"prfOutput":"{{Prf}}"}""";

        // Act
        var result = await sut.AuthenticateAsync("cred");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.UserVerified);
        Assert.False(result.KeysDerived);
        mockDerivator.Verify(x => x.DeriveKeys(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenAFailedAssertion_WhenAuthenticating_ThenNoKeysAreDerivedAndTheErrorIsReturned()
    {
        // Arrange
        js.Results["webAuthnWrapper.authenticate"] = """{"success":false,"userPresent":false,"userVerified":false,"prfOutput":null,"error":"Operation was cancelled or timed out"}""";

        // Act
        var result = await sut.AuthenticateAsync("cred");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.KeysDerived);
        Assert.Equal("Operation was cancelled or timed out", result.Error);
        mockDerivator.Verify(x => x.DeriveKeys(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenAVerifiedRegistrationWithPrf_WhenRegistering_ThenTheKeysAreDerived()
    {
        // Arrange
        js.Results["webAuthnWrapper.register"] = $$"""{"success":true,"credentialID":"cred","userID":"uid","username":"bob","prfEnabled":true,"userVerified":true,"prfOutput":"{{Prf}}"}""";

        // Act
        var result = await sut.RegisterAsync("bob");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.PrfEnabled);
        Assert.True(result.UserVerified);
        Assert.True(result.KeysDerived);
        Assert.Equal("cred", result.CredentialID);
        mockDerivator.Verify(x => x.DeriveKeys(It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task GivenARegistrationWithoutUserVerification_WhenRegistering_ThenNoKeysAreDerived()
    {
        // Arrange
        js.Results["webAuthnWrapper.register"] = $$"""{"success":true,"credentialID":"cred","userID":"uid","username":"bob","prfEnabled":true,"userVerified":false,"prfOutput":"{{Prf}}"}""";

        // Act
        var result = await sut.RegisterAsync("bob");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.KeysDerived);
        mockDerivator.Verify(x => x.DeriveKeys(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public async Task GivenARegistrationWithoutPrf_WhenRegistering_ThenPrfIsReportedOffAndNoKeysAreDerived()
    {
        // Arrange
        js.Results["webAuthnWrapper.register"] = """{"success":true,"credentialID":"cred","userID":"uid","username":"bob","prfEnabled":false,"userVerified":true,"prfOutput":null}""";

        // Act
        var result = await sut.RegisterAsync("bob");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.PrfEnabled);
        Assert.False(result.KeysDerived);
        mockDerivator.Verify(x => x.DeriveKeys(It.IsAny<byte[]>()), Times.Never);
    }

    /// <summary>
    /// Answers each JavaScript call with JSON, read into the service's own result types as the browser's would be.
    /// </summary>
    private sealed class FakeJsRuntime : IJSRuntime
    {
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

        public Dictionary<string, string> Results { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(JsonSerializer.Deserialize<TValue>(Results[identifier], Options)!);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
