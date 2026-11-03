namespace Devoctomy.Blazwiz.Auth.Migrations.Interfaces;

/// <summary>
/// One step in a package's stored data format: changes data written in the format before it (browser storage,
/// IndexedDB, files kept by the app) to its own. Added with
/// <see cref="Extensions.AuthServiceCollectionExtensions.AddDataMigration{TMigration}"/>; run at sign in, in order,
/// before modules start (see <see cref="DataMigrationRunner"/>).
/// </summary>
/// <remarks>
/// <para>
/// A version that was never recorded counts as 0, so every migration runs for data from before it (or for none at
/// all): a migration must do nothing when there is nothing to migrate, and it must be safe to run again if it was
/// stopped part way.
/// </para>
/// <para>
/// From 1.0 every released change to stored data ships with a migration, so an app never has to migrate data by hand.
/// Before 1.0 formats change freely and nothing migrates data from development builds.
/// </para>
/// </remarks>
public interface IDataMigration
{
    /// <summary>
    /// Gets whose data format this is, versioned on its own, e.g. the package's name <c>Devoctomy.Blazwiz.Sync</c>.
    /// </summary>
    string Area { get; }

    /// <summary>
    /// Gets the version the data is at after this migration: 1, 2, 3 and so on within its area and scope.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Gets whose data it changes: the device's, or the signed-in user's.
    /// </summary>
    DataMigrationScope Scope { get; }

    /// <summary>
    /// Changes the data.
    /// </summary>
    /// <param name="context">The signed-in user.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task MigrateAsync(DataMigrationContext context);
}
