# Devoctomy.Blazwiz.UI

The blazwiz app shell: the layout and sidebar, sign in, profile and settings pages, the theme, choosing the language,
and the extension points that modules and apps add to. Views are markup only; each one's logic is in its view model
(MVVM).

## Usage

After `AddBlazwizApp`:

```csharp
builder.Services.AddBlazwizUi(builder.Configuration);
```

That registers the shell and what it uses (`Core`, `Cryptography`, `Browser` and `Auth`). It reads the `AppSettings`
section of the configuration (title, description, copyright, `EnablePortalLoginAuthn` to offer a passkey after
"Remember me", and `EnableRememberMeName` to let the user give a name to be remembered by, shown on the sign-in page
instead of their email address), and `Text:SupportedCultures` for the languages offered.

Add to the shell, or change it, with:

- `AddNavigationItem`: a sidebar item.
- `AddShellComponent<T>(region)`: a component in a region of the layout.
- `AddSettingsSection<T>(order)`: a section on the Settings page.
- `RemoveSettingsSection<T>()`: takes a section off the Settings page, whoever added it (the framework, a module or
  the app) and whether it is added before or after the call.
- `AddSignInOption<TOption, TComponent>()`: another way to sign in.
- `AddPageAssembly`: pages from another assembly.
- `ReplacePage<T>(BlazwizPages.Login)` and `DisablePage(BlazwizPages.Sync)`: replace or turn off a framework page.

Text comes from resources (`UiText`), so apps can reword and translate it.

Two-step verification: the sign-in page asks for the code from the user's authenticator app when the sign-in service
wants one, or shows how to set one up first when it requires one. The Profile page turns it on and off
(`MfaSettingsPanel`), and Settings lists the user's remembered devices with a Forget button on each
(`RememberedDevicesSettingsSection`). Setting up shows the secret three ways (`AuthenticatorSetupPanel`): a link that
opens the authenticator app on this device, a QR code (drawn with QRCoder, as an SVG `data:` image) and the key with
a Copy button. A passkey set up on the sign-in page also remembers the device, so passkey sign in is not asked for a
code.

When something registers an `ISignOutEverywhereHandler` (user events, `Devoctomy.Blazwiz.Events`), the Profile page
offers "Sign out everywhere", and Settings shows a This device section where the user names the device. When a session
ends because of another device (`SessionEnded` with a reason), the sign-in page says why, e.g. "You were signed out
from Work laptop."

The package's JS initializer registers the app's service worker and, when a new version has downloaded, shows the
`#app-update-banner` element of the app's `index.html`, whose button switches to it. While the new version downloads,
`AppUpdateIndicator` (in both layouts) shows a small indicator in the corner, with the percentage of files cached when
the service worker posts `{ type: 'INSTALL_PROGRESS', done, total }` to its pages as it caches each one, as the
template's `service-worker.published.js` does.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
