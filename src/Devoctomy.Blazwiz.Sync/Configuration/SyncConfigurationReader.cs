using System.Globalization;
using Devoctomy.Blazwiz.Sync.Models;
using Microsoft.Extensions.Configuration;

namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// Applies a sync configuration section to a <see cref="SyncBuilder"/>, through the builder's own methods, so a profile
/// set up in configuration is checked exactly as one set up in code. Used by <see cref="SyncBuilder.UseConfiguration"/>.
/// </summary>
/// <param name="builder">The builder to configure.</param>
internal sealed class SyncConfigurationReader(SyncBuilder builder)
{
    private const string ChangeDebounceKey = "ChangeDebounce";
    private const string ConnectivityTimingsKey = "ConnectivityTimings";
    private const string EventLogKey = "EventLog";
    private const string ProfilesKey = "Profiles";
    private const string DatabaseKey = "Database";
    private const string StoreKey = "Store";
    private const string RemoteKey = "Remote";
    private const string IncludeEmptyFoldersKey = "IncludeEmptyFolders";
    private const string EncryptedKey = "Encrypted";
    private const string IndexStoreKey = "IndexStore";
    private const string FileStoreKey = "FileStore";
    private const string StoragesKey = "Storages";
    private const string DefaultStorageKey = "DefaultStorage";
    private const string RootKey = "Root";
    private const string MetadataDatabaseKey = "MetadataDatabase";
    private const string MetadataStoreKey = "MetadataStore";

    private static readonly string[] SyncKeys = [ChangeDebounceKey, ConnectivityTimingsKey, EventLogKey, ProfilesKey];
    private static readonly string[] ConnectivityTimingsKeys = ["CacheFor", "ProbeTimeout", "RecheckBackoff"];
    private static readonly string[] EventLogKeys = [DatabaseKey, StoreKey, "MaxAge", "MaxEventsPerUser"];
    private static readonly string[] ProfileKeys = [RemoteKey, IncludeEmptyFoldersKey, EncryptedKey, IndexStoreKey, FileStoreKey, StoragesKey, DefaultStorageKey];
    private static readonly string[] IndexedDbKeys = [DatabaseKey, StoreKey];
    private static readonly string[] OpfsKeys = [RootKey, MetadataDatabaseKey, MetadataStoreKey];
    private static readonly string[] LocalFolderKeys = [MetadataDatabaseKey, MetadataStoreKey];

    private readonly SyncBuilder builder = builder ?? throw new ArgumentNullException(nameof(builder));

    /// <summary>
    /// Applies the section: the timings, the event log, and a profile for each child of <c>Profiles</c>.
    /// </summary>
    /// <param name="section">The section, e.g. <c>Sync</c>.</param>
    /// <exception cref="InvalidOperationException">The section has an unknown key, a missing or invalid value, or a
    /// profile the builder refuses.</exception>
    public void Read(IConfiguration section)
    {
        ArgumentNullException.ThrowIfNull(section);

        CheckKeys(section, SyncKeys);
        if (ReadTimeSpan(section, ChangeDebounceKey) is { } debounce)
        {
            Apply(section, () => builder.UseChangeDebounce(debounce));
        }

        ReadConnectivityTimings(section.GetSection(ConnectivityTimingsKey));
        ReadEventLog(section.GetSection(EventLogKey));

        // Configuration keeps keys in order of name, so profiles are added in order of name.
        foreach (var profile in section.GetSection(ProfilesKey).GetChildren())
        {
            ReadProfile(profile);
        }
    }

    private static string PathOf(IConfiguration section, string? key = null)
    {
        var path = section is IConfigurationSection named ? named.Path : string.Empty;
        return key == null ? path : ConfigurationPath.Combine(path, key);
    }

