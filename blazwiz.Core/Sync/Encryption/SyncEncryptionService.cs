using System.Security.Cryptography;
using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Encryption.Interfaces;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="ISyncEncryptionService"/>. Encrypted profiles are synced under their cross tab lock;
/// when encryption is turned on or off, other tabs are told to reload (they then unlock again).
/// </summary>
public sealed class SyncEncryptionService : ISyncEncryptionService, IDisposable
{
    private readonly IE2eKeyManager keyManager;
    private readonly IE2eKeyRing keyRing;
    private readonly ISyncEncryptionSweeper sweeper;
    private readonly IReadOnlyList<EncryptedSyncProfile> profiles;
    private readonly ISyncService syncService;
    private readonly ISyncScheduler scheduler;
    private readonly IConnectivityService connectivityService;
    private readonly ISyncLockService lockService;
    private readonly ISyncStorageChannel channel;

    private bool isBusy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEncryptionService"/> class.
    /// </summary>
    /// <param name="keyManager">Manages the keys.</param>
    /// <param name="keyRing">The unlocked keys.</param>
    /// <param name="sweeper">Encrypts and decrypts the files.</param>
    /// <param name="profiles">The encrypted profiles.</param>
    /// <param name="syncService">Runs the passes.</param>
    /// <param name="scheduler">Asks for passes once encryption allows them.</param>
    /// <param name="connectivityService">Offline mode, and whether the app is online.</param>
    /// <param name="lockService">The cross tab sync lock.</param>
    /// <param name="channel">Tells other tabs to reload.</param>
    public SyncEncryptionService(
        IE2eKeyManager keyManager,
        IE2eKeyRing keyRing,
        ISyncEncryptionSweeper sweeper,
        IEnumerable<EncryptedSyncProfile> profiles,
        ISyncService syncService,
        ISyncScheduler scheduler,
        IConnectivityService connectivityService,
        ISyncLockService lockService,
        ISyncStorageChannel channel)
    {
        this.keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
        this.keyRing = keyRing ?? throw new ArgumentNullException(nameof(keyRing));
        this.sweeper = sweeper ?? throw new ArgumentNullException(nameof(sweeper));
        this.profiles = (profiles ?? throw new ArgumentNullException(nameof(profiles))).ToList();
        this.syncService = syncService ?? throw new ArgumentNullException(nameof(syncService));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.connectivityService = connectivityService ?? throw new ArgumentNullException(nameof(connectivityService));
        this.lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));

        this.keyManager.StatusChanged += OnStatusChanged;
        this.syncService.FilesChanged += OnFilesChanged;
        this.connectivityService.StatusChanged += OnConnectivityChanged;
    }

    /// <inheritdoc/>
    public event EventHandler<E2eStatus>? StatusChanged;

    /// <inheritdoc/>
    public E2eStatus Status => keyManager.Status;

    /// <inheritdoc/>
    public bool HasPasskeyKeys => keyManager.HasKeyEncryptionKey;

    /// <inheritdoc/>
    public async Task<E2eStatus> InitializeAsync(CancellationToken ct = default)
    {
        var status = await keyManager.InitializeAsync(ct);
        if (status == E2eStatus.Unlocked)
        {
            await EncryptArrivalsAsync(null, ct);
        }

        return status;
    }

    /// <inheritdoc/>
    public async Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        await RunExclusiveAsync(async () =>
        {
            Report(progress, E2eStep.Sync, false);
            await SyncEncryptedProfilesAsync(requireComplete: false, ct);
            Report(progress, E2eStep.Sync, true);

            await WhileOfflineAsync(progress, async () =>
            {
                Report(progress, E2eStep.CheckExistingKeys, false);
                if (await keyManager.IsEnabledAsync(ct))
                {
                    await keyManager.InitializeAsync(ct);
                    throw new E2eAlreadyEnabledException();
                }

                Report(progress, E2eStep.CheckExistingKeys, true);

                Report(progress, E2eStep.CheckIndex, false);
                var encrypted = await sweeper.CountEncryptedAsync(ct);
                if (encrypted > 0)
                {
                    // Should never happen with encryption off: stop rather than guess what those files are.
                    throw new InvalidOperationException($"{encrypted} file(s) are already marked as encrypted, so encryption was not turned on.");
                }

                Report(progress, E2eStep.CheckIndex, true);

                await keyManager.EnableAsync(passphrase, progress, ct);
                await sweeper.EncryptAsync(progress, null, ct);
            });
        });

        await channel.PostAsync(SyncStorageChannelMessage.Changed);
    }

    /// <inheritdoc/>
    public async Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default)
    {
        await keyManager.SetUpDeviceAsync(passphrase, ct);
        await EncryptArrivalsAsync(null, ct);
        await scheduler.SyncNowAsync();
    }

    /// <inheritdoc/>
    public Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default)
    {
        return keyManager.ChangePassphraseAsync(currentPassphrase, newPassphrase, ct);
    }

    /// <inheritdoc/>
    public async Task DisableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        await RunExclusiveAsync(async () =>
        {
            Report(progress, E2eStep.VerifyPassphrase, false);
            await keyManager.VerifyPassphraseAsync(passphrase, ct);
            Report(progress, E2eStep.VerifyPassphrase, true);

            // Every encrypted file must be on this device, as nothing can decrypt it once the keys are deleted.
            Report(progress, E2eStep.Sync, false);
            await SyncEncryptedProfilesAsync(requireComplete: true, ct);
            Report(progress, E2eStep.Sync, true);

            await WhileOfflineAsync(progress, async () =>
            {
                keyRing.SetWriteMode(E2eWriteMode.Plaintext);
                try
                {
                    await sweeper.DecryptAsync(progress, false, ct);
                }
                catch (Exception)
                {
                    // Keep the files that are still encrypted readable, and new writes encrypted.
                    keyRing.SetWriteMode(E2eWriteMode.Encrypt);
                    throw;
                }

                Report(progress, E2eStep.DeleteKeys, false);
                await keyManager.RemoveAsync(ct);
                Report(progress, E2eStep.DeleteKeys, true);
            });
        });

        await channel.PostAsync(SyncStorageChannelMessage.Changed);
    }

    /// <inheritdoc/>
    public async Task ResolveDisabledElsewhereAsync(string? passphrase = null, CancellationToken ct = default)
    {
        await RunExclusiveAsync(async () =>
        {
            // Encrypted files already synced are replaced by the decrypted copies on the next sync; only changes made
            // here and not yet synced need the keys.
            if (await sweeper.CountPendingEncryptedAsync(ct) > 0)
            {
                await keyManager.UnlockLocalCopyAsync(passphrase, ct);
                await sweeper.DecryptAsync(null, pendingOnly: true, ct);
            }

            await keyManager.ForgetDeviceAsync();
        });

        await scheduler.SyncNowAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> ResolveNeedsPassphraseAsync(CancellationToken ct = default)
    {
        return await sweeper.CountPendingEncryptedAsync(ct) > 0 && !await keyManager.CanUnlockLocalCopyWithPasskeyAsync();
    }

    /// <inheritdoc/>
    public bool CanDecrypt(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        if (!keyRing.TryGetFileKey(keyId, out var key))
        {
            return false;
        }

        CryptographicOperations.ZeroMemory(key);
        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        keyManager.StatusChanged -= OnStatusChanged;
        syncService.FilesChanged -= OnFilesChanged;
        connectivityService.StatusChanged -= OnConnectivityChanged;
    }

    private static void Report(IProgress<E2eProgress>? progress, E2eStep step, bool completed)
    {
        progress?.Report(new E2eProgress(step, completed));
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        if (isBusy)
        {
            throw new InvalidOperationException("End to end encryption is already being changed.");
        }

        isBusy = true;
        try
        {
            await action();
        }
        finally
        {
            isBusy = false;
        }
    }

    // Offline mode stops passes (in every tab) while files change; it is put back as it was afterwards, and going back
    // online syncs the changed files.
    private async Task WhileOfflineAsync(IProgress<E2eProgress>? progress, Func<Task> action)
    {
        var wasOffline = connectivityService.IsForcedOffline;
        Report(progress, E2eStep.GoOffline, false);
        await connectivityService.SetForcedOfflineAsync(true);
        Report(progress, E2eStep.GoOffline, true);
        try
        {
            await action();
        }
        finally
        {
            Report(progress, E2eStep.GoOnline, false);
            if (!wasOffline)
            {
                await connectivityService.SetForcedOfflineAsync(false);
            }

            Report(progress, E2eStep.GoOnline, true);
        }
    }

    private async Task SyncEncryptedProfilesAsync(bool requireComplete, CancellationToken ct)
    {
        if (!(await connectivityService.CheckAsync(ct)).IsOnline)
        {
            if (requireComplete)
            {
                throw new InvalidOperationException("Go online first, so every encrypted file is on this device before it is decrypted.");
            }

            // Anything not on this device yet is encrypted when it arrives.
            return;
        }

        foreach (var profile in profiles)
        {
            await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile.Name, ct))
            {
                var result = await syncService.SyncAsync(profile.Name, verify: false, ct);
                if (requireComplete && (result.Failed > 0 || result.Errors.Count > 0))
                {
                    throw new InvalidOperationException("Sync did not finish, so encryption was left on. Try again once it syncs.");
                }
            }
        }
    }

    // Files can arrive unencrypted from a device that was not set up yet: encrypt them here.
    private async Task EncryptArrivalsAsync(string? profile, CancellationToken ct)
    {
        if (isBusy || keyManager.Status != E2eStatus.Unlocked || keyRing.WriteMode != E2eWriteMode.Encrypt)
        {
            return;
        }

        try
        {
            await sweeper.EncryptAsync(null, profile, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Tried again after the next download or sign in.
        }
    }

    private void OnStatusChanged(object? sender, E2eStatus e)
    {
        StatusChanged?.Invoke(this, e);
    }

    private async void OnFilesChanged(object? sender, SyncFilesChangedEventArgs e)
    {
        try
        {
            if (profiles.Any(p => p.Name == e.Profile))
            {
                await EncryptArrivalsAsync(e.Profile, CancellationToken.None);
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }

    // Whether encryption is on could not be worked out while offline: try again once online, and sync if it allows.
    private async void OnConnectivityChanged(object? sender, ConnectivityStatus e)
    {
        try
        {
            if (e.IsOnline && !isBusy && keyManager.Status == E2eStatus.Unknown)
            {
                var status = await InitializeAsync();
                if (status != E2eStatus.Unknown)
                {
                    await scheduler.SyncNowAsync();
                }
            }
        }
        catch (Exception)
        {
            // Event handlers must not throw.
        }
    }
}
