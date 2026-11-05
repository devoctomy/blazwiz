using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

/// <summary>
/// An in memory <see cref="IUserKeysStore"/> that can be made to fail.
/// </summary>
public class FakeUserKeysStore : IUserKeysStore
{
    public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.Ordinal);


    public Exception? Failure { get; set; }

    public int GetCount { get; private set; }

    public int SetCount { get; private set; }

    public int CreateCount { get; private set; }

    public int DeleteCount { get; private set; }

    public Task<string?> GetAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        GetCount++;
        ThrowIfFailing();
        return Task.FromResult(Values.GetValueOrDefault(scope.Id));
    }

    public Task SetAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        SetCount++;
        ThrowIfFailing();
        Set(scope, value);
        return Task.CompletedTask;
    }

    public Task<bool> TryCreateAsync(E2eKeyScope scope, string value, CancellationToken ct = default)
    {
        CreateCount++;
        ThrowIfFailing();
        if (Values.ContainsKey(scope.Id))
        {
            return Task.FromResult(false);
        }

        Set(scope, value);
        return Task.FromResult(true);
    }

    public Task DeleteAsync(E2eKeyScope scope, CancellationToken ct = default)
    {
        DeleteCount++;
        ThrowIfFailing();
        Set(scope, null);
        return Task.CompletedTask;
    }

    private void Set(E2eKeyScope scope, string? value)
    {
        if (value == null)
        {
            Values.Remove(scope.Id);
        }
        else
        {
            Values[scope.Id] = value;
        }
    }

    private void ThrowIfFailing()
    {
        if (Failure != null)
        {
            throw Failure;
        }
    }
}
