namespace Devoctomy.Blazwiz.Encryption.Models;

/// <summary>
/// A key scope: one profile's keys document, passphrase, keys in memory and lock state.
/// </summary>
public sealed record E2eKeyScope
{
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
    /// Gets the id.
    /// </summary>
    public string Id { get; }

    /// <inheritdoc/>
    public override string ToString() => Id;
}
