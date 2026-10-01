using System.Net;
using Amazon.CognitoIdentityProvider;
using blazwiz.Portal.Application.Services;
using Moq;
using Moq.Protected;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class BrowserHttpClientFactoryTests
{
    private readonly Mock<HttpMessageHandler> mockInner = new();
    private readonly AmazonCognitoIdentityProviderConfig clientConfig = new();

    [Fact]
    public void GivenNullHandler_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new BrowserHttpClientFactory(null!));
        Assert.Equal("handler", exception.ParamName);
    }

    [Fact]
    public void GivenFactory_WhenCreatingHttpClients_ThenSharesOneUncachedClient()
    {
        // Arrange
        using var handler = new BufferingHttpHandler(mockInner.Object);
        using var sut = new BrowserHttpClientFactory(handler);

        // Act
        var first = sut.CreateHttpClient(clientConfig);
        var second = sut.CreateHttpClient(clientConfig);

        // Assert
        Assert.Same(first, second);
        Assert.False(sut.UseSDKHttpClientCaching(clientConfig));
        Assert.False(sut.DisposeHttpClientsAfterUse(clientConfig));
    }

    [Fact]
    public async Task GivenFactory_WhenClientSendsRequest_ThenGoesThroughHandler()
    {
        // Arrange
        mockInner
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var handler = new BufferingHttpHandler(mockInner.Object);
        using var sut = new BrowserHttpClientFactory(handler);

        // Act
        using var response = await sut.CreateHttpClient(clientConfig).GetAsync(new Uri("https://cognito-idp.eu-west-2.amazonaws.com/"));

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockInner.Protected().Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GivenFactoryDisposed_WhenUsingHandler_ThenHandlerStillWorks()
    {
        // Arrange - the handler belongs to the dependency injection container, not the factory.
        mockInner
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        using var handler = new BufferingHttpHandler(mockInner.Object);
        var sut = new BrowserHttpClientFactory(handler);
        var client = sut.CreateHttpClient(clientConfig);

        // Act
        sut.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.GetAsync(new Uri("https://cognito-idp.eu-west-2.amazonaws.com/")));
        using var invoker = new HttpMessageInvoker(handler, disposeHandler: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://cognito-idp.eu-west-2.amazonaws.com/");
        using var response = await invoker.SendAsync(request, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
