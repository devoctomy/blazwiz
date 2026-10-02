using System.Globalization;
using blazwiz.Core.Sync.Configuration;
using blazwiz.Core.Sync.Exceptions;
using blazwiz.Core.Sync.Interfaces;
using blazwiz.Core.Sync.Models;

namespace blazwiz.Core.Sync.Storage;

/// <summary>
/// Moves the signed in user's files between storages. Each profile is moved under its cross tab sync lock, so no pass
/// runs meanwhile in any tab, and every step is recorded in <see cref="ISyncStorageStateStore"/> so a move cut short
/// is undone (while copying) or finished (while clearing the source) by <see cref="RecoverAsync"/>.
/// </summary>
/// <remarks>
/// Files are copied byte for byte with their modified times, and the sync index is not touched, so nothing is
/// uploaded or downloaded again afterwards.
/// </remarks>
public class SyncStorageMigrationService(
    SyncOptions options,
    ISyncStorageResolver resolver,
    ISyncStorageStateStore stateStore,
    ISyncScheduler scheduler,
    ISyncLockService lockService,
    ISyncStorageChannel channel)
    : ISyncStorageMigrationService
{
    /// <summary>
    /// The message when the picked folder already has files for the profile ({0}).
    /// </summary>
    public const string FolderNotEmptyMessage = "The folder already has {0} files in it. Choose an empty folder, or move them out first.";

    private readonly SyncOptions options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly ISyncStorageResolver resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly ISyncStorageStateStore stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    private readonly ISyncScheduler scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    private readonly ISyncLockService lockService = lockService ?? throw new ArgumentNullException(nameof(lockService));
    private readonly ISyncStorageChannel channel = channel ?? throw new ArgumentNullException(nameof(channel));

    /// <inheritdoc/>
    public async Task MigrateAsync(SyncStorageKind target, IProgress<SyncStorageMigrationProgress>? progress = null, CancellationToken ct = default)
    {
        var profiles = new List<string>();
        foreach (var profile in GetSwitchableProfiles().Where(p => resolver.GetKinds(p).Contains(target)))
        {
            if ((await resolver.GetStateAsync(profile)).Kind != target)
            {
                profiles.Add(profile);
            }
        }

        if (profiles.Count == 0)
        {
            return;
        }

        var wasStarted = scheduler.IsStarted;
        await scheduler.StopAsync();
        await channel.PostAsync(SyncStorageChannelMessage.Migrating);
        try
        {
            foreach (var profile in profiles)
            {
                await MigrateProfileAsync(profile, target, progress, ct);
            }
        }
        finally
        {
            await channel.PostAsync(SyncStorageChannelMessage.Changed);
            if (wasStarted)
            {
                _ = RestartSyncAsync();
            }
        }
    }

    /// <inheritdoc/>
    public async Task RecoverAsync(CancellationToken ct = default)
    {
        foreach (var profile in GetSwitchableProfiles())
        {
            if ((await resolver.GetStateAsync(profile)).MigrationPhase == null)
            {
                continue;
            }

            await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile, ct))
            {
                // Read again under the lock: another tab may have been moving the files, and has now finished.
                var state = await resolver.GetStateAsync(profile);
                switch (state.MigrationPhase)
                {
                    case SyncStorageMigrationPhase.Copying:
                        var source = state.MigrationFrom ?? state.Kind;
                        if (state.MigrationTo is SyncStorageKind partial && partial != source)
                        {
                            await ClearAsync(resolver.GetStore(profile, partial));
                        }

                        await SaveAsync(state, source);
                        break;

                    case SyncStorageMigrationPhase.Cleanup:
                        if (state.MigrationFrom is SyncStorageKind old && old != state.Kind)
                        {
                            await ClearAsync(resolver.GetStore(profile, old));
                        }

                        await SaveAsync(state, state.Kind);
                        break;
                }
            }
        }
    }

    private static async Task ClearAsync(ISyncFileStore store)
    {
        foreach (var file in await store.ListAsync())
        {
            await store.DeleteAsync(file.Key);
        }
    }

    private static async Task CopyAsync(string profile, ISyncFileStore source, ISyncFileStore destination, IProgress<SyncStorageMigrationProgress>? progress, CancellationToken ct)
    {
        var files = await source.ListAsync(ct);
        progress?.Report(new SyncStorageMigrationProgress(profile, 0, files.Count));
        var done = 0;
        foreach (var info in files)
        {
            ct.ThrowIfCancellationRequested();
            var file = await source.ReadAsync(info.Key, ct)
                ?? throw new SyncStorageMigrationException($"'{info.Key}' could not be read.");
            await destination.WriteAsync(file.Key, file.Content, file.ModifiedAt, ct);

            var copy = await destination.ReadAsync(file.Key, ct);
            if (copy == null || !copy.Content.AsSpan().SequenceEqual(file.Content))
            {
                throw new SyncStorageMigrationException($"'{info.Key}' did not copy correctly.");
            }

            progress?.Report(new SyncStorageMigrationProgress(profile, ++done, files.Count));
        }
    }

    private async Task MigrateProfileAsync(string profile, SyncStorageKind target, IProgress<SyncStorageMigrationProgress>? progress, CancellationToken ct)
    {
        await using (await lockService.AcquireAsync(SyncDefaults.LockNamePrefix + profile, ct))
        {
            var state = await resolver.GetStateAsync(profile);
            var from = state.Kind;
            if (from == target)
            {
                return;
            }

            var source = resolver.GetStore(profile, from);
            var destination = resolver.GetStore(profile, target);
            if (target == SyncStorageKind.LocalFolder && (await destination.ListAsync(ct)).Count > 0)
            {
                // The user's own folder: never clear files the app did not put there.
                throw new SyncStorageMigrationException(string.Format(CultureInfo.InvariantCulture, FolderNotEmptyMessage, profile));
            }

            await SaveAsync(state, from, from, target, SyncStorageMigrationPhase.Copying);
            try
            {
                // Anything left from an earlier move that was undone.
                await ClearAsync(destination);
                await CopyAsync(profile, source, destination, progress, ct);
            }
            catch (Exception)
            {
                await SaveAsync(state, from);
                try
                {
                    await ClearAsync(destination);
                }
                catch (Exception)
                {
                    // The next move clears it first; report the failure that stopped this one.
                }

                throw;
            }

            // The target is in use from here; clearing the source is finished by RecoverAsync if cut short.
            await SaveAsync(state, target, from, target, SyncStorageMigrationPhase.Cleanup);
            await ClearAsync(source);
            await SaveAsync(state, target);
        }
    }

    private IEnumerable<string> GetSwitchableProfiles()
    {
        return options.Profiles.Select(p => p.Name).Where(resolver.IsSwitchable);
    }

    private Task SaveAsync(SyncStorageState state, SyncStorageKind kind, SyncStorageKind? from = null, SyncStorageKind? to = null, SyncStorageMigrationPhase? phase = null)
    {
        return stateStore.SaveAsync(new SyncStorageState
        {
            Profile = state.Profile,
            Owner = state.Owner,
            Kind = kind,
            MigrationFrom = from,
            MigrationTo = to,
            MigrationPhase = phase,
        });
    }

    private async Task RestartSyncAsync()
    {
        try
        {
            await scheduler.StartAsync();
        }
        catch (Exception)
        {
            // Sync failures show in the status and the sync log; the files are already moved.
        }
    }
}
