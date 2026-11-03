using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Text;
using blazwiz.Portal.Application.Text;

namespace blazwiz.Portal.Application.UnitTests.Text;

/// <summary>
/// The sync module's error codes, described with its own text (<see cref="SyncUiText"/>).
/// </summary>
public class SyncErrorTextTests
{
    private readonly TestText testText = new();

    [Fact]
    public void GivenCodedException_WhenDescribe_ThenItsTextWithArguments()
    {
        var text = testText.Errors.Describe(new SyncStorageAccessException(SyncErrorCodes.FolderAccessNeeded, "Notes"));

        Assert.Equal("Allow access to the folder 'Notes' to use your files.", text);
    }

    [Theory]
    [InlineData(1, "1 file could not be synced.")]
    [InlineData(3, "3 files could not be synced.")]
    public void GivenCodeWithPluralText_WhenDescribe_ThenFormForTheCount(int count, string expected)
    {
        Assert.Equal(expected, testText.Errors.Describe(SyncErrorCodes.FilesFailed, [count]));
    }

    [Fact]
    public void GivenModuleWithoutItsErrorText_WhenDescribe_ThenGeneralText()
    {
        var sut = new ErrorText(new OverridingStringLocalizerFactory(testText.Options), [], testText.Plurals);

        Assert.Equal("Something went wrong.", sut.Describe(SyncErrorCodes.RemoteUnreachable, []));
    }

    [Fact]
    public void GivenAppOverride_WhenDescribe_ThenOverrideUsed()
    {
        new TextBuilder(testText.Options).Override("Error.Sync.Remote.Unreachable", "The cloud is down.");

        Assert.Equal("The cloud is down.", testText.Errors.Describe(new SyncNetworkException(SyncErrorCodes.RemoteUnreachable)));
    }
}
