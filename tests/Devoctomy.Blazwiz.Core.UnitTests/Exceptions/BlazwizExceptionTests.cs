using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Core.UnitTests.Exceptions;

public class BlazwizExceptionTests
{
    [Fact]
    public void GivenACodeAndArguments_WhenConstructed_ThenKeepsThemAndTheCodeIsTheMessage()
    {
        // Act
        var ex = new BlazwizException("Sync.Storage.FolderNotEmpty", 3);

        // Assert
        Assert.Equal("Sync.Storage.FolderNotEmpty", ex.Code);
        Assert.Equal("Sync.Storage.FolderNotEmpty", ex.Message);
        Assert.Equal(new object[] { 3 }, ex.Arguments);
        Assert.Null(ex.InnerException);
    }

    [Fact]
    public void GivenAnInnerException_WhenConstructed_ThenKeepsIt()
    {
        // Arrange
        var inner = new InvalidOperationException("cause");

        // Act
        var ex = new BlazwizException("Code", inner);

        // Assert
        Assert.Same(inner, ex.InnerException);
        Assert.Empty(ex.Arguments);
    }

    [Fact]
    public void GivenNullArguments_WhenConstructed_ThenArgumentsAreEmpty()
    {
        // Act
        var ex = new BlazwizException("Code", null, null!);

        // Assert
        Assert.NotNull(ex.Arguments);
        Assert.Empty(ex.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenABlankCode_WhenConstructed_ThenThrowsArgumentException(string code)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new BlazwizException(code));
    }

    [Fact]
    public void GivenANullCode_WhenConstructed_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new BlazwizException(null!));
    }
}
