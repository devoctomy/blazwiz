using Devoctomy.Blazwiz.Auth.Models;

namespace Devoctomy.Blazwiz.Auth.UnitTests.Models;

public class WebAuthnStoredCredentialTests
{
    [Fact]
    public void GivenNewCredential_WhenReadingIt_ThenNothingStored()
    {
        // Act
        var credential = new WebAuthnStoredCredential();

        // Assert
        Assert.Equal(string.Empty, credential.CredentialID);
        Assert.Equal(string.Empty, credential.UserID);
        Assert.Equal(default, credential.CreatedAt);
        Assert.Null(credential.EncryptedPassword);
        Assert.Null(credential.EncryptedDevice);
    }

    [Fact]
    public void GivenValues_WhenSettingThem_ThenTheyAreKept()
    {
        // Arrange
        var createdAt = new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc);

        // Act
        var credential = new WebAuthnStoredCredential
        {
            CredentialID = "cred1",
            UserID = "user1",
            CreatedAt = createdAt,
            EncryptedPassword = "enc-password",
            EncryptedDevice = "enc-device",
        };

        // Assert
        Assert.Equal("cred1", credential.CredentialID);
        Assert.Equal("user1", credential.UserID);
        Assert.Equal(createdAt, credential.CreatedAt);
        Assert.Equal("enc-password", credential.EncryptedPassword);
        Assert.Equal("enc-device", credential.EncryptedDevice);
    }
}
