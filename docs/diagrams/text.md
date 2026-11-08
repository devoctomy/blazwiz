# Text: overrides, languages and pseudo-localisation

Every text the framework shows comes from resources (`.resx`), so an app can reword it and translate it: `UiText`
(`src/Devoctomy.Blazwiz.UI/Text/UiText.resx`) for the shell and its pages, `SyncUiText`
(`src/Devoctomy.Blazwiz.UI.Sync/Text/SyncUiText.resx`) for the sync module. The packages ship the neutral (English)
text only. Views and view models read it through `IStringLocalizer<UiText>` and the like, and error codes through
`IErrorText`.

Main classes:

- `OverridingStringLocalizerFactory` and `OverridingStringLocalizer` (`src/Devoctomy.Blazwiz.Core/Text/`): replace the
  standard `IStringLocalizerFactory`, so every `IStringLocalizer<T>` looks in the app's text first
- `TextBuilder` and `TextOptions` (`src/Devoctomy.Blazwiz.Core/Text/`), set up with `AddBlazwizText(text => ...)`:
  overrides, languages, plural rules, pseudo-localisation
- `TextPlurals` (`ITextPlurals`): which plural form a count takes in a language
- `ErrorText` (`src/Devoctomy.Blazwiz.UI/Text/ErrorText.cs`): describes a `BlazwizException`'s code from the resources
  registered with `AddErrorText<TResource>()`
- `CultureService` (`src/Devoctomy.Blazwiz.UI/Services/CultureService.cs`): the language at startup and its choice in Settings
- `FrameworkText` (`src/Devoctomy.Blazwiz.Testing/FrameworkText.cs`): checks an app's overrides in its tests

## 1. How a text is found

`OverridingStringLocalizer.FindUncached` looks for the key in the current UI culture first, then its parents
(`fr-FR`, `fr`, then the neutral text). In each culture it takes the first of:

1. an override set in code (`text.Override(key, text, culture)`),
2. the app's override resources (`text.AddOverrides<TResource>()`), the last added first,
3. the package's own resources.

So an app's French translation beats the package's English text, and the app's neutral (English) rewording is used
when no language more specific has the key. A key found nowhere shows as the key itself. Results are cached per
culture and key, until the overrides change.

```mermaid
flowchart TB
    Ask["text[&quot;Login.Title&quot;]<br/>IStringLocalizer&lt;UiText&gt;"]
    Culture["Culture = CurrentUICulture<br/>(e.g. fr-FR)"]
    Code{"Override in code<br/>for this culture?"}
    App{"App override resources<br/>(last added first)<br/>for this culture?"}
    Pkg{"Package resources<br/>(UiText.resx)<br/>for this culture?"}
    Parent{"A parent culture left?<br/>fr-FR, fr, neutral"}
    Found["The text, pseudo-localised<br/>if Text:PseudoLocalization is on"]
    Missing["The key itself<br/>(ResourceNotFound)"]

    Ask --> Culture --> Code
    Code -- yes --> Found
    Code -- no --> App
    App -- yes --> Found
    App -- no --> Pkg
    Pkg -- yes --> Found
    Pkg -- no --> Parent
    Parent -- "yes: try it" --> Code
    Parent -- no --> Missing
```

Text with placeholders (`text["Session.Remaining", "04:59"]`) is formatted with the current culture after it is found.
Sidebar items name their resources (`NavigationItem.TextResource`) and keys, and `NavigationMenuService` reads them
on every call, so the current language and overrides apply.

## 2. Overriding the framework's text

The template's notes example is an app override: `AddBlazwizText(text => text.AddOverrides<NotesText>())` with
`NotesText.resx`, whose keys are framework keys (for example the sync module's "files" wording) with the example's
own words. An app adds its translations the same way, as `MyText.fr.resx` beside `MyText.resx`.

An upgrade can rename or remove a key, or change its placeholders, and an override of it would silently stop working.
`FrameworkText` (Testing) catches both in the app's tests:

| Method | Finds |
|---|---|
| `UnknownOverrideKeys(typeof(MyText))` | Keys in the app's neutral overrides that are not framework text (never used) |
| `MismatchedOverridePlaceholders(typeof(MyText))` | Overrides whose placeholders (`{0}`) differ from the framework text's (wrong values, or a format error) |

## 3. Choosing the language

The languages an app offers are `Text:SupportedCultures` in `wwwroot/appsettings.json` (for example
`["en-GB", "fr-FR"]`), which `AddBlazwizUi` passes to `text.SupportedCultures(...)`. The first is the default. With
none set the app follows the browser and Settings shows no choice. `Program.cs` applies the language before the app
starts, because Blazor WebAssembly reads it once.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant Program as Program.cs
    participant Cult as CultureService
    participant Lang as LanguageSettingsViewModel
    participant Nav as NavigationService
    box rgb(255,245,230) Browser storage
        participant LS as localStorage
    end
    participant Doc as document (html lang)

    Program->>Cult: ApplySavedCultureAsync() (after Build, before RunAsync)
    alt No supported cultures configured
        Cult-->>Program: Nothing changed
    else Cultures configured
        Cult->>LS: GET myapp_culture
        Cult->>Cult: The saved culture, else the browser's (same culture, or same language), else the first
        Cult->>Cult: CultureInfo.DefaultThreadCurrentCulture / UICulture = culture
        Cult->>Doc: setLanguage(culture) (browser.js)
    end

    User->>Lang: Choose fr-FR in Settings
    Lang->>Cult: ChangeCultureAsync("fr-FR")
    Cult->>LS: SET myapp_culture = fr-FR
    Cult->>Nav: Reload() (the app starts again in French)
```

`LanguageTests` (template UI tests) covers choosing French, the reload, and the language being remembered.

## 4. Pseudo-localisation

`Text:PseudoLocalization` (testing only) makes `AddBlazwizUi` call `text.UsePseudoLocalization()`: every text found
in resources is shown with its letters accented and wrapped, for example `[!! Šéššîöñ 04:59 !!]`, leaving
placeholders alone. Text on screen that is not marked like that did not come from resources, so it cannot be reworded
or translated.

```mermaid
sequenceDiagram
    autonumber
    participant Test as PseudoLocalizationTests
    participant Browser as Playwright browser
    participant App as The app
    participant Loc as OverridingStringLocalizer

    Test->>Browser: Route **/appsettings*.json, adding Text:PseudoLocalization = true
    Test->>Test: Note the app's own text in AppSettings (title, description, copyright)
    Browser->>App: Open the sign-in page, sign in, each framework page
    App->>Loc: text[key]
    Loc-->>App: "[!! ... !!]"
    Test->>Browser: Read every visible text
    Test->>Test: Fail on text that is not marked, except the app's own (its settings, the user's name, its sidebar items)
```

The template's UI tests run it on the sign-in page, the password reset and every framework page.
