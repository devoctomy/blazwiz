using System.Security.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Interfaces;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="ISyncKeyRotationService"/>. Every file is encrypted again first, on this device, through
/// the profile's own file store, so each write is tracked and synced like any edit; then one sync pass uploads them
/// all, reporting each upload. A file whose header already names the active key is skipped, which is what lets a
/// change cut short be finished: files encrypted again but not uploaded are still marked to upload.
/// </summary>
/// <param name="profiles">Rotates the profile's keys with its own settings.</param>
/// <param name="manager">Clears the rotation mark.</param>
/// <param name="keyRings">The profiles' keys.</param>
/// <param name="registry">The profile's stores.</param>
/// <param name="syncService">Syncs the profile.</param>
/// <param name="connectivityService">Whether this device is online.</param>
/// <param name="lockService">The cross tab sync lock.</param>
/// <param name="targetProvider">Whether there is a sync target at all.</param>
/// <param name="scratchStore">Where the throwaway file is uploaded.</param>
/// <param name="encryptedFileFormat">Encrypts the throwaway file and reads files' key ids.</param>
/// <param name="cipher">The size of a file key.</param>
/// <param name="timeProvider">Times the throwaway file.</param>
public class SyncKeyRotationService(
    ISyncProtectedProfileService profiles,
    IE2eProtectedProfileManager manager,
    IE2eKeyRings keyRings,
    ISyncProfileRegistry registry,
    ISyncService syncService,
    IConnectivityService connectivityService,
    ISyncLockService lockService,
    IS3SyncTargetProvider targetProvider,
    ISyncScratchStore scratchStore,
    IEncryptedFileFormat encryptedFileFormat,
    IAeadCipher cipher,
    TimeProvider timeProvider)
    : ISyncKeyRotationService
{
    /// <summary>
    /// How many times the files are gone through when some are still under an earlier key after uploading (e.g.
    /// another device's edit won a conflict), before the change is reported as not finished.
    /// </summary>
    public const int MaxRounds = 3;

    /// <summary>
    /// The smallest throwaway file timed, in bytes.
    /// </summary>
    public const int MinProbeSize = 1024;

    /// <summary>
    /// The largest throwaway file timed, in bytes.
    /// </summary>
    public const int MaxProbeSize = 1024 * 1024;

    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly IE2eKeyRings keyRings = keyRings ?? throw new ArgumentNullException(nameof(keyRings));
    private readonly ISyncProfileRegistry registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly ISyncService syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
    private readonly IConnectivityService connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
    private readonly ISyncLockService lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    private readonly IS3SyncTargetProvider targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));
    private readonly ISyncScratchStore scratchStore = scratchStore ?? throw new ArgumentNullException(nameof(scratchStore));
    private readonly IEncryptedFileFormat encryptedFileFormat = encryptedFileFormat ?? throw new ArgumentNullException(nameof(encryptedFileFormat));
    private readonly IAeadCipher cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly HashSet<E2eKeyScope> ready = [];

    /// <inheritdoc/>
    public IReadOnlyCollection<E2eKeyScope> ReadyToComplete => [.. ready];

    /// <inheritdoc/>
    public async Task<SyncKeyRotationEstimate> PrepareAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        var name = FindProfileName(scope);
        await SyncAsync(name, null, ct);

        var files = (await registry.GetFileStore(name).ListAsync(ct)).Where(f => !f.IsFolder).ToList();
        if (files.Count == 0)
        {
            return new SyncKeyRotationEstimate(0, 0, TimeSpan.Zero);
        }

        var totalBytes = files.Sum(f => f.Size);
        var size = (int)Math.Clamp(totalBytes / files.Count, MinProbeSize, MaxProbeSize);
        var perFile = await TimeThrowawayFileAsync(scope, size, ct);
        return new SyncKeyRotationEstimate(files.Count, totalBytes, perFile * files.Count);
    }

    /// <inheritdoc/>
    public async Task RotateAsync(E2eKeyScope scope, string currentPassphrase, string newPassphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var name = FindProfileName(scope);
        await profiles.ChangePassphraseAsync(scope, currentPassphrase, newPassphrase, progress, ct);
        await ReencryptAsync(scope, name, progress, ct);
    }

    /// <inheritdoc/>
    public async Task FinishAsync(E2eKeyScope scope, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        var name = FindProfileName(scope);
        await SyncAsync(name, null, ct);
        await ReencryptAsync(scope, name, progress, ct);
    }

    /// <inheritdoc/>
    public async Task CompleteAsync(CancellationToken ct = default)
    {
        foreach (var scope in ready.ToList())
        {
            await manager.CompleteRotationAsync(scope, ct);
            ready.Remove(scope);
        }
    }

    private static async Task ReencryptFileAsync(ISyncFileStore store, string key, CancellationToken ct)
    {
        var file = await store.ReadAsync(key, ct);
        if (file == null)
        {
            return;
        }

        try
        {
            // Its edit time is kept, so an edit made meanwhile on another device still wins a conflict over it.
            await store.WriteAsync(key, file.Content, file.ModifiedAt, ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(file.Content);
        }
    }

    private string FindProfileName(E2eKeyScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        return registry.Profiles.FirstOrDefault(p => p.KeyScope == scope)?.Name
            ?? throw new ArgumentException($"There is no profile '{scope}'.", nameof(scope));
    }

    // A full pass of the profile, which must succeed: offline, or with any file failing, nothing goes on.
    private async Task SyncAsync(string name, IProgress<SyncPassProgress>? progress, CancellationToken ct)
    {
        if (!targetProvider.IsConfigured)
        {
            // Nothing set up to sync with: the files on this device are all there are.
            return;
        }

        if (!(await connectivityService.RecheckAsync(ct)).IsOnline)
        {
            throw new SyncKeyRotationException(SyncErrorCodes.RotationOffline);
        }

        SyncResult result;
        await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + name, ct))
        {
            result = await syncService.SyncAsync(name, verify: false, progress, ct);
        }

        if (result.Failed > 0 || result.Errors.Count > 0)
        {
            throw new SyncKeyRotationException(SyncErrorCodes.RotationSyncFailed);
        }
    }

    private async Task<TimeSpan> TimeThrowawayFileAsync(E2eKeyScope scope, int size, CancellationToken ct)
    {
        // Random bytes under a random key: nothing of the profile's, and never synced.
        var content = RandomNumberGenerator.GetBytes(size);
        var key = RandomNumberGenerator.GetBytes(cipher.KeySize);
        var name = Guid.NewGuid().ToString("N");
        var started = timeProvider.GetTimestamp();
        try
        {
            var encrypted = encryptedFileFormat.Encrypt(content, "throwaway", key, new E2eFileBinding(scope.Id, $"{SyncDefaults.ScratchFolder}/{name}"));
            if (targetProvider.IsConfigured)
            {
                await scratchStore.PutAsync(name, encrypted, ct);
            }

            return timeProvider.GetElapsedTime(started);
        }
        catch (SyncNetworkException ex)
        {
            throw new SyncKeyRotationException(SyncErrorCodes.RotationOffline, ex);
        }
        catch (SyncAccessDeniedException ex)
        {
            throw new SyncKeyRotationException(SyncErrorCodes.RotationSyncFailed, ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (targetProvider.IsConfigured)
            {
                await DeleteThrowawayFileAsync(name);
            }
        }
    }

    private async Task DeleteThrowawayFileAsync(string name)
    {
        try
        {
            await scratchStore.DeleteAsync(name);
        }
        catch (Exception)
        {
            // Random bytes nobody can read, which sync never sees: not worth failing the change for.
        }
    }

    private async Task ReencryptAsync(E2eKeyScope scope, string name, IProgress<E2eProgress>? progress, CancellationToken ct)
    {
        var activeKeyId = keyRings.Get(scope).ActiveKeyId ?? throw new E2eLockedException();
        var store = registry.GetFileStore(name);
        var untracked = registry.GetUntrackedFileStore(name);
        var encrypted = 0;
        var encryptedTotal = 0;
        var uploaded = 0;
        var uploadedTotal = 0;
        for (var round = 1; ; round++)
        {
            var pending = await FindFilesUnderEarlierKeysAsync(store, untracked, activeKeyId, ct);
            if (pending.Count == 0)
            {
                break;
            }

            if (round > MaxRounds)
            {
                throw new SyncKeyRotationException(SyncErrorCodes.RotationSyncFailed);
            }

            // Each file is uploaded once encrypted, so the uploads are counted from the start; the pass puts its own
            // count in once it has planned.
            encryptedTotal += pending.Count;
            var uploadBase = uploaded;
            uploadedTotal = uploadBase + pending.Count;
            progress?.Report(new E2eProgress(E2eStep.ReencryptFiles, false, encrypted, encryptedTotal));
            progress?.Report(new E2eProgress(E2eStep.UploadFiles, false, uploaded, uploadedTotal));
            encrypted = await ReencryptFilesAsync(store, pending, encrypted, encryptedTotal, progress, ct);
            var passProgress = progress == null ? null : new PassProgress(p =>
            {
                uploaded = uploadBase + p.Done;
                uploadedTotal = uploadBase + p.Total;
                progress.Report(new E2eProgress(E2eStep.UploadFiles, false, uploaded, uploadedTotal));
            });
            await SyncAsync(name, passProgress, ct);
            uploaded = uploadedTotal;
        }

        progress?.Report(new E2eProgress(E2eStep.ReencryptFiles, true, encrypted, encryptedTotal));
        progress?.Report(new E2eProgress(E2eStep.UploadFiles, true, uploaded, uploadedTotal));
        ready.Add(scope);
    }

    private async Task<int> ReencryptFilesAsync(ISyncFileStore store, IReadOnlyList<string> pending, int encrypted, int encryptedTotal, IProgress<E2eProgress>? progress, CancellationToken ct)
    {
        foreach (var key in pending)
        {
            await ReencryptFileAsync(store, key, ct);
            encrypted++;
            progress?.Report(new E2eProgress(E2eStep.ReencryptFiles, false, encrypted, encryptedTotal));
        }

        progress?.Report(new E2eProgress(E2eStep.ReencryptFiles, true, encrypted, encryptedTotal));
        return encrypted;
    }

    private async Task<IReadOnlyList<string>> FindFilesUnderEarlierKeysAsync(ISyncFileStore store, ISyncFileStore untracked, string activeKeyId, CancellationToken ct)
    {
        var pending = new List<string>();
        foreach (var file in await store.ListAsync(ct))
        {
            if (file.IsFolder)
            {
                continue;
            }

            var keyId = file.EncryptionKeyId;
            if (keyId == null)
            {
                // Not in the index: the file's own header says.
                var raw = await untracked.ReadAsync(file.Key, ct);
                if (raw == null || !encryptedFileFormat.TryGetKeyId(raw.Content, out keyId))
                {
                    // Gone, or not an encrypted file of the profile (which the profile cannot read either).
                    continue;
                }
            }

            if (!string.Equals(keyId, activeKeyId, StringComparison.Ordinal))
            {
                pending.Add(file.Key);
            }
        }

        return pending;
    }

    /// <summary>
    /// Passes a sync pass's progress on as it is reported, on the caller's context (unlike <see cref="Progress{T}"/>).
    /// </summary>
    private sealed class PassProgress(Action<SyncPassProgress> report)
        : IProgress<SyncPassProgress>
    {
        public void Report(SyncPassProgress value) => report(value);
    }
}
