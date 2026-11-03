namespace Devoctomy.Blazwiz.Core.Offline.Models;

/// <summary>
/// Why the app went into offline mode.
/// </summary>
public enum OfflineEntryReason
{
    /// <summary>
    /// The user chose offline mode.
    /// </summary>
    Chosen,

    /// <summary>
    /// Signing in could not reach the sign-in service, so the user was signed in offline instead.
    /// </summary>
    SignInUnreachable,
}
