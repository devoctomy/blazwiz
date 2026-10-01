using Amazon.Runtime;

namespace blazwiz.Portal.Application.Services;

/// <summary>
/// Gives the AWS SDK an <see cref="HttpClient"/> that works in the browser. The SDK's default handler sets options
/// (e.g. connection limits) the browser's fetch based handler does not support, so every SDK client shares one
/// <see cref="HttpClient"/> built on a <see cref="BufferingHttpHandler"/>.
/// </summary>
public sealed class BrowserHttpClientFactory : HttpClientFactory, IDisposable
{
    private readonly HttpClient httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="BrowserHttpClientFactory"/> class.
    /// </summary>
    /// <param name="handler">The handler the shared client sends requests through. It is not disposed with the factory.</param>
    public BrowserHttpClientFactory(BufferingHttpHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        httpClient = new HttpClient(handler, disposeHandler: false);
    }

    /// <inheritdoc/>
    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => httpClient;

    /// <inheritdoc/>
    public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

    /// <inheritdoc/>
    public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;

    /// <inheritdoc/>
    public void Dispose() => httpClient.Dispose();
}
