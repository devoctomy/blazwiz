using Devoctomy.Blazwiz.Sync.IndexedDb;

namespace Devoctomy.Blazwiz.Sync.UnitTests.IndexedDb;

public class IndexedDbStoreTests
{
    [Fact]
    public void GivenValidValues_WhenConstructing_ThenPropertiesSet()
    {
        // Arrange
        var sut = new IndexedDbStore("db", "store", ["id"]);

        // Act & Assert
        Assert.Equal("db", sut.Database);
        Assert.Equal("store", sut.Name);
        Assert.Equal(["id"], sut.KeyPath);
        Assert.Empty(sut.Indexes);
    }

    [Fact]
    public void GivenInvalidValues_WhenConstructing_ThenThrows()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IndexedDbStore(" ", "store", ["id"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbStore("db", " ", ["id"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbStore("db", "store", []));
        Assert.Throws<ArgumentException>(() => new IndexedDbIndex(" ", ["owner"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbIndex("owner", []));
    }
}
