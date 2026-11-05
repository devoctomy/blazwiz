using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Folder;

/// <summary>
/// <see cref="ISyncFolderService"/> over <see cref="IFolderAccessClient"/>, with the folder kept under the signed in
/// user's id. One folder per user is shared by every profile, as their keys do not overlap (as in S3).
/// </summary>
public class SyncFolderService(
    IFolderAccessClient folderClient,
    ISyncUserContext userContext,
    ISyncStorageResolver resolver,
    ISyncProfileRegistry registry)
    : ISyncFolderService
{
    private readonly IFolderAccessClient folderClient = folderClient ?? throw new ArgumentNullException(nameof(folderClient));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncStorageResolver resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync()
    {
        return folderClient.IsSupportedAsync();
    }

    /// <inheritdoc/>
    public async Task<string?> PickAsync()
    {
        return await folderClient.PickAsync(await GetOwnerAsync());
    }

    /// <inheritdoc/>
    public async Task<SyncFolderStatus?> GetStatusAsync()
    {
        return await folderClient.GetStatusAsync(await GetOwnerAsync());
    }

    /// <inheritdoc/>
    public async Task<bool> NeedsReconnectAsync()
    {
        foreach (var profile in registry.Profiles.Where(p => resolver.IsSwitchable(p.Name)))
        {
            if ((await resolver.GetStateAsync(profile.Name)).Kind == SyncStorageKind.LocalFolder)
            {
                return (await GetStatusAsync())?.IsGranted != true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<bool> ReconnectAsync()
    {
        return await folderClient.ReconnectAsync(await GetOwnerAsync());
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
    }
}
