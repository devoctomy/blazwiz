namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A key scope: one keys document, one passphrase, one set of keys in memory and one lock state. The account's end to
/// end encryption is <see cref="Account"/>.
/// </summary>
public sealed record E2eKeyScope
{
    /// <summary>
    /// The id of <see cref="Account"/>.
    /// </summary>
    public const string AccountId = "account";

    /// <summary>
    /// The longest id allowed.
    /// </summary>
    public const int MaxIdLength = 64;

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eKeyScope"/> class.
    /// </summary>
    /// <param name="id">The id: 1 to <see cref="MaxIdLength"/> ASCII letters, digits, <c>-</c> or <c>_</c>. It names the
    /// scope's keys document and its keys in memory.</param>
    /// <exception cref="ArgumentException">The id is not valid.</exception>
    public E2eKeyScope(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        if (id.Length > MaxIdLength || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            throw new ArgumentException($"A key scope id is 1 to {MaxIdLength} ASCII letters, digits, '-' or '_'.", nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// Gets the account's scope, used by profiles configured with <c>UseEncryption()</c>.
    /// </summary>
    public static E2eKeyScope Account { get; } = new E2eKeyScope(AccountId);

    /// <summary>
    /// Gets the id.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets a value indicating whether this is the account's scope.
    /// </summary>
    public bool IsAccount => Id == AccountId;

    /// <inheritdoc/>
    public override string ToString() => Id;
}
