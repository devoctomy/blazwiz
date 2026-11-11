using Devoctomy.Blazwiz.Cryptography;
using Devoctomy.Blazwiz.Cryptography.Configuration;
using Devoctomy.Blazwiz.Cryptography.Models;
using Devoctomy.Blazwiz.Encryption;
using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Microsoft.Extensions.Time.Testing;

namespace Devoctomy.Blazwiz.Encryption.UnitTests;

public class E2eKeysDocumentServiceTests
{
    private readonly Argon2idOptions options = new() { Parameters = new Argon2idParameters { MemoryKib = 64, Iterations = 1, Parallelism = 1, KeyLength = 32 } };
    private readonly BouncyCastleArgon2idKeyDerivationService argon2 = new();
    private readonly AeadKeyWrapService keyWrap = new(new BouncyCastleAesGcmAeadCipher());
    private readonly FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
    private readonly E2eKeysDocumentService sut;

    public E2eKeysDocumentServiceTests()
    {
        sut = new E2eKeysDocumentService(argon2, keyWrap, options, clock);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("argon2", Assert.Throws<ArgumentNullException>(() => new E2eKeysDocumentService(null!, keyWrap, options, clock)).ParamName);
        Assert.Equal("keyWrapService", Assert.Throws<ArgumentNullException>(() => new E2eKeysDocumentService(argon2, null!, options, clock)).ParamName);
        Assert.Equal("argon2Options", Assert.Throws<ArgumentNullException>(() => new E2eKeysDocumentService(argon2, keyWrap, null!, clock)).ParamName);
        Assert.Equal("timeProvider", Assert.Throws<ArgumentNullException>(() => new E2eKeysDocumentService(argon2, keyWrap, options, null!)).ParamName);
    }

