using Devoctomy.Blazwiz.Testing;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.Core.Exceptions;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.UI.Text;

namespace Devoctomy.Blazwiz.UI.UnitTests.Text;

public class ErrorTextTests
{
    private readonly FrameworkText testText = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var factory = new OverridingStringLocalizerFactory(testText.Options);

        // Act & Assert
        Assert.Equal("localizerFactory", Assert.Throws<ArgumentNullException>(() => new ErrorText(null!, [], testText.Plurals)).ParamName);
        Assert.Equal("sources", Assert.Throws<ArgumentNullException>(() => new ErrorText(factory, null!, testText.Plurals)).ParamName);
        Assert.Equal("plurals", Assert.Throws<ArgumentNullException>(() => new ErrorText(factory, [], null!)).ParamName);
        Assert.Throws<ArgumentNullException>(() => new ErrorTextSource(null!));
    }

    [Fact]
    public void GivenFrameworkCode_WhenDescribe_ThenFoundInTheUiText()
    {
        // Act & Assert
        Assert.Equal("Incorrect email or password.", testText.Errors.Describe(AuthErrorCodes.NotAuthorized, []));
    }

    [Fact]
    public void GivenUncodedExceptionOrUnknownCode_WhenDescribe_ThenGeneralTextNeverTheMessage()
    {
        // Act & Assert
        Assert.Equal("Something went wrong.", testText.Errors.Describe(new InvalidOperationException("internal detail")));
        Assert.Equal("Something went wrong.", testText.Errors.Describe(new BlazwizException("No.Such.Code")));
    }

    [Fact]
    public void GivenBadArguments_WhenDescribe_ThenThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => testText.Errors.Describe((Exception)null!));
        Assert.Throws<ArgumentException>(() => testText.Errors.Describe(" ", []));
        Assert.Throws<ArgumentNullException>(() => testText.Errors.Describe("Code", null!));
    }

    [Fact]
    public void GivenCodedException_WhenCreated_ThenCodeArgumentsAndCauseKept()
    {
        // Arrange
        var cause = new InvalidOperationException("cause");
        var sut = new BlazwizException("Some.Code", cause, 1, "two");

        // Act & Assert
        Assert.Equal("Some.Code", sut.Code);
        Assert.Equal("Some.Code", sut.Message);
        Assert.Same(cause, sut.InnerException);
        Assert.Equal([1, "two"], sut.Arguments);
        Assert.Throws<ArgumentException>(() => new BlazwizException(" "));
    }

    [Fact]
    public void GivenAppOverrideOfAFrameworkError_WhenDescribe_ThenOverrideUsed()
    {
        // Act
        new TextBuilder(testText.Options).Override("Error.Auth.NotAuthorized", "Wrong details.");

        // Assert
        Assert.Equal("Wrong details.", testText.Errors.Describe(AuthErrorCodes.NotAuthorized, []));
    }
}
