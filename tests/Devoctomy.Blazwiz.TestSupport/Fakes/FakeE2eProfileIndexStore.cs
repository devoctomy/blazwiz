using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

/// <summary>
/// An in memory <see cref="IE2eProfileIndexStore"/> that can be made to fail. The ETag is a hash of the value, as S3's
/// is, so setting <see cref="Value"/> directly changes it too, and writes are conditional on it.
/// </summary>
public class FakeE2eProfileIndexStore : IE2eProfileIndexStore
{
    public string? Value { get; set; }

    public Exception? Failure { get; set; }

    /// <summary>
    /// Gets or sets what happens just before each conditional write is checked, e.g. another device writing the index,
    /// so the write meets a changed index.
    /// </summary>
    public Action? BeforeWrite { get; set; }

    public int SetCount { get; private set; }

    public int ConflictCount { get; private set; }

    public Task<StoredDocument?> GetIndexAsync(CancellationToken ct = default)
    {
        ThrowIfFailing();
        return Task.FromResult(Value == null ? null : new StoredDocument(Value, FakeUserKeysStore.ETagOf(Value)));
    }

    public Task<string> SetIndexAsync(string value, string? ifMatch, CancellationToken ct = default)
    {
        SetCount++;
        ThrowIfFailing();
        BeforeWrite?.Invoke();
        var current = Value == null ? null : FakeUserKeysStore.ETagOf(Value);
        if (current != ifMatch)
        {
            ConflictCount++;
            throw new E2eDocumentChangedException();
        }

        Value = value;
        return Task.FromResult(FakeUserKeysStore.ETagOf(value));
    }

    private void ThrowIfFailing()
    {
        if (Failure != null)
        {
            throw Failure;
        }
    }
}
