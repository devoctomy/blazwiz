using System.Reflection;
using System.Text.Json;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// Configures sync: the log, timings and profiles. Used by <c>AddBlazwizSync</c>.
/// </summary>
/// <param name="options">The options to configure.</param>
public class SyncBuilder(SyncOptions options)
{
    /// <summary>
    /// Gets the options being configured.
    /// </summary>
    public SyncOptions Options { get; } = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// Keeps the sync log in IndexedDB.
    /// </summary>
    /// <param name="database">The database name.</param>
    /// <param name="store">The object store name.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseIndexedDbEventLog(string database, string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(store);

        Options.EventLogDatabase = database;
        Options.EventLogStore = store;
        Options.EventLogFactory = null;
        return this;
    }

    /// <summary>
    /// Keeps the sync log in a custom store, created from the service provider.
    /// </summary>
    /// <typeparam name="T">The log type.</typeparam>
    /// <returns>The builder.</returns>
    public SyncBuilder UseEventLog<T>()
        where T : class, ISyncEventLog
    {
        Options.EventLogFactory = sp => ActivatorUtilities.CreateInstance<T>(sp);
        return this;
    }

    /// <summary>
    /// Sets how long sync log entries are kept, and the most kept per user. Pruned after each pass.
    /// </summary>
    /// <param name="maxAge">The longest an entry is kept.</param>
    /// <param name="maxEventsPerUser">The most entries kept per user.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseEventLogRetention(TimeSpan maxAge, int maxEventsPerUser)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAge, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEventsPerUser, 1);

        Options.EventLogMaxAge = maxAge;
        Options.EventLogMaxEventsPerUser = maxEventsPerUser;
        return this;
    }

    /// <summary>
    /// Sets how long after a local change a pass runs.
    /// </summary>
    /// <param name="debounce">The delay.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseChangeDebounce(TimeSpan debounce)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(debounce, TimeSpan.Zero);

        Options.ChangeDebounce = debounce;
        return this;
    }

    /// <summary>
    /// Sets the largest remote file a pass downloads. Larger ones are reported as failed and left in the cloud, so a
    /// file far bigger than the app ever writes cannot exhaust the browser's memory.
    /// </summary>
    /// <param name="bytes">The limit in bytes, at least 1.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseMaxFileSize(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 1);

        Options.MaxFileSize = bytes;
        return this;
    }

    /// <summary>
    /// Sets the connectivity check timings.
    /// </summary>
    /// <param name="configure">Changes the timings.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseConnectivityTimings(Action<ConnectivityTimings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Options.ConnectivityTimings);
        if (Options.ConnectivityTimings.RecheckBackoff is not { Count: > 0 })
        {
            throw new ArgumentException("At least one recheck delay is required.", nameof(configure));
        }

        return this;
    }

    /// <summary>
    /// Applies sync settings and profiles from configuration, e.g. <c>builder.Configuration.GetSection("Sync")</c>. Code
    /// before and after this call can change or add to them; the last setting applied wins, and a profile name may be
    /// used once. A missing section changes nothing.
    /// </summary>
    /// <remarks>
    /// <para>The section holds <c>ChangeDebounce</c>, <c>ConnectivityTimings</c> (<c>CacheFor</c>,
    /// <c>ProbeTimeout</c>, <c>RecheckBackoff</c>), <c>EventLog</c> (<c>Database</c>, <c>Store</c>, <c>MaxAge</c>,
    /// <c>MaxEventsPerUser</c>) and <c>Profiles</c>, each keyed by the profile name, with <c>Remote</c> (required),
    /// <c>IncludeEmptyFolders</c>, <c>Id</c> (required: a GUID), <c>IndexStore</c> (<c>Database</c>, <c>Store</c>), and either
    /// <c>FileStore</c> (<c>Database</c>, <c>Store</c>) or <c>Storages</c> keyed by <see cref="Models.SyncStorageKind"/>
    /// (<c>IndexedDb</c>: <c>Database</c>, <c>Store</c>; <c>Opfs</c>: <c>Root</c>, <c>MetadataDatabase</c>,
    /// <c>MetadataStore</c>; <c>LocalFolder</c>: <c>MetadataDatabase</c>, <c>MetadataStore</c>) with
    /// <c>DefaultStorage</c>. Time spans are written as <c>00:00:30</c> or <c>30.00:00:00</c>.</para>
    /// <para>Profiles are added in order of name, and storages in the order of <see cref="Models.SyncStorageKind"/>.
    /// Custom stores and logs (<see cref="UseEventLog{T}"/>, <see cref="SyncProfileBuilder.UseIndexStore{T}"/>,
    /// <see cref="SyncProfileBuilder.UseFileStore{T}"/>) are set in code.</para>
    /// </remarks>
    /// <param name="section">The configuration section.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="InvalidOperationException">The section has an unknown key, a missing or invalid value, or a
    /// profile that cannot be added.</exception>
    public SyncBuilder UseConfiguration(IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        new SyncConfigurationReader(this).Read(section);
        return this;
    }

    /// <summary>
    /// Applies sync settings and profiles from a JSON file embedded in an assembly, from its <c>Sync</c> section (see
    /// <see cref="UseConfiguration"/>). Embed it with <c>&lt;EmbeddedResource Include="sync.json" LogicalName="sync.json" /&gt;</c>.
    /// </summary>
    /// <param name="assembly">The assembly holding the file, e.g. <c>typeof(Program).Assembly</c>.</param>
    /// <param name="resourceName">The resource name, e.g. <c>sync.json</c>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="InvalidOperationException">The resource is missing, is not valid JSON, has no <c>Sync</c>
    /// section, or its settings are invalid.</exception>
    public SyncBuilder UseEmbeddedConfiguration(Assembly assembly, string resourceName)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        IConfigurationRoot configuration;
        using (var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"'{assembly.GetName().Name}' has no embedded resource called '{resourceName}'."))
        {
            try
            {
                configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
            }
            catch (Exception ex) when (ex is FormatException or JsonException)
            {
                throw new InvalidOperationException($"The embedded resource '{resourceName}' is not valid JSON: {ex.Message}", ex);
            }
        }

        var section = configuration.GetSection(SyncDefaults.ConfigurationSection);
        if (!section.Exists())
        {
            throw new InvalidOperationException($"The embedded resource '{resourceName}' has no '{SyncDefaults.ConfigurationSection}' section.");
        }

        return UseConfiguration(section);
    }

    /// <summary>
    /// Sets the security settings of the profiles users create: when they lock themselves, whether they can be
    /// unlocked with the passkey, their passphrase rules and Argon2id costs.
    /// </summary>
    /// <param name="configure">Changes the settings, starting from the framework's defaults.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder UseProfileDefaults(Action<E2eProfileSettings> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        configure(Options.ProfileDefaults);
        return this;
    }

    /// <summary>
    /// Adds a profile defined as a class.
    /// </summary>
    /// <typeparam name="TProfile">The profile definition.</typeparam>
    /// <returns>The builder.</returns>
    public SyncBuilder AddProfile<TProfile>()
        where TProfile : SyncProfileDefinition, new()
    {
        return AddProfile(new TProfile());
    }

    /// <summary>
    /// Adds a profile defined as a class.
    /// </summary>
    /// <param name="definition">The profile definition.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder AddProfile(SyncProfileDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return AddProfile(definition.Name, definition.Configure);
    }

    /// <summary>
    /// Adds every profile defined in an assembly: each concrete <see cref="SyncProfileDefinition"/> with a parameterless
    /// constructor, public or not, in order of profile name.
    /// </summary>
    /// <param name="assembly">The assembly, e.g. <c>typeof(Program).Assembly</c>.</param>
    /// <returns>The builder.</returns>
    /// <exception cref="InvalidOperationException">A definition has no parameterless constructor.</exception>
    public SyncBuilder AddProfilesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var definitions = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false } && typeof(SyncProfileDefinition).IsAssignableFrom(t))
            .Select(CreateDefinition)
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .ToList();
        foreach (var definition in definitions)
        {
            AddProfile(definition);
        }

        return this;
    }

    /// <summary>
    /// Adds every profile defined in the assembly holding <typeparamref name="T"/> (see <see cref="AddProfilesFromAssembly"/>).
    /// </summary>
    /// <typeparam name="T">A type in the assembly, e.g. <c>Program</c>.</typeparam>
    /// <returns>The builder.</returns>
    public SyncBuilder AddProfilesFromAssemblyContaining<T>()
    {
        return AddProfilesFromAssembly(typeof(T).Assembly);
    }

    /// <summary>
    /// Adds a profile: one set of index store, file store and remote pattern.
    /// </summary>
    /// <param name="name">The profile name, e.g. <c>documents</c>. App code gets its file store with this service key.</param>
    /// <param name="configure">Configures the profile. <see cref="SyncProfileBuilder.UseRemote"/> is required.</param>
    /// <returns>The builder.</returns>
    public SyncBuilder AddProfile(string name, Action<SyncProfileBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        if (Options.Profiles.Any(p => p.Name == name))
        {
            throw new ArgumentException($"A sync profile called '{name}' has already been added.", nameof(name));
        }

        var profile = new SyncProfileOptions { Name = name };
        configure(new SyncProfileBuilder(profile));
        if (string.IsNullOrWhiteSpace(profile.RemotePattern))
        {
            throw new ArgumentException($"Sync profile '{name}' needs a remote pattern (UseRemote).", nameof(configure));
        }

        if (profile.KeyScope == null)
        {
            throw new ArgumentException($"Sync profile '{name}' needs an id for its keys (UseKeyId).", nameof(configure));
        }

        if (Options.Profiles.Any(p => p.KeyScope == profile.KeyScope))
        {
            throw new ArgumentException($"Sync profile '{name}' has the same id as another profile.", nameof(configure));
        }

        foreach (var other in Options.Profiles)
        {
            if (profile.FindClash(other) is { } clash)
            {
                throw new ArgumentException($"Sync profiles '{name}' and '{other.Name}' clash: {clash}.", nameof(configure));
            }
        }

        if (profile.Overlaps(SyncDefaults.ProtectedProfilesFolder + "/"))
        {
            throw new ArgumentException(
                $"The remote pattern of sync profile '{name}' ('{profile.RemotePattern}') could match the files of the profiles users create, under '{SyncDefaults.ProtectedProfilesFolder}/'.",
                nameof(configure));
        }

        if (profile.Overlaps(SyncDefaults.KeysFolder + "/"))
        {
            throw new ArgumentException(
                $"The remote pattern of sync profile '{name}' ('{profile.RemotePattern}') could match the user's keys, under '{SyncDefaults.KeysFolder}/'.",
                nameof(configure));
        }

        Options.Profiles.Add(profile);
        return this;
    }

    private static SyncProfileDefinition CreateDefinition(Type type)
    {
        try
        {
            var definition = Activator.CreateInstance(type, nonPublic: true);
            return (SyncProfileDefinition)definition!;
        }
        catch (MissingMethodException ex)
        {
            throw new InvalidOperationException($"The sync profile definition '{type.FullName}' needs a parameterless constructor.", ex);
        }
    }
}
