using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Services;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Services;

public class SyncEventPresenterTests
{
    private readonly TestText testText = new();
    private readonly SyncEventPresenter sut;

    public SyncEventPresenterTests()
    {
        sut = new SyncEventPresenter(testText.Sync, testText.Plurals, testText.Errors);
    }

    public static TheoryData<SyncEventKind> AllKinds()
    {
        var data = new TheoryData<SyncEventKind>();
        foreach (var kind in Enum.GetValues<SyncEventKind>())
        {
            data.Add(kind);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void GivenEveryKind_WhenPresent_ThenHasIconLabelColourAndGroup(SyncEventKind kind)
    {
        // Act
        var result = sut.Present(kind);

        // Assert
        Assert.StartsWith("bi-", result.Icon, StringComparison.Ordinal);
        Assert.NotEqual("bi-circle", result.Icon);
        Assert.False(string.IsNullOrWhiteSpace(result.Label));
        Assert.StartsWith("text-", result.ColorClass, StringComparison.Ordinal);
        Assert.Contains(kind, sut.GetKinds(result.Group));
    }

    [Theory]
    [InlineData(SyncEventKind.UploadNew, "bi-cloud-plus", SyncEventGroup.Uploads)]
    [InlineData(SyncEventKind.UploadLocalNewer, "bi-cloud-arrow-up", SyncEventGroup.Uploads)]
    [InlineData(SyncEventKind.DownloadNew, "bi-file-earmark-arrow-down", SyncEventGroup.Downloads)]
    [InlineData(SyncEventKind.DownloadRemoteNewer, "bi-cloud-arrow-down", SyncEventGroup.Downloads)]
    [InlineData(SyncEventKind.DeleteRemote, "bi-cloud-slash", SyncEventGroup.Deletes)]
    [InlineData(SyncEventKind.DeleteLocal, "bi-trash", SyncEventGroup.Deletes)]
    [InlineData(SyncEventKind.ConflictResolved, "bi-exclamation-triangle", SyncEventGroup.Conflicts)]
    [InlineData(SyncEventKind.Failed, "bi-x-circle", SyncEventGroup.Errors)]
    [InlineData(SyncEventKind.WentOffline, "bi-wifi-off", SyncEventGroup.Connectivity)]
    [InlineData(SyncEventKind.WentOnline, "bi-wifi", SyncEventGroup.Connectivity)]
    [InlineData(SyncEventKind.OfflineModeChanged, "bi-airplane", SyncEventGroup.Connectivity)]
    [InlineData(SyncEventKind.PassStarted, "bi-arrow-repeat", SyncEventGroup.Activity)]
    public void GivenKind_WhenPresent_ThenMatchesThePlan(SyncEventKind kind, string icon, SyncEventGroup group)
    {
        // Act
        var result = sut.Present(kind);

        // Assert
        Assert.Equal(icon, result.Icon);
        Assert.Equal(group, result.Group);
    }

    [Fact]
    public void GivenUnknownKind_WhenPresent_ThenGenericActivity()
    {
        // Act
        var result = sut.Present((SyncEventKind)999);

        // Assert
        Assert.Equal("bi-circle", result.Icon);
        Assert.Equal(SyncEventGroup.Activity, result.Group);
    }

    [Fact]
    public void GivenPresentation_WhenChangedByCaller_ThenSharedTableUnaffected()
    {
        // Act
        sut.Present(SyncEventKind.Failed).Icon = "changed";

        // Assert
        Assert.Equal("bi-x-circle", sut.Present(SyncEventKind.Failed).Icon);
    }

    [Fact]
    public void GivenNullDependencies_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("text", Assert.Throws<ArgumentNullException>(() => new SyncEventPresenter(null!, testText.Plurals, testText.Errors)).ParamName);
        Assert.Equal("plurals", Assert.Throws<ArgumentNullException>(() => new SyncEventPresenter(testText.Sync, null!, testText.Errors)).ParamName);
        Assert.Equal("errorText", Assert.Throws<ArgumentNullException>(() => new SyncEventPresenter(testText.Sync, testText.Plurals, null!)).ParamName);
    }

    [Fact]
    public void GivenNullEntry_WhenDescribe_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => sut.Describe(null!));
    }

    [Theory]
    [InlineData(SyncEventCodes.PassFailed, "Sync failed: S3 refused the credentials.")]
    [InlineData(SyncEventCodes.UploadFailed, "Upload failed: S3 refused the credentials.")]
    [InlineData(SyncEventCodes.DownloadFailed, "Download failed: S3 refused the credentials.")]
    [InlineData(SyncEventCodes.DeleteFailed, "Delete failed: S3 refused the credentials.")]
    [InlineData(SyncEventCodes.FileFailed, "Sync failed: S3 refused the credentials.")]
    public void GivenAFailureWithItsErrorCode_WhenDescribe_ThenTheErrorIsDescribed(string code, string expected)
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.Failed, Code = code, Arguments = ["Sync.Remote.AccessDenied"] };

        // Act
        var result = sut.Describe(entry);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GivenAFailureWithTheErrorsArguments_WhenDescribe_ThenTheyFillTheErrorsText()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.Failed, Code = SyncEventCodes.UploadFailed, Arguments = ["Sync.Storage.CopyMismatch", "notes/a.md"] };

        // Act
        var result = sut.Describe(entry);

        // Assert
        Assert.Equal("Upload failed: 'notes/a.md' did not copy correctly.", result);
    }

    [Fact]
    public void GivenAFailureWithoutAnErrorCode_WhenDescribe_ThenTheUnknownErrorIsDescribed()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.Failed, Code = SyncEventCodes.PassFailed };

        // Act
        var result = sut.Describe(entry);

        // Assert
        Assert.Equal("Sync failed: Something went wrong.", result);
    }

    [Fact]
    public void GivenWentOfflineWithoutAReason_WhenDescribe_ThenJustOffline()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline };

        // Act
        var result = sut.Describe(entry);

        // Assert
        Assert.Equal("Offline", result);
    }

    [Theory]
    [InlineData("NoNetwork", "Offline: no network")]
    [InlineData("OfflineMode", "Offline: offline mode")]
    [InlineData("SyncDisabled", "Sync turned off")]
    public void GivenWentOfflineWithAReason_WhenDescribe_ThenTheReasonIsShown(string reason, string expected)
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.WentOffline, Code = SyncEventCodes.WentOffline, Arguments = [reason] };

        // Act & Assert
        Assert.Equal(expected, sut.Describe(entry));
    }

    [Theory]
    [InlineData(SyncEventCodes.OfflineModeOn, "Offline mode turned on")]
    [InlineData(SyncEventCodes.OfflineModeOff, "Offline mode turned off")]
    [InlineData(SyncEventCodes.SyncTurnedOff, "Sync turned off")]
    [InlineData(SyncEventCodes.BackOnline, "Back online")]
    public void GivenAnOfflineOrOnlineChange_WhenDescribe_ThenItsTextIsShown(string code, string expected)
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.OfflineModeChanged, Code = code };

        // Act & Assert
        Assert.Equal(expected, sut.Describe(entry));
    }

    [Fact]
    public void GivenAnUnknownCode_WhenDescribe_ThenTheCodeIsShown()
    {
        // Arrange
        var entry = new SyncEvent { Code = "SomethingNew" };

        // Act & Assert
        Assert.Equal("SomethingNew", sut.Describe(entry));
    }

    [Fact]
    public void GivenASummaryOfNothing_WhenDescribe_ThenUpToDate()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.PassCompleted, Code = SyncEventCodes.Summary, Arguments = ["0", "0", "0", "0", "0"] };

        // Act & Assert
        Assert.Equal("Up to date", sut.Describe(entry));
    }

    [Fact]
    public void GivenASummaryWithMissingOrUnreadableCounts_WhenDescribe_ThenOnlyTheCountsReadAreShown()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.PassCompleted, Code = SyncEventCodes.Summary, Arguments = ["2", "many"] };

        // Act & Assert
        Assert.Equal("2 uploaded", sut.Describe(entry));
    }

    [Fact]
    public void GivenASummaryOfEverything_WhenDescribe_ThenEachCountIsListed()
    {
        // Arrange
        var entry = new SyncEvent { Kind = SyncEventKind.PassCompleted, Code = SyncEventCodes.Summary, Arguments = ["1", "2", "3", "1", "4"] };

        // Act & Assert
        Assert.Equal("1 uploaded, 2 downloaded, 3 deleted, 1 conflict resolved, 4 failed", sut.Describe(entry));
    }

    [Fact]
    public void GivenNoCompletedPass_WhenDescribePass_ThenChecking()
    {
        // Act & Assert
        Assert.Equal("Checking for changes", sut.DescribePass(null));
    }

    [Theory]
    [InlineData(null, "All")]
    [InlineData(SyncEventGroup.Errors, "Errors")]
    [InlineData(SyncEventGroup.Connectivity, "Connectivity")]
    public void GivenAGroup_WhenDescribeGroup_ThenItsName(SyncEventGroup? group, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, sut.DescribeGroup(group));
    }
}
