using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.InMemory;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.IntTests.Support;

/// <summary>
/// A device's sync profiles, as the app's registry composes them (app code writes plaintext through the encrypting
/// store; the device stores and syncs ciphertext), with in-memory storage and the shared bucket.
/// </summary>
public sealed class DeviceProfileRegistry(ProtectedDevice device, SharedCloud cloud)
    : ISyncProfileRegistry
{
    private readonly Dictionary<string, (SyncProfileOptions Options, SyncProfile SyncProfile, ISyncFileStore AppFiles)> profiles = [];

    public event EventHandler<string>? ProfileAdded;

    public event EventHandler<string>? ProfileRemoved;

    public IReadOnlyList<SyncProfileOptions> Profiles => profiles.Values.Select(p => p.Options).ToList();

    public SyncProfileOptions? Find(string name) => profiles.TryGetValue(name, out var p) ? p.Options : null;

    public void Add(SyncProfileOptions profile)
    {
        var stored = device.CreateStoredFiles(profile.Name);
        var index = new InMemorySyncIndexStore();
        var matcher = new GlobSyncPathMatcher(profile.RemotePattern, profile.IncludeEmptyFolders);
        var syncProfile = new SyncProfile(profile.Name, stored, index, new SharedCloudRemoteStore(cloud, matcher), matcher);
        profiles.Add(profile.Name, (profile, syncProfile, device.CreateAppFiles(profile, stored, index)));
        ProfileAdded?.Invoke(this, profile.Name);
    }

    public bool Remove(string name)
    {
        if (!profiles.Remove(name))
        {
            return false;
        }

        ProfileRemoved?.Invoke(this, name);
        return true;
    }

    public SyncProfile GetSyncProfile(string name) => profiles[name].SyncProfile;

    public ISyncFileStore GetFileStore(string name) => profiles[name].AppFiles;

    public ISyncFileStore GetUntrackedFileStore(string name) => profiles[name].SyncProfile.FileStore;

    public ISyncIndexStore GetIndexStore(string name) => profiles[name].SyncProfile.IndexStore;

    public ISyncPathMatcher GetMatcher(string name) => profiles[name].SyncProfile.Matcher;
}
