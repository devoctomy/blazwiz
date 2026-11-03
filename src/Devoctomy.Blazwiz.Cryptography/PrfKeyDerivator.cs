using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Interfaces;

namespace Devoctomy.Blazwiz.Cryptography;

/// <summary>
/// Implementation of <see cref="IPrfKeyDerivator"/>. Each key is derived with HKDF from the PRF output, salted with
/// the application ID, with "{application name}:{key name}" as the info, so every key differs per name and per app.
/// </summary>
public class PrfKeyDerivator(
    PrfKeyDerivationOptions options,
    IKeyDerivationService keyDerivationService,
    IDerivedKeyStore keyStore)
    : IPrfKeyDerivator
{
    private readonly PrfKeyDerivationOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly IKeyDerivationService keyDerivationService = keyDerivationService ?? throw new ArgumentNullException(nameof(keyDerivationService));
    private readonly IDerivedKeyStore keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));

    /// <inheritdoc/>
    public void DeriveKeys(byte[] prfOutput)
    {
        ArgumentNullException.ThrowIfNull(prfOutput);
        if (prfOutput.Length == 0)
        {
            throw new ArgumentException("The PRF output must not be empty.", nameof(prfOutput));
        }

        if (string.IsNullOrWhiteSpace(options.ApplicationName) || options.ApplicationId == Guid.Empty)
        {
            throw new InvalidOperationException("The application name and ID must be set with UseApplication before keys can be derived.");
        }

        var salt = Encoding.UTF8.GetBytes(options.ApplicationId.ToString("D"));
        foreach (var keyOptions in options.Keys)
        {
            var info = Encoding.UTF8.GetBytes($"{options.ApplicationName}:{keyOptions.Name}");
            var key = keyDerivationService.DeriveKey(prfOutput, salt, info, keyOptions.Length);
            try
            {
                keyStore.SetKey(keyOptions.Name, key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
            }
        }
    }
}
