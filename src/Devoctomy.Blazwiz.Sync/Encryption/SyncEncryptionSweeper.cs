using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="ISyncEncryptionSweeper"/>. One sweep runs at a time.
/// </summary>
public sealed class SyncEncryptionSweeper(
    ISyncProfileRegistry registry,
    ISyncUserContext userContext)
    : ISyncEncryptionSweeper, IDisposable
{
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

    /// <inheritdoc/>
    public async Task<int> CountEncryptedAsync(CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        var count = 0;
        foreach (var profile in registry.GetAccountEncryptedProfiles())
        {
            count += (await profile.IndexStore.GetAllAsync(profile.Name, owner, ct)).Count(r => r.Encrypted && !r.Deleted);
        }

        return count;
    }

    /// <inheritdoc/>
    public async Task<int> CountPendingEncryptedAsync(CancellationToken ct = default)
    {
        var owner = await GetOwnerAsync();
        var count = 0;
        foreach (var profile in registry.GetAccountEncryptedProfiles())
        {
            count += (await profile.IndexStore.GetAllAsync(profile.Name, owner, ct)).Count(r => r.Encrypted && r.Dirty && !r.Deleted);
        }

        return count;
    }

    /// <inheritdoc/>
    public Task<int> EncryptAsync(IProgress<E2eProgress>? progress = null, string? profile = null, CancellationToken ct = default)
    {
        return SweepAsync(E2eStep.EncryptFiles, progress, ct, (item, file) =>
            (profile == null || item.Name == profile) && file.EncryptionKeyId == null);
    }

    /// <inheritdoc/>
    public async Task<int> DecryptAsync(IProgress<E2eProgress>? progress = null, bool pendingOnly = false, CancellationToken ct = default)
    {
        var pending = new HashSet<(string Profile, string Key)>();
        if (pendingOnly)
        {
            var owner = await GetOwnerAsync();
            foreach (var profile in registry.GetAccountEncryptedProfiles())
            {
                foreach (var record in await profile.IndexStore.GetAllAsync(profile.Name, owner, ct))
                {
                    if (record.Dirty && !record.Deleted)
                    {
                        pending.Add((profile.Name, record.Key));
                    }
                }
            }
        }

        return await SweepAsync(E2eStep.DecryptFiles, progress, ct, (item, file) =>
            file.EncryptionKeyId != null && (!pendingOnly || pending.Contains((item.Name, file.Key))));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        gate.Dispose();
    }

    private async Task<string> GetOwnerAsync()
    {
        return await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in to use your files.");
    }

    private async Task<int> SweepAsync(E2eStep step, IProgress<E2eProgress>? progress, CancellationToken ct, Func<EncryptedSyncProfile, SyncFileInfo, bool> include)
    {
        await gate.WaitAsync(ct);
        try
        {
            var work = new List<(EncryptedSyncProfile Profile, string Key)>();
            foreach (var profile in registry.GetAccountEncryptedProfiles())
            {
                foreach (var file in await profile.FileStore.ListAsync(ct))
                {
                    if (!file.IsFolder && include(profile, file))
                    {
                        work.Add((profile, file.Key));
                    }
                }
            }

            var done = 0;
            progress?.Report(new E2eProgress(step, false, done, work.Count));
            foreach (var (profile, key) in work)
            {
                ct.ThrowIfCancellationRequested();

                // Read and written through the store app code is given: decrypted on read, then written as the key
                // ring says. The write marks the file in the index, so a sweep cut short carries on from here.
                var file = await profile.FileStore.ReadAsync(key, ct);
                if (file != null)
                {
                    await profile.FileStore.WriteAsync(key, file.Content, file.ModifiedAt, ct);
                }

                done++;
                progress?.Report(new E2eProgress(step, false, done, work.Count));
            }

            progress?.Report(new E2eProgress(step, true, done, work.Count));
            return done;
        }
        finally
        {
            gate.Release();
        }
    }
}
