namespace Devoctomy.Blazwiz.Auth.Migrations;

/// <summary>
/// Whose data a migration changes, which decides how its version is kept.
/// </summary>
public enum DataMigrationScope
{
    /// <summary>
    /// The app's data on this device, shared by everyone who signs in on it. Its version is kept once per device.
    /// </summary>
    App,

    /// <summary>
    /// The signed-in user's data. Its version is kept per user, on each device.
    /// </summary>
    User,
}
