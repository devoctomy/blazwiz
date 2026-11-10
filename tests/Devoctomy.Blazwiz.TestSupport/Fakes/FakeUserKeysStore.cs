using System.Security.Cryptography;
using System.Text;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

/// <summary>
/// An in memory <see cref="IUserKeysStore"/> that can be made to fail. Each value's ETag is a hash of it, as S3's is,
/// so changing <see cref="Values"/> directly changes the ETag too, and writes are conditional on it.
/// </summary>
public class FakeUserKeysStore : IUserKeysStore
{
    public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Exception? Failure { get; set; }

    /// <summary>
    /// Gets or sets what happens just before each conditional write is checked, e.g. another device writing the same
    /// document, so the write meets a changed document.
    /// </summary>
    public Action<E2eKeyScope>? BeforeWrite { get; set; }

    public int GetCount { get; private set; }

    public int SetCount { get; private set; }

    public int CreateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public int ConflictCount { get; private set; }

    public static string ETagOf(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public Task<StoredDocument?> GetAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        GetCount++;
        ThrowIfFailing();
        return Task.FromResult(Values.TryGetValue(scope.Id, out var value) ? new StoredDocument(value, ETagOf(value)) : null);
    }

    public Task<string> SetAsync(E2eKeyScope scope, string value, string ifMatch, CancellationToken ct = default)
    {
        SetCount++;
        ThrowIfFailing();
        BeforeWrite?.Invoke(scope);
        if (!Values.TryGetValue(scope.Id, out var current) || ETagOf(current) != ifMatch)
        {
            ConflictCount++;
            throw new E2eDocumentChangedException();
        }

        Values[scope.Id] = value;
        return Task.FromResult(ETagOf(value));
    }

    public Task<string?> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        CreateCount++;
        ThrowIfFailing();
        BeforeWrite?.Invoke(scope);
        if (Values.ContainsKey(scope.Id))
        {
            return Task.FromResult<string?>(null);
        }

        Values[scope.Id] = value;
        return Task.FromResult<string?>(ETagOf(value));
    }

    public Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        DeleteCount++;
        ThrowIfFailing();
        Values.Remove(scope.Id);
        return Task.CompletedTask;
    }

    private void ThrowIfFailing()
    {
        if (Failure != null)
        {
            throw Failure;
        }
    }
}
