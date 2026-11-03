namespace Devoctomy.Blazwiz.Sync.Interfaces;

/// <summary>
/// The signed in user, supplied by the app. Local files, the index and the log are partitioned by this id.
/// </summary>
public interface ISyncUserContext
{
    /// <summary>
    /// Gets the signed in user's id.
    /// </summary>
    /// <returns>The id, or null if nobody is signed in.</returns>
    Task<string?> GetUserIdAsync();
}