    private static void CheckKeys(IConfiguration section, string[] allowed)
    {
        foreach (var child in section.GetChildren())
        {
            if (!allowed.Contains(child.Key, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Unknown sync setting '{child.Path}'. Expected one of: {string.Join(", ", allowed)}.");
            }
        }
    }

    private static void Apply(IConfiguration section, Action apply)
    {
        try
        {
            apply();
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException($"Invalid sync setting '{PathOf(section)}': {ex.Message}", ex);
        }
    }

    private static string? ReadString(IConfiguration section, string key)
    {
        var value = section[key];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ReadRequiredString(IConfiguration section, string key)
    {
        return ReadString(section, key)
            ?? throw new InvalidOperationException($"The sync setting '{PathOf(section, key)}' is required.");
    }

    private static T? Parse<T>(IConfiguration section, string key, Func<string, T> parse, string expected)
        where T : struct
    {
        var value = ReadString(section, key);
        if (value == null)
        {
            return null;
        }

        try
        {
            return parse(value);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
        {
            throw new InvalidOperationException($"The sync setting '{PathOf(section, key)}' is '{value}', which is not {expected}.", ex);
        }
    }

    private static TimeSpan? ReadTimeSpan(IConfiguration section, string key)
    {
        return Parse(section, key, v => TimeSpan.Parse(v, CultureInfo.InvariantCulture), "a time span (e.g. 00:00:30 or 30.00:00:00)");
    }

    private static int? ReadInt(IConfiguration section, string key)
    {
        return Parse(section, key, v => int.Parse(v, NumberStyles.Integer, CultureInfo.InvariantCulture), "a whole number");
    }

    private static bool? ReadBool(IConfiguration section, string key)
    {
        return Parse(section, key, bool.Parse, "true or false");
    }

    private static SyncStorageKind ParseStorageKind(IConfiguration section, string key, string value)
    {
        return Enum.TryParse<SyncStorageKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind)
            ? kind
            : throw new InvalidOperationException($"The sync setting '{PathOf(section, key)}' is '{value}', which is not a storage. Expected one of: {string.Join(", ", Enum.GetNames<SyncStorageKind>())}.");
    }

    private static List<(SyncStorageKind Kind, IConfigurationSection Section)> ReadStorages(IConfigurationSection profile)
    {
        // Each storage is keyed by its kind; they are added in the order of SyncStorageKind, which is also the default
        // when DefaultStorage is not set. An empty object (e.g. "LocalFolder": {}) adds the storage with its defaults.
        var storages = profile.GetSection(StoragesKey);
        var found = new Dictionary<SyncStorageKind, IConfigurationSection>();
        foreach (var child in storages.GetChildren())
        {
            var kind = ParseStorageKind(storages, child.Key, child.Key);
            var allowed = kind switch
            {
                SyncStorageKind.IndexedDb => IndexedDbKeys,
                SyncStorageKind.Opfs => OpfsKeys,
                _ => LocalFolderKeys,
            };
            CheckKeys(child, allowed);
            found[kind] = child;
        }

        return [.. found.OrderBy(s => s.Key).Select(s => (s.Key, s.Value))];
    }

    private static void AddStorage(SyncFileStoresBuilder stores, SyncStorageKind kind, IConfigurationSection section)
    {
        var metadataDatabase = ReadString(section, MetadataDatabaseKey) ?? SyncDefaults.IndexDatabase;
        var metadataStore = ReadString(section, MetadataStoreKey);
        switch (kind)
        {
            case SyncStorageKind.IndexedDb:
                stores.AddIndexedDb(ReadRequiredString(section, DatabaseKey), ReadRequiredString(section, StoreKey));
                break;
            case SyncStorageKind.Opfs:
                stores.AddOpfs(ReadRequiredString(section, RootKey), metadataDatabase, metadataStore);
                break;
            default:
                stores.AddLocalFolder(metadataDatabase, metadataStore);
                break;
        }
    }

    private void ReadConnectivityTimings(IConfigurationSection section)
    {
        CheckKeys(section, ConnectivityTimingsKeys);
        var cacheFor = ReadTimeSpan(section, "CacheFor");
        var probeTimeout = ReadTimeSpan(section, "ProbeTimeout");
        var backoffSection = section.GetSection("RecheckBackoff");
        var backoff = backoffSection.GetChildren()
            .Select(c => ReadTimeSpan(backoffSection, c.Key) ?? throw new InvalidOperationException($"The sync setting '{c.Path}' is empty."))
            .ToList();
        if (cacheFor == null && probeTimeout == null && backoff.Count == 0)
        {
            return;
        }

        Apply(section, () => builder.UseConnectivityTimings(timings =>
        {
            timings.CacheFor = cacheFor ?? timings.CacheFor;
            timings.ProbeTimeout = probeTimeout ?? timings.ProbeTimeout;
            if (backoff.Count > 0)
            {
                timings.RecheckBackoff = backoff;
            }
        }));
    }

    private void ReadEventLog(IConfigurationSection section)
    {
        CheckKeys(section, EventLogKeys);
        var options = builder.Options;
        var database = ReadString(section, DatabaseKey);
        var store = ReadString(section, StoreKey);
        if (database != null || store != null)
        {
            Apply(section, () => builder.UseIndexedDbEventLog(database ?? options.EventLogDatabase, store ?? options.EventLogStore));
        }

        var maxAge = ReadTimeSpan(section, "MaxAge");
        var maxEvents = ReadInt(section, "MaxEventsPerUser");
        if (maxAge != null || maxEvents != null)
        {
            Apply(section, () => builder.UseEventLogRetention(maxAge ?? options.EventLogMaxAge, maxEvents ?? options.EventLogMaxEventsPerUser));
        }
    }

    private void ReadProfile(IConfigurationSection section)
    {
        CheckKeys(section, ProfileKeys);
        var remote = ReadRequiredString(section, RemoteKey);
        var includeEmptyFolders = ReadBool(section, IncludeEmptyFoldersKey) ?? false;
        var encrypted = ReadBool(section, EncryptedKey) ?? false;

        var indexStore = section.GetSection(IndexStoreKey);
        CheckKeys(indexStore, IndexedDbKeys);
        var hasIndexStore = indexStore.GetChildren().Any();

        var fileStore = section.GetSection(FileStoreKey);
        CheckKeys(fileStore, IndexedDbKeys);
        var hasFileStore = fileStore.GetChildren().Any();

        var storages = ReadStorages(section);
        var defaultStorage = ReadString(section, DefaultStorageKey) is { } value
            ? ParseStorageKind(section, DefaultStorageKey, value)
            : (SyncStorageKind?)null;
        if (hasFileStore && storages.Count > 0)
        {
            throw new InvalidOperationException($"Sync profile '{PathOf(section)}' has both {FileStoreKey} and {StoragesKey}; use one.");
        }

        if (defaultStorage != null && storages.Count == 0)
        {
            throw new InvalidOperationException($"Sync profile '{PathOf(section)}' has a {DefaultStorageKey} but no {StoragesKey}.");
        }

        Apply(section, () => builder.AddProfile(section.Key, profile =>
        {
            if (hasIndexStore)
            {
                profile.UseIndexedDbIndexStore(ReadRequiredString(indexStore, DatabaseKey), ReadRequiredString(indexStore, StoreKey));
            }

            if (hasFileStore)
            {
                profile.UseIndexedDbFileStore(ReadRequiredString(fileStore, DatabaseKey), ReadRequiredString(fileStore, StoreKey));
            }

            if (storages.Count > 0)
            {
                profile.UseFileStores(stores =>
                {
                    foreach (var (kind, storage) in storages)
                    {
                        AddStorage(stores, kind, storage);
                    }

                    if (defaultStorage is { } defaultKind)
                    {
                        stores.UseDefault(defaultKind);
                    }
                });
            }

            profile.UseRemote(remote, includeEmptyFolders);
            if (encrypted)
            {
                profile.UseEncryption();
            }
        }));
    }
}
