using Devoctomy.Blazwiz.Encryption.Exceptions;
using Devoctomy.Blazwiz.Encryption.Models;
using Devoctomy.Blazwiz.Sync.Connectivity;
using Devoctomy.Blazwiz.Sync.Exceptions;
using Devoctomy.Blazwiz.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Models;
using Devoctomy.Blazwiz.UI.Sync.Text;
using Devoctomy.Blazwiz.UI.Text;

namespace Devoctomy.Blazwiz.UI.Sync.UnitTests.Text;

/// <summary>
/// Every code the sync module can produce has text, so users never see a raw code or key.
/// </summary>
public class SyncTextResourcesTests
{
    private static readonly HashSet<string> UiKeys = Keys(typeof(UiText));
    private static readonly HashSet<string> SyncKeys = Keys(typeof(SyncUiText));

    public static TheoryData<string> SyncAndE2eCodes() =>
        new(ConstantsOf(typeof(SyncErrorCodes)).Concat(ConstantsOf(typeof(E2eErrorCodes))));

    [Theory]
    [MemberData(nameof(SyncAndE2eCodes))]
    public void GivenSyncOrE2eErrorCode_WhenLookingForItsText_ThenSyncTextHasIt(string code)
    {
        // Act & Assert
        Assert.True(SyncKeys.Contains("Error." + code) || SyncKeys.Contains("Error." + code + ".Other"), code);
    }

    [Fact]
    public void GivenEveryLogCodeKindGroupReasonAndStep_WhenLookingForTheirText_ThenSyncTextHasThem()
    {
        // Arrange
        var missing = new List<string>();
        foreach (var code in ConstantsOf(typeof(SyncEventCodes)))
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

        // Act
        missing.AddRange(Enum.GetNames<E2eStep>().Select(s => "Sync.E2e.Step." + s).Where(k => !SyncKeys.Contains(k)));

        // Assert
        Assert.Empty(missing);
    }

    private static IEnumerable<string> ConstantsOf(Type type) =>
        type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!);

    private static HashSet<string> Keys(Type resource)
    {
        var set = new System.Resources.ResourceManager(resource.FullName!, resource.Assembly)
            .GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
        return set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).ToHashSet(StringComparer.Ordinal);
    }
}
