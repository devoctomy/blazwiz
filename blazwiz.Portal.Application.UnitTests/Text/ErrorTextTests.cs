using blazwiz.Core.Exceptions;
using blazwiz.Core.Text;
using blazwiz.Portal.Application.Exceptions;
using blazwiz.Portal.Application.Text;

namespace blazwiz.Portal.Application.UnitTests.Text;

public class ErrorTextTests
{
    private readonly TestText testText = new();

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        var factory = new OverridingStringLocalizerFactory(testText.Options);
        Assert.Equal("localizerFactory", Assert.Throws<ArgumentNullException>(() => new ErrorText(null!, [], testText.Plurals)).ParamName);
        Assert.Equal("sources", Assert.Throws<ArgumentNullException>(() => new ErrorText(factory, null!, testText.Plurals)).ParamName);
        Assert.Equal("plurals", Assert.Throws<ArgumentNullException>(() => new ErrorText(factory, [], null!)).ParamName);
        Assert.Throws<ArgumentNullException>(() => new ErrorTextSource(null!));
    }

    [Fact]
    public void GivenFrameworkCode_WhenDescribe_ThenFoundInTheUiText()
    {
        Assert.Equal("Incorrect email or password.", testText.Errors.Describe(AuthErrorCodes.NotAuthorized, []));
    }

    [Fact]
    public void GivenUncodedExceptionOrUnknownCode_WhenDescribe_ThenGeneralTextNeverTheMessage()
    {
        Assert.Equal("Something went wrong.", testText.Errors.Describe(new InvalidOperationException("internal detail")));
        Assert.Equal("Something went wrong.", testText.Errors.Describe(new BlazwizException("No.Such.Code")));
    }

    [Fact]
    public void GivenBadArguments_WhenDescribe_ThenThrows()
    {
        Assert.Throws<ArgumentNullException>(() => testText.Errors.Describe((Exception)null!));
        Assert.Throws<ArgumentException>(() => testText.Errors.Describe(" ", []));
        Assert.Throws<ArgumentNullException>(() => testText.Errors.Describe("Code", null!));
    }

    [Fact]
    public void GivenCodedException_WhenCreated_ThenCodeArgumentsAndCauseKept()
    {
        var cause = new InvalidOperationException("cause");
        var sut = new BlazwizException("Some.Code", cause, 1, "two");

        Assert.Equal("Some.Code", sut.Code);
        Assert.Equal("Some.Code", sut.Message);
        Assert.Same(cause, sut.InnerException);
        Assert.Equal([1, "two"], sut.Arguments);
        Assert.Throws<ArgumentException>(() => new BlazwizException(" "));
    }

    [Fact]
    public void GivenAppOverrideOfAFrameworkError_WhenDescribe_ThenOverrideUsed()
    {
        new TextBuilder(testText.Options).Override("Error.Auth.NotAuthorized", "Wrong details.");

        Assert.Equal("Wrong details.", testText.Errors.Describe(AuthErrorCodes.NotAuthorized, []));
    }
}
