using Devoctomy.Blazwiz.Encryption.Exceptions;

namespace Devoctomy.Blazwiz.Encryption.UnitTests.Exceptions;

public class E2eWrongPassphraseExceptionTests
{
    [Fact]
    public void GivenNoArguments_WhenConstructed_ThenHasTheWrongPassphraseCode()
    {
        // Act
        var ex = new E2eWrongPassphraseException();

        // Assert
        Assert.Equal(E2eErrorCodes.WrongPassphrase, ex.Code);
        Assert.Null(ex.InnerException);
        Assert.Empty(ex.Arguments);
    }

    [Fact]
    public void GivenAnInnerException_WhenConstructed_ThenKeepsItWithTheWrongPassphraseCode()
    {
        // Arrange
        var inner = new InvalidOperationException("cause");

        // Act
        var ex = new E2eWrongPassphraseException(inner);

        // Assert
        Assert.Equal(E2eErrorCodes.WrongPassphrase, ex.Code);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void GivenACodeAndArguments_WhenConstructed_ThenKeepsThem()
    {
        // Act
        var ex = new E2eWrongPassphraseException("E2e.Custom", 3, "x");

        // Assert
        Assert.Equal("E2e.Custom", ex.Code);
        Assert.Equal(new object[] { 3, "x" }, ex.Arguments);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void GivenACodeAnInnerExceptionAndArguments_WhenConstructed_ThenKeepsThem()
    {
        // Arrange
        var inner = new InvalidOperationException("cause");

        // Act
        var ex = new E2eWrongPassphraseException("E2e.Custom", inner, 7);

        // Assert
        Assert.Equal("E2e.Custom", ex.Code);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(new object[] { 7 }, ex.Arguments);
    }
}
