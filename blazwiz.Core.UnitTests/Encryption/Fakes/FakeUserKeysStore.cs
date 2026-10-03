using blazwiz.Core.Cryptography.Interfaces;

namespace blazwiz.Core.UnitTests.Encryption.Fakes;

/// <summary>
/// An in memory <see cref="IUserKeysStore"/> that can be made to fail.
/// </summary>
public class FakeUserKeysStore : IUserKeysStore
{
    public string? Value { get; set; }

    public Exception? Failure { get; set; }

    public int GetCount { get; private set; }

    public int SetCount { get; private set; }

    public int CreateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public Task<string?> GetAsync(CancellationToken ct = default)
    {
        GetCount++;
        ThrowIfFailing();
        return Task.FromResult(Value);
    }

    public Task SetAsync(string value, CancellationToken ct = default)
    {
        SetCount++;
        ThrowIfFailing();
        Value = value;
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateAsync(string value, CancellationToken ct = default)
    {
        CreateCount++;
        ThrowIfFailing();
        if (Value != null)
        {
            return Task.FromResult(false);
        }

        Value = value;
        return Task.FromResult(true);
    }

    public Task DeleteAsync(CancellationToken ct = default)
    {
        DeleteCount++;
        ThrowIfFailing();
        Value = null;
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
