using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="ISyncProtectedProfileService"/>. A protected profile syncs
/// <c>profiles/&lt;id&gt;/**</c>, keeps its index in its own object store, and keeps its files in IndexedDB, OPFS or
/// the picked folder, like other profiles that can switch storage.
/// </summary>
/// <param name="manager">The protected profiles' keys.</param>
/// <param name="registry">The sync profiles.</param>
/// <param name="storageResolver">The storages each profile's files are kept in.</param>
/// <param name="userContext">The signed in user.</param>
/// <param name="lockService">The cross tab sync lock.</param>
/// <param name="lockChannel">Locks a protected profile in every tab when it is locked in one.</param>
/// <param name="options">The settings of the profiles users create.</param>
/// <param name="idleLock">Locks a profile left unused for its idle timeout.</param>
/// <param name="labelStore">The labels the user's profiles are shown by on this device.</param>
public class SyncProtectedProfileService(
    IE2eProtectedProfileManager manager,
    ISyncProfileRegistry registry,
    ISyncStorageResolver storageResolver,
    ISyncUserContext userContext,
    ISyncLockService lockService,
    IE2eLockChannel lockChannel,
    SyncOptions options,
    IE2eIdleLock idleLock,
    IE2eProfileLabelStore labelStore)
    : ISyncProtectedProfileService
{
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncStorageResolver storageResolver = storageResolver ?? throw new ArgumentNullException(nameof(storageResolver));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncLockService lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    private readonly IE2eLockChannel lockChannel = lockChannel ?? throw new ArgumentNullException(nameof(lockChannel));
    private readonly SyncOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IE2eIdleLock idleLock = idleLock ?? throw new ArgumentNullException(nameof(idleLock));
    private readonly IE2eProfileLabelStore labelStore = labelStore ?? throw new ArgumentNullException(nameof(labelStore));
    private readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<E2eKeyScope> setUp = [];
    private bool listening;

    /// <inheritdoc/>
    public event EventHandler? ProfilesChanged;

    /// <inheritdoc/>
    public bool IsLoaded { get; private set; }

    /// <inheritdoc/>
    public IReadOnlyList<SyncProtectedProfile> Profiles => IsLoaded
        ? registry.Profiles
            .Where(p => p.KeyScope != null)
            .Select(p => new SyncProtectedProfile
            {
                Name = p.Name,
                Scope = p.KeyScope!,
                IsCreatedByUser = IsCreatedByUser(p),
                IsSetUp = setUp.Contains(p.KeyScope!),
                Label = IsCreatedByUser(p) ? labels.GetValueOrDefault(p.KeyScope!.Id) : null,
                Settings = p.Settings,
            })
            .OrderBy(p => p.IsCreatedByUser)
            .ToList()
        : [];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<E2eKeyScope>> LoadAsync(CancellationToken ct = default)
    {
        if (!listening)
        {
            listening = true;
            idleLock.Start();
            manager.LockStateChanged += OnLockStateChanged;
            lockChannel.LockReceived += OnLockReceived;
            await lockChannel.StartAsync();
        }

        var scopes = (await manager.GetProfilesAsync(ct)).Select(p => new E2eKeyScope(p.Id)).ToList();
        foreach (var gone in CreatedProfiles().Where(p => !scopes.Contains(p.KeyScope!)).ToList())
        {
            // Deleted on another device: nothing of it is kept here either.
            await manager.ForgetAsync(gone.KeyScope!);
            await labelStore.RemoveAsync(gone.KeyScope!.Id);
            await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + gone.Name, ct))
            {
                await DeleteLocalFilesAsync(gone.Name, ct);
            }

            registry.Remove(gone.Name);
        }

        foreach (var scope in scopes.Where(s => FindProfile(s) == null))
        {
            registry.Add(CreateOptions(scope, options.ProfileDefaults));
        }

        setUp.Clear();
        setUp.UnionWith(scopes);
        labels.Clear();
        foreach (var (id, label) in await labelStore.GetAllAsync())
        {
            labels[id] = label;
        }

        IsLoaded = true;
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return scopes;
    }

    /// <inheritdoc/>
    public async Task<E2eKeyScope> CreateAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var scope = await manager.CreateAsync(passphrase, options.ProfileDefaults, progress, ct);
        registry.Add(CreateOptions(scope, options.ProfileDefaults));
        setUp.Add(scope);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        return scope;
    }

    /// <inheritdoc/>
    public async Task SetUpAsync(E2eKeyScope scope, string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var profile = FindProfile(scope) ?? throw new ArgumentException($"There is no profile '{scope}'.", nameof(scope));
        await manager.CreateKeysAsync(scope, passphrase, profile.Settings, progress, ct);
        setUp.Add(scope);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public Task ChangePassphraseAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, CancellationToken ct = default)
    {
        var profile = FindProfile(scope) ?? throw new ArgumentException($"There is no profile '{scope}'.", nameof(scope));
        return manager.ChangePassphraseAsync(scope, currentPassphrase, newPassphrase, profile.Settings, ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var profile = CreatedProfiles().FirstOrDefault(p => p.KeyScope == scope)
            ?? throw new ArgumentException($"There is no profile '{scope}' created by the user; profiles set up in code are not deleted.", nameof(scope));

        await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile.Name, ct))
        {
            // Its files in S3 are found before anything is deleted, so a profile is never left without keys but with
            // files it could not reach.
            var syncProfile = registry.GetSyncProfile(profile.Name);
            var remoteFiles = await ListRemoteAsync(syncProfile, ct);

            // Then the keys: whatever happens to the files after this, they can no longer be decrypted.
            await manager.DeleteAsync(scope, ct);

            foreach (var remote in remoteFiles)
            {
                await syncProfile.RemoteStore.DeleteAsync(remote.Key, ifMatch: null, ct);
            }

            await DeleteLocalFilesAsync(profile.Name, ct);
        }

        registry.Remove(profile.Name);
        setUp.Remove(scope);
        labels.Remove(scope.Id);
        await labelStore.RemoveAsync(scope.Id);
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task SetLabelAsync(E2eKeyScope scope, string label)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!CreatedProfiles().Any(p => p.KeyScope == scope))
        {
            throw new ArgumentException($"There is no profile '{scope}' created by the user; profiles set up in code have display names.", nameof(scope));
        }

        if (labels.TryGetValue(scope.Id, out var current) && current == label)
        {
            return;
        }

        await labelStore.SetAsync(scope.Id, label);
        labels[scope.Id] = label;
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public Task EnableDeviceUnlockAsync(E2eKeyScope scope)
    {
        CheckDeviceUnlockAllowed(scope);
        return manager.EnableDeviceUnlockAsync(scope);
    }

    /// <inheritdoc/>
    public Task UnlockWithDeviceAsync(E2eKeyScope scope)
    {
        CheckDeviceUnlockAllowed(scope);
        return manager.UnlockWithDeviceAsync(scope);
    }

    /// <inheritdoc/>
    public void Unload()
    {
        manager.LockAll();
        foreach (var profile in CreatedProfiles().ToList())
        {
            registry.Remove(profile.Name);
        }

        setUp.Clear();
        labels.Clear();
        IsLoaded = false;
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<IReadOnlyList<RemoteObjectInfo>> ListRemoteAsync(SyncProfile syncProfile, CancellationToken ct)
    {
        try
        {
            return await syncProfile.RemoteStore.ListAsync(ct);
        }
        catch (SyncAccessDeniedException ex) when (ex.Code == SyncErrorCodes.RemoteNoCredentials)
        {
            // No sync target in this session (e.g. local development without Cognito): nothing was synced from it.
            return [];
        }
    }

    private static SyncProfileOptions CreateOptions(E2eKeyScope scope, E2eProfileSettings settings)
    {
        var id = scope.Id;
        var profile = new SyncProfileOptions
        {
            Name = id,
            KeyScope = scope,
            RemotePattern = $"{SyncDefaults.ProtectedProfilesFolder}/{id}/**/*",
            IncludeEmptyFolders = true,
            Settings = settings,
        };
        new SyncFileStoresBuilder(profile)
            .AddIndexedDb(SyncDefaults.FileDatabase, id)
            .AddOpfs(root: id)
            .AddLocalFolder()
            .UseDefault(SyncStorageKind.IndexedDb);
        return profile;
    }

    private async Task DeleteLocalFilesAsync(string name, CancellationToken ct)
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to delete a protected profile.");
        var indexStore = registry.GetIndexStore(name);
        foreach (var record in await indexStore.GetAllAsync(name, owner, ct))
        {
            await indexStore.DeleteAsync(name, owner, record.Key, ct);
        }

        // The storage in use, and IndexedDB and OPFS whichever is in use (the picked folder is only reachable while it
        // is the storage in use).
        var current = (await storageResolver.GetStateAsync(name)).Kind;
        foreach (var kind in storageResolver.GetKinds(name).Where(k => k == current || k != SyncStorageKind.LocalFolder))
        {
            var store = storageResolver.GetStore(name, kind);
            foreach (var file in await store.ListAsync(ct))
            {
                await store.DeleteAsync(file.Key, ct);
            }
        }
    }

    private SyncProfileOptions? FindProfile(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return registry.Profiles.FirstOrDefault(p => p.KeyScope == scope);
    }

    // The profiles users created: every profile except those set up in code.
    private IEnumerable<SyncProfileOptions> CreatedProfiles() => registry.Profiles.Where(IsCreatedByUser);

    private bool IsCreatedByUser(SyncProfileOptions profile) => !options.Profiles.Any(c => c.Name == profile.Name);

    private void CheckDeviceUnlockAllowed(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (FindProfile(scope) is not { Settings.AllowDeviceUnlock: true })
        {
            throw new InvalidOperationException($"Protected profile '{scope}' cannot be unlocked with the passkey.");
        }
    }

    private void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (!manager.IsUnlocked(scope))
        {
            // Locked here (by the user, or on sign out): lock it in the other tabs too. Unlocking is never sent.
            _ = PostLockAsync(scope.Id);
        }
    }

    private async Task PostLockAsync(string profileId)
    {
        try
        {
            await lockChannel.PostLockAsync(profileId);
        }
        catch (Exception)
        {
            // Another tab keeps it unlocked until it locks it itself or signs out.
        }
    }

    private void OnLockReceived(object? sender, string profileId)
    {
        if (registry.Profiles.FirstOrDefault(p => p.KeyScope?.Id == profileId) is { KeyScope: { } scope })
        {
            manager.Lock(scope);
        }
    }
}
