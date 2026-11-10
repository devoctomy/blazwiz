using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.UnitTests;

/// <summary>
/// An <see cref="ISyncProfileRegistry"/> over given profiles, for tests of the services that read it.
/// </summary>
internal sealed class FakeSyncProfileRegistry : ISyncProfileRegistry
{
    private readonly List<SyncProfileOptions> configured;
    private readonly List<SyncProfileOptions> added = [];
    private readonly Dictionary<string, SyncProfile> syncProfiles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (ISyncFileStore Store, ISyncFileStore Untracked)> fileStores = new(StringComparer.Ordinal);

    private FakeSyncProfileRegistry(List<SyncProfileOptions> configured)
    {
        this.configured = configured;
    }

    public event EventHandler<string>? ProfileAdded;

    public event EventHandler<string>? ProfileRemoved;

    public IReadOnlyList<SyncProfileOptions> Profiles => [.. configured, .. added];

    // Over the options' own list, so profiles added to the options later are seen.
    public static FakeSyncProfileRegistry Of(SyncOptions options) => new(options.Profiles);

    public static FakeSyncProfileRegistry Of(params SyncProfile[] profiles)
    {
        var registry = new FakeSyncProfileRegistry(profiles.Select(p => new SyncProfileOptions { Name = p.Name, RemotePattern = "**/*" }).ToList());
        foreach (var profile in profiles)
        {
            registry.syncProfiles[profile.Name] = profile;
        }

        return registry;
    }

    public SyncProfileOptions? Find(string name) => Profiles.FirstOrDefault(p => p.Name == name);

    public void Add(SyncProfileOptions profile)
    {
        added.Add(profile);
        ProfileAdded?.Invoke(this, profile.Name);
    }

    public void Add(SyncProfile profile)
    {
        syncProfiles[profile.Name] = profile;
        Add(new SyncProfileOptions { Name = profile.Name, RemotePattern = "**/*" });
    }

    // Gives a profile (configured or added) its engine parts.
    public void SetSyncProfile(SyncProfile profile) => syncProfiles[profile.Name] = profile;

    public bool Remove(string name)
    {
        if (added.RemoveAll(p => p.Name == name) == 0)
        {
            return false;
        }

        syncProfiles.Remove(name);
        ProfileRemoved?.Invoke(this, name);
        return true;
    }

    public SyncProfile GetSyncProfile(string name) =>
        syncProfiles.TryGetValue(name, out var profile) ? profile : throw new ArgumentException($"There is no sync profile called '{name}'.", nameof(name));

    // Gives a profile the stores app code and the engine use.
    public void SetFileStores(string name, ISyncFileStore store, ISyncFileStore untracked) => fileStores[name] = (store, untracked);

    public ISyncFileStore GetFileStore(string name) =>
        fileStores.TryGetValue(name, out var stores) ? stores.Store : throw new NotSupportedException();

    public ISyncFileStore GetUntrackedFileStore(string name) =>
        fileStores.TryGetValue(name, out var stores) ? stores.Untracked : throw new NotSupportedException();

    public ISyncIndexStore GetIndexStore(string name) => GetSyncProfile(name).IndexStore;

    public ISyncPathMatcher GetMatcher(string name) => GetSyncProfile(name).Matcher;
}
