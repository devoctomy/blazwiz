using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Services;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.UnitTests.Services;

public class SyncEventPresenterTests
{
    private readonly SyncEventPresenter sut = new();

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
        var result = sut.Present(kind);

        Assert.Equal(icon, result.Icon);
        Assert.Equal(group, result.Group);
    }

    [Fact]
    public void GivenUnknownKind_WhenPresent_ThenGenericActivity()
    {
        var result = sut.Present((SyncEventKind)999);

        Assert.Equal("bi-circle", result.Icon);
        Assert.Equal(SyncEventGroup.Activity, result.Group);
    }

    [Fact]
    public void GivenPresentation_WhenChangedByCaller_ThenSharedTableUnaffected()
    {
        sut.Present(SyncEventKind.Failed).Icon = "changed";

        Assert.Equal("bi-x-circle", sut.Present(SyncEventKind.Failed).Icon);
    }
}
