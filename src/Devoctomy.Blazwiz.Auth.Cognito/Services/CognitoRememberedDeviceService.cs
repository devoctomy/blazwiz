using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Services.Interfaces;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Offline.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// The signed-in user's remembered devices in the Cognito user pool. Offline mode makes no network calls, so both need
/// it off.
/// </summary>
public class CognitoRememberedDeviceService(
    ICognitoUserPoolClient userPoolClient,
    IAuthenticationService authenticationService,
    IOfflineModeService offlineModeService,
    ICognitoCurrentDevice currentDevice)
    : IRememberedDeviceService
{
    private readonly ICognitoUserPoolClient userPoolClient = userPoolClient ?? throw new ArgumentNullException(nameof(userPoolClient));
    private readonly IAuthenticationService authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
    private readonly IOfflineModeService offlineModeService = offlineModeService ?? throw new ArgumentNullException(nameof(offlineModeService));
    private readonly ICognitoCurrentDevice currentDevice = currentDevice ?? throw new ArgumentNullException(nameof(currentDevice));

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RememberedDevice>> ListAsync()
    {
        var accessToken = await GetAccessTokenAsync();
        var devices = await CallAsync(() => userPoolClient.ListDevicesAsync(accessToken));
        foreach (var device in devices)
        {
            device.IsThisDevice = device.Key == currentDevice.DeviceKey;
        }

        return devices.OrderByDescending(d => d.LastSignedInAt ?? DateTimeOffset.MinValue).ToList();
    }

    /// <inheritdoc/>
    public async Task ForgetAsync(string deviceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceKey);

        var accessToken = await GetAccessTokenAsync();
        await CallAsync(async () =>
        {
            await userPoolClient.ForgetDeviceAsync(accessToken, deviceKey);
            return true;
        });

        if (deviceKey == currentDevice.DeviceKey)
        {
            currentDevice.DeviceKey = null;
        }
    }

    private static async Task<T> CallAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (CognitoException ex)
        {
            var code = ex.ErrorType switch
            {
                "TooManyRequestsException" or "LimitExceededException" => AuthErrorCodes.TooManyAttempts,
                "NotAuthorizedException" => AuthErrorCodes.SignInAgain,
                _ => AuthErrorCodes.DevicesFailed,
            };
            throw new BlazwizException(code, ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BlazwizException(AuthErrorCodes.Unreachable, ex);
        }
    }

    private async Task<string> GetAccessTokenAsync()
    {
        if (offlineModeService.IsOffline)
        {
            throw new BlazwizException(AuthErrorCodes.AccountChangeOffline);
        }

        var session = await authenticationService.GetCurrentUserAsync();
        if (session == null || session.IsOffline || string.IsNullOrEmpty(session.AccessToken))
        {
            throw new BlazwizException(AuthErrorCodes.NotSignedIn);
        }

        return session.AccessToken;
    }
}
