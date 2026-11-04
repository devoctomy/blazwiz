# Devoctomy.Blazwiz.UI.Sync

The blazwiz sync module: sync (`Devoctomy.Blazwiz.Sync`) wired to the app shell, Cognito and S3, with its UI: the Sync
page, the status pill, and storage and end to end encryption settings. Each user's keys are kept in AWS Secrets
Manager, and can be unlocked with a passkey.

## Usage

After `AddBlazwizUi`:

```csharp
builder.Services.AddBlazwizSyncModule(builder.Configuration, sync => sync
    .AddProfile("files", profile => profile
        .UseRemote("files/**/*")
        .UseEncryption()));
```

See `Devoctomy.Blazwiz.Sync` for the builder. The module adds Cognito sign in (`AddBlazwizCognito`) itself, and reads
the `AwsS3` section of the configuration, which the deploy script of `Devoctomy.Blazwiz.Aws` writes:

```json
"AwsS3": {
  "Region": "eu-west-2",
  "DataBucketName": "..."
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
