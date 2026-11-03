using System.Collections;
using System.Reflection;
using System.Resources;
using Devoctomy.Blazwiz.Auth.Exceptions;
using Devoctomy.Blazwiz.UI.Text;

namespace Devoctomy.Blazwiz.Tests.Text;

/// <summary>
/// Every code the framework can produce has text, so users never see a raw code or key.
/// </summary>
public class TextResourcesTests
{
    private static readonly HashSet<string> UiKeys = Keys(typeof(UiText));

    public static TheoryData<string> AuthCodes() => new(ConstantsOf(typeof(AuthErrorCodes)));

    [Theory]
    [MemberData(nameof(AuthCodes))]
    public void GivenAuthErrorCode_WhenLookingForItsText_ThenUiTextHasIt(string code)
    {
        // Act & Assert
        Assert.Contains("Error." + code, UiKeys);
    }

    internal static IEnumerable<string> ConstantsOf(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!);

    internal static HashSet<string> Keys(Type resource)
    {
        var set = new ResourceManager(resource.FullName!, resource.Assembly)
            .GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!;
        return set.Cast<DictionaryEntry>().Select(e => (string)e.Key).ToHashSet(StringComparer.Ordinal);
    }
}
