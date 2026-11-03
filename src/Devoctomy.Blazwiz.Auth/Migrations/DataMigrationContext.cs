using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.Migrations;

/// <summary>
/// What a migration runs for.
/// </summary>
public class DataMigrationContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataMigrationContext"/> class.
    /// </summary>
    /// <param name="session">The signed-in user.</param>
    public DataMigrationContext(UserSession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Gets the signed-in user. For <see cref="DataMigrationScope.User"/> migrations, the user whose data to migrate.
    /// </summary>
    public UserSession Session { get; }
}
