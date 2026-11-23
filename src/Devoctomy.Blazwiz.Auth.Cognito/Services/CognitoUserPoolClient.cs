using System.Security.Cryptography;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Extensions.CognitoAuthentication;
using Amazon.Runtime;
using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Models;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// <see cref="ICognitoUserPoolClient"/> using AWS's Amazon.Extensions.CognitoAuthentication library for
/// SRP sign-in, the new password challenge, token refresh and password changes and resets. Only translates to and
/// from the library's types.
/// </summary>
public class CognitoUserPoolClient : ICognitoUserPoolClient
{
    private const int DevicePasswordBytes = 40;
    private const int DevicesPageSize = 60;
    private const string DeviceNameAttribute = "device_name";
    private const string SoftwareTokenMfa = "SOFTWARE_TOKEN_MFA";

    private readonly IAmazonCognitoIdentityProvider provider;
    private readonly AwsCognitoConfig cognitoConfig;
    private readonly CognitoUserPool userPool;

    private CognitoUser? challengedUser;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitoUserPoolClient"/> class.
    /// </summary>
    /// <param name="provider">The Cognito user pool API client.</param>
    /// <param name="cognitoConfig">The Cognito settings.</param>
    public CognitoUserPoolClient(IAmazonCognitoIdentityProvider provider, AwsCognitoConfig cognitoConfig)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        this.cognitoConfig = cognitoConfig ?? throw new ArgumentNullException(nameof(cognitoConfig));
        userPool = new CognitoUserPool(cognitoConfig.UserPoolId, cognitoConfig.UserPoolClientId, provider);
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> SignInAsync(string username, string password)
    {
        return SignInAsync(username, password, null);
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> SignInAsync(string username, string password, CognitoDeviceCredential? device)
    {
        return CallAsync(async () =>
        {
            challengedUser = null;
            var user = CreateUser(username);
            var request = new InitiateSrpAuthRequest { Password = password };
            if (device != null)
            {
                // The library answers DEVICE_SRP_AUTH and DEVICE_PASSWORD_VERIFIER itself. It throws when the group
                // key or password is missing as the challenge comes, and reads the device without a null check, so
                // all three are always set together.
                var now = DateTime.UtcNow;
                user.Device = new CognitoDevice(device.DeviceKey, new Dictionary<string, string>(), now, now, now, user);
                request.DeviceGroupKey = device.DeviceGroupKey;
                request.DevicePass = device.DevicePassword;
            }

            var response = await user.StartWithSrpAuthAsync(request);
            if (response.AuthenticationResult == null)
            {
                challengedUser = user;
            }

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> RespondToMfaAsync(string sessionId, string code)
    {
        return CallAsync(async () =>
        {
            var user = challengedUser ?? throw new InvalidOperationException("There is no sign-in waiting for a code.");
            var response = await user.RespondToMfaAuthAsync(new RespondToMfaRequest
            {
                SessionID = sessionId,
                MfaCode = code,
                ChallengeNameType = ChallengeNameType.SOFTWARE_TOKEN_MFA,
            });

            if (response.AuthenticationResult != null)
            {
                challengedUser = null;
            }

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task<(string SecretCode, string SessionId)> BeginMfaSetupAsync(string sessionId)
    {
        return CallAsync(async () =>
        {
            _ = challengedUser ?? throw new InvalidOperationException("There is no sign-in waiting for an authenticator app.");
            var response = await provider.AssociateSoftwareTokenAsync(new AssociateSoftwareTokenRequest { Session = sessionId });
            return (response.SecretCode, response.Session);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> CompleteMfaSetupAsync(string sessionId, string code)
    {
        return CallAsync(async () =>
        {
            var user = challengedUser ?? throw new InvalidOperationException("There is no sign-in waiting for an authenticator app.");
            var verified = await provider.VerifySoftwareTokenAsync(new VerifySoftwareTokenRequest { Session = sessionId, UserCode = code });
            if (verified.Status != VerifySoftwareTokenResponseType.SUCCESS)
            {
                throw new CognitoException("CodeMismatchException", "The code from the authenticator app was not accepted.");
            }

            var response = await provider.RespondToAuthChallengeAsync(new RespondToAuthChallengeRequest
            {
                ClientId = cognitoConfig.UserPoolClientId,
                ChallengeName = ChallengeNameType.MFA_SETUP,
                Session = verified.Session,
                ChallengeResponses = new Dictionary<string, string> { ["USERNAME"] = user.Username },
            });

            if (response.AuthenticationResult != null)
            {
                challengedUser = null;
            }

            return ToResult(response.AuthenticationResult, response.ChallengeName, response.Session);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoDeviceCredential> ConfirmDeviceAsync(string accessToken, string deviceKey, string deviceGroupKey, string deviceName)
    {
        return CallAsync(async () =>
        {
            // A random password only this device keeps; Cognito stores only its SRP verifier, made by the library.
            var devicePassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(DevicePasswordBytes));
            var verifier = CreateUser(deviceKey).GenerateDeviceVerifier(deviceGroupKey, devicePassword, deviceKey);
            var response = await provider.ConfirmDeviceAsync(new ConfirmDeviceRequest
            {
                AccessToken = accessToken,
                DeviceKey = deviceKey,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName,
                DeviceSecretVerifierConfig = verifier,
            });

            if (response.UserConfirmationNecessary == true)
            {
                // Only when the pool remembers devices on the user's say-so: this is that say-so.
                await provider.UpdateDeviceStatusAsync(new UpdateDeviceStatusRequest
                {
                    AccessToken = accessToken,
                    DeviceKey = deviceKey,
                    DeviceRememberedStatus = DeviceRememberedStatusType.Remembered,
                });
            }

            return new CognitoDeviceCredential
            {
                DeviceKey = deviceKey,
                DeviceGroupKey = deviceGroupKey,
                DevicePassword = devicePassword,
            };
        });
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<RememberedDevice>> ListDevicesAsync(string accessToken)
    {
        return CallAsync<IReadOnlyList<RememberedDevice>>(async () =>
        {
            var devices = new List<RememberedDevice>();
            string? paginationToken = null;
            do
            {
                var response = await provider.ListDevicesAsync(new ListDevicesRequest
                {
                    AccessToken = accessToken,
                    Limit = DevicesPageSize,
                    PaginationToken = paginationToken,
                });

                devices.AddRange((response.Devices ?? []).Select(ToRememberedDevice));
                paginationToken = response.PaginationToken;
            }
            while (!string.IsNullOrEmpty(paginationToken));

            return devices;
        });
    }

    /// <inheritdoc/>
    public Task ForgetDeviceAsync(string accessToken, string deviceKey)
    {
        return CallAsync(async () =>
        {
            await provider.ForgetDeviceAsync(new ForgetDeviceRequest { AccessToken = accessToken, DeviceKey = deviceKey });
            return true;
        });
    }

    /// <inheritdoc/>
    public Task<bool> IsSoftwareTokenMfaEnabledAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            var user = await provider.GetUserAsync(new GetUserRequest { AccessToken = accessToken });
            return user.UserMFASettingList?.Contains(SoftwareTokenMfa) == true;
        });
    }

    /// <inheritdoc/>
    public Task<string> AssociateSoftwareTokenAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            var response = await provider.AssociateSoftwareTokenAsync(new AssociateSoftwareTokenRequest { AccessToken = accessToken });
            return response.SecretCode;
        });
    }

    /// <inheritdoc/>
    public Task VerifySoftwareTokenAsync(string accessToken, string code)
    {
        return CallAsync(async () =>
        {
            var response = await provider.VerifySoftwareTokenAsync(new VerifySoftwareTokenRequest { AccessToken = accessToken, UserCode = code });
            if (response.Status != VerifySoftwareTokenResponseType.SUCCESS)
            {
                throw new CognitoException("CodeMismatchException", "The code from the authenticator app was not accepted.");
            }

            return true;
        });
    }

    /// <inheritdoc/>
    public Task SetSoftwareTokenMfaAsync(string accessToken, bool enabled)
    {
        return CallAsync(async () =>
        {
            await provider.SetUserMFAPreferenceAsync(new SetUserMFAPreferenceRequest
            {
                AccessToken = accessToken,
                SoftwareTokenMfaSettings = new SoftwareTokenMfaSettingsType { Enabled = enabled, PreferredMfa = enabled },
            });

            return true;
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> CompleteNewPasswordAsync(string sessionId, string newPassword)
    {
        return CallAsync(async () =>
        {
            var user = challengedUser ?? throw new InvalidOperationException("There is no sign-in waiting for a new password.");
            var response = await user.RespondToNewPasswordRequiredAsync(new RespondToNewPasswordRequiredRequest
            {
                SessionID = sessionId,
                NewPassword = newPassword,
            });

            if (response.AuthenticationResult != null)
            {
                challengedUser = null;
            }

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task<CognitoSignInResult> RefreshAsync(string username, string refreshToken)
    {
        return CallAsync(async () =>
        {
            var user = CreateUser(username);
            var now = DateTime.UtcNow;
            user.SessionTokens = new CognitoUserSession(null, null, refreshToken, now, now);
            var response = await user.StartWithRefreshTokenAuthAsync(new InitiateRefreshTokenAuthRequest
            {
                AuthFlowType = AuthFlowType.REFRESH_TOKEN_AUTH,
            });

            return ToResult(response);
        });
    }

    /// <inheritdoc/>
    public Task RevokeAsync(string refreshToken)
    {
        return CallAsync(async () =>
        {
            await provider.RevokeTokenAsync(new RevokeTokenRequest
            {
                ClientId = cognitoConfig.UserPoolClientId,
                Token = refreshToken,
            });

            return true;
        });
    }

    /// <inheritdoc/>
    public Task GlobalSignOutAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            await provider.GlobalSignOutAsync(new GlobalSignOutRequest { AccessToken = accessToken });
            return true;
        });
    }

    /// <inheritdoc/>
    public Task CheckAccessTokenAsync(string accessToken)
    {
        return CallAsync(async () =>
        {
            await provider.GetUserAsync(new GetUserRequest { AccessToken = accessToken });
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ChangePasswordAsync(string username, string idToken, string accessToken, DateTimeOffset expiresAt, string currentPassword, string newPassword)
    {
        return CallAsync(async () =>
        {
            var user = CreateUser(username);
            user.SessionTokens = new CognitoUserSession(idToken, accessToken, null, DateTime.UtcNow, expiresAt.UtcDateTime);
            await user.ChangePasswordAsync(currentPassword, newPassword);
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ForgotPasswordAsync(string username)
    {
        return CallAsync(async () =>
        {
            await CreateUser(username).ForgotPasswordAsync();
            return true;
        });
    }

    /// <inheritdoc/>
    public Task ConfirmForgotPasswordAsync(string username, string code, string newPassword)
    {
        return CallAsync(async () =>
        {
            await CreateUser(username).ConfirmForgotPasswordAsync(code, newPassword);
            return true;
        });
    }

    private static CognitoSignInResult ToResult(AuthFlowResponse response)
    {
        return ToResult(response.AuthenticationResult, response.ChallengeName, response.SessionID);
    }

    private static CognitoSignInResult ToResult(AuthenticationResultType? tokens, ChallengeNameType? challengeName, string? sessionId)
    {
        return new CognitoSignInResult
        {
            IdToken = tokens?.IdToken,
            AccessToken = tokens?.AccessToken,
            RefreshToken = tokens?.RefreshToken,
            ExpiresIn = tokens?.ExpiresIn ?? 0,
            ChallengeName = challengeName?.Value,
            SessionId = sessionId,
            NewDeviceKey = tokens?.NewDeviceMetadata?.DeviceKey,
            NewDeviceGroupKey = tokens?.NewDeviceMetadata?.DeviceGroupKey,
        };
    }

    private static RememberedDevice ToRememberedDevice(DeviceType device)
    {
        var name = device.DeviceAttributes?.FirstOrDefault(a => a.Name == DeviceNameAttribute)?.Value;
        return new RememberedDevice
        {
            Key = device.DeviceKey,
            Name = string.IsNullOrWhiteSpace(name) ? null : name,
            LastSignedInAt = device.DeviceLastAuthenticatedDate is { } last ? new DateTimeOffset(DateTime.SpecifyKind(last, DateTimeKind.Utc)) : null,
        };
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AmazonCognitoIdentityProviderException ex)
        {
            // E.g. NotAuthorizedException for a wrong password, InvalidPasswordException for a weak new password.
            throw new CognitoException(ex.ErrorCode ?? ex.GetType().Name, ex.Message);
        }
        catch (AmazonServiceException ex) when (ex.InnerException is HttpRequestException)
        {
            throw new HttpRequestException(ex.Message, ex);
        }
    }

    private CognitoUser CreateUser(string username)
    {
        return new CognitoUser(username, cognitoConfig.UserPoolClientId, userPool, provider);
    }
}
