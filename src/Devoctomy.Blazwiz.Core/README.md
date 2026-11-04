# Devoctomy.Blazwiz.Core

The core of blazwiz: the app's identity and the names of everything it keeps in the browser, offline mode, text that
apps can reword and translate, and coded errors. Every other blazwiz package depends on it.

## Usage

Call `AddBlazwizApp` first, with the app's display name and its ID (lower case letters and numbers). Browser storage is
named after the ID, so `session` is kept as `myapp_session`.

```csharp
builder.Services.AddBlazwizApp("My App", "myapp");
```

`AddBlazwizText` changes the framework's text: override wording, add languages, or turn on pseudo-localisation to find
text that does not come from resources. `AddBlazwizUi` (in `Devoctomy.Blazwiz.UI`) registers the core services for you.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
