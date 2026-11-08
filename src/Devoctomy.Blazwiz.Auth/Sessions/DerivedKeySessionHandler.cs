using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Cryptography.Interfaces;

namespace Devoctomy.Blazwiz.Auth.Sessions;

/// <summary>
/// Wipes every key in the <see cref="IDerivedKeyStore"/> (the keys derived from the passkey, and anything else held
/// there) once the user has signed out, so they do not outlive the session in any app, whatever page it shows next.
/// Runs last, after the modules have locked what they hold.
/// </summary>
/// <param name="keyStore">The keys to wipe.</param>
public class DerivedKeySessionHandler(IDerivedKeyStore keyStore)
    : ISessionLifecycleHandler
{
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public int Order => int.MaxValue;

    /// <inheritdoc/>
    public Task OnSignedOutAsync()
    {
        keyStore.Clear();
        return Task.CompletedTask;
    }
}
