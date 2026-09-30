# OPFS for Offline Storage of User Data Files

> Status: WIP / not implemented. Notes captured for future work.

## Goal

Store user data files locally so the app works fully offline, and synchronise them to S3 when the user is back online.

## Why OPFS (Origin Private File System)

- Available in all modern browsers, including Safari/iOS (`navigator.storage.getDirectory()`).
- No permission prompts or folder picker required.
- A real file system (directories, files, streaming writes), well suited to large and binary files.
- Fast, especially using `createSyncAccessHandle()` inside a Web Worker.
- Trade-off: files are private to the browser origin and not visible to the user in Explorer or Finder. That's fine here because the user doesn't need to manage the files directly.

### Alternatives considered

- **File System Access API (`showDirectoryPicker`)**: gives access to a real folder the user can see, but works only in Chromium (not Firefox or Safari/iOS), needs a user gesture and permission prompts, and permission persists only in installed Chromium PWAs. Rejected for portability.
- **IndexedDB blobs**: workable, but clunkier for file-like data. Better used for metadata (see below).

## Design notes

### 1. Sync log in IndexedDB

Keep OPFS for file contents only. Track metadata per file in IndexedDB:

- path
- local modified time
- content hash
- S3 ETag / version ID
- last-synced time
- dirty flag / deleted tombstone

Deletes must be recorded as tombstones, or a deleted file will come back on the next sync.

### 2. When to sync

Background Sync (`sync` event in a service worker) is Chromium-only, so don't rely on it. Sync on:

- app start
- `online` event
- `visibilitychange` (app regains focus)
- a periodic timer while the app is open
- Background Sync as a progressive enhancement where it's supported

### 3. Protecting against eviction

- Call `navigator.storage.persist()` to request persistent storage.
- Safari deletes script-writable storage after ~7 days without use, **except for web apps added to the home screen**. Encourage iOS users to install the PWA.
- Until a file is uploaded, the local copy is the only copy.

### 4. Quota

- Check with `navigator.storage.estimate()`.
- Handle `QuotaExceededError` on writes.

### 5. Conflict policy

Decide up front. Options:

- last write wins (by timestamp)
- keep both copies (rename one)
- use S3 object versioning

Use the ETag / `If-Match` on writes to detect changes made elsewhere.

### 6. S3 credentials

Both options need a connection, so fetch credentials when the sync runs, not while offline.

- **Cognito Identity Pool** temporary credentials, scoped by IAM policy to a per-user prefix, e.g. `users/${cognito-identity.amazonaws.com:sub}/`.
- **Presigned URLs** issued by a backend.

Use multipart upload for large files.

## Blazor WASM integration

- Write a small JS module that wraps the OPFS operations (read, write, list, delete) and the IndexedDB sync log.
- Call it from a C# service via `IJSRuntime` / `IJSObjectReference`.
- Pass file data as streams or byte arrays (`IJSStreamReference` / `DotNetStreamReference`) so large files don't go through base64 strings.
- The AWS SDK for .NET is heavy in WASM. Presigned URLs plus `fetch` from JS is likely lighter.
