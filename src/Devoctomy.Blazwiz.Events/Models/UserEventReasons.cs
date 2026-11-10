namespace Devoctomy.Blazwiz.Events.Models;

/// <summary>
/// The known <see cref="UserEvent.Reason"/> values.
/// </summary>
public static class UserEventReasons
{
    /// <summary>
    /// The user asked to sign out everywhere.
    /// </summary>
    public const string UserRequested = "user-requested";

    /// <summary>
    /// The user's keys were rotated (their passphrase changed).
    /// </summary>
    public const string KeysRotated = "keys-rotated";
}
