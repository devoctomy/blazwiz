# Devoctomy.Blazwiz.Browser

Browser services for blazwiz apps: local storage (named with the app's prefix), the theme, navigation and WebAuthn
passkeys. The package brings its own JavaScript modules; apps add nothing to `index.html`.

## Usage

`AddBlazwizUi` (in `Devoctomy.Blazwiz.UI`) registers it for you. On its own, after `AddBlazwizApp`:

```csharp
builder.Services.AddBlazwizBrowser();
```

Then inject `ILocalStorageService`, `INavigationService` or `IWebAuthnService`. Give storage short names (`settings`);
the service adds the app's prefix.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
