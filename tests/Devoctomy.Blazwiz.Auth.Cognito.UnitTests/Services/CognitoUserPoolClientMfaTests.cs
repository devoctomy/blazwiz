using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

// The code from the authenticator app, setting one up, and remembered devices, through the real library against a
// mocked Cognito API.
public class CognitoUserPoolClientMfaTests
{
    private const string Username = "bob@example.com";

    // Values Cognito's own checks accept: sessions of 20 characters or more, device keys as "<region>_<uuid>", secrets of
    // 16 characters or more.
    private const string S2 = "session-2-0123456789abcdef";
    private const string S3 = "session-3-0123456789abcdef";
    private const string S4 = "session-4-0123456789abcdef";
    private const string S5 = "session-5-0123456789abcdef";
    private const string NewKey = "eu-west-2_0a1b2c3d-0000-4000-8000-00000000000a";
    private const string K1 = "eu-west-2_0a1b2c3d-0000-4000-8000-000000000001";
    private const string K2 = "eu-west-2_0a1b2c3d-0000-4000-8000-000000000002";
    private const string K3 = "eu-west-2_0a1b2c3d-0000-4000-8000-000000000003";
    private const string Secret = "JBSWY3DPEHPK3PXPJBSWY3DP";

    private readonly Mock<IAmazonCognitoIdentityProvider> mockProvider = new();
    private readonly AwsCognitoConfig config = new() { Region = "eu-west-2", UserPoolId = "eu-west-2_Pool", UserPoolClientId = "client-123" };

    [Fact]
    public async Task GivenMfaChallenge_WhenSignInAsync_ThenReturnsTheChallenge()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupRespondTo(ChallengeNameType.PASSWORD_VERIFIER, Challenge(ChallengeNameType.SOFTWARE_TOKEN_MFA, S2));

        // Act
        var result = await CreateSut().SignInAsync(Username, "password");

