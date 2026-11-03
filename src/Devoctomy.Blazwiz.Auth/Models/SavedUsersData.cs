namespace Devoctomy.Blazwiz.Auth.Models;

/// <summary>
/// Represents the collection of saved users stored in local storage.
/// </summary>
public class SavedUsersData
{
    /// <summary>
    /// Gets or sets the list of saved users.
    /// </summary>
    public List<SavedUser> Users { get; set; } = [];
}
