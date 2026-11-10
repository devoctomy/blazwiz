namespace Devoctomy.Blazwiz.Events.Models;

/// <summary>
/// The known <see cref="UserEvent.Type"/> values.
/// </summary>
public static class UserEventTypes
{
    /// <summary>
    /// The user was signed out everywhere: end the session. Its <see cref="UserEvent.Reason"/> says why.
    /// </summary>
    public const string SignedOutEverywhere = "signed-out-everywhere";
}
