# Devoctomy.Blazwiz.Encryption

End to end encryption keys for blazwiz apps: protected profiles, each with its own passphrase, master key and file
key (`IE2eProtectedProfileManager`), the key rings that hold them while a profile is unlocked, and the encrypted file
format, which binds each file to its profile and path. Data is encrypted in the browser, and only the user can decrypt
it. There is no recovery: a forgotten passphrase loses the profile's data.

## Usage

The sync module (`AddBlazwizSyncModule` in `Devoctomy.Blazwiz.UI.Sync`) registers it for you. On its own:

```csharp
builder.Services.AddBlazwizE2eEncryption();

// Or with different Argon2id settings for deriving keys from passphrases.
builder.Services.AddBlazwizE2eEncryption(argon2 => { /* ... */ });
```

The app also registers where the keys documents and the index of profiles are kept (`IUserKeysStore`,
`IE2eProfileIndexStore`; `AddBlazwizSync` does). Their writes are conditional: each read returns the document with its
version (`StoredDocument.ETag`), and a write made from an older version throws `E2eDocumentChangedException`. The
manager then reads the document again and reapplies its change, up to `E2eProtectedProfileManager.MaxWriteAttempts`
times. A device's copy of a keys document keeps its version too (`E2eLocalKeys.CachedDocumentETag`).

## Changing a passphrase rotates the keys

`IE2eProtectedProfileManager.ChangePassphraseAsync` does more than wrap the same keys again
(`IE2eKeysDocumentService.RotateAsync`):

- It makes a new master key, wrapped by the new passphrase, and a new active file key.
- Every earlier file key is wrapped again under the new master key and marked `retired`, so files not yet encrypted
  again still decrypt. Retired keys are kept.
- The keys document is marked `rotation_in_progress` (`UserKeysDocument.RotationInProgress`).
- The profile is left unlocked with the new keys. This device's passkey copy of the master key is replaced when the
  passkey's key is in memory, and otherwise removed.

Someone who had the old passphrase and a copy of the old keys document, or who once held the old master key, can
still read what they could read before. They cannot unwrap the new keys, so they cannot read anything encrypted after
the change.

Encrypting the files again and signing every device out is not this package's job (the sync module does both, see
`ISyncKeyRotationService` in `Devoctomy.Blazwiz.Sync`). Once that is done, `CompleteRotationAsync` clears the mark.
Until then `IsRotationInProgress(scope)` is true for an unlocked profile, and the app must finish the change before
using it.

`UnlockWithDeviceAsync` reads the keys document from the store when it can be reached. If the keys were rotated on
another device, the master key kept here unwraps nothing in it: device unlock is turned off on this device and
`E2ePassphraseRequiredException` is thrown with the code `E2eErrorCodes.KeysChanged`. When the store cannot be
reached, this device's copy is used as before.

## Versions

Every `Devoctomy.Blazwiz.*` package and the `Devoctomy.Blazwiz.Templates` template are released together at one
version. Use the same version of all of them; an app made from the template sets it once, as `BlazwizVersion` in its
`Directory.Packages.props`. Before 1.0 any release may break. See [versioning](https://github.com/devoctomy/blazwiz/blob/main/docs/versioning.md) and
the [changelog](https://github.com/devoctomy/blazwiz/blob/main/CHANGELOG.md).

Part of [blazwiz](https://github.com/devoctomy/blazwiz): a framework for Blazor WebAssembly PWAs that sign in with AWS Cognito, sync files to S3 with end
to end encryption, and work offline.
