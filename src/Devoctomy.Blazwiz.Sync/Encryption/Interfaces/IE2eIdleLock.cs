using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Sync.Encryption.Interfaces;

/// <summary>
/// Locks a protected profile when its files have not been read or written for its idle timeout
/// (<see cref="E2eProfileSettings.IdleTimeout"/>).
/// </summary>
public interface IE2eIdleLock
{
    /// <summary>
    /// Starts timing the profiles as they are unlocked. Safe to call more than once.
    /// </summary>
    void Start();

    /// <summary>
    /// Records that a profile's files were read or written, restarting its timer.
    /// </summary>
    /// <param name="scope">The profile's scope.</param>
    void Touch(E2eKeyScope scope);
}
