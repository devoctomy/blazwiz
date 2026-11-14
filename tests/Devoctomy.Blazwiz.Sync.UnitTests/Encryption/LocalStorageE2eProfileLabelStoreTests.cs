using System.Security.Cryptography;
using Devoctomy.Blazwiz.Browser.Services.Interfaces;
using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Sync.Encryption;
using Devoctomy.Blazwiz.Sync.Interfaces;
using Moq;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Encryption;

public class LocalStorageE2eProfileLabelStoreTests
{
    private const string StorageKey = LocalStorageE2eProfileLabelStore.StorageKeyPrefix + "bob";

    private readonly Dictionary<string, string> storage = [];
    private readonly Mock<ILocalStorageService> mockLocalStorage = new();
    private readonly Mock<ISyncUserContext> mockUser = new();
    private readonly BouncyCastleAesGcmAeadCipher crypto = new();
    private readonly InMemoryDerivedKeyStore keys = new();

    public LocalStorageE2eProfileLabelStoreTests()
    {
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("bob");
        mockLocalStorage.Setup(x => x.GetItemAsync(It.IsAny<string>())).ReturnsAsync((string key) => storage.GetValueOrDefault(key));
        mockLocalStorage.Setup(x => x.SetItemAsync(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((k, v) => storage[k] = v).Returns(Task.CompletedTask);
        mockLocalStorage.Setup(x => x.RemoveItemAsync(It.IsAny<string>())).Callback<string>(k => storage.Remove(k)).Returns(Task.CompletedTask);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("localStorageService", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(null!, mockUser.Object, crypto, keys)).ParamName);
        Assert.Equal("userContext", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(mockLocalStorage.Object, null!, crypto, keys)).ParamName);
        Assert.Equal("cipher", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(mockLocalStorage.Object, mockUser.Object, null!, keys)).ParamName);
        Assert.Equal("keyStore", Assert.Throws<ArgumentNullException>(() => new LocalStorageE2eProfileLabelStore(mockLocalStorage.Object, mockUser.Object, crypto, null!)).ParamName);
    }

    [Fact]
    public async Task GivenTheLabelKey_WhenLabelsSetAndRemoved_ThenKeptUnderTheUser_Encrypted()
    {
        // Arrange
        SetLabelKey();
        var sut = CreateSut();

        // Act: the id and label contain '-' and ' ', which base64 never does, so the ciphertext cannot contain them by
        // chance (a short plaintext like "p1" sometimes appears in random base64).
        await sut.SetAsync("profile-1", "Work notes");
        await sut.SetAsync("profile-2", "Home notes");
        await sut.RemoveAsync("profile-2");

        // Assert
        Assert.Equal(new Dictionary<string, string> { ["profile-1"] = "Work notes" }, await sut.GetAllAsync());
        Assert.DoesNotContain("Work notes", storage[StorageKey], StringComparison.Ordinal);
        Assert.DoesNotContain("profile-1", storage[StorageKey], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenLabelsKept_WhenReadWithoutTheLabelKey_ThenNone_AndStillKept()
    {
        // Arrange: kept while signed in with the passkey, then read after a password sign in (or a reload).
        SetLabelKey();
        await CreateSut().SetAsync("p1", "Work");
        keys.Clear();

        // Act
        var labels = await CreateSut().GetAllAsync();

        // Assert
        Assert.Empty(labels);
        Assert.True(storage.ContainsKey(StorageKey));
    }

    [Fact]
    public async Task GivenNoLabelKey_WhenALabelIsSet_ThenNothingIsKept()
    {
        // Act
        await CreateSut().SetAsync("p1", "Work");

        // Assert
        Assert.Empty(storage);
    }

    [Fact]
    public async Task GivenLabelsKept_AndNoLabelKey_WhenOneIsRemoved_ThenTheUsersLabelsAreRemoved()
    {
        // Arrange: the encrypted entry can't be changed without the key, so it goes.
        SetLabelKey();
        await CreateSut().SetAsync("p1", "Work");
        keys.Clear();

        // Act
        await CreateSut().RemoveAsync("p1");

        // Assert
        Assert.False(storage.ContainsKey(StorageKey));
    }

    [Fact]
    public async Task GivenLabelsKeptWithAnotherKey_WhenRead_ThenNone_AndANewLabelReplacesThem()
    {
        // Arrange: e.g. kept with another passkey.
        SetLabelKey();
        await CreateSut().SetAsync("p1", "Work");
        SetLabelKey();
        var sut = CreateSut();

        // Act
        var before = await sut.GetAllAsync();
        await sut.SetAsync("p2", "Home");

        // Assert
        Assert.Empty(before);
        Assert.Equal(new Dictionary<string, string> { ["p2"] = "Home" }, await sut.GetAllAsync());
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("{\"p1\":\"Work\"}")]
    [InlineData("AAAA")]
    public async Task GivenUnreadableLabels_WhenGot_ThenNone(string stored)
    {
        // Arrange
        SetLabelKey();
        storage[StorageKey] = stored;

        // Act
        var labels = await CreateSut().GetAllAsync();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GivenLabelsThatDecryptToNull_WhenGot_ThenNone()
    {
        // Arrange: encrypted with the right key for the right user, but the JSON inside is null.
        var key = RandomNumberGenerator.GetBytes(32);
        keys.SetKey(E2eKeyNames.LabelKey, key);
        var nonce = RandomNumberGenerator.GetBytes(crypto.NonceSize);
        var ciphertext = crypto.Encrypt(key, nonce, "null"u8.ToArray(), "bob"u8.ToArray());
        storage[StorageKey] = Convert.ToBase64String([.. nonce, .. ciphertext]);

        // Act
        var labels = await CreateSut().GetAllAsync();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GivenLabelsKeptForOneUser_WhenReadAsAnotherUserWithTheSameKey_ThenNone()
    {
        // Arrange: the labels are bound to the user they were kept for.
        SetLabelKey();
        await CreateSut().SetAsync("p1", "Work");
        storage[LocalStorageE2eProfileLabelStore.StorageKeyPrefix + "alice"] = storage[StorageKey];
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync("alice");

        // Act
        var labels = await CreateSut().GetAllAsync();

        // Assert
        Assert.Empty(labels);
    }

    [Fact]
    public async Task GivenSignedOut_WhenUsed_ThenThrowsInvalidOperationException()
    {
        // Arrange
        mockUser.Setup(x => x.GetUserIdAsync()).ReturnsAsync((string?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut().GetAllAsync());
    }

    private void SetLabelKey() => keys.SetKey(E2eKeyNames.LabelKey, RandomNumberGenerator.GetBytes(32));

    private LocalStorageE2eProfileLabelStore CreateSut() => new(mockLocalStorage.Object, mockUser.Object, crypto, keys);
}
