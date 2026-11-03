using Devoctomy.Blazwiz.Sync.Connectivity;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Connectivity;

public class ConnectivityStatusTests
{
    [Fact]
    public void GivenStatus_WhenClone_ThenCopyIsIndependent()
    {
        // Arrange
        var sut = new ConnectivityStatus { IsOnline = true };
        var copy = sut.Clone();

        // Act
        copy.IsOnline = false;

        // Assert
        Assert.True(sut.IsOnline);
    }
}
