namespace Devoctomy.Blazwiz.Auth.Cognito.Services;

/// <summary>
/// Reads each response body into memory asynchronously, so later synchronous reads come from the buffer. The AWS SDK
/// reads responses synchronously, which the browser does not allow.
/// </summary>
public sealed class BufferingHttpHandler : DelegatingHandler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BufferingHttpHandler"/> class.
    /// </summary>
    /// <param name="innerHandler">The handler that sends the requests.</param>
    public BufferingHttpHandler(HttpMessageHandler innerHandler)
        : base(innerHandler ?? throw new ArgumentNullException(nameof(innerHandler)))
    {
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        await response.Content.LoadIntoBufferAsync(cancellationToken);
        return response;
    }
}
