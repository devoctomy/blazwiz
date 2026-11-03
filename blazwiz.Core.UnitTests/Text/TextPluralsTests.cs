using System.Globalization;
using blazwiz.Core.Extensions;
using blazwiz.Core.Text;
using blazwiz.Core.Text.Interfaces;
using blazwiz.Core.UnitTests.Text.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace blazwiz.Core.UnitTests.Text;

public class TextPluralsTests
{
    private readonly TextOptions options = new();

    [Theory]
    [InlineData(0, PluralCategory.Other)]
    [InlineData(1, PluralCategory.One)]
    [InlineData(2, PluralCategory.Other)]
    public void GivenEnglish_WhenGetCategory_ThenOneOrOther(long count, PluralCategory expected)
    {
        Assert.Equal(expected, new TextPlurals(options).GetCategory(CultureInfo.GetCultureInfo("en-GB"), count));
    }

    [Fact]
    public void GivenRulesForALanguage_WhenGetCategory_ThenThoseRulesUsed()
    {
        new TextBuilder(options).UsePluralRules("pl", n => n == 1 ? PluralCategory.One : n % 10 is >= 2 and <= 4 ? PluralCategory.Few : PluralCategory.Many);
        var sut = new TextPlurals(options);

        Assert.Equal(PluralCategory.Few, sut.GetCategory(CultureInfo.GetCultureInfo("pl-PL"), 3));
        Assert.Equal(PluralCategory.Many, sut.GetCategory(CultureInfo.GetCultureInfo("pl-PL"), 5));
    }

    [Fact]
    public void GivenCountsAndForms_WhenFormat_ThenRightFormWithCountAndArguments()
    {
        var text = new OverridingStringLocalizerFactory(options).Create(typeof(PackageText));
        var sut = new TextPlurals(options);

        Assert.Equal("1 file", sut.Format(text, "Files", 1));
        Assert.Equal("3 files", sut.Format(text, "Files", 3));

        // No .One form: falls back to .Other.
        Assert.Equal("1 moved to OPFS", sut.Format(text, "Moved", 1, "OPFS"));
    }

    [Fact]
    public void GivenBadArguments_WhenUsed_ThenThrows()
    {
        var sut = new TextPlurals(options);
        Assert.Throws<ArgumentNullException>(() => new TextPlurals(null!));
        Assert.Throws<ArgumentNullException>(() => sut.GetCategory(null!, 1));
        Assert.Throws<ArgumentNullException>(() => sut.Format(null!, "Files", 1));
        Assert.Throws<ArgumentException>(() => sut.Format(new OverridingStringLocalizerFactory(options).Create(typeof(PackageText)), " ", 1));
    }

    [Fact]
    public void GivenBuilder_WhenConfigured_ThenOptionsHoldIt()
    {
        var sut = new TextBuilder(options)
            .Override("A", "one")
            .Override("A", "un", "fr")
            .SupportedCultures("en-GB", "fr-FR");

        Assert.Same(options, sut.Options);
        Assert.Equal("one", options.Overrides["A"][string.Empty]);
        Assert.Equal("un", options.Overrides["A"]["fr"]);
        Assert.Equal(["en-GB", "fr-FR"], options.SupportedCultures.Select(c => c.Name));
        Assert.Throws<ArgumentException>(() => sut.Override(" ", "x"));
        Assert.Throws<ArgumentNullException>(() => sut.Override("A", null!));
        Assert.Throws<CultureNotFoundException>(() => sut.Override("A", "x", "not a culture"));
        Assert.Throws<ArgumentNullException>(() => sut.UsePluralRules("pl", null!));
        Assert.Throws<ArgumentNullException>(() => new TextBuilder(null!));
    }

    [Fact]
    public void GivenServices_WhenAddBlazwizTextTwice_ThenOneOptionsWithBothAndLocalizersResolvable()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddBlazwizText(t => t.Override("A", "first"));
        services.AddBlazwizText(t => t.Override("B", "second"));

        // Assert
        using var provider = services.BuildServiceProvider();
        var options = Assert.Single(provider.GetServices<TextOptions>());
        Assert.Equal(["A", "B"], options.Overrides.Keys.Order());
        Assert.IsType<OverridingStringLocalizerFactory>(provider.GetRequiredService<IStringLocalizerFactory>());
        Assert.Equal("Hello", provider.GetRequiredService<IStringLocalizer<PackageText>>()["Greeting"].Value);
        Assert.IsType<TextPlurals>(provider.GetRequiredService<ITextPlurals>());
        Assert.Throws<ArgumentNullException>(() => TextServiceCollectionExtensions.AddBlazwizText(null!));
    }
}
