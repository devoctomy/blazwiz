using System.Collections;
using System.Globalization;
using System.Resources;
using Devoctomy.Blazwiz.Core.Text;
using Devoctomy.Blazwiz.Core.UnitTests.Text.Resources;
using Microsoft.Extensions.Localization;

namespace Devoctomy.Blazwiz.Core.UnitTests.Text;

public class OverridingStringLocalizerTests
{
    private readonly TextOptions options = new();

    [Fact]
    public void GivenNullArguments_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new OverridingStringLocalizerFactory(null!)).ParamName);
        Assert.Equal("resources", Assert.Throws<ArgumentNullException>(() => new OverridingStringLocalizer(null!, options)).ParamName);
    }

    [Fact]
    public void GivenNoOverrides_WhenLookingUp_ThenPackageText()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        var sut = Create();

        // Act
        var text = sut["Greeting"];

        // Assert
        Assert.Equal("Hello", text.Value);
        Assert.False(text.ResourceNotFound);
    }

    [Fact]
    public void GivenUnknownKey_WhenLookingUp_ThenKeyReturnedAndNotFound()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");

        // Act
        var text = Create()["Missing.Key", 1];

        // Assert
        Assert.Equal("Missing.Key", text.Value);
        Assert.True(text.ResourceNotFound);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("fr-CA")]
    [InlineData("fr")]
    public void GivenFrenchCulture_WhenLookingUp_ThenPackageFrenchTextWithFallbackToNeutral(string name)
    {
        // Arrange
        using var culture = new CultureScope(name);

        // Act
        var sut = Create();

        // Assert
        Assert.Equal("Bonjour", sut["Greeting"].Value);
        Assert.Equal("Neutral only", sut["OnlyNeutral"].Value);
    }

    [Fact]
    public void GivenAppNeutralOverride_WhenLookingUpInEnglish_ThenAppText()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");

        // Act
        options.OverrideResources.Add(new System.Resources.ResourceManager(typeof(AppOverrides).FullName!, typeof(AppOverrides).Assembly));

        // Assert
        Assert.Equal("App neutral", Create()["Shared"].Value);
    }

    [Fact]
    public void GivenAppNeutralOverrideAndPackageFrench_WhenLookingUpInFrench_ThenPackageFrenchWins()
    {
        // Arrange
        using var culture = new CultureScope("fr-FR");

        // Act
        new TextBuilder(options).AddOverrides<AppOverrides>();

        // Assert
        // The app only reworded the English, so French users still get the package's translation.
        Assert.Equal("Paquet français", Create()["Shared"].Value);
    }

    [Fact]
    public void GivenAppFrenchOverride_WhenLookingUpInFrench_ThenAppFrenchWins()
    {
        // Arrange
        using var culture = new CultureScope("fr-FR");

        // Act
        new TextBuilder(options).AddOverrides<AppOverrides>();

        // Assert
        Assert.Equal("Salut", Create()["Greeting"].Value);
    }

    [Fact]
    public void GivenCodeOverrides_WhenLookingUp_ThenCultureSpecificThenNeutralCodeOverridesWin()
    {
        // Arrange
        new TextBuilder(options).AddOverrides<AppOverrides>()
            .Override("Shared", "Code neutral")
            .Override("Greeting", "Coucou", "fr");

        using (new CultureScope("en-GB"))
        {
            Assert.Equal("Code neutral", Create()["Shared"].Value);
            Assert.Equal("Hello", Create()["Greeting"].Value);
        }

        // Act & Assert
        using (new CultureScope("fr-FR"))
        {
            Assert.Equal("Coucou", Create()["Greeting"].Value);
        }
    }

    [Fact]
    public void GivenArguments_WhenLookingUp_ThenFormattedWithTheCurrentCulture()
    {
        // Arrange
        using var culture = new CultureScope("fr-FR");

        // Act
        var sut = Create();

        // Assert
        Assert.Equal("Costs 1,5", sut["Price", 1.5].Value);
    }

    [Fact]
    public void GivenOverriddenFormat_WhenLookingUpWithArguments_ThenOverrideFormatted()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");

        // Act
        new TextBuilder(options).AddOverrides<AppOverrides>();

        // Assert
        Assert.Equal("a at b", Create()["Named", "a", "b"].Value);
    }

    [Fact]
    public void GivenResources_WhenGetAllStrings_ThenEveryKeyWithItsText()
    {
        // Arrange
        using var culture = new CultureScope("fr-FR");

        // Act & Assert
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
        // Arrange
        var sut = new OverridingStringLocalizerFactory(options);

        var byType = sut.Create(typeof(PackageText));

        // Act & Assert
        Assert.Same(byType, sut.Create(typeof(PackageText)));
        Assert.Same(byType, sut.Create(typeof(PackageText).FullName!, typeof(PackageText).Assembly.GetName().Name!));
        Assert.Throws<ArgumentNullException>(() => sut.Create(null!));
        Assert.Throws<ArgumentException>(() => sut.Create(" ", "x"));
    }

    [Fact]
    public void GivenPseudoLocalization_WhenGettingText_ThenAccentedAndWrapped()
    {
        // Arrange
        new TextBuilder(options).UsePseudoLocalization();
        var sut = Create();

        // Act
        var result = sut["Greeting"];

        // Assert
        Assert.Equal("[!! Héļļö !!]", result.Value);
        Assert.False(result.ResourceNotFound);
    }

    [Fact]
    public void GivenPseudoLocalization_WhenGettingTextWithArguments_ThenPlaceholdersAndArgumentsKept()
    {
        // Arrange
        new TextBuilder(options).UsePseudoLocalization();
        var sut = Create();

        // Act
        var result = sut["Named", "Notes", "Work"];

        // Assert
        Assert.Equal("[!! Notes îñ Work !!]", result.Value);
    }

    [Fact]
    public void GivenPseudoLocalization_WhenTextIsMissing_ThenTheKeyIsNotWrapped()
    {
        // Arrange
        new TextBuilder(options).UsePseudoLocalization();
        var sut = Create();

        // Act
        var result = sut["Not.A.Key"];

        // Assert
        Assert.Equal("Not.A.Key", result.Value);
        Assert.True(result.ResourceNotFound);
    }

    [Fact]
    public void GivenNoPseudoLocalization_WhenGettingText_ThenPlain()
    {
        // Arrange
        var sut = Create();

        // Act
        var result = sut["Greeting"];

        // Assert
        Assert.False(options.PseudoLocalization);
        Assert.Equal("Hello", result.Value);
    }

    [Fact]
    public void GivenTextAlreadyUsed_WhenAnOverrideIsAddedAfterwards_ThenTheOverrideIsUsed()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        var sut = Create();
        var before = sut["Shared"].Value;

        // Act
        options.OverrideResources.Add(new System.Resources.ResourceManager(typeof(AppOverrides).FullName!, typeof(AppOverrides).Assembly));

        // Assert
        Assert.NotEqual("App neutral", before);
        Assert.Equal("App neutral", sut["Shared"].Value);
    }

    [Fact]
    public void GivenTextUsedInOneCulture_WhenUsedInAnother_ThenEachCulturesOwnText()
    {
        // Arrange
        var sut = Create();
        string english;
        using (new CultureScope("en-GB"))
        {
            english = sut["Greeting"].Value;
        }

        // Act
        string french;
        using (new CultureScope("fr-FR"))
        {
            french = sut["Greeting"].Value;
        }

        // Assert
        Assert.Equal("Bonjour", french);
        Assert.NotEqual(french, english);
        using (new CultureScope("en-GB"))
        {
            Assert.Equal(english, sut["Greeting"].Value);
        }
    }

    [Fact]
    public void GivenAMissingKeyLookedUpTwice_WhenLookingUp_ThenItIsStillReportedMissing()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        var sut = Create();
        _ = sut["NoSuchKey"];

        // Act
        var text = sut["NoSuchKey"];

        // Assert
        Assert.True(text.ResourceNotFound);
        Assert.Equal("NoSuchKey", text.Value);
    }

    [Fact]
    public void GivenNullOptions_WhenConstructing_ThenThrowsArgumentNullException()
    {
        // Arrange
        var resources = new ResourceManager(typeof(PackageText).FullName!, typeof(PackageText).Assembly);

        // Act & Assert
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => new OverridingStringLocalizer(resources, null!)).ParamName);
    }

    [Fact]
    public void GivenAnOverrideResourceWithoutAManifest_WhenLookingUp_ThenItIsSkippedForThePackageText()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        options.OverrideResources.Add(new FakeResourceManager(_ => throw new MissingManifestResourceException("no manifest")));
        var sut = Create();

        // Act
        var text = sut["Greeting"];

        // Assert
        Assert.Equal("Hello", text.Value);
        Assert.False(text.ResourceNotFound);
    }

    [Fact]
    public void GivenResourcesThatAreNotText_WhenGetAllStrings_ThenOnlyTheTextIsListed()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        var set = new ResourceSet(new HashtableResourceReader(new Hashtable { ["Title"] = "My app", ["Logo"] = new byte[] { 1, 2, 3 } }));
        var sut = new OverridingStringLocalizer(new FakeResourceManager(_ => set), options);

        // Act
        var all = sut.GetAllStrings(includeParentCultures: false).ToList();

        // Assert
        var only = Assert.Single(all);
        Assert.Equal("Title", only.Name);
        Assert.Equal("My app", only.Value);
        Assert.Equal("Fake", only.SearchedLocation);
    }

    [Fact]
    public void GivenNoResourceSetForTheCulture_WhenGetAllStrings_ThenNothingIsListed()
    {
        // Arrange
        using var culture = new CultureScope("en-GB");
        var sut = new OverridingStringLocalizer(new FakeResourceManager(_ => null), options);

        // Act
        var all = sut.GetAllStrings(includeParentCultures: true);

        // Assert
        Assert.Empty(all);
    }

    private IStringLocalizer Create() => new OverridingStringLocalizerFactory(options).Create(typeof(PackageText));

    /// <summary>
    /// A resource manager answering every culture from a function.
    /// </summary>
    private sealed class FakeResourceManager(Func<CultureInfo, ResourceSet?> sets) : ResourceManager
    {
        public override string BaseName => "Fake";

        public override ResourceSet? GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) => sets(culture);
    }

    /// <summary>
    /// Reads resources from a hashtable, so a set can hold values of any type.
    /// </summary>
    private sealed class HashtableResourceReader(Hashtable values) : IResourceReader
    {
        public IDictionaryEnumerator GetEnumerator() => values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }

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
