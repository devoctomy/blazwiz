using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Interfaces;

namespace blazwiz.Core.Sync.Encryption;

/// <summary>
/// Holds back passes of the encrypted profiles while encryption is on but this device cannot encrypt (no passkey
/// keys, not set up, turned off elsewhere), or it is not known yet whether it is on, so plaintext is never synced over
/// encrypted files. Other profiles are not affected.
/// </summary>
public class E2eSyncGate(IE2eKeyManager keyManager, SyncOptions options)
    : ISyncGate
{
    private readonly IE2eKeyManager keyManager = keyManager ?? throw new ArgumentNullException(nameof(keyManager));
    private readonly HashSet<string> profiles = (options ?? throw new ArgumentNullException(nameof(options))).Profiles
        .Where(p => p.IsEncrypted)
        .Select(p => p.Name)
        .ToHashSet(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool CanSync(string profile)
    {
        return !profiles.Contains(profile) || keyManager.Status is E2eStatus.Unsupported or E2eStatus.Unavailable or E2eStatus.Disabled or E2eStatus.Unlocked;
    }
}
