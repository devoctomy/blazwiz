using blazwiz.Core.Sync.IndexedDb;

namespace blazwiz.Core.UnitTests.Sync.IndexedDb;

public class IndexedDbStoreTests
{
    [Fact]
    public void GivenValidValues_WhenConstructing_ThenPropertiesSet()
    {
        var sut = new IndexedDbStore("db", "store", ["id"]);

        Assert.Equal("db", sut.Database);
        Assert.Equal("store", sut.Name);
        Assert.Equal(["id"], sut.KeyPath);
        Assert.Empty(sut.Indexes);
    }

    [Fact]
    public void GivenInvalidValues_WhenConstructing_ThenThrows()
    {
        Assert.Throws<ArgumentException>(() => new IndexedDbStore(" ", "store", ["id"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbStore("db", " ", ["id"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbStore("db", "store", []));
        Assert.Throws<ArgumentException>(() => new IndexedDbIndex(" ", ["owner"]));
        Assert.Throws<ArgumentException>(() => new IndexedDbIndex("owner", []));
    }
}
