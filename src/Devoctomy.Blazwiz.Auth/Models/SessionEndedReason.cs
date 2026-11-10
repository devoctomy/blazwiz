namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Why a session ended without the user signing out on this device (see <see cref="SessionEndedEventArgs"/>).
/// </summary>
public enum SessionEndedReason
{
    /// <summary>
    /// The session ran out or could not be renewed, or an offline session ended.
    /// </summary>
    Expired,

    /// <summary>
    /// The sign-in service no longer accepts the session, e.g. the user signed out everywhere while this device was
    /// closed, offline or asleep, so it is not known why or from where.
    /// </summary>
    Revoked,

    /// <summary>
    /// Another device signed the user out everywhere.
    /// </summary>
    SignedOutEverywhere,

    /// <summary>
    /// Another device rotated the user's keys (their passphrase changed) and signed them out everywhere.
    /// </summary>
    KeysRotated,
}
