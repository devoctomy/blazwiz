using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Storage;

/// <summary>
/// <see cref="ISyncStorageResolver"/> over the profiles' configured storages. Each file store is created from its
/// factory the first time it is needed, and kept for the scope.
/// </summary>
public class SyncStorageResolver(
    IServiceProvider services,
    ISyncProfileRegistry registry,
    ISyncStorageStateStore stateStore,
    ISyncUserContext userContext,
    IOpfsClient opfsClient,
    IFolderAccessClient folderClient)
    : ISyncStorageResolver
{
    private readonly IServiceProvider services = services ?? throw new ArgumentNullException(nameof(services));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncStorageStateStore stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly IOpfsClient opfsClient = opfsClient ?? throw new ArgumentNullException(nameof(opfsClient));
    private readonly IFolderAccessClient folderClient = folderClient ?? throw new ArgumentNullException(nameof(folderClient));
    private readonly Dictionary<(string Profile, SyncStorageKind Kind), ISyncFileStore> stores = [];

    /// <inheritdoc/>
    public bool IsSwitchable(string profile)
    {
        return Find(profile)?.IsSwitchable == true;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SyncStorageKind> GetKinds(string profile)
    {
        return Find(profile)?.FileStores.Select(s => s.Kind).ToList() ?? [];
    }

    /// <inheritdoc/>
    public ISyncFileStore GetStore(string profile, SyncStorageKind kind)
    {
        if (stores.TryGetValue((profile, kind), out var store))
        {
            return store;
        }

        var storage = Find(profile)?.FileStores.FirstOrDefault(s => s.Kind == kind)
            ?? throw new ArgumentException($"Sync profile '{profile}' has no {kind} storage.", nameof(kind));
        store = storage.Factory(services);
        stores[(profile, kind)] = store;
        return store;
    }

    /// <inheritdoc/>
    public async Task<SyncStorageState> GetStateAsync(string profile)
    {
        var settings = Find(profile) is { IsSwitchable: true } found
            ? found
            : throw new ArgumentException($"Sync profile '{profile}' cannot switch storage.", nameof(profile));
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");

        var state = await stateStore.GetAsync(profile, owner);
        if (state != null && settings.FileStores.Any(s => s.Kind == state.Kind))
        {
            return state;
        }

        var deviceDefault = await GetDeviceDefaultKindAsync(owner);
        var kind = deviceDefault is { } chosen && settings.FileStores.Any(s => s.Kind == chosen)
            ? chosen
            : settings.ResolvedDefaultStorageKind;
        return new SyncStorageState { Profile = profile, Owner = owner, Kind = kind };
    }

    /// <inheritdoc/>
    public async Task<SyncStorageKind?> GetDeviceDefaultKindAsync()
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
        return await GetDeviceDefaultKindAsync(owner);
    }

    /// <inheritdoc/>
    public async Task SetDeviceDefaultKindAsync(SyncStorageKind kind)
    {
        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
        await stateStore.SaveAsync(new SyncStorageState { Profile = SyncDefaults.DeviceDefaultStorageName, Owner = owner, Kind = kind });
    }

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync(SyncStorageKind kind)
    {
        return kind switch
        {
            SyncStorageKind.IndexedDb => true,
            SyncStorageKind.Opfs => await opfsClient.IsSupportedAsync(),
            SyncStorageKind.LocalFolder => await folderClient.IsSupportedAsync(),
            _ => false,
        };
    }

    private async Task<SyncStorageKind?> GetDeviceDefaultKindAsync(string owner)
    {
        return (await stateStore.GetAsync(SyncDefaults.DeviceDefaultStorageName, owner))?.Kind;
    }

    private SyncProfileOptions? Find(string profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profile);

        return registry.Find(profile);
    }
}
