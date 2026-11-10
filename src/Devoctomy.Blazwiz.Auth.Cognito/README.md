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

While no user pool is configured, the placeholder sign in (any credentials) stays in use only where the app allows it:
`AddBlazwizCognito(configuration, allowPlaceholderSignIn: builder.HostEnvironment.IsDevelopment())`. Anywhere else,
missing settings stop the app from starting rather than letting anyone in.

Signing out everywhere (`IAuthenticationService.SignOutEverywhereAsync`) calls Cognito's `GlobalSignOut`, which
revokes every refresh token the user has and, because the app client has `EnableTokenRevocation`, their access tokens
too. The resume check (`CheckSessionAsync`) calls `GetUser` with the session's access token, so a device that missed
the news finds out as soon as it resumes. In offline mode neither calls Cognito.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
