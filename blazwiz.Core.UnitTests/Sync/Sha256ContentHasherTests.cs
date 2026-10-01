using System.Text;
using blazwiz.Core.Sync;

namespace blazwiz.Core.UnitTests.Sync;

public class Sha256ContentHasherTests
{
    [Fact]
    public void GivenContent_WhenComputeHash_ThenReturnsLowerCaseSha256Hex()
    {
        // Arrange
        var sut = new Sha256ContentHasher();

        // Act
        var result = sut.ComputeHash(Encoding.UTF8.GetBytes("abc"));

        // Assert (the SHA-256 test vector for "abc")
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result);
    }

    [Fact]
    public void GivenEmptyContent_WhenComputeHash_ThenReturnsHashOfNothing()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", new Sha256ContentHasher().ComputeHash([]));
    }

    [Fact]
    public void GivenNull_WhenComputeHash_ThenThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Sha256ContentHasher().ComputeHash(null!));
    }
}
