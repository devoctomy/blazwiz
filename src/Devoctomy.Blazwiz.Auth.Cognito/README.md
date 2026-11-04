# Devoctomy.Blazwiz.Auth.Cognito

Signs blazwiz apps in with an AWS Cognito user pool (SRP, through `Amazon.Extensions.CognitoAuthentication`), and gets
the session's AWS credentials from a Cognito identity pool.

## Usage

After `AddBlazwizUi`:

```csharp
builder.Services.AddBlazwizCognito(builder.Configuration);
```

It reads the `AwsCognito` section of the configuration (`wwwroot/appsettings.json`). The base stack in
`Devoctomy.Blazwiz.Aws` creates the pools, and its deploy script writes these values.

```json
"AwsCognito": {
  "Region": "eu-west-2",
  "UserPoolId": "...",
  "UserPoolClientId": "...",
  "IdentityPoolId": "..."
}
```

While no user pool is configured (local development), the placeholder sign in stays in use.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
