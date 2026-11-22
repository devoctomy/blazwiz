# Views and view models

blazwiz uses the **MVVM** pattern (Model, View, View model), through the
[Blazing.Mvvm](https://github.com/gragra33/Blazing.Mvvm) and
[CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) libraries:

- The **view** is a Razor file (`.razor`). It holds markup only: what to show, and which command to run when
  something is clicked.
- The **view model** is a plain C# class. It holds the view's state (properties) and what it can do (commands). It
  never touches the browser directly, so a unit test can create it, call it and check what it did.

The framework follows this everywhere, and your app should too. It is what lets every line of logic be unit tested.

## The rules

1. Every view inherits `MvvmComponentBase<TViewModel>`.
2. Every view model inherits `ViewModelBase`, lives in `MyApp.Portal.Application/ViewModels`, and gets what it needs
   (services, settings) through its constructor.
3. **The code-behind (`.razor.cs`) holds parameters only.** No event subscriptions, no decisions, no computed
   properties. The one thing it may do is hand a parameter to its view model.
4. The view re-renders when its view model raises `PropertyChanged`. You don't write `StateHasChanged` or
   `PropertyChanged` handlers in the view: `MvvmComponentBase<TViewModel>` does it.

## Adding a page

Say you want a page at `/things`.

**1. The view model**, in `MyApp.Portal.Application/ViewModels/ThingsViewModel.cs`:

```csharp
using Blazing.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Devoctomy.Blazwiz.UI.Models;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace MyApp.Portal.Application.ViewModels;

/// <summary>
/// ViewModel for the things page.
/// </summary>
public partial class ThingsViewModel : ViewModelBase
{
    private readonly IToastService toastService;
    private int count;

    public ThingsViewModel(IToastService toastService)
    {
        this.toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));
    }

    /// <summary>
    /// Gets how many things have been added.
    /// </summary>
    public int Count
    {
        get => count;
        private set => SetProperty(ref count, value);
    }

    /// <inheritdoc/>
    public override Task OnInitializedAsync()
    {
        // Load what the page shows. Called once, when the view is first shown.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a thing.
    /// </summary>
    [RelayCommand]
    public void Add()
    {
        Count++;
        toastService.Show("Thing added", ToastTone.Success);
    }
}
```

- `SetProperty` stores the value and raises `PropertyChanged` when it changed, so the view updates.
- `[RelayCommand]` makes a command property the view can run: `Add` becomes `AddCommand`. For an `async Task`
  method named `SaveAsync`, the command is `SaveCommand`, run with `ExecuteAsync(null)`.
- The class is `partial` because the command is generated code.

You don't register the view model yourself: `AddMvvm` in `Program.cs` registers every view model in the application
assembly (`RegisterViewModelsFromAssemblyContaining<DashboardViewModel>()`).

**2. The view**, `MyApp.Portal/Pages/Things.razor`:

```razor
@page "/things"
@inherits MvvmComponentBase<ThingsViewModel>

<PageHeader Title="Things" Icon="bi-box" Subtitle="Everything you have added.">
    <button type="button" class="btn btn-primary" @onclick="() => ViewModel.AddCommand.Execute(null)">
        <i class="bi bi-plus-lg me-1"></i>Add
    </button>
</PageHeader>

<div class="panel">
    <p class="panel-text">You have added @ViewModel.Count things.</p>
</div>
```

`PageHeader` gives the page a standard heading and sets the browser tab's title. `panel` is one of the framework's
styles (see the Showcase page for more).

**3. The code-behind**, `MyApp.Portal/Pages/Things.razor.cs`. It declares the class and nothing else:

```csharp
using Blazing.Mvvm.Components;
using MyApp.Portal.Application.ViewModels;

namespace MyApp.Portal.Pages;

/// <summary>
/// Code-behind for the things page. Logic lives in <see cref="ThingsViewModel"/>.
/// </summary>
public partial class Things : MvvmComponentBase<ThingsViewModel>
{
}
```

**4. The sidebar item**, in `AddAppServices` (`MyApp.Portal.Application/Extensions/AppServiceCollectionExtensions.cs`):

```csharp
services.AddNavigationItem(new NavigationItem
{
    Text = "Things",
    Href = "things",
    Icon = "bi-box",
    Description = "Everything you have added.",
    Order = 20,
});
```

`Order` sets its place in the sidebar (lower comes first). The template's Dashboard also shows every sidebar item as a
quick link, with its `Description`. To make the text translatable, see
[Text and translation](text-and-translation.md#sidebar-items).

**5. The tests**, in `MyApp.Portal.Application.UnitTests/ViewModels/ThingsViewModelTests.cs`. See
[Testing your app](testing.md).

## Passing a parameter to the view model

A page often needs a value from its route or query string. Declare it as a parameter in the code-behind, and hand it
to the view model. That is the only code a code-behind may have. The template's notes example does it in
`Pages/Files.razor.cs`:

```csharp
public partial class Files : MvvmComponentBase<FilesViewModel>
{
    [SupplyParameterFromQuery(Name = NoteEditorViewModel.NotebookQueryParameter)]
    public string? NotebookName { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        await ViewModel.OpenNotebookAsync(NotebookName);
    }
}
```

The framework's `E2eUnlockPanel` does the same with its `Profile` parameter (`ViewModel.SetProfileAsync(Profile)`),
and `ModalDialog` with its `FocusMode` and `OnClose`.

## Dialogs

Show a dialog by rendering it while a view model property says it is open. The view model owns the state and the
commands; the view only shows them:

```razor
@if (ViewModel.IsDeleteDialogOpen)
{
    <ModalDialog Id="delete-dialog" Title="Delete this thing?" Icon="bi-trash3"
                 FocusMode="ModalDialogFocusMode.Dialog"
                 OnClose="() => ViewModel.CloseDeleteDialogCommand.Execute(null)">
        <ChildContent>
            <p class="mb-0">It cannot be undone.</p>
        </ChildContent>
        <Footer>
            <button type="button" class="btn btn-secondary" @onclick="() => ViewModel.CloseDeleteDialogCommand.Execute(null)">Cancel</button>
            <button type="button" class="btn btn-danger" @onclick="() => ViewModel.DeleteCommand.ExecuteAsync(null)">Delete</button>
        </Footer>
    </ModalDialog>
}
```

See [`ModalDialog`](components.md#modaldialog) for its parameters.

## A view model that owns another

Some framework components show a view model they are given, rather than one of their own. `E2ePassphraseForm` is one:
your dialog's view model takes an `E2ePassphraseFormViewModel` in its constructor and passes it to the component.

Such a component is only drawn again when its parent is. So the parent's view model must say when the child changes,
by raising `PropertyChanged` itself. The template's `NotebooksViewModel` does this:

```csharp
public NotebooksViewModel(/* ... */ E2ePassphraseFormViewModel passphraseForm)
{
    PassphraseForm = passphraseForm ?? throw new ArgumentNullException(nameof(passphraseForm));
    PassphraseForm.PropertyChanged += OnPassphraseFormChanged;
}

public E2ePassphraseFormViewModel PassphraseForm { get; }

private void OnPassphraseFormChanged(object? sender, PropertyChangedEventArgs e)
{
    OnPropertyChanged(nameof(PassphraseForm));
}
```

and removes the handler when it is disposed:

```csharp
protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        PassphraseForm.PropertyChanged -= OnPassphraseFormChanged;
    }

    base.Dispose(disposing);
}
```

The same goes for any event a view model subscribes to (a service's `Changed` event, say): subscribe in the
constructor or in `OnInitializedAsync`, unsubscribe in `Dispose(bool)`.

## Showing errors

Don't show an exception's message to users: it is English, technical and sometimes leaks details. Inject `IErrorText`
and describe the exception instead:

```csharp
catch (Exception ex)
{
    Error = errorText.Describe(ex);
}
```

Framework errors carry a code (`BlazwizException.Code`), which `IErrorText` turns into the text resource
`Error.<code>`, in the user's language. Anything else becomes a general "something went wrong" message. Show the
result with `ErrorAlert`, or your own markup. See [Text and translation](text-and-translation.md#errors).

## Code that only runs in the browser

If a view model needs something only a browser has (JavaScript, `OperatingSystem.IsBrowser()`), put it behind an
injectable service, as the template's notes editor does with `INoteEditorInteropService`. The view model calls the
interface; a unit test gives it a mock.

## Why the rules matter

- Razor markup is not run by unit tests. Every line of logic left in a view is a line no test covers.
- The framework's views are built the same way, so its view models can be read and reused as examples:
  `src/Devoctomy.Blazwiz.UI/ViewModels` and `src/Devoctomy.Blazwiz.UI.Sync/ViewModels`.
