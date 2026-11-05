using Devoctomy.Blazwiz.Encryption.Interfaces;

namespace Devoctomy.Blazwiz.TestSupport.Fakes;

public class FakeE2eProfileIndexStore : IE2eProfileIndexStore
{
    public string? Value { get; set; }

    public Exception? Failure { get; set; }

    public int SetCount { get; private set; }

    public Task<string?> GetIndexAsync(CancellationToken ct = default)
    {
        ThrowIfFailing();
        return Task.FromResult(Value);
    }

    public Task SetIndexAsync(string value, CancellationToken ct = default)
    {
        SetCount++;
        ThrowIfFailing();
        Value = value;
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
