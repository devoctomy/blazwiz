using blazwiz.Core.Encryption.Interfaces;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync;

/// <summary>
/// Runs one pass for one profile: snapshot both sides and the index, plan, then execute each action, updating the
/// index straight after each one succeeds. Everything it does is written to the sync log.
/// </summary>
/// <remarks>
/// It works on the stored bytes, so end to end encrypted files sync as ciphertext and it never needs the keys. It only
/// records in the index which key a synced file is encrypted with, read from the file header.
/// </remarks>
public class SyncEngine(
    ISyncPlanner planner,
    ISyncConflictResolver conflictResolver,
    IContentHasher contentHasher,
    ISyncUserContext userContext,
    ISyncEventLog eventLog,
    TimeProvider timeProvider,
    IEncryptedFileFormat encryptedFileFormat)
    : ISyncEngine
{
    private readonly ISyncPlanner planner = planner ?? throw new ArgumentNullException(nameof(planner));
    private readonly ISyncConflictResolver conflictResolver = conflictResolver ?? throw new ArgumentNullException(nameof(conflictResolver));
    private readonly IContentHasher contentHasher = contentHasher ?? throw new ArgumentNullException(nameof(contentHasher));
    private readonly ISyncUserContext userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    private readonly ISyncEventLog eventLog = eventLog ?? throw new ArgumentNullException(nameof(eventLog));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly IEncryptedFileFormat encryptedFileFormat = encryptedFileFormat ?? throw new ArgumentNullException(nameof(encryptedFileFormat));

    /// <inheritdoc/>
    public async Task<SyncResult> SyncAsync(SyncProfile profile, bool verify, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var owner = await userContext.GetUserIdAsync()
            ?? throw new InvalidOperationException("Sign in before syncing.");
        var pass = new Pass(profile, owner, Guid.NewGuid().ToString("N"), new SyncResult { Profile = profile.Name });
        AddEvent(pass, SyncEventKind.PassStarted, null, "Checking for changes");

        var passFailed = false;
        try
        {
            var plan = await SnapshotAndPlanAsync(pass, verify, ct);
            foreach (var action in plan.Actions.Where(a => a.Kind != SyncActionKind.None))
            {
                ct.ThrowIfCancellationRequested();
                await ExecuteSafelyAsync(pass, action, ct);
            }
        }
        catch (Exception ex) when (ex is SyncNetworkException or SyncAccessDeniedException)
        {
            passFailed = true;
            pass.Result.Errors.Add(ex.Message);
            AddEvent(pass, SyncEventKind.Failed, null, $"Sync failed: {ex.Message}", ex.Message);
            throw;
        }
        finally
        {
            await WriteLogAsync(pass, passFailed, ct.IsCancellationRequested);
        }

        return pass.Result;
    }

    private static string DescribeFailure(SyncActionKind kind) => kind switch
    {
        SyncActionKind.Upload or SyncActionKind.UploadNew => "Upload failed",
        SyncActionKind.Download or SyncActionKind.DownloadNew or SyncActionKind.CompareContent => "Download failed",
        SyncActionKind.DeleteRemote or SyncActionKind.DeleteLocal or SyncActionKind.DropRecord => "Delete failed",
        _ => "Sync failed",
    };

    private static bool LocalChangedSince(SyncIndexRecord? snapshot, SyncIndexRecord? current)
    {
        return current != null && current.Revision != (snapshot?.Revision ?? 0);
    }

    private async Task<SyncPlan> SnapshotAndPlanAsync(Pass pass, bool verify, CancellationToken ct)
    {
        var profile = pass.Profile;
        var local = (await profile.FileStore.ListAsync(ct)).Where(f => profile.Matcher.IsMatch(f.Key)).ToList();
        var remote = await profile.RemoteStore.ListAsync(ct);
        var index = (await profile.IndexStore.GetAllAsync(profile.Name, pass.Owner, ct))
            .Where(r => profile.Matcher.IsMatch(r.Key))
            .ToList();

        Dictionary<string, string>? hashes = null;
        if (verify)
        {
            // Catches a file written without its index being marked (e.g. a crash between the two).
            hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in local)
            {
                var content = await profile.FileStore.ReadAsync(file.Key, ct);
                if (content != null)
                {
                    hashes[file.Key] = contentHasher.ComputeHash(content.Content);
                }
            }
        }

        return planner.Plan(local, remote, index, hashes);
    }

    private async Task ExecuteSafelyAsync(Pass pass, SyncAction action, CancellationToken ct)
    {
        try
        {
            try
            {
                await ExecuteAsync(pass, action, ct);
            }
            catch (SyncPreconditionFailedException)
            {
                // Changed remotely since the listing: look at the file again and plan it once more.
                AddEvent(pass, SyncEventKind.Retried, action.Key, "Changed in S3 during sync, checking again");
                var replanned = await ReplanAsync(pass, action.Key, ct);
                await ExecuteAsync(pass, replanned, ct);
            }
        }
        catch (Exception ex) when (ex is not SyncNetworkException and not SyncAccessDeniedException and not OperationCanceledException)
        {
            pass.Result.Failed++;
            pass.Result.Errors.Add($"{action.Key}: {ex.Message}");
            AddEvent(pass, SyncEventKind.Failed, action.Key, $"{DescribeFailure(action.Kind)}: {ex.Message}", ex.ToString());
        }
    }

    private async Task<SyncAction> ReplanAsync(Pass pass, string key, CancellationToken ct)
    {
        var profile = pass.Profile;
        var file = await profile.FileStore.ReadAsync(key, ct);
        var remote = await profile.RemoteStore.GetInfoAsync(key, ct);
        var record = await profile.IndexStore.GetAsync(profile.Name, pass.Owner, key, ct);
        return planner.PlanItem(new SyncPlanItem
        {
            Key = key,
            Local = file?.ToInfo(),
            LocalHash = file == null ? null : contentHasher.ComputeHash(file.Content),
            Remote = remote,
            Record = record,
        });
    }

    private Task ExecuteAsync(Pass pass, SyncAction action, CancellationToken ct)
    {
        return action.Kind switch
        {
            SyncActionKind.Upload => UploadAsync(pass, action, action.Record?.BaseETag, false, SyncEventKind.UploadLocalNewer, null, ct),
            SyncActionKind.UploadNew => UploadAsync(pass, action, null, true, SyncEventKind.UploadNew, null, ct),
            SyncActionKind.Download => DownloadAsync(pass, action, SyncEventKind.DownloadRemoteNewer, null, ct),
            SyncActionKind.DownloadNew => DownloadAsync(pass, action, SyncEventKind.DownloadNew, null, ct),
            SyncActionKind.CompareContent => CompareContentAsync(pass, action, ct),
            SyncActionKind.DeleteRemote => DeleteRemoteAsync(pass, action, action.Record?.BaseETag, null, ct),
            SyncActionKind.DeleteLocal => DeleteLocalAsync(pass, action, ct),
            SyncActionKind.DropRecord => pass.Profile.IndexStore.DeleteAsync(pass.Profile.Name, pass.Owner, action.Key, ct),
            SyncActionKind.Conflict => ResolveConflictAsync(pass, action, ct),
            _ => Task.CompletedTask,
        };
    }

    private async Task UploadAsync(Pass pass, SyncAction action, string? ifMatch, bool ifNoneMatch, SyncEventKind eventKind, string? conflictDetail, CancellationToken ct)
    {
        var profile = pass.Profile;
        var file = await profile.FileStore.ReadAsync(action.Key, ct);
        if (file == null)
        {
            // Deleted locally during the pass: the next pass deals with it.
            return;
        }

        var etag = await profile.RemoteStore.PutAsync(action.Key, file.Content, file.ModifiedAt, ifMatch, ifNoneMatch, ct);
        await SaveSyncedAsync(pass, action, file.Content, etag, ct);
        pass.Result.Uploaded++;
        AddFileEvent(pass, eventKind, action.Key, conflictDetail);
    }

    private async Task DownloadAsync(Pass pass, SyncAction action, SyncEventKind eventKind, string? conflictDetail, CancellationToken ct)
    {
        var profile = pass.Profile;
        var remote = await profile.RemoteStore.GetAsync(action.Key, ct);
        if (remote == null)
        {
            // Deleted remotely during the pass: the next pass deals with it.
            return;
        }

        await DownloadAsync(pass, action, remote, eventKind, conflictDetail, ct);
    }

    private async Task DownloadAsync(Pass pass, SyncAction action, RemoteObject remote, SyncEventKind eventKind, string? conflictDetail, CancellationToken ct)
    {
        var profile = pass.Profile;
        var current = await profile.IndexStore.GetAsync(profile.Name, pass.Owner, action.Key, ct);
        if (LocalChangedSince(action.Record, current))
        {
            // Edited locally during the pass: never overwrite it; the next pass sees both changes.
            return;
        }

        await profile.FileStore.WriteAsync(action.Key, remote.Content, remote.EditedAt, ct);
        await SaveSyncedAsync(pass, action, remote.Content, remote.ETag, ct);
        pass.Result.Downloaded++;
        pass.Result.AddChangedLocalKey(profile.Name, action.Key);
        AddFileEvent(pass, eventKind, action.Key, conflictDetail);
    }

    private async Task CompareContentAsync(Pass pass, SyncAction action, CancellationToken ct)
    {
        var profile = pass.Profile;
        var remote = await profile.RemoteStore.GetAsync(action.Key, ct);
        var file = await profile.FileStore.ReadAsync(action.Key, ct);
        if (remote == null || file == null)
        {
            // One side went away during the pass: the next pass deals with it.
            return;
        }

        var hash = contentHasher.ComputeHash(file.Content);
        if (hash == contentHasher.ComputeHash(remote.Content))
        {
            // Same on both sides (e.g. an interrupted pass, or a lost index): adopt it as the base.
            await SaveSyncedAsync(pass, action, file.Content, remote.ETag, ct);
            pass.Result.Adopted++;
            AddEvent(pass, SyncEventKind.AlreadyInSync, action.Key, "Same on both sides, nothing to do");
            return;
        }

        var conflict = new SyncConflict
        {
            Key = action.Key,
            Kind = SyncConflictKind.EditEdit,
            LocalChangedAt = file.ModifiedAt,
            Remote = remote,
        };

        if (conflictResolver.Resolve(conflict) == SyncConflictWinner.Local)
        {
            await UploadAsync(pass, action, remote.ETag, false, SyncEventKind.ConflictResolved, "local", ct);
        }
        else
        {
            await DownloadAsync(pass, action, remote, SyncEventKind.ConflictResolved, "remote", ct);
        }

        pass.Result.ConflictsResolved++;
    }

    private async Task DeleteRemoteAsync(Pass pass, SyncAction action, string? ifMatch, string? conflictDetail, CancellationToken ct)
    {
        var profile = pass.Profile;
        await profile.RemoteStore.DeleteAsync(action.Key, ifMatch, ct);

        var current = await profile.IndexStore.GetAsync(profile.Name, pass.Owner, action.Key, ct);
        if (current != null && LocalChangedSince(action.Record, current) && !current.Deleted)
        {
            // Created again locally during the pass: keep it as a new file, to upload next time.
            current.BaseETag = null;
            current.BaseHash = null;
            await profile.IndexStore.SaveAsync(current, ct);
        }
        else
        {
            await profile.IndexStore.DeleteAsync(profile.Name, pass.Owner, action.Key, ct);
        }

        pass.Result.DeletedRemote++;
        AddFileEvent(pass, SyncEventKind.DeleteRemote, action.Key, conflictDetail);
    }

    private async Task DeleteLocalAsync(Pass pass, SyncAction action, CancellationToken ct)
    {
        var profile = pass.Profile;
        var current = await profile.IndexStore.GetAsync(profile.Name, pass.Owner, action.Key, ct);
        if (LocalChangedSince(action.Record, current))
        {
            return;
        }

        await profile.FileStore.DeleteAsync(action.Key, ct);
        await profile.IndexStore.DeleteAsync(profile.Name, pass.Owner, action.Key, ct);
        pass.Result.DeletedLocal++;
        pass.Result.AddChangedLocalKey(profile.Name, action.Key);
        AddEvent(pass, SyncEventKind.DeleteLocal, action.Key, "Deleted remotely, removing local copy");
    }

    private async Task ResolveConflictAsync(Pass pass, SyncAction action, CancellationToken ct)
    {
        var profile = pass.Profile;
        var kind = action.ConflictKind ?? SyncConflictKind.EditEdit;

        // ListObjectsV2 has no metadata, so read the remote edit time (and current ETag) for this file only.
        var remote = kind == SyncConflictKind.EditDelete ? null : await profile.RemoteStore.GetInfoAsync(action.Key, ct);
        if (remote == null && kind == SyncConflictKind.DeleteEdit)
        {
            // Deleted on both sides after all.
            await profile.IndexStore.DeleteAsync(profile.Name, pass.Owner, action.Key, ct);
            return;
        }

        if (remote == null)
        {
            kind = SyncConflictKind.EditDelete;
        }

        var conflict = new SyncConflict
        {
            Key = action.Key,
            Kind = kind,
            LocalChangedAt = kind == SyncConflictKind.DeleteEdit ? action.Record?.DeletedAt : action.Local?.ModifiedAt,
            Remote = remote,
        };

        var winner = conflictResolver.Resolve(conflict);
        var detail = winner == SyncConflictWinner.Local ? "local" : "remote";
        switch (kind, winner)
        {
            case (SyncConflictKind.EditEdit, SyncConflictWinner.Local):
                await UploadAsync(pass, action, remote!.ETag, false, SyncEventKind.ConflictResolved, detail, ct);
                break;
            case (SyncConflictKind.EditDelete, SyncConflictWinner.Local):
                await UploadAsync(pass, action, null, true, SyncEventKind.ConflictResolved, detail, ct);
                break;
            case (SyncConflictKind.DeleteEdit, SyncConflictWinner.Local):
                await DeleteRemoteAsync(pass, action, remote!.ETag, detail, ct);
                break;
            case (SyncConflictKind.EditDelete, SyncConflictWinner.Remote):
                await DeleteLocalAsync(pass, action, ct);
                break;
            default:
                await DownloadAsync(pass, action, SyncEventKind.ConflictResolved, detail, ct);
                break;
        }

        pass.Result.ConflictsResolved++;
    }

    private async Task SaveSyncedAsync(Pass pass, SyncAction action, byte[] content, string etag, CancellationToken ct)
    {
        var profile = pass.Profile;
        var current = await profile.IndexStore.GetAsync(profile.Name, pass.Owner, action.Key, ct);
        var record = current ?? new SyncIndexRecord { Profile = profile.Name, Owner = pass.Owner, Key = action.Key };
        record.BaseHash = contentHasher.ComputeHash(content);
        record.BaseETag = etag;
        record.LastSyncedAt = timeProvider.GetUtcNow();
        if (!LocalChangedSince(action.Record, current))
        {
            // Only clear the local change flags if nothing changed locally while this file was syncing.
            record.Dirty = false;
            record.Deleted = false;
            record.DeletedAt = null;

            // Changed locally meanwhile: the write that changed it recorded its own key.
            record.KeyId = encryptedFileFormat.TryGetKeyId(content, out var keyId) ? keyId : null;
            record.Encrypted = record.KeyId != null;
        }

        await profile.IndexStore.SaveAsync(record, ct);
    }

    private void AddFileEvent(Pass pass, SyncEventKind kind, string key, string? conflictWinner)
    {
        if (conflictWinner != null)
        {
            AddEvent(pass, SyncEventKind.ConflictResolved, key, $"Changed on both sides, kept the newer {conflictWinner} version", $"The {conflictWinner} version was newer.");
            return;
        }

        var message = kind switch
        {
            SyncEventKind.UploadNew => "New local file, uploading",
            SyncEventKind.UploadLocalNewer => "Local newer than remote, uploading",
            SyncEventKind.DownloadNew => "New remote file, downloading",
            SyncEventKind.DownloadRemoteNewer => "Remote newer than local, downloading",
            SyncEventKind.DeleteRemote => "Deleted locally, removing from S3",
            _ => kind.ToString(),
        };
        AddEvent(pass, kind, key, message);
    }

    private void AddEvent(Pass pass, SyncEventKind kind, string? key, string message, string? detail = null)
    {
        pass.Events.Add(new SyncEvent
        {
            Owner = pass.Owner,
            Profile = pass.Profile.Name,
            PassId = pass.PassId,
            Timestamp = timeProvider.GetUtcNow(),
            Kind = kind,
            Key = key,
            Message = message,
            Detail = detail,
        });
    }

    private async Task WriteLogAsync(Pass pass, bool passFailed, bool cancelled)
    {
        try
        {
            var fileEvents = pass.Events.Count(e => e.Kind != SyncEventKind.PassStarted);
            if (fileEvents == 0 && !passFailed && !cancelled)
            {
                await eventLog.RecordNoChangePassAsync(pass.Owner, pass.Profile.Name, pass.PassId, timeProvider.GetUtcNow());
                return;
            }

            AddEvent(pass, SyncEventKind.PassCompleted, null, cancelled ? "Stopped" : pass.Result.Describe());
            await eventLog.WriteAsync(pass.Events);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The log is diagnostic only: never fail a pass because it could not be written.
        }
    }

    private sealed class Pass(SyncProfile profile, string owner, string passId, SyncResult result)
    {
        public SyncProfile Profile { get; } = profile;

        public string Owner { get; } = owner;

        public string PassId { get; } = passId;

        public SyncResult Result { get; } = result;

        public List<SyncEvent> Events { get; } = [];
    }
}
