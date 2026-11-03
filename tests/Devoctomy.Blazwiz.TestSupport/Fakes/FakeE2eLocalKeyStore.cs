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
    private string? json;

    public E2eLocalKeys? Saved => json == null ? null : JsonSerializer.Deserialize<E2eLocalKeys>(json);

    public Task<E2eLocalKeys?> GetAsync() => Task.FromResult(Saved);

    public Task SaveAsync(E2eLocalKeys keys)
    {
        json = JsonSerializer.Serialize(keys);
        return Task.CompletedTask;
    }

    public Task DeleteAsync()
    {
        json = null;
        return Task.CompletedTask;
    }
}
