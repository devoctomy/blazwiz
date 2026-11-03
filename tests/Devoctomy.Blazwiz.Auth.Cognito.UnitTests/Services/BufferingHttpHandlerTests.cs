using System.Net;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Moq;
using Moq.Protected;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class BufferingHttpHandlerTests
{
    private readonly Mock<HttpMessageHandler> mockInner = new();

    [Fact]
    public void GivenNullInnerHandler_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => new BufferingHttpHandler(null!));

        // Assert
        Assert.Equal("innerHandler", exception.ParamName);
    }

    [Fact]
    public async Task GivenResponse_WhenSendAsync_ThenBodyIsBufferedBeforeReturning()
    {
        // Arrange - once the response is returned the body stream is gone, so it can only be read from the buffer.
        var body = new MemoryStream("hello"u8.ToArray());
        SetupInnerResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });
        using var invoker = new HttpMessageInvoker(new BufferingHttpHandler(mockInner.Object));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://cognito-idp.eu-west-2.amazonaws.com/");

        // Act
        using var response = await invoker.SendAsync(request, CancellationToken.None);
        await body.DisposeAsync();

        // Assert - read synchronously, as the AWS SDK does.
        using var reader = new StreamReader(response.Content.ReadAsStream());
        Assert.Equal("hello", reader.ReadToEnd());
    }

    [Fact]
    public async Task GivenRequest_WhenSendAsync_ThenPassesRequestAndTokenToInnerHandler()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        SetupInnerResponse(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var invoker = new HttpMessageInvoker(new BufferingHttpHandler(mockInner.Object));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://cognito-idp.eu-west-2.amazonaws.com/");

        // Act
        using var response = await invoker.SendAsync(request, cancellation.Token);

        // Assert
        mockInner.Protected().Verify("SendAsync", Times.Once(), request, cancellation.Token);
    }

    private void SetupInnerResponse(HttpResponseMessage response)
    {
        mockInner
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
