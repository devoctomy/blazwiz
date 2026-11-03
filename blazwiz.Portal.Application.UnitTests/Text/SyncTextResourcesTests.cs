using blazwiz.Core.Encryption.Exceptions;
using blazwiz.Core.Encryption.Models;
using blazwiz.Core.Sync.Connectivity;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Models;
using blazwiz.Portal.Application.Text;
using blazwiz.Portal.Models.Sync;

namespace blazwiz.Portal.Application.UnitTests.Text;

/// <summary>
/// Every code the sync module can produce has text, so users never see a raw code or key.
/// </summary>
public class SyncTextResourcesTests
{
    private static readonly HashSet<string> UiKeys = TextResourcesTests.Keys(typeof(UiText));
    private static readonly HashSet<string> SyncKeys = TextResourcesTests.Keys(typeof(SyncUiText));

    public static TheoryData<string> SyncAndE2eCodes() =>
        new(TextResourcesTests.ConstantsOf(typeof(SyncErrorCodes)).Concat(TextResourcesTests.ConstantsOf(typeof(E2eErrorCodes))));

    [Theory]
    [MemberData(nameof(SyncAndE2eCodes))]
    public void GivenSyncOrE2eErrorCode_WhenLookingForItsText_ThenSyncTextHasIt(string code)
    {
        Assert.True(SyncKeys.Contains("Error." + code) || SyncKeys.Contains("Error." + code + ".Other"), code);
    }

    [Fact]
    public void GivenEveryLogCodeKindGroupReasonAndStep_WhenLookingForTheirText_ThenSyncTextHasThem()
    {
        var missing = new List<string>();
        foreach (var code in TextResourcesTests.ConstantsOf(typeof(SyncEventCodes)))
        {
            // The summary is built from its parts.
            if (code != SyncEventCodes.Summary && !SyncKeys.Contains("Sync.Log." + code))
            {
                missing.Add("Sync.Log." + code);
            }
        }

        missing.AddRange(Enum.GetNames<SyncEventKind>().Select(k => "Sync.Log.Kind." + k).Where(k => !SyncKeys.Contains(k)));
        missing.AddRange(Enum.GetNames<SyncEventGroup>().Select(g => "Sync.Log.Group." + g).Where(k => !SyncKeys.Contains(k)));
        missing.AddRange(Enum.GetNames<OfflineReason>().Select(r => "Sync.Connectivity." + r).Where(k => !SyncKeys.Contains(k)));
        missing.AddRange(Enum.GetNames<E2eStep>().Select(s => "Sync.E2e.Step." + s).Where(k => !SyncKeys.Contains(k)));

        Assert.Empty(missing);
    }

    [Fact]
    public void GivenTheNotesOverrides_WhenCompared_ThenEveryKeyIsAFrameworkKey()
    {
        var unknown = TextResourcesTests.Keys(typeof(NotesText)).Where(k => !SyncKeys.Contains(k) && !UiKeys.Contains(k)).ToList();

        Assert.Empty(unknown);
    }
}
