using Devoctomy.Blazwiz.Encryption.Interfaces;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

public class FakeE2eLocalProfileIndexStore : IE2eLocalProfileIndexStore
{
    public string? Value { get; set; }

    public Task<string?> GetAsync() => Task.FromResult(Value);

    public Task SaveAsync(string value)
    {
        Value = value;
        return Task.CompletedTask;
    }
}
