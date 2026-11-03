using System.Globalization;
using blazwiz.Core.Text;
using blazwiz.Core.UnitTests.Text.Resources;
using Microsoft.Extensions.Localization;

namespace blazwiz.Core.UnitTests.Text;

public class OverridingStringLocalizerTests
{
    private readonly TextOptions options = new();

    [Fact]
    public void GivenNullArguments_WhenConstructing_ThenThrowsArgumentNullException()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new OverridingStringLocalizerFactory(null!)).ParamName);
        Assert.Equal("resources", Assert.Throws<ArgumentNullException>(() => new OverridingStringLocalizer(null!, options)).ParamName);
    }

    [Fact]
    public void GivenNoOverrides_WhenLookingUp_ThenPackageText()
    {
        using var culture = new CultureScope("en-GB");
        var sut = Create();

        var text = sut["Greeting"];

        Assert.Equal("Hello", text.Value);
        Assert.False(text.ResourceNotFound);
    }

    [Fact]
    public void GivenUnknownKey_WhenLookingUp_ThenKeyReturnedAndNotFound()
    {
        using var culture = new CultureScope("en-GB");
        var text = Create()["Missing.Key", 1];

        Assert.Equal("Missing.Key", text.Value);
        Assert.True(text.ResourceNotFound);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("fr-CA")]
    [InlineData("fr")]
    public void GivenFrenchCulture_WhenLookingUp_ThenPackageFrenchTextWithFallbackToNeutral(string name)
    {
        using var culture = new CultureScope(name);
        var sut = Create();

        Assert.Equal("Bonjour", sut["Greeting"].Value);
        Assert.Equal("Neutral only", sut["OnlyNeutral"].Value);
    }

    [Fact]
    public void GivenAppNeutralOverride_WhenLookingUpInEnglish_ThenAppText()
    {
        using var culture = new CultureScope("en-GB");
        options.OverrideResources.Add(new System.Resources.ResourceManager(typeof(AppOverrides).FullName!, typeof(AppOverrides).Assembly));

        Assert.Equal("App neutral", Create()["Shared"].Value);
    }

    [Fact]
    public void GivenAppNeutralOverrideAndPackageFrench_WhenLookingUpInFrench_ThenPackageFrenchWins()
    {
        using var culture = new CultureScope("fr-FR");
        new TextBuilder(options).AddOverrides<AppOverrides>();

        // The app only reworded the English, so French users still get the package's translation.
        Assert.Equal("Paquet français", Create()["Shared"].Value);
    }

    [Fact]
    public void GivenAppFrenchOverride_WhenLookingUpInFrench_ThenAppFrenchWins()
    {
        using var culture = new CultureScope("fr-FR");
        new TextBuilder(options).AddOverrides<AppOverrides>();

        Assert.Equal("Salut", Create()["Greeting"].Value);
    }

    [Fact]
    public void GivenCodeOverrides_WhenLookingUp_ThenCultureSpecificThenNeutralCodeOverridesWin()
    {
        new TextBuilder(options).AddOverrides<AppOverrides>()
            .Override("Shared", "Code neutral")
            .Override("Greeting", "Coucou", "fr");

        using (new CultureScope("en-GB"))
        {
            Assert.Equal("Code neutral", Create()["Shared"].Value);
            Assert.Equal("Hello", Create()["Greeting"].Value);
        }

        using (new CultureScope("fr-FR"))
        {
            Assert.Equal("Coucou", Create()["Greeting"].Value);
        }
    }

    [Fact]
    public void GivenArguments_WhenLookingUp_ThenFormattedWithTheCurrentCulture()
    {
        using var culture = new CultureScope("fr-FR");
        var sut = Create();

        Assert.Equal("Costs 1,5", sut["Price", 1.5].Value);
    }

    [Fact]
    public void GivenOverriddenFormat_WhenLookingUpWithArguments_ThenOverrideFormatted()
    {
        using var culture = new CultureScope("en-GB");
        new TextBuilder(options).AddOverrides<AppOverrides>();

        Assert.Equal("a at b", Create()["Named", "a", "b"].Value);
    }

    [Fact]
    public void GivenResources_WhenGetAllStrings_ThenEveryKeyWithItsText()
    {
        using var culture = new CultureScope("fr-FR");

        var all = Create().GetAllStrings(includeParentCultures: true).ToDictionary(s => s.Name, s => s.Value);

        Assert.Equal("Bonjour", all["Greeting"]);
        Assert.Equal("Neutral only", all["OnlyNeutral"]);

        // Without parents only the exact culture counts: fr-FR has no resources of its own, fr has two.
        Assert.Empty(Create().GetAllStrings(includeParentCultures: false));
        using var french = new CultureScope("fr");
        Assert.Equal(["Greeting", "Shared"], Create().GetAllStrings(includeParentCultures: false).Select(s => s.Name));
    }

    [Fact]
    public void GivenFactory_WhenCreatingTwiceOrByName_ThenSameLocalizer()
    {
        var sut = new OverridingStringLocalizerFactory(options);

        var byType = sut.Create(typeof(PackageText));

        Assert.Same(byType, sut.Create(typeof(PackageText)));
        Assert.Same(byType, sut.Create(typeof(PackageText).FullName!, typeof(PackageText).Assembly.GetName().Name!));
        Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
        Assert.Throws<ArgumentException>(() => sut.Create(" ", "x"));
    }

    private IStringLocalizer Create() => new OverridingStringLocalizerFactory(options).Create(typeof(PackageText));

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string name)
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
