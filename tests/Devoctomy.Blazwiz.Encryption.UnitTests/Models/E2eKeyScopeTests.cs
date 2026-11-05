using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests.Models;

public class E2eKeyScopeTests
{
    [Theory]
    [InlineData("s1")]
    [InlineData("A-b_9")]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123")]
    public void GivenAValidId_WhenConstructed_ThenTheIdIsKept(string id)
    {
        // Act
        var sut = new E2eKeyScope(id);

        // Assert
        Assert.Equal(id, sut.Id);
        Assert.Equal(id, sut.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("a:b")]
    [InlineData("a/b")]
    [InlineData("a b")]
    [InlineData("é")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234")]
    public void GivenAnInvalidId_WhenConstructed_ThenThrowsArgumentException(string id)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => new E2eKeyScope(id));
    }

    [Fact]
    public void GivenNullId_WhenConstructed_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new E2eKeyScope(null!));
    }

    [Fact]
    public void GivenTwoScopesWithTheSameId_WhenCompared_ThenEqual()
    {
        // Act & Assert
        Assert.Equal(new E2eKeyScope("s1"), new E2eKeyScope("s1"));
        Assert.NotEqual(new E2eKeyScope("s1"), new E2eKeyScope("s2"));
    }
}
