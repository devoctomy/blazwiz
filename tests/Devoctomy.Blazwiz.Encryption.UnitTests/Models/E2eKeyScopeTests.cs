using Devoctomy.Blazwiz.Encryption.Models;

namespace Devoctomy.Blazwiz.Encryption.UnitTests.Models;

public class E2eKeyScopeTests
{
    [Theory]
    [InlineData("account")]
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
    public void GivenTheAccountScope_WhenRead_ThenItIsTheAccount()
    {
        // Act & Assert
        Assert.Equal(E2eKeyScope.AccountId, E2eKeyScope.Account.Id);
        Assert.True(E2eKeyScope.Account.IsAccount);
        Assert.False(new E2eKeyScope("s1").IsAccount);
        Assert.Equal(E2eKeyScope.Account, new E2eKeyScope("account"));
    }
}
