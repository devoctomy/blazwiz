using Devoctomy.Blazwiz.Sync.Interfaces;

namespace Devoctomy.Blazwiz.Sync;

/// <summary>
/// The parts of one configured profile, as the engine sees them.
/// </summary>
/// <param name="name">The profile name, e.g. <c>documents</c>.</param>
/// <param name="fileStore">The local file store, without change tracking (the engine's writes are not local changes).</param>
/// <param name="indexStore">The index store.</param>
/// <param name="remoteStore">The remote store.</param>
/// <param name="matcher">The remote pattern.</param>
public class SyncProfile(string name, ISyncFileStore fileStore, ISyncIndexStore indexStore, ISyncRemoteStore remoteStore, ISyncPathMatcher matcher)
{
    /// <summary>
    /// Gets the profile name.
    /// </summary>
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    /// <summary>
    /// Gets the local file store, without change tracking.
    /// </summary>
    public ISyncFileStore FileStore { get; } = fileStore ?? throw new ArgumentNullException(nameof(fileStore));

    /// <summary>
    /// Gets the index store.
    /// </summary>
    public ISyncIndexStore IndexStore { get; } = indexStore ?? throw new ArgumentNullException(nameof(indexStore));

    /// <summary>
    /// Gets the remote store.
    /// </summary>
    public ISyncRemoteStore RemoteStore { get; } = remoteStore ?? throw new ArgumentNullException(nameof(remoteStore));

    /// <summary>
    /// Gets the remote pattern.
    /// </summary>
    public ISyncPathMatcher Matcher { get; } = matcher ?? throw new ArgumentNullException(nameof(matcher));
}
