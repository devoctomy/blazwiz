using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.IndexedDb;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.S3;
using Devoctomy.Blazwiz.Sync.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// Implementation of <see cref="ISyncProfileRegistry"/>. A profile's stores are created from its configuration the
/// first time they are asked for, and kept until it is removed.
/// </summary>
/// <remarks>
/// The stores need services that themselves depend on the registry (the storage resolver, the scheduler), so they are
/// resolved from the service provider when a profile's stores are first created, not when the registry is.
/// </remarks>
/// <param name="services">Resolves the services the stores need.</param>
/// <param name="options">The profiles configured when the app started.</param>
public class SyncProfileRegistry(IServiceProvider services, SyncOptions options)
    : ISyncProfileRegistry
{
    private readonly IServiceProvider services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly SyncOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly Lock gate = new Lock();
    private readonly List<SyncProfileOptions> added = [];
    private readonly Dictionary<string, Parts> parts = new Dictionary<string, Parts>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public event EventHandler<string>? ProfileAdded;

    /// <inheritdoc/>
    public event EventHandler<string>? ProfileRemoved;

    /// <inheritdoc/>
    public IReadOnlyList<SyncProfileOptions> Profiles
    {
        get
        {
            lock (gate)
            {
                return [.. options.Profiles, .. added];
            }
        }
    }

    /// <inheritdoc/>
    public SyncProfileOptions? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return Profiles.FirstOrDefault(p => p.Name == name);
    }

    /// <inheritdoc/>
    public void Add(SyncProfileOptions profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ArgumentException("A sync profile needs a name.", nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.RemotePattern))
        {
            throw new ArgumentException($"Sync profile '{profile.Name}' needs a remote pattern.", nameof(profile));
        }

        lock (gate)
        {
            if (options.Profiles.Concat(added).Any(p => p.Name == profile.Name))
            {
                throw new ArgumentException($"A sync profile called '{profile.Name}' already exists.", nameof(profile));
            }

            added.Add(profile);
        }

        ProfileAdded?.Invoke(this, profile.Name);
    }

    /// <inheritdoc/>
    public bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        lock (gate)
        {
            if (options.Profiles.Any(p => p.Name == name))
            {
                throw new InvalidOperationException($"Sync profile '{name}' was configured when the app started and cannot be removed.");
            }

            if (added.RemoveAll(p => p.Name == name) == 0)
            {
                return false;
            }

            parts.Remove(name);
        }

        ProfileRemoved?.Invoke(this, name);
        return true;
    }

    /// <inheritdoc/>
    public SyncProfile GetSyncProfile(string name) => GetParts(name).SyncProfile.Value;

    /// <inheritdoc/>
    public ISyncFileStore GetFileStore(string name) => GetParts(name).FileStore.Value;

    /// <inheritdoc/>
    public ISyncFileStore GetUntrackedFileStore(string name) => GetParts(name).UntrackedFileStore.Value;

    /// <inheritdoc/>
    public ISyncIndexStore GetIndexStore(string name) => GetParts(name).IndexStore.Value;

    /// <inheritdoc/>
    public ISyncPathMatcher GetMatcher(string name) => GetParts(name).Matcher.Value;

    /// <inheritdoc/>
    public IReadOnlyList<EncryptedSyncProfile> GetAccountEncryptedProfiles()
    {
        return Profiles
            .Where(p => p.IsEncrypted && !p.IsProtected)
            .Select(p => new EncryptedSyncProfile(p.Name, GetFileStore(p.Name), GetIndexStore(p.Name)))
            .ToList();
    }

    private Parts GetParts(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var profile = Find(name) ?? throw new ArgumentException($"There is no sync profile called '{name}'.", nameof(name));
        lock (gate)
        {
            if (!parts.TryGetValue(name, out var found))
            {
                found = new Parts(this, profile);
                parts[name] = found;
            }

            return found;
        }
    }

    /// <summary>
    /// One profile's stores, created when first asked for.
    /// </summary>
    private sealed class Parts
    {
        public Parts(SyncProfileRegistry registry, SyncProfileOptions profile)
        {
            var sp = registry.services;
            var name = profile.Name;

            Matcher = new Lazy<ISyncPathMatcher>(() => new GlobSyncPathMatcher(profile.RemotePattern, profile.IncludeEmptyFolders));

            IndexStore = new Lazy<ISyncIndexStore>(() => profile.IndexStoreFactory?.Invoke(sp) ?? new IndexedDbSyncIndexStore(
                sp.GetRequiredService<IIndexedDbClient>(),
                IndexedDbSyncIndexStore.DescribeStore(profile.IndexDatabase, profile.ResolvedIndexStoreName)));

            UntrackedFileStore = new Lazy<ISyncFileStore>(() => profile.IsSwitchable
                ? new SwitchableSyncFileStore(name, sp.GetRequiredService<ISyncStorageResolver>())
                : profile.FileStoreFactory?.Invoke(sp) ?? new IndexedDbSyncFileStore(
                    sp.GetRequiredService<IIndexedDbClient>(),
                    sp.GetRequiredService<ISyncUserContext>(),
                    sp.GetRequiredService<TimeProvider>(),
                    IndexedDbSyncFileStore.DescribeStore(profile.FileDatabase, profile.ResolvedFileStoreName)));

            FileStore = new Lazy<ISyncFileStore>(() =>
            {
                ISyncFileStore store = new TrackingSyncFileStore(
                    name,
                    UntrackedFileStore.Value,
                    IndexStore.Value,
                    sp.GetRequiredService<ISyncUserContext>(),
                    sp.GetRequiredService<ISyncScheduler>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<IEncryptedFileFormat>());

                // App code reads and writes plaintext; the store holds (and syncs) ciphertext. A protected profile
                // always uses its own keys; another encrypted profile uses the account's once encryption is on.
                if (profile.KeyScope is { } scope)
                {
                    return new EncryptingSyncFileStore(store, sp.GetRequiredService<IE2eKeyRings>().Get(scope), sp.GetRequiredService<IEncryptedFileFormat>());
                }

                return profile.IsEncrypted
                    ? new EncryptingSyncFileStore(store, sp.GetRequiredService<IE2eKeyRings>().Get(E2eKeyScope.Account), sp.GetRequiredService<IEncryptedFileFormat>())
                    : store;
            });

            SyncProfile = new Lazy<SyncProfile>(() => new SyncProfile(
                name,
                UntrackedFileStore.Value,
                IndexStore.Value,
                new S3SyncRemoteStore(sp.GetRequiredService<IS3SyncTargetProvider>(), Matcher.Value),
                Matcher.Value));
        }

        public Lazy<ISyncPathMatcher> Matcher { get; }

        public Lazy<ISyncIndexStore> IndexStore { get; }

        public Lazy<ISyncFileStore> UntrackedFileStore { get; }

        public Lazy<ISyncFileStore> FileStore { get; }

        public Lazy<SyncProfile> SyncProfile { get; }
    }
}
