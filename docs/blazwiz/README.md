# The blazwiz developer guide

blazwiz is a framework for building **Blazor WebAssembly PWAs**: web apps written in C# that run in the browser,
can be installed like a native app (a PWA, or progressive web app) and keep working offline. A blazwiz app signs its
users in with **AWS Cognito**, can sync their files with **Amazon S3**, encrypts those files **end to end** (only the
user can read them), and works without a connection.

You get it in two parts:

- **NuGet packages** (`Devoctomy.Blazwiz.*`), which hold the framework: the app shell, sign in, settings, storage, sync
  and encryption.
- **A `dotnet new` template**, which creates an app built on those packages. Your code lives in the app; the
  framework stays in the packages, so you upgrade it by changing one version number.

> blazwiz is in development and unreleased (0.x). Before 1.0, any release may break things. See
> [docs/versioning.md](../versioning.md).

## Start here

1. [Getting started](getting-started.md): create an app from the template, choose its options, run it.
2. [How an app is put together](app-structure.md): the projects, `Program.cs`, and the configuration files.
3. [Views and view models](views-and-view-models.md): how to write a page the blazwiz way.

Then read the topics you need:

| Topic | What it covers |
|---|---|
| [Sign in and sessions](sign-in-and-sessions.md) | Cognito, the placeholder sign in, sessions and their lifecycle, offline mode, passkeys, passwords, signing out everywhere |
| [Browser storage](storage.md) | Short names and the app prefix, local storage, IndexedDB, what the framework keeps where |
| [Sync and end to end encryption](sync-and-encryption.md) | Sync profiles, file stores, passphrases, unlocking, Argon2id settings, changing a passphrase |
| [The shell and its extension points](shell-and-extension-points.md) | Shell regions, Settings sections, sign-in options, lifecycle handlers, navigation items, page assemblies, replacing pages |
| [Reusable components](components.md) | `ModalDialog`, `PageHeader`, toasts, banners, `E2eUnlockPanel`, `E2ePassphraseForm` and the rest |
| [Text and translation](text-and-translation.md) | `.resx` resources, `UiText`, `SyncUiText`, overriding framework text, languages, error codes |
| [Theming](theming.md) | Light and dark mode, the `--bw-*` colour tokens, your own styles |
| [Testing your app](testing.md) | Unit tests of view models, `FakeTimeProvider`, `Devoctomy.Blazwiz.Testing`, the UI tests |
| [Deploying](deploying.md) | The AWS stack and its scripts |

## The packages

You reference only a few packages directly. They bring in the rest.

| Package | What it gives you |
|---|---|
| `Devoctomy.Blazwiz.Core` | The app's identity, browser storage names, offline mode, overridable text, coded errors. |
| `Devoctomy.Blazwiz.Cryptography` | AES-GCM, Argon2id, HKDF, key wrapping and keys from a passkey, all built on BouncyCastle. |
| `Devoctomy.Blazwiz.Encryption` | End to end encryption keys: protected profiles, key rings and the encrypted file format. |
| `Devoctomy.Blazwiz.Browser` | Local storage, theme, navigation, passkeys (WebAuthn), the clipboard, the device's name, page resume events. |
| `Devoctomy.Blazwiz.Auth` | The sign-in contract, sessions and their lifecycle, offline sign in, remembered passwords. |
| `Devoctomy.Blazwiz.Auth.Cognito` | Sign in with a Cognito user pool, and AWS credentials for the session. |
| `Devoctomy.Blazwiz.Events` | User events: a channel shared by the user's open tabs and devices. |
| `Devoctomy.Blazwiz.Events.AppSync` | User events over AWS AppSync Events, reaching every device. |
| `Devoctomy.Blazwiz.Sync` | The offline-first sync engine (IndexedDB, OPFS or a local folder, synced with S3). |
| `Devoctomy.Blazwiz.UI` | The app shell: layout, sign-in, profile and settings pages, theme, extension points. |
| `Devoctomy.Blazwiz.UI.Sync` | The sync module: sync wired to the shell, Cognito and S3, with its UI. |
| `Devoctomy.Blazwiz.Testing` | Helpers for your app's tests. |
| `Devoctomy.Blazwiz.Aws` | The AWS base stack and its deploy scripts (content only, no code). |

Every package and the template share one version. An app sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`.

## A few words used throughout

- **Shell**: the frame around every page: the sidebar, the top bar, banners and overlays. It comes from
  `Devoctomy.Blazwiz.UI`.
- **Module**: an optional package that plugs into the shell, such as the sync module (`Devoctomy.Blazwiz.UI.Sync`).
- **View model**: a plain C# class that holds a page's state and logic. The page (the view) only shows it. See
  [Views and view models](views-and-view-models.md).
- **Sync profile**: a set of files synced together, with its own place on the device, its own S3 folder and its own
  encryption keys. See [Sync and end to end encryption](sync-and-encryption.md).
- **Passphrase**: the secret a user enters to unlock a sync profile. It is separate from their sign-in password.

## More reference material

- Each package's `README.md` in `src/Devoctomy.Blazwiz.<Package>` (it also ships inside the NuGet package).
- The diagrams in [docs/diagrams](../diagrams/overview.md): how sign in, sessions, sync and the extension points work
  inside.
- The template's own [README](../../template/README.md), which every generated app gets.
