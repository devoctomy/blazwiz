namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// How much a passphrase change has to do: every file of the profile is encrypted again and uploaded.
/// </summary>
/// <param name="fileCount">How many files the profile has.</param>
/// <param name="totalBytes">Their total size, in bytes.</param>
/// <param name="duration">About how long encrypting and uploading them all will take.</param>
public class SyncKeyRotationEstimate(int fileCount, long totalBytes, TimeSpan duration)
{
    /// <summary>
    /// Gets how many files the profile has.
    /// </summary>
    public int FileCount { get; } = fileCount;

    /// <summary>
    /// Gets their total size, in bytes.
    /// </summary>
    public long TotalBytes { get; } = totalBytes;

    /// <summary>
    /// Gets about how long encrypting and uploading them all will take.
    /// </summary>
    public TimeSpan Duration { get; } = duration;
}
