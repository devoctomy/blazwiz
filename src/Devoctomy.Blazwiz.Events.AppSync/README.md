# Devoctomy.Blazwiz.Events.AppSync

User events (`Devoctomy.Blazwiz.Events`) over an AWS AppSync Events API. Each signed-in user's channel reaches all
their open devices, so "Sign out everywhere" on one device signs the others out straight away. The connection is
authorised with the session's Cognito access token. The package speaks AppSync's protocol directly, with no Amplify.

## How it works

- The API has one channel namespace, `users`. A user's channel is `/users/<sub>`, where `sub` is their user pool
  subject. The namespace's handlers refuse any channel that is not the caller's own, so no user can listen to or
  write to another user's channel.
- **Receiving:** a WebSocket to the API's real-time endpoint (`wss://<realtime>/event/realtime`), subscribed to the
  user's channel. It reconnects with backoff when the connection drops, and straight away when the app resumes.
- **Publishing:** an HTTPS `POST` to `https://<http>/event`.
- Events say only that something happened, never the change itself or a secret. A device that is closed, offline or
  in the background misses them. The sign-in service and the resume check in `Devoctomy.Blazwiz.Auth` are the
  fallback.

## Cost

AppSync Events is charged per use: $1.00 per million event operations (connections, publishes, subscriptions and
deliveries) and $0.08 per million connection minutes. There is no fixed monthly charge. It is still a resource that
costs money, so apps choose it: the template's `Events` option (on by default) adds it, and turning the option off
creates nothing.

## Usage

The AWS base stack creates the API when its `Events` parameter is `enabled`. `cloudformation/deploy.ps1` writes the
endpoints to `appsettings.Production.json`:

```json
"Events": {
  "HttpEndpoint": "abc123.appsync-api.eu-west-2.amazonaws.com",
  "RealtimeEndpoint": "abc123.appsync-realtime-api.eu-west-2.amazonaws.com"
}
```

It also adds both endpoints to the Content-Security-Policy's `connect-src`. After `AddBlazwizApp` and
`AddBlazwizCognito`:

```csharp
builder.Services.AddBlazwizAppSyncEvents(builder.Configuration, allowLocalChannel: builder.HostEnvironment.IsDevelopment());
```

When the endpoints are missing:

- **In development** (`allowLocalChannel`), the local channel stands in. It reaches the user's other tabs in the same
  browser only.
- **Anywhere else**, the app runs without events and logs a warning when a session starts. "Sign out everywhere" is
  then not offered.

The package never falls back to the local channel in a deployed app, because that would look like it works while
reaching no other device.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
