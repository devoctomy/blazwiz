# Devoctomy.Blazwiz.Auth

Sign in for blazwiz apps: the authentication contract (`IAuthenticationService`), sessions and their lifecycle,
offline sign in, remembered passwords, password changes and resets, and migrations of stored data, run at sign in.
It does not talk to an identity provider itself: `Devoctomy.Blazwiz.Auth.Cognito` signs in with AWS Cognito, and
without it a placeholder sign in is used for local development.

## Usage

`AddBlazwizUi` (in `Devoctomy.Blazwiz.UI`) registers it for you. On its own, after `AddBlazwizApp`:

```csharp
builder.Services.AddBlazwizAuth();

// Run code when a session starts or ends.
builder.Services.AddSessionLifecycleHandler<MySessionHandler>();

// Change data the app keeps in the browser to a new format, at sign in.
builder.Services.AddDataMigration<MyDataMigration>();
```

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