    [Fact]
    public async Task GivenAPassphrase_WhenCreated_ThenTheMasterKeyUnwrapsWithItAndTheFileKeyIsActive()
    {
        // Arrange
        var reported = new List<E2eProgress>();

        // Act
        using var keys = await sut.CreateAsync("correct horse", progress: new Progress(reported));

        // Assert
        var document = sut.Parse(sut.Serialize(keys.Document));
        Assert.StartsWith(E2eKeysDocumentService.KeyIdPrefix, keys.FileKeyId);
        Assert.Equal(FileKeyEntry.ActiveStatus, Assert.Single(document.Keys).Value.Status);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, document.Keys[keys.FileKeyId].Created);
        Assert.Equal(64, document.Kdf.MemoryKib);
        Assert.Equal(keys.MasterKey, await sut.UnwrapMasterKeyAsync(document, "correct horse"));
        Assert.Equal(
            [(E2eStep.DeriveKey, false), (E2eStep.DeriveKey, true), (E2eStep.CreateKeys, false), (E2eStep.CreateKeys, true)],
            reported.Select(p => (p.Step, p.Completed)));
    }

    [Fact]
    public async Task GivenNewKeys_WhenDisposed_ThenTheRawKeysAreWiped()
    {
        // Arrange
        var keys = await sut.CreateAsync("correct horse");

        // Act
        keys.Dispose();

        // Assert
        Assert.All(keys.MasterKey, b => Assert.Equal(0, b));
        Assert.All(keys.FileKey, b => Assert.Equal(0, b));
    }

    [Fact]
    public async Task GivenTheWrongPassphrase_WhenUnwrapped_ThenThrowsWrongPassphrase()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");

        // Act & Assert
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => sut.UnwrapMasterKeyAsync(keys.Document, "wrong"));
    }

    [Fact]
    public async Task GivenANewPassphrase_WhenRotated_ThenOnlyTheNewOneUnwrapsANewMasterKey()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        var reported = new List<E2eProgress>();

        // Act
        using var rotated = await sut.RotateAsync(keys.Document, keys.MasterKey, "battery staple", progress: new Progress(reported));

        // Assert
        Assert.NotEqual(keys.MasterKey, rotated.MasterKey);
        Assert.Equal(rotated.MasterKey, await sut.UnwrapMasterKeyAsync(rotated.Document, "battery staple"));
        await Assert.ThrowsAsync<E2eWrongPassphraseException>(() => sut.UnwrapMasterKeyAsync(rotated.Document, "correct horse"));
        Assert.True(rotated.Document.RotationInProgress);
        Assert.Equal(
            [(E2eStep.DeriveKey, false), (E2eStep.DeriveKey, true), (E2eStep.CreateKeys, false), (E2eStep.CreateKeys, true)],
            reported.Select(p => (p.Step, p.Completed)));
    }

    [Fact]
    public async Task GivenKeysRotatedTwice_WhenLoaded_ThenEveryEarlierFileKeyStillUnwrapsAndExactlyOneIsActive()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        var firstKeyId = keys.FileKeyId;
        var firstKey = keys.FileKey.ToArray();
        using var second = await sut.RotateAsync(keys.Document, keys.MasterKey, "battery staple");
        var secondKey = second.FileKey.ToArray();
        using var third = await sut.RotateAsync(second.Document, second.MasterKey, "tr0ub4dor");
        var ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), new E2eKeyScope("s1"));

        // Act
        var document = sut.Parse(sut.Serialize(third.Document));
        sut.LoadFileKeys(ring, document, third.MasterKey);

        // Assert
        Assert.Equal(3, document.Keys.Count);
        Assert.Equal(third.FileKeyId, Assert.Single(document.Keys, k => k.Value.Status == FileKeyEntry.ActiveStatus).Key);
        Assert.Equal(2, document.Keys.Count(k => k.Value.Status == FileKeyEntry.RetiredStatus));
        Assert.Equal(third.FileKeyId, ring.ActiveKeyId);
        Assert.True(ring.TryGetFileKey(firstKeyId, out var first));
        Assert.Equal(firstKey, first);
        Assert.True(ring.TryGetFileKey(second.FileKeyId, out var secondLoaded));
        Assert.Equal(secondKey, secondLoaded);
        Assert.Equal(clock.GetUtcNow().UtcDateTime, document.Keys[firstKeyId].Created);
    }

    [Fact]
    public async Task GivenRotatedKeys_WhenLoadedWithTheOldMasterKey_ThenThrowsInvalidData()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        var oldMasterKey = keys.MasterKey.ToArray();
        using var rotated = await sut.RotateAsync(keys.Document, keys.MasterKey, "battery staple");
        var ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), new E2eKeyScope("s1"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.LoadFileKeys(ring, rotated.Document, oldMasterKey));
        Assert.Null(ring.ActiveKeyId);
    }

    [Fact]
    public async Task GivenAnotherMasterKey_WhenRotated_ThenThrowsInvalidDataAndTheDocumentIsUnchanged()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        using var other = await sut.CreateAsync("correct horse");
        var before = sut.Serialize(keys.Document);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.RotateAsync(keys.Document, other.MasterKey, "battery staple"));
        Assert.Equal(before, sut.Serialize(keys.Document));
    }

    [Fact]
    public async Task GivenADocument_WhenFileKeysLoaded_ThenTheRingHoldsThemAndEncrypts()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        var ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), new E2eKeyScope("s1"));

        // Act
        sut.LoadFileKeys(ring, keys.Document, keys.MasterKey);

        // Assert
        Assert.Equal(keys.FileKeyId, ring.ActiveKeyId);
        Assert.True(ring.TryGetFileKey(keys.FileKeyId, out var fileKey));
        Assert.Equal(keys.FileKey, fileKey);
    }

    [Fact]
    public async Task GivenAnotherMasterKey_WhenFileKeysLoaded_ThenThrowsInvalidData()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        using var other = await sut.CreateAsync("correct horse");
        var ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), new E2eKeyScope("s1"));

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.LoadFileKeys(ring, keys.Document, other.MasterKey));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"version":2,"kdf":{"alg":"argon2id"},"keys":{}}""")]
    [InlineData("""{"version":1,"kdf":{"alg":"pbkdf2"},"keys":{}}""")]
    [InlineData("""{"version":1,"kdf":{"alg":"argon2id"},"keys":{}}""")]
    public void GivenAnInvalidDocument_WhenParsed_ThenThrowsInvalidData(string json)
    {
        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.Parse(json));
    }

    [Theory]
    [InlineData(nameof(KdfSettings.MemoryKib), E2eKeysDocumentService.MaxMemoryKib + 1)]
    [InlineData(nameof(KdfSettings.MemoryKib), 7)]
    [InlineData(nameof(KdfSettings.Iterations), 0)]
    [InlineData(nameof(KdfSettings.Iterations), E2eKeysDocumentService.MaxIterations + 1)]
    [InlineData(nameof(KdfSettings.Parallelism), 0)]
    [InlineData(nameof(KdfSettings.Parallelism), E2eKeysDocumentService.MaxParallelism + 1)]
    [InlineData(nameof(KdfSettings.KeyLength), 16)]
    [InlineData(nameof(KdfSettings.KeyLength), 64)]
    public async Task GivenKeyDerivationSettingsOutOfRange_WhenParsed_ThenThrowsInvalidData(string setting, int value)
    {
        // Arrange: the settings come from the document, so a tampered one must not exhaust memory or get as far as the
        // cipher with the wrong key length.
        using var keys = await sut.CreateAsync("correct horse");
        typeof(KdfSettings).GetProperty(setting)!.SetValue(keys.Document.Kdf, value);
        var json = sut.Serialize(keys.Document);

        // Act & Assert
        Assert.Throws<InvalidDataException>(() => sut.Parse(json));
    }

    [Fact]
    public async Task GivenABadSalt_WhenUnwrapped_ThenThrowsInvalidData()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        keys.Document.Kdf.Salt = "not base64!";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidDataException>(() => sut.UnwrapMasterKeyAsync(keys.Document, "correct horse"));
    }

    [Fact]
    public async Task GivenNullArguments_WhenCalled_ThenThrows()
    {
        // Arrange
        using var keys = await sut.CreateAsync("correct horse");
        var ring = new E2eKeyRing(new InMemoryDerivedKeyStore(), new E2eKeyScope("s1"));

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.CreateAsync(string.Empty));
        Assert.Throws<ArgumentNullException>(() => sut.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => sut.Serialize(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.UnwrapMasterKeyAsync(null!, "p"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.UnwrapMasterKeyAsync(keys.Document, string.Empty));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RotateAsync(keys.Document, null!, "p"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RotateAsync(null!, keys.MasterKey, "p"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => sut.RotateAsync(keys.Document, keys.MasterKey, string.Empty));
        Assert.Throws<ArgumentNullException>(() => sut.LoadFileKeys(null!, keys.Document, keys.MasterKey));
        Assert.Throws<ArgumentNullException>(() => sut.LoadFileKeys(ring, null!, keys.MasterKey));
    }

    private sealed class Progress(List<E2eProgress> reported) : IProgress<E2eProgress>
    {
        public void Report(E2eProgress value) => reported.Add(value);
    }
}
