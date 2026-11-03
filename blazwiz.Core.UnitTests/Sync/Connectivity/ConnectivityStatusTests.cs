using blazwiz.Core.Sync.Connectivity;

namespace blazwiz.Core.UnitTests.Sync.Connectivity;

public class ConnectivityStatusTests
{
    [Fact]
    public void GivenStatus_WhenClone_ThenCopyIsIndependent()
    {
        var sut = new ConnectivityStatus { IsOnline = true };
        var copy = sut.Clone();
        copy.IsOnline = false;

        Assert.True(sut.IsOnline);
    }
}
