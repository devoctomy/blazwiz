# Text and translation

Every piece of text the framework shows comes from **resources**: `.resx` files, the standard .NET way to keep text
apart from code. That lets your app reword any of it, and translate it into other languages, without touching the
framework.

- `UiText` (`Devoctomy.Blazwiz.UI.Text`) holds the shell's text: sign in, Profile, Settings, errors.
- `SyncUiText` (`Devoctomy.Blazwiz.UI.Sync.Text`) holds the sync module's.

The packages ship English text only.

## Reading text

Views and view models read text through `IStringLocalizer<T>`, where `T` is the class the resources are named after:

```razor
@inject IStringLocalizer<UiText> Text

<button type="button" class="btn btn-secondary">@Text["Common.Cancel"]</button>
```

```csharp
public class MyViewModel(IStringLocalizer<SyncUiText> text) : ViewModelBase
{
    public string Warning => text["Sync.E2e.NoRecoveryWarning"];
}
```

Text with placeholders takes arguments, in the usual `{0}` style:

```csharp
text["Sync.E2e.Banner.NeedsPassphrase", profileName]   // "{0} needs a passphrase before it can be used."
```

A key that is found nowhere shows as the key itself, which makes a typo easy to spot.

## Your app's own text

The same mechanism works for your own text. Make an empty class with a `.resx` file next to it, with the same name:

```csharp
namespace MyApp.Portal.Application.Text;

public sealed class AppText
{
    private AppText()
    {
    }
}
```

`MyApp.Portal.Application/Text/AppText.resx` holds the English text, and `AppText.fr.resx` the French. Inject
`IStringLocalizer<AppText>` to read it.

The template's own pages use plain English in their markup, which is fine for an app in one language. Move the text
to resources when you want to translate it.

## Rewording the framework's text

Give your resources the **same keys** as the framework's, and add them as overrides:

```csharp
builder.Services.AddBlazwizText(text => text.AddOverrides<AppText>());
```

Any key in `AppText.resx` that the framework also has now shows your text instead. The template's notes example does
this: the sync module says "files", and `NotesText.resx` rewords it to say "notes" (for example, `Sync.Profile.Files`
becomes "Notes"). It is added in `AddNotes` with `AddBlazwizText(text => text.AddOverrides<NotesText>())`.

You can also override a single text in code:

```csharp
builder.Services.AddBlazwizText(text => text.Override("Common.Cancel", "Never mind"));
```

An override must keep the original's placeholders (`{0}`, `{1}` ...). Find the keys in the framework's resource files:
`src/Devoctomy.Blazwiz.UI/Text/UiText.resx` and `src/Devoctomy.Blazwiz.UI.Sync/Text/SyncUiText.resx`.

How a text is chosen, for the current language, first match wins:

1. an override set in code (`Override`);
2. your override resources (`AddOverrides`), the last added first;
3. the package's own resources.

Each step tries the exact language first (`fr-FR`), then the general one (`fr`), then English.

**After a framework upgrade**, a key you override may have been renamed or removed, or its placeholders changed. Add
a test with `Devoctomy.Blazwiz.Testing` so you find out at once (see [Testing your app](testing.md#checking-your-text-overrides)).

## Offering more languages

1. List the languages in `wwwroot/appsettings.json`. The first is the default:

   ```json
   "Text": { "SupportedCultures": [ "en-GB", "fr-FR" ] }
   ```

   (Or in code: `AddBlazwizText(text => text.SupportedCultures("en-GB", "fr-FR"))`.)

2. Translate the framework's text with your own resources, for example `Text/AppText.fr.resx` holding the framework's
   keys (`Settings.Language.Title`, ...) in French, added with `AddBlazwizText(text => text.AddOverrides<AppText>())`.

Settings then shows a Language section. The user's choice is kept on the device; until they choose, the browser's
language is used when the app has it. `Program.cs` applies it before the app starts:

```csharp
await scope.ServiceProvider.GetRequiredService<ICultureService>().ApplySavedCultureAsync();
```

Dates and numbers follow the language without any work.

### Plurals

Text that depends on a count ("1 profile unlocked", "3 profiles unlocked") has one resource per plural form, named
`<key>.One`, `<key>.Other` and so on (`PluralCategory`: `Zero`, `One`, `Two`, `Few`, `Many`, `Other`). Inject
`ITextPlurals` and let it pick:

```csharp
var label = plurals.Format(text, "Sync.E2e.Indicator.Unlocked", count);
```

The count is placeholder `{0}`. English (and any language without rules of its own) uses `One` and `Other`. Add rules
for a language that needs more forms:

```csharp
builder.Services.AddBlazwizText(text => text.UsePluralRules("pl", count => /* return a PluralCategory */ PluralCategory.Other));
```

## Sidebar items

Give a `NavigationItem` a `TextResource`, and its `Text` and `Description` become keys:

```csharp
services.AddNavigationItem(new NavigationItem
{
    Text = "Nav.Things",
    Description = "Nav.Things.Description",
    TextResource = typeof(AppText),
    Href = "things",
    Icon = "bi-box",
    Order = 20,
});
```

## Errors

Errors users might see are thrown as `BlazwizException` (`Devoctomy.Blazwiz.Core.Exceptions`), with a **code**
rather than English text. The text is the resource `Error.<code>`. For example, the code `E2e.WrongPassphrase` is shown
with `Error.E2e.WrongPassphrase` from `SyncUiText`.

Show any exception with `IErrorText`:

```csharp
Error = errorText.Describe(ex);
```

A `BlazwizException` becomes its `Error.<code>` text, formatted with its `Arguments`. Anything else becomes a general
message (`Error.Unknown`), never the exception's own text.

Your app can use the same approach for its own errors: throw `new BlazwizException("Things.NameTaken", name)`, put
`Error.Things.NameTaken` in your resources, and register them:

```csharp
builder.Services.AddErrorText<AppText>();
```

## Finding text that isn't from resources

Pseudo-localisation replaces every text from resources with an accented, bracketed version (`[!! Śéţţîñĝš !!]`). Any
plain text left on screen is hard-coded, so it can't be translated. Turn it on with `"Text": { "PseudoLocalization":
true }` in the settings, or `AddBlazwizText(text => text.UsePseudoLocalization())`. It is for testing only. The
template's `PseudoLocalizationTests` UI tests use it to check the framework's pages.

More detail: [docs/diagrams/text.md](../diagrams/text.md).
