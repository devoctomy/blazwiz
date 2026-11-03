using Devoctomy.Blazwiz.Auth.Migrations.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Devoctomy.Blazwiz.Auth.Migrations;

/// <summary>
/// Runs the data migrations once the app knows who is signed in, before modules start their work: for each area and
/// scope, those newer than the version recorded on this device, in order, recording each as it completes. A failed
/// migration stops its area (the rest wait for the next sign in) and does not stop the others or the sign in.
/// </summary>
public class DataMigrationRunner : ISessionLifecycleHandler
{
    /// <summary>
    /// The local storage key the recorded versions are kept under.
    /// </summary>
    public const string StorageKey = "data_versions";

    private readonly IReadOnlyList<IDataMigration> migrations;
    private readonly ILocalStorageService localStorageService;
    private readonly ILogger<DataMigrationRunner> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataMigrationRunner"/> class.
    /// </summary>
    /// <param name="migrations">Every package's migrations.</param>
    /// <param name="localStorageService">Where the versions are recorded.</param>
    /// <param name="logger">Records failed migrations.</param>
    /// <exception cref="InvalidOperationException">Two migrations have the same area, scope and version.</exception>
    public DataMigrationRunner(IEnumerable<IDataMigration> migrations, ILocalStorageService localStorageService, ILogger<DataMigrationRunner> logger)
    {
        this.migrations = (migrations ?? throw new ArgumentNullException(nameof(migrations))).ToList();
        this.localStorageService = localStorageService ?? throw new ArgumentNullException(nameof(localStorageService));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var duplicate = this.migrations
            .GroupBy(m => (m.Area, m.Scope, m.Version))
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"More than one data migration to version {duplicate.Key.Version} of '{duplicate.Key.Area}' ({duplicate.Key.Scope}).");
        }
    }

    /// <summary>
    /// Gets the handler's position: first, so data is migrated before any module uses it.
    /// </summary>
    public int Order => int.MinValue;

    /// <inheritdoc/>
    public async Task OnShellLoadingAsync(UserSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (migrations.Count == 0)
        {
            return;
        }

        var versions = await localStorageService.GetObjectAsync<Dictionary<string, int>>(StorageKey) ?? [];
        var context = new DataMigrationContext(session);
        foreach (var area in migrations.GroupBy(m => (m.Area, m.Scope)).OrderBy(g => g.Key.Area, StringComparer.Ordinal).ThenBy(g => g.Key.Scope))
        {
            var key = area.Key.Scope == DataMigrationScope.App
                ? $"{area.Key.Area}|app"
                : $"{area.Key.Area}|user|{session.Username.ToLowerInvariant()}";
            var current = versions.GetValueOrDefault(key);
            foreach (var migration in area.Where(m => m.Version > current).OrderBy(m => m.Version))
            {
                try
                {
                    await migration.MigrateAsync(context);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Data migration to version {Version} of {Area} ({Scope}) failed; it is tried again at the next sign in.", migration.Version, area.Key.Area, area.Key.Scope);
                    break;
                }

                versions[key] = migration.Version;
                await localStorageService.SetObjectAsync(StorageKey, versions);
            }
        }
    }
}
