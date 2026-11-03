using Devoctomy.Blazwiz.Cryptography;

namespace Devoctomy.Blazwiz.Cryptography.UnitTests;

public class InMemoryDerivedKeyStoreTests
{
    private readonly InMemoryDerivedKeyStore sut = new();

    [Fact]
    public void GivenNoKey_WhenTryGetKey_ThenReturnsFalse()
    {
        // Act
        var found = sut.TryGetKey("apple", out var key);

        // Assert
        Assert.False(found);
        Assert.Null(key);
    }

    [Fact]
    public void GivenStoredKey_WhenTryGetKey_ThenReturnsKey()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);

        // Act
        var found = sut.TryGetKey("apple", out var key);

        // Assert
        Assert.True(found);
        Assert.Equal([1, 2, 3], key);
    }

    [Fact]
    public void GivenStoredKey_WhenTryGetKeyWithDifferentCase_ThenReturnsFalse()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);

        // Act & Assert
        Assert.False(sut.TryGetKey("Apple", out _));
    }

    [Fact]
    public void GivenStoredKey_WhenCallerChangesTheArrayItPassedIn_ThenStoredKeyIsUnchanged()
    {
        // Arrange
        var original = new byte[] { 1, 2, 3 };
        sut.SetKey("apple", original);

        // Act
        original[0] = 9;

        // Assert
        sut.TryGetKey("apple", out var key);
        Assert.Equal([1, 2, 3], key);
    }

    [Fact]
    public void GivenStoredKey_WhenCallerChangesTheReturnedKey_ThenStoredKeyIsUnchanged()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);
        sut.TryGetKey("apple", out var first);

        // Act
        first![0] = 9;

        // Assert
        sut.TryGetKey("apple", out var second);
        Assert.Equal([1, 2, 3], second);
    }

    [Fact]
    public void GivenStoredKey_WhenSetKeyAgain_ThenNewKeyIsReturned()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);

        // Act
        sut.SetKey("apple", [4, 5, 6]);

        // Assert
        sut.TryGetKey("apple", out var key);
        Assert.Equal([4, 5, 6], key);
    }

    [Fact]
    public void GivenStoredKeys_WhenClear_ThenNoKeysAreReturned()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);
        sut.SetKey("orange", [4, 5, 6]);

        // Act
        sut.Clear();

        // Assert
        Assert.False(sut.TryGetKey("apple", out _));
        Assert.False(sut.TryGetKey("orange", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenInvalidName_WhenSetKey_ThenThrowsArgumentException(string? name)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.SetKey(name!, [1]));
    }

    [Fact]
    public void GivenNullKey_WhenSetKey_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.SetKey("apple", null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenInvalidName_WhenTryGetKey_ThenThrowsArgumentException(string? name)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.TryGetKey(name!, out _));
    }

    [Fact]
    public void GivenStoredKeys_WhenRemoveKey_ThenOnlyThatKeyIsForgotten()
    {
        // Arrange
        sut.SetKey("apple", [1, 2, 3]);
        sut.SetKey("orange", [4, 5, 6]);

        // Act
        var removed = sut.RemoveKey("apple");

        // Assert
        Assert.True(removed);
        Assert.False(sut.TryGetKey("apple", out _));
        Assert.True(sut.TryGetKey("orange", out var orange));
        Assert.Equal(new byte[] { 4, 5, 6 }, orange);
    }

    [Fact]
    public void GivenNoKey_WhenRemoveKey_ThenReturnsFalse()
    {
        // Act & Assert
        Assert.False(sut.RemoveKey("apple"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GivenInvalidName_WhenRemoveKey_ThenThrowsArgumentException(string? name)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => sut.RemoveKey(name!));
    }
}
