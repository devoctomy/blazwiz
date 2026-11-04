# Devoctomy.Blazwiz.Encryption

End to end encryption keys for blazwiz apps: the user's master key, a key per file, the key ring that holds them, and
the encrypted file format. Data is encrypted in the browser, and only the user can decrypt it.

## Usage

The sync module (`AddBlazwizSyncModule` in `Devoctomy.Blazwiz.UI.Sync`) registers it for you. On its own:

```csharp
builder.Services.AddBlazwizE2eEncryption();

// Or with different Argon2id settings for deriving keys from the password.
builder.Services.AddBlazwizE2eEncryption(argon2 => { /* ... */ });
```

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
