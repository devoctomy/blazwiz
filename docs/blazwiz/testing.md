# Testing your app

An app made with `--tests` (the default) has two test projects:

- `MyApp.Portal.Application.UnitTests`: fast unit tests of your view models and services, with xUnit and Moq. Its
  folders mirror `MyApp.Portal.Application`.
- `MyApp.Portal.UITests`: end to end tests that drive the real app in a browser, with Playwright and MSTest.

```powershell
dotnet test MyApp.Portal.Application.UnitTests
dotnet test MyApp.Portal.UITests
```

## Unit tests

Because the logic lives in view models and services, not in Razor files, you test it by creating the class with mocks
and calling it. No browser is needed. The template's tests follow one style:

- Name each test `Given<State>_When<Action>_Then<Outcome>`.
- Mark its parts with `// Arrange`, `// Act` and `// Assert` comments (`// Act & Assert` when the assertion does the
  acting, as with `Assert.Throws`).

From the template's `DashboardViewModelTests`:

```csharp
public class DashboardViewModelTests
{
    private readonly Mock<IAuthenticationService> mockAuth = new();
    private readonly Mock<IGreetingService> mockGreeting = new();
    private readonly Mock<INavigationMenuService> mockMenu = new();
    private readonly AppSettings appSettings = new();

    [Fact]
    public async Task GivenSignedInUser_WhenInitialized_ThenGreetsUserByDisplayName()
    {
        // Arrange
        mockAuth.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(new UserSession { DisplayName = "Bob" });
        mockGreeting.Setup(x => x.GetGreeting("Bob")).Returns("Good morning, Bob");
        var sut = new DashboardViewModel(mockAuth.Object, mockGreeting.Object, mockMenu.Object, appSettings);

        // Act
        await sut.OnInitializedAsync();

        // Assert
        Assert.Equal("Good morning, Bob", sut.Greeting);
    }
}
```

Call view model lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`) yourself, and run commands as the view
would (`sut.SaveCommand.ExecuteAsync(null)`).

### Time

Never read the clock directly (`DateTime.Now`, `DateTimeOffset.UtcNow`). Take a `TimeProvider` in the constructor: the
framework registers `TimeProvider.System`. In tests, pass a `FakeTimeProvider` (from the
`Microsoft.Extensions.TimeProvider.Testing` package, already referenced) and set or move its time:

```csharp
private readonly FakeTimeProvider timeProvider = new();

[Fact]
public void GivenMorning_WhenGettingTheGreeting_ThenSaysGoodMorning()
{
    // Arrange
    timeProvider.SetUtcNow(new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero));
    timeProvider.SetLocalTimeZone(TimeZoneInfo.Utc);
    var sut = new GreetingService(timeProvider);

    // Act
    var result = sut.GetGreeting("Bob");

    // Assert
    Assert.Equal("Good morning, Bob", result);
}
```

`timeProvider.Advance(TimeSpan.FromMinutes(5))` moves time on, which also fires timers, so timeouts and delays can be
tested without waiting.

### The framework's real text

View models that show framework text take an `IStringLocalizer<UiText>` (or `SyncUiText`) or an `IErrorText`. Rather
than mock them, use `FrameworkText` from `Devoctomy.Blazwiz.Testing`: it gives you the framework's real text, as your
app gets it, so tests can check the actual wording.

```csharp
private readonly FrameworkText testText = new(typeof(SyncUiText));   // pass the text of the modules your app uses

var text = testText.For<SyncUiText>();   // IStringLocalizer<SyncUiText>
var ui = testText.Ui;                    // IStringLocalizer<UiText>
var errors = testText.Errors;            // IErrorText
var plurals = testText.Plurals;          // ITextPlurals
```

The template's `NotebooksViewModelTests` builds its view model this way.

### Checking your text overrides

If your app rewords framework text ([Text and translation](text-and-translation.md#rewording-the-frameworks-text)),
an upgrade could rename or remove a key you override, or change its placeholders. Your override would then silently
stop working. Two checks catch it; the template's `NotesTextTests` has both:

```csharp
public class NotesTextTests
{
    private readonly FrameworkText framework = new(typeof(SyncUiText));

    [Fact]
    public void GivenTheNotesOverrides_WhenComparedWithTheFramework_ThenEveryKeyIsFrameworkText()
    {
        // Act
        var unknown = framework.UnknownOverrideKeys(typeof(NotesText));

        // Assert
        Assert.Empty(unknown);
    }

    [Fact]
    public void GivenTheNotesOverrides_WhenComparedWithTheFramework_ThenTheirPlaceholdersMatch()
    {
        // Act
        var mismatched = framework.MismatchedOverridePlaceholders(typeof(NotesText));

        // Assert
        Assert.Empty(mismatched);
    }
}
```

Write the same for your own override resources, and run them after every framework upgrade.

### Coverage

The unit test project references `coverlet.collector`, so you can measure which lines your tests reach:

```powershell
dotnet test MyApp.Portal.Application.UnitTests --collect:"XPlat Code Coverage"
```

Razor markup is not run by unit tests, which is another reason to keep logic out of it.

## UI tests

The UI tests (`MyApp.Portal.UITests`) build and start the app themselves, on a free port, and give each test a fresh
browser, so local storage starts empty. The app's output goes to `TestResults/portal.log`, and screenshots to
`TestResults/Screenshots`.

Install Playwright's browser once:

```powershell
dotnet build MyApp.Portal.UITests
pwsh MyApp.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium
```

- To test an app that is already running, or deployed, set `MYAPP_UITEST_URL` (with your app's name), e.g.
  `$env:MYAPP_UITEST_URL = "http://localhost:5090"`.
- Timeouts are deliberately short (10 seconds a step, 30 seconds a test): see `TestBase.cs` and `.runsettings`.
- They run with the placeholder sign in, and with the light Argon2id settings from `appsettings.Development.json`, so
  setting a passphrase is quick.

The template's UI tests cover the shell, sign in, passwords, languages, sync, end to end encryption, user events and
the notes example. `PseudoLocalizationTests` checks that the framework's pages show no text that isn't from resources.
They are good examples to copy when you add your own pages.
