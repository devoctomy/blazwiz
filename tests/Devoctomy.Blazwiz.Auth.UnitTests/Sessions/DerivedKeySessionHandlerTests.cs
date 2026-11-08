using Devoctomy.Blazwiz.Auth.Models;
using Devoctomy.Blazwiz.Auth.Sessions;
using Devoctomy.Blazwiz.Auth.Sessions.Interfaces;
using Devoctomy.Blazwiz.Cryptography;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Sessions;

public class DerivedKeySessionHandlerTests
{
    private readonly InMemoryDerivedKeyStore keyStore = new();
    private readonly DerivedKeySessionHandler sut;

    public DerivedKeySessionHandlerTests()
    {
        sut = new DerivedKeySessionHandler(keyStore);
    }

    [Fact]
    public void GivenNullKeyStore_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new DerivedKeySessionHandler(null!)).ParamName);
    }

    [Fact]
    public void GivenTheHandler_WhenOrdered_ThenItRunsLast()
    {
        // Act & Assert
        Assert.Equal(int.MaxValue, sut.Order);
    }

    [Fact]
    public async Task GivenKeysInTheStore_WhenSignedOut_ThenEveryKeyIsGone()
    {
        // Arrange
        keyStore.SetKey("rememberme", [1, 2, 3]);
        keyStore.SetKey("e2e-kek", [4, 5, 6]);

        // Act
        await sut.OnSignedOutAsync();

        // Assert
        Assert.False(keyStore.TryGetKey("rememberme", out _));
        Assert.False(keyStore.TryGetKey("e2e-kek", out _));
    }

    [Fact]
    public async Task GivenKeysInTheStore_WhenTheSessionStartsOrIsSigningOut_ThenTheyAreKept()
    {
        // Arrange: the modules still need them while they lock what they hold.
        keyStore.SetKey("rememberme", [1, 2, 3]);
        var session = new UserSession { Username = "bob@example.com" };
        ISessionLifecycleHandler handler = sut;

        // Act
        await handler.OnSignedInAsync();
        await handler.OnShellLoadingAsync(session);
        await handler.OnSessionStartedAsync(session);
        await handler.OnSigningOutAsync();

        // Assert
        Assert.True(keyStore.TryGetKey("rememberme", out _));
    }
}
