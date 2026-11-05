# Devoctomy.Blazwiz.UI.Sync

The blazwiz sync module: sync (`Devoctomy.Blazwiz.Sync`) wired to the app shell, Cognito and S3, with its UI: the Sync
page, the status pill, and storage and end to end encryption settings. Each profile's keys are kept in the user's own
AWS Secrets Manager secrets (in the browser when no Cognito user pool is configured, for local development), and a
profile can allow unlocking with a passkey.

## Usage

After `AddBlazwizUi`:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .AddProfile("files", profile => profile
        .UseKeyId("3f2b8c1e-6d4a-4f7b-9c2e-8a1d5b7e9f40")
        .UseRemote("files/**/*")));
```

See `Devoctomy.Blazwiz.Sync` for the builder. The module adds Cognito sign in (`AddBlazwizCognito`) itself, and reads
the `AwsS3` section of the configuration, which the deploy script of `Devoctomy.Blazwiz.Aws` writes:

```json
"AwsS3": {
  "Region": "eu-west-2",
  "DataBucketName": "..."
}
```

and the optional `Argon2id` section, the Argon2id settings for new keys in place of the build's defaults (lighter
ones suit local development and UI tests):

```json
"Argon2id": {
  "MemoryKib": 19456
}
```

Its text comes from resources (`SyncUiText`), so apps can reword and translate it.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
