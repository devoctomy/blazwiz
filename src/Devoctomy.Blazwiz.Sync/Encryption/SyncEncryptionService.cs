using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Encryption.Interfaces;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Implementation of <see cref="ISyncEncryptionService"/> over <see cref="ISyncProtectedProfileService"/>: the profiles
/// set up in code (<see cref="SyncOptions.Profiles"/>), each with its own keys, set, unlocked and changed with one
/// passphrase.
/// </summary>
/// <param name="profiles">Sets up and changes the profiles' keys.</param>
/// <param name="manager">Unlocks the profiles and says when they lock.</param>
/// <param name="keyRings">The profiles' unlocked keys.</param>
/// <param name="options">The profiles set up in code.</param>
/// <param name="syncScheduler">Syncs once the profiles are unlocked.</param>
public class SyncEncryptionService(
    ISyncProtectedProfileService profiles,
    IE2eProtectedProfileManager manager,
    IE2eKeyRings keyRings,
    SyncOptions options,
    ISyncScheduler syncScheduler)
    : ISyncEncryptionService, IDisposable
{
    private readonly ISyncProtectedProfileService profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    private readonly IE2eProtectedProfileManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly IE2eKeyRings keyRings = keyRings ?? throw new ArgumentNullException(nameof(keyRings));
    private readonly SyncOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ISyncScheduler syncScheduler = syncScheduler ?? throw new ArgumentNullException(nameof(syncScheduler));

    private E2eStatus status = E2eStatus.Unknown;
    private bool listening;

    /// <inheritdoc/>
    public event EventHandler<E2eStatus>? StatusChanged;

    /// <inheritdoc/>
    public E2eStatus Status => status;

    private IEnumerable<E2eKeyScope> Scopes => options.Profiles.Select(p => p.KeyScope!);

    /// <inheritdoc/>
    public async Task<E2eStatus> InitializeAsync(CancellationToken ct = default)
    {
        if (!listening)
        {
            // Locked by the idle timer, another tab or sign out: the banner asks for the passphrase again.
            listening = true;
            manager.LockStateChanged += OnLockStateChanged;
        }

        try
        {
            foreach (var scope in Scopes)
            {
                if (!await profiles.IsSetUpAsync(scope, ct))
                {
                    return SetStatus(E2eStatus.Disabled);
                }
            }
        }
        catch (InvalidOperationException)
        {
            return SetStatus(E2eStatus.Unsupported);
        }

        return SetStatus(LockedOrUnlocked());
    }

    /// <inheritdoc/>
    public async Task EnableAsync(string passphrase, IProgress<E2eProgress>? progress = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        foreach (var scope in Scopes)
        {
            if (await profiles.IsSetUpAsync(scope, ct))
            {
                if (!manager.IsUnlocked(scope))
                {
                    await manager.UnlockAsync(scope, passphrase, ct);
                }
            }
            else
            {
                await profiles.SetUpAsync(scope, passphrase, progress, ct);
            }
        }

        SetStatus(LockedOrUnlocked());
        await syncScheduler.SyncNowAsync();
    }

    /// <inheritdoc/>
    public async Task SetUpDeviceAsync(string passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        foreach (var scope in Scopes.Where(s => !manager.IsUnlocked(s)))
        {
            await manager.UnlockAsync(scope, passphrase, ct);
        }

        SetStatus(LockedOrUnlocked());
        await syncScheduler.SyncNowAsync();
    }

    /// <inheritdoc/>
    public async Task ChangePassphraseAsync(string currentPassphrase, string newPassphrase, CancellationToken ct = default)
    {
        foreach (var scope in Scopes)
        {
            await profiles.ChangePassphraseAsync(scope, currentPassphrase, newPassphrase, ct);
        }
    }

    /// <inheritdoc/>
    public bool CanDecrypt(string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        foreach (var scope in Scopes)
        {
            if (keyRings.Get(scope).TryGetFileKey(keyId, out var key))
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        manager.LockStateChanged -= OnLockStateChanged;
    }

    private E2eStatus LockedOrUnlocked() => Scopes.All(manager.IsUnlocked) ? E2eStatus.Unlocked : E2eStatus.NeedsSetup;

    private void OnLockStateChanged(object? sender, E2eKeyScope scope)
    {
        if (status is E2eStatus.Unlocked or E2eStatus.NeedsSetup && Scopes.Contains(scope))
        {
            SetStatus(LockedOrUnlocked());
        }
    }

    private E2eStatus SetStatus(E2eStatus value)
    {
        if (status != value)
        {
            status = value;
            StatusChanged?.Invoke(this, value);
        }

        return value;
    }
}
