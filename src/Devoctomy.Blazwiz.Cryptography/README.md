# Devoctomy.Blazwiz.Cryptography

Cryptography for blazwiz apps, built on BouncyCastle: AES-GCM encryption, Argon2id and HKDF key derivation, key
wrapping, and keys derived from the WebAuthn PRF output of a passkey.

## Usage

`AddBlazwizUi` (in `Devoctomy.Blazwiz.UI`) registers it for you. On its own:

```csharp
builder.Services.AddBlazwizCryptography();

// Keys derived from a passkey's PRF output, one per name.
builder.Services.AddBlazwizPrfKeyDerivation(prf => prf.AddKey("my-key"));
```

Use these services rather than writing cryptography yourself.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