        // Assert
        Assert.Equal("SOFTWARE_TOKEN_MFA", result.ChallengeName);
        Assert.Equal(S2, result.SessionId);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task GivenMfaChallenge_WhenRespondToMfaAsync_ThenSendsTheCode_AndReturnsTokensWithTheNewDevice()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupRespondTo(ChallengeNameType.PASSWORD_VERIFIER, Challenge(ChallengeNameType.SOFTWARE_TOKEN_MFA, S2));
        var sut = CreateSut();
        await sut.SignInAsync(Username, "password");
        RespondToAuthChallengeRequest? request = null;
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA), It.IsAny<CancellationToken>()))
            .Callback<RespondToAuthChallengeRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(TokensResponse(new NewDeviceMetadataType { DeviceKey = NewKey, DeviceGroupKey = "ng" }));

        // Act
        var result = await sut.RespondToMfaAsync(S2, "123456");

        // Assert
        Assert.Equal("id", result.IdToken);
        Assert.Equal(NewKey, result.NewDeviceKey);
        Assert.Equal("ng", result.NewDeviceGroupKey);
        Assert.NotNull(request);
        Assert.Equal(S2, request.Session);
        Assert.Equal("123456", request.ChallengeResponses["SOFTWARE_TOKEN_MFA_CODE"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RespondToMfaAsync(S2, "123456"));
    }

    [Fact]
    public async Task GivenWrongCode_WhenRespondToMfaAsync_ThenThrowsCodeMismatch_AndTheCodeCanBeTriedAgain()
    {
        // Arrange
        SetupPasswordVerifierChallenge();
        SetupRespondTo(ChallengeNameType.PASSWORD_VERIFIER, Challenge(ChallengeNameType.SOFTWARE_TOKEN_MFA, S2));
        var sut = CreateSut();
        await sut.SignInAsync(Username, "password");
        mockProvider
            .SetupSequence(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.SOFTWARE_TOKEN_MFA), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CodeMismatchException("Invalid code received for user") { ErrorCode = "CodeMismatchException" })
            .ReturnsAsync(TokensResponse(null));

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => sut.RespondToMfaAsync(S2, "000000"));
        var result = await sut.RespondToMfaAsync(S2, "123456");

        // Assert
        Assert.Equal("CodeMismatchException", exception.ErrorType);
        Assert.Equal("id", result.IdToken);
        Assert.Null(result.NewDeviceKey);
    }

    [Fact]
    public async Task GivenNoChallenge_WhenRespondingOrSettingUp_ThenThrowsInvalidOperation()
    {
        // Arrange
        var sut = CreateSut();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RespondToMfaAsync(S2, "123456"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.BeginMfaSetupAsync(S2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteMfaSetupAsync(S3, "123456"));
    }

    [Fact]
    public async Task GivenMfaSetupChallenge_WhenBeginMfaSetupAsync_ThenTheSecretAndTheNextSession()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockProvider
            .Setup(x => x.AssociateSoftwareTokenAsync(It.Is<AssociateSoftwareTokenRequest>(r => r.Session == S2 && r.AccessToken == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssociateSoftwareTokenResponse { SecretCode = Secret, Session = S3 });

        // Act
        var (secret, session) = await sut.BeginMfaSetupAsync(S2);

        // Assert
        Assert.Equal(Secret, secret);
        Assert.Equal(S3, session);
    }

    [Fact]
    public async Task GivenFirstCodeAccepted_WhenCompleteMfaSetupAsync_ThenAnswersMfaSetup_AndReturnsTokens()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockProvider
            .Setup(x => x.VerifySoftwareTokenAsync(It.Is<VerifySoftwareTokenRequest>(r => r.Session == S3 && r.UserCode == "123456"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse { Status = VerifySoftwareTokenResponseType.SUCCESS, Session = S4 });
        RespondToAuthChallengeRequest? request = null;
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.MFA_SETUP), It.IsAny<CancellationToken>()))
            .Callback<RespondToAuthChallengeRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(TokensResponse(null));

        // Act
        var result = await sut.CompleteMfaSetupAsync(S3, "123456");

        // Assert
        Assert.Equal("id", result.IdToken);
        Assert.NotNull(request);
        Assert.Equal(S4, request.Session);
        Assert.Equal("client-123", request.ClientId);
        Assert.Equal(Username, request.ChallengeResponses["USERNAME"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CompleteMfaSetupAsync(S4, "123456"));
    }

    [Fact]
    public async Task GivenMfaSetupAnsweredWithAnotherChallenge_WhenCompleteMfaSetupAsync_ThenReturnsTheChallenge_AndStillWaits()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockProvider
            .Setup(x => x.VerifySoftwareTokenAsync(It.IsAny<VerifySoftwareTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse { Status = VerifySoftwareTokenResponseType.SUCCESS, Session = S4 });
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.MFA_SETUP), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RespondToAuthChallengeResponse { ChallengeName = ChallengeNameType.SOFTWARE_TOKEN_MFA, Session = S5, ResponseMetadata = new ResponseMetadata() });

        // Act
        var result = await sut.CompleteMfaSetupAsync(S3, "123456");

        // Assert
        Assert.Equal("SOFTWARE_TOKEN_MFA", result.ChallengeName);
        Assert.Equal(S5, result.SessionId);
        Assert.Null(result.IdToken);
    }

    [Fact]
    public async Task GivenFirstCodeRejected_WhenCompleteMfaSetupAsync_ThenThrowsCodeMismatch()
    {
        // Arrange
        var sut = await CreateSutAwaitingSetupAsync();
        mockProvider
            .Setup(x => x.VerifySoftwareTokenAsync(It.IsAny<VerifySoftwareTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse { Status = VerifySoftwareTokenResponseType.ERROR, Session = S4 });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => sut.CompleteMfaSetupAsync(S3, "000000"));

        // Assert
        Assert.Equal("CodeMismatchException", exception.ErrorType);
    }

    [Fact]
    public async Task GivenRememberedDevice_WhenSignInAsync_ThenSendsTheDeviceKey_AndAnswersTheDeviceChallenges()
    {
        // Arrange
        InitiateAuthRequest? initiate = null;
        SetupPasswordVerifierChallenge(r => initiate = r);
        mockProvider
            .SetupSequence(x => x.RespondToAuthChallengeAsync(It.IsAny<RespondToAuthChallengeRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Challenge(ChallengeNameType.DEVICE_SRP_AUTH, S2))
            .ReturnsAsync(new RespondToAuthChallengeResponse
            {
                ChallengeName = ChallengeNameType.DEVICE_PASSWORD_VERIFIER,
                Session = S3,
                ChallengeParameters = new Dictionary<string, string>
                {
                    ["SALT"] = "abc123",
                    ["SECRET_BLOCK"] = Convert.ToBase64String(new byte[] { 5, 6, 7, 8 }),
                    ["SRP_B"] = "2",
                    ["USERNAME"] = Username,
                    ["DEVICE_KEY"] = "dk",
                },
                ResponseMetadata = new ResponseMetadata(),
            })
            .ReturnsAsync(TokensResponse(null));
        var device = new CognitoDeviceCredential { DeviceKey = "dk", DeviceGroupKey = "dg", DevicePassword = "dp" };

        // Act
        var result = await CreateSut().SignInAsync(Username, "password", device);

        // Assert (the device challenges were answered: the third response, with the tokens, was reached)
        Assert.Equal("id", result.IdToken);
        Assert.NotNull(initiate);
        Assert.Equal("dk", initiate.AuthParameters["DEVICE_KEY"]);
        mockProvider.Verify(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.DEVICE_SRP_AUTH && r.ChallengeResponses["DEVICE_KEY"] == "dk"), It.IsAny<CancellationToken>()), Times.Once);
        mockProvider.Verify(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == ChallengeNameType.DEVICE_PASSWORD_VERIFIER), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenNewDevice_WhenConfirmDeviceAsync_ThenRegistersAVerifier_AndReturnsTheCredential()
    {
        // Arrange
        ConfirmDeviceRequest? request = null;
        mockProvider
            .Setup(x => x.ConfirmDeviceAsync(It.IsAny<ConfirmDeviceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConfirmDeviceRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new ConfirmDeviceResponse { UserConfirmationNecessary = false });

        // Act
        var credential = await CreateSut().ConfirmDeviceAsync("access", "eu-west-2_nk", "ng", "Work laptop");

        // Assert
        Assert.Equal("eu-west-2_nk", credential.DeviceKey);
        Assert.Equal("ng", credential.DeviceGroupKey);
        Assert.False(string.IsNullOrEmpty(credential.DevicePassword));
        Assert.NotNull(request);
        Assert.Equal("access", request.AccessToken);
        Assert.Equal("eu-west-2_nk", request.DeviceKey);
        Assert.Equal("Work laptop", request.DeviceName);
        Assert.False(string.IsNullOrEmpty(request.DeviceSecretVerifierConfig.PasswordVerifier));
        Assert.False(string.IsNullOrEmpty(request.DeviceSecretVerifierConfig.Salt));
        mockProvider.Verify(x => x.UpdateDeviceStatusAsync(It.IsAny<UpdateDeviceStatusRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GivenTwoDevices_WhenConfirmDeviceAsync_ThenEachGetsItsOwnPassword()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ConfirmDeviceAsync(It.IsAny<ConfirmDeviceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmDeviceResponse());
        var sut = CreateSut();

        // Act
        var first = await sut.ConfirmDeviceAsync("access", K1, "ng", "One");
        var second = await sut.ConfirmDeviceAsync("access", K2, "ng", "Two");

        // Assert
        Assert.NotEqual(first.DevicePassword, second.DevicePassword);
    }

    [Fact]
    public async Task GivenPoolAsksTheUser_WhenConfirmDeviceAsync_ThenMarksItRemembered()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ConfirmDeviceAsync(It.IsAny<ConfirmDeviceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConfirmDeviceResponse { UserConfirmationNecessary = true });
        mockProvider
            .Setup(x => x.UpdateDeviceStatusAsync(It.IsAny<UpdateDeviceStatusRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateDeviceStatusResponse());

        // Act
        await CreateSut().ConfirmDeviceAsync("access", NewKey, "ng", "Work laptop");

        // Assert
        mockProvider.Verify(
            x => x.UpdateDeviceStatusAsync(It.Is<UpdateDeviceStatusRequest>(r => r.AccessToken == "access" && r.DeviceKey == NewKey && r.DeviceRememberedStatus == DeviceRememberedStatusType.Remembered), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GivenNoName_WhenConfirmDeviceAsync_ThenSendsNone(string name)
    {
        // Arrange
        ConfirmDeviceRequest? request = null;
        mockProvider
            .Setup(x => x.ConfirmDeviceAsync(It.IsAny<ConfirmDeviceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ConfirmDeviceRequest, CancellationToken>((r, _) => request = r)
            .ReturnsAsync(new ConfirmDeviceResponse());

        // Act
        await CreateSut().ConfirmDeviceAsync("access", NewKey, "ng", name);

        // Assert
        Assert.Null(request?.DeviceName);
    }

    [Fact]
    public async Task GivenDevicesOverTwoPages_WhenListDevicesAsync_ThenAllOfThem_WithNamesAndLastSignIn()
    {
        // Arrange
        var last = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Unspecified);
        mockProvider
            .Setup(x => x.ListDevicesAsync(It.Is<ListDevicesRequest>(r => r.PaginationToken == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListDevicesResponse
            {
                Devices = [new DeviceType { DeviceKey = K1, DeviceAttributes = [new AttributeType { Name = "device_name", Value = "Work laptop" }], DeviceLastAuthenticatedDate = last }],
                PaginationToken = "page2",
            });
        mockProvider
            .Setup(x => x.ListDevicesAsync(It.Is<ListDevicesRequest>(r => r.PaginationToken == "page2"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListDevicesResponse
            {
                Devices = [new DeviceType { DeviceKey = K2, DeviceAttributes = [new AttributeType { Name = "device_name", Value = " " }] }, new DeviceType { DeviceKey = K3, DeviceAttributes = [new AttributeType { Name = "device_status", Value = "valid" }] }],
            });

        // Act
        var devices = await CreateSut().ListDevicesAsync("access");

        // Assert
        Assert.Equal([K1, K2, K3], devices.Select(d => d.Key));
        Assert.Equal("Work laptop", devices[0].Name);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 8, 0, 0, TimeSpan.Zero), devices[0].LastSignedInAt);
        Assert.Null(devices[1].Name);
        Assert.Null(devices[2].Name);
        Assert.Null(devices[2].LastSignedInAt);
        Assert.All(devices, d => Assert.False(d.IsThisDevice));
        mockProvider.Verify(x => x.ListDevicesAsync(It.Is<ListDevicesRequest>(r => r.AccessToken == "access"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GivenNoDevices_WhenListDevicesAsync_ThenEmpty()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ListDevicesAsync(It.IsAny<ListDevicesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListDevicesResponse());

        // Act
        var devices = await CreateSut().ListDevicesAsync("access");

        // Assert
        Assert.Empty(devices);
    }

    [Fact]
    public async Task GivenDeviceWithoutAttributes_WhenListDevicesAsync_ThenNoName()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ListDevicesAsync(It.IsAny<ListDevicesRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListDevicesResponse { Devices = [new DeviceType { DeviceKey = K1 }] });

        // Act
        var devices = await CreateSut().ListDevicesAsync("access");

        // Assert
        Assert.Null(Assert.Single(devices).Name);
    }

    [Fact]
    public async Task GivenDevice_WhenForgetDeviceAsync_ThenForgetsIt()
    {
        // Arrange
        mockProvider.Setup(x => x.ForgetDeviceAsync(It.IsAny<ForgetDeviceRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ForgetDeviceResponse());

        // Act
        await CreateSut().ForgetDeviceAsync("access", K1);

        // Assert
        mockProvider.Verify(x => x.ForgetDeviceAsync(It.Is<ForgetDeviceRequest>(r => r.AccessToken == "access" && r.DeviceKey == K1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(new[] { "SOFTWARE_TOKEN_MFA" }, true)]
    [InlineData(new[] { "SMS_MFA" }, false)]
    [InlineData(null, false)]
    public async Task GivenUsersMfaSettings_WhenIsSoftwareTokenMfaEnabledAsync_ThenWhetherTheAppIsOn(string[]? settings, bool expected)
    {
        // Arrange
        mockProvider
            .Setup(x => x.GetUserAsync(It.Is<GetUserRequest>(r => r.AccessToken == "access"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetUserResponse { UserMFASettingList = settings?.ToList() });

        // Act
        var result = await CreateSut().IsSoftwareTokenMfaEnabledAsync("access");

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GivenSignedIn_WhenAssociateSoftwareTokenAsync_ThenTheSecret()
    {
        // Arrange
        mockProvider
            .Setup(x => x.AssociateSoftwareTokenAsync(It.Is<AssociateSoftwareTokenRequest>(r => r.AccessToken == "access"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssociateSoftwareTokenResponse { SecretCode = Secret });

        // Act
        var secret = await CreateSut().AssociateSoftwareTokenAsync("access");

        // Assert
        Assert.Equal(Secret, secret);
    }

    [Fact]
    public async Task GivenCodeAccepted_WhenVerifySoftwareTokenAsync_ThenSucceeds()
    {
        // Arrange
        mockProvider
            .Setup(x => x.VerifySoftwareTokenAsync(It.IsAny<VerifySoftwareTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse { Status = VerifySoftwareTokenResponseType.SUCCESS });

        // Act
        await CreateSut().VerifySoftwareTokenAsync("access", "123456");

        // Assert
        mockProvider.Verify(x => x.VerifySoftwareTokenAsync(It.Is<VerifySoftwareTokenRequest>(r => r.AccessToken == "access" && r.UserCode == "123456"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GivenCodeRefused_WhenVerifySoftwareTokenAsync_ThenThrowsCodeMismatch()
    {
        // Arrange
        mockProvider
            .Setup(x => x.VerifySoftwareTokenAsync(It.IsAny<VerifySoftwareTokenRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifySoftwareTokenResponse { Status = VerifySoftwareTokenResponseType.ERROR });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => CreateSut().VerifySoftwareTokenAsync("access", "000000"));

        // Assert
        Assert.Equal("CodeMismatchException", exception.ErrorType);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenChoice_WhenSetSoftwareTokenMfaAsync_ThenSetsItAsThePreference(bool enabled)
    {
        // Arrange
        mockProvider.Setup(x => x.SetUserMFAPreferenceAsync(It.IsAny<SetUserMFAPreferenceRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new SetUserMFAPreferenceResponse());

        // Act
        await CreateSut().SetSoftwareTokenMfaAsync("access", enabled);

        // Assert
        mockProvider.Verify(
            x => x.SetUserMFAPreferenceAsync(
                It.Is<SetUserMFAPreferenceRequest>(r => r.AccessToken == "access" && r.SoftwareTokenMfaSettings.Enabled == enabled && r.SoftwareTokenMfaSettings.PreferredMfa == enabled),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GivenCognitoRefuses_WhenForgetDeviceAsync_ThenThrowsCognitoException()
    {
        // Arrange
        mockProvider
            .Setup(x => x.ForgetDeviceAsync(It.IsAny<ForgetDeviceRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ResourceNotFoundException("Device does not exist.") { ErrorCode = "ResourceNotFoundException" });

        // Act
        var exception = await Assert.ThrowsAsync<CognitoException>(() => CreateSut().ForgetDeviceAsync("access", K1));

        // Assert
        Assert.Equal("ResourceNotFoundException", exception.ErrorType);
    }

    private static RespondToAuthChallengeResponse Challenge(ChallengeNameType name, string session) => new()
    {
        ChallengeName = name,
        Session = session,
        ChallengeParameters = [],
        ResponseMetadata = new ResponseMetadata(),
    };

    private static RespondToAuthChallengeResponse TokensResponse(NewDeviceMetadataType? device) => new()
    {
        AuthenticationResult = new AuthenticationResultType { IdToken = "id", AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 3600, NewDeviceMetadata = device },
        ResponseMetadata = new ResponseMetadata(),
    };

    private async Task<CognitoUserPoolClient> CreateSutAwaitingSetupAsync()
    {
        SetupPasswordVerifierChallenge();
        SetupRespondTo(ChallengeNameType.PASSWORD_VERIFIER, Challenge(ChallengeNameType.MFA_SETUP, S2));
        var sut = CreateSut();
        Assert.Equal("MFA_SETUP", (await sut.SignInAsync(Username, "password")).ChallengeName);
        return sut;
    }

    private void SetupRespondTo(ChallengeNameType challenge, RespondToAuthChallengeResponse response)
    {
        mockProvider
            .Setup(x => x.RespondToAuthChallengeAsync(It.Is<RespondToAuthChallengeRequest>(r => r.ChallengeName == challenge), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupPasswordVerifierChallenge(Action<InitiateAuthRequest>? capture = null)
    {
        // Dummy SRP values: the library does the SRP calculation, and the mocked Cognito accepts whatever it sends.
        mockProvider
            .Setup(x => x.InitiateAuthAsync(It.IsAny<InitiateAuthRequest>(), It.IsAny<CancellationToken>()))
            .Callback<InitiateAuthRequest, CancellationToken>((r, _) => capture?.Invoke(r))
            .ReturnsAsync(new InitiateAuthResponse
            {
                ChallengeName = ChallengeNameType.PASSWORD_VERIFIER,
                ChallengeParameters = new Dictionary<string, string>
                {
                    ["SALT"] = "abc123",
                    ["SECRET_BLOCK"] = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }),
                    ["SRP_B"] = "2",
                    ["USERNAME"] = Username,
                    ["USER_ID_FOR_SRP"] = Username,
                },
                ResponseMetadata = new ResponseMetadata(),
            });
    }

    private CognitoUserPoolClient CreateSut() => new(mockProvider.Object, config);
}
