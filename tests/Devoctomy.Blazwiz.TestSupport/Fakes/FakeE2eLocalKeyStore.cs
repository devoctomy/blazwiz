using System.Text.Json;
using Devoctomy.Blazwiz.Encryption.Interfaces;
using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

/// <summary>
/// An in memory <see cref="IE2eLocalKeyStore"/>. Keeps a serialized copy, like local storage, so callers cannot change
/// what is saved by changing their object afterwards.
/// </summary>
public class FakeE2eLocalKeyStore : IE2eLocalKeyStore
{
    private readonly Dictionary<string, string> json = new Dictionary<string, string>(StringComparer.Ordinal);

    public E2eLocalKeys? Get(E2eKeyScope scope) =>
        json.TryGetValue(scope.Id, out var value) ? JsonSerializer.Deserialize<E2eLocalKeys>(value) : null;

    public Task<E2eLocalKeys?> GetAsync(E2eKeyScope scope) => Task.FromResult(Get(scope));

    public Task SaveAsync(E2eKeyScope scope, E2eLocalKeys keys)
    {
        json[scope.Id] = JsonSerializer.Serialize(keys);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(E2eKeyScope scope)
    {
        json.Remove(scope.Id);
        return Task.CompletedTask;
    }
}
