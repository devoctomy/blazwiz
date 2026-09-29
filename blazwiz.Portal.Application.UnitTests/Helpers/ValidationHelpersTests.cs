using blazwiz.Portal.Application.Helpers;

namespace blazwiz.Portal.Application.UnitTests.Helpers;

public class ValidationHelpersTests
{
    [Fact]
    public void GivenNonNullValue_WhenVerifiedAssignment_ThenReturnsValue()
    {
        // Arrange
        var value = new object();

        // Act
        var result = ValidationHelpers.VerifiedAssignment(value);

        // Assert
        Assert.Same(value, result);
    }

    [Fact]
    public void GivenNullValue_WhenVerifiedAssignment_ThenThrowsWithCallerParameterName()
    {
        // Arrange
        object? myParameter = null;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => ValidationHelpers.VerifiedAssignment(myParameter));
        Assert.Equal("myParameter", exception.ParamName);
    }
}
