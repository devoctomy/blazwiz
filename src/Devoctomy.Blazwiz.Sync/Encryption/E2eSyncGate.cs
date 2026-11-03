using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Configuration;
using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync.Encryption;

/// <summary>
/// Holds back passes of the encrypted profiles while encryption is on but this device cannot encrypt (no passkey
/// keys, not set up, turned off elsewhere), or it is not known yet whether it is on, so plaintext is never synced over
/// encrypted files. Other profiles are not affected.
/// </summary>
/// <remarks>
/// Nothing tells this device when another device turns encryption on, so while it is off (or unknown) here keys.json is
/// read again before every pass of an encrypted profile. Passes only run after changes, on focus and on reconnecting, so this is
/// one keys store call per pass.
/// </remarks>
public class E2eSyncGate(IE2eKeyManager keyManager, SyncOptions options)
    : ISyncGate
{
    private readonly IE2eKeyManager keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
    private readonly HashSet<string> profiles = (options ?? throw new ArgumentNullException(nameof(options))).Profiles
        .Where(p => p.IsEncrypted)
        .Select(p => p.Name)
        .ToHashSet(StringComparer.Ordinal);

    /// <inheritdoc/>
    public async Task<bool> CanSyncAsync(string profile, CancellationToken ct = default)
    {
        if (!profiles.Contains(profile))
        {
            return true;
        }

        var status = keyManager.Status;
        if (status is E2eStatus.Disabled or E2eStatus.Unavailable or E2eStatus.Unknown)
        {
            // Off here: check it was not turned on elsewhere, or this device's plaintext could replace another
            // device's ciphertext in S3. Unknown: an earlier check could not reach keys.json, and passes run online.
            status = await RecheckAsync(ct);
        }

        return status is E2eStatus.Unsupported or E2eStatus.Unavailable or E2eStatus.Disabled or E2eStatus.Unlocked;
    }

    private async Task<E2eStatus> RecheckAsync(CancellationToken ct)
    {
        try
        {
            return await keyManager.InitializeAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // keys.json exists but could not be read: hold back rather than sync plaintext.
            return E2eStatus.Unknown;
        }
    }
}
