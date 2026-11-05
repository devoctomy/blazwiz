namespace Devoctomy.Blazwiz.UI.Sync.Services.Interfaces;

/// <summary>
/// The names sync profiles are shown by: a profile's display name from resources (<c>UseDisplayText</c>), or its
/// name, which for a profile a user created is its id.
/// </summary>
public interface ISyncProfileDisplayNames
{
    /// <summary>
    /// Gets the name a profile is shown by.
    /// </summary>
    /// <param name="profile">The profile's name.</param>
    /// <returns>The display name, or the profile's name when it has none.</returns>
    string GetDisplayName(string profile);
}
