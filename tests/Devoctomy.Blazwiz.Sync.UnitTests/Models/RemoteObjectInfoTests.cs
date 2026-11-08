using Devoctomy.Blazwiz.Sync.Models;

namespace Devoctomy.Blazwiz.Sync.UnitTests.Models;

public class RemoteObjectInfoTests
{
    private static readonly DateTimeOffset Uploaded = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenNoEditTime_WhenEditedAt_ThenTheUploadTime()
    {
        // Arrange
        var sut = new RemoteObjectInfo { LastModified = Uploaded };

        // Act & Assert
        Assert.Equal(Uploaded, sut.EditedAt);
    }

    [Fact]
    public void GivenAnEditTimeBeforeTheUpload_WhenEditedAt_ThenTheEditTime()
    {
        // Arrange
        var edited = Uploaded.AddHours(-2);
        var sut = new RemoteObjectInfo { LastModified = Uploaded, ModifiedAt = edited };

        // Act & Assert
        Assert.Equal(edited, sut.EditedAt);
    }

    [Fact]
    public void GivenAnEditTimeALittleAfterTheUpload_WhenEditedAt_ThenTheEditTime()
    {
        // Arrange: the device's clock ran a little fast.
        var edited = Uploaded + RemoteObjectInfo.MaxClockSkew;
        var sut = new RemoteObjectInfo { LastModified = Uploaded, ModifiedAt = edited };

        // Act & Assert
        Assert.Equal(edited, sut.EditedAt);
    }

    [Fact]
    public void GivenAnEditTimeFarAfterTheUpload_WhenEditedAt_ThenTheUploadTime()
    {
        // Arrange: a file cannot be edited after it was uploaded, so the metadata was not written by a device syncing
        // normally, and must not win every conflict.
        var sut = new RemoteObjectInfo { LastModified = Uploaded, ModifiedAt = Uploaded.AddYears(10) };

        // Act & Assert
        Assert.Equal(Uploaded, sut.EditedAt);
    }

    [Fact]
    public void GivenNoUploadTime_WhenEditedAt_ThenTheEditTime()
    {
        // Arrange
        var edited = Uploaded.AddYears(10);
        var sut = new RemoteObjectInfo { ModifiedAt = edited };

        // Act & Assert
        Assert.Equal(edited, sut.EditedAt);
    }
}
