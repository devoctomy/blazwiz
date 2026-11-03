using Devoctomy.Blazwiz.Auth.Cognito.Exceptions;
using Devoctomy.Blazwiz.Auth.Cognito.Services;
using Devoctomy.Blazwiz.Auth.Cognito.Services.Interfaces;
using Devoctomy.Blazwiz.Auth.Models;
using Moq;

namespace Devoctomy.Blazwiz.Auth.Cognito.UnitTests.Services;

public class AwsCredentialsSessionHandlerTests
{
    private readonly Mock<IAwsCredentialsProvider> mockCredentials = new();
    private readonly UserSession session = new() { Username = "bob@example.com" };

    public static TheoryData<Exception> CredentialFailures() =>
    [
        new CognitoException("NotAuthorizedException", "no"),
        new HttpRequestException(),
    ];

    [Fact]
    public void GivenNullProvider_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("credentialsProvider", Assert.Throws<ArgumentNullException>(() => new AwsCredentialsSessionHandler(null!)).ParamName);
    }

    [Fact]
    public async Task GivenSessionStarts_WhenOnSessionStarted_ThenAwsCredentialsFetched()
    {
        // Act
        await new AwsCredentialsSessionHandler(mockCredentials.Object).OnSessionStartedAsync(session);

        // Assert
        mockCredentials.Verify(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [MemberData(nameof(CredentialFailures))]
    public async Task GivenCredentialsUnavailable_WhenOnSessionStarted_ThenNoException(Exception failure)
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        // Act
        var exception = await Record.ExceptionAsync(() => new AwsCredentialsSessionHandler(mockCredentials.Object).OnSessionStartedAsync(session));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task GivenSessionStillFetching_WhenOnSessionStarted_ThenDoesNotWait()
    {
        // Arrange
        mockCredentials.Setup(x => x.GetCredentialsAsync(It.IsAny<CancellationToken>())).Returns(new TaskCompletionSource<Devoctomy.Blazwiz.Auth.Cognito.Models.AwsTemporaryCredentials?>().Task);

        // Act
        var task = new AwsCredentialsSessionHandler(mockCredentials.Object).OnSessionStartedAsync(session);

        // Assert
        Assert.True(task.IsCompletedSuccessfully);
        await task;
    }

    [Fact]
    public async Task GivenSignedOut_WhenOnSignedOut_ThenCredentialsForgotten()
    {
        // Act
        await new AwsCredentialsSessionHandler(mockCredentials.Object).OnSignedOutAsync();

        // Assert
        mockCredentials.Verify(x => x.Clear(), Times.Once);
    }
}
