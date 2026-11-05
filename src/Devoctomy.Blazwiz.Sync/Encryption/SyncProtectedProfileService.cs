using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
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
public class SyncProtectedProfileService(
    IE2eProtectedProfileManager manager,
    ISyncProfileRegistry registry,
    ISyncStorageResolver storageResolver,
    ISyncUserContext userContext,
    ISyncLockService lockService,
    IE2eLockChannel lockChannel,
    SyncOptions options,
    IE2eIdleLock idleLock)
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
    private bool listening;

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
            // Deleted on another device.
            manager.Lock(gone.KeyScope!);
            registry.Remove(gone.Name);
        }

        foreach (var scope in scopes.Where(s => FindProfile(s) == null))
        {
            registry.Add(CreateOptions(scope, options.ProfileDefaults));
        }

        return scopes;
    }

    /// <inheritdoc/>
    public async Task<E2eKeyScope> CreateAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var scope = await manager.CreateAsync(passphrase, options.ProfileDefaults, progress, ct);
        registry.Add(CreateOptions(scope, options.ProfileDefaults));
        return scope;
    }

    /// <inheritdoc/>
    public async Task<bool> IsSetUpAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return (await manager.GetProfilesAsync(ct)).Any(p => p.Id == scope.Id);
    }

    /// <inheritdoc/>
    public Task SetUpAsync(E2eKeyScope scope, string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var profile = FindProfile(scope) ?? throw new ArgumentException($"There is no profile '{scope}'.", nameof(scope));
        return manager.CreateKeysAsync(scope, passphrase, profile.Settings, progress, ct);
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

        // The keys go first: whatever happens to the files after this, they can no longer be decrypted.
        await manager.DeleteAsync(scope, ct);

        await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile.Name, ct))
        {
            var syncProfile = registry.GetSyncProfile(profile.Name);
            foreach (var remote in await syncProfile.RemoteStore.ListAsync(ct))
            {
                await syncProfile.RemoteStore.DeleteAsync(remote.Key, ifMatch: null, ct);
            }

            var owner = await userContext.GetUserIdAsync()
                ?? throw new InvalidOperationException("Sign in to delete a protected profile.");
            foreach (var record in await syncProfile.IndexStore.GetAllAsync(profile.Name, owner, ct))
            {
                await syncProfile.IndexStore.DeleteAsync(profile.Name, owner, record.Key, ct);
            }

            // The storage in use, and IndexedDB and OPFS whichever is in use (the picked folder is only reachable
            // while it is the storage in use).
            var current = (await storageResolver.GetStateAsync(profile.Name)).Kind;
            foreach (var kind in storageResolver.GetKinds(profile.Name).Where(k => k == current || k != SyncStorageKind.LocalFolder))
            {
                var store = storageResolver.GetStore(profile.Name, kind);
                foreach (var file in await store.ListAsync(ct))
                {
                    await store.DeleteAsync(file.Key, ct);
                }
            }
        }

        registry.Remove(profile.Name);
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

    private SyncProfileOptions? FindProfile(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return registry.Profiles.FirstOrDefault(p => p.KeyScope == scope);
    }

    // The profiles users created: every profile except those set up in code.
    private IEnumerable<SyncProfileOptions> CreatedProfiles() =>
        registry.Profiles.Where(p => !options.Profiles.Any(c => c.Name == p.Name));

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
