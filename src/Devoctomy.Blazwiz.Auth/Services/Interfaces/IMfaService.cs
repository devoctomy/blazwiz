namespace Devoctomy.Blazwiz.Auth.Services.Interfaces;

/// <summary>
/// Turns two-step verification with an authenticator app (TOTP) on and off for the signed-in user. Needs the sign-in
/// service: failures throw a <see cref="Core.Exceptions.BlazwizException"/> with one of
/// <see cref="Exceptions.AuthErrorCodes"/>.
/// </summary>
public interface IMfaService
{
    /// <summary>
    /// Gets whether the signed-in user has two-step verification on.
    /// </summary>
    /// <returns>True when it is on.</returns>
    Task<bool> IsEnabledAsync();

    /// <summary>
    /// Starts setting up an authenticator app: gets a new secret for it. Nothing changes until
    /// <see cref="CompleteSetupAsync"/> confirms it with a code.
    /// </summary>
    /// <returns>The secret (base32), to show as a QR code, a link to the app, and text.</returns>
    Task<string> BeginSetupAsync();

    /// <summary>
    /// Confirms the authenticator app with a code it shows, and turns two-step verification on.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CompleteSetupAsync(string code);

    /// <summary>
    /// Turns two-step verification off.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisableAsync();
}
