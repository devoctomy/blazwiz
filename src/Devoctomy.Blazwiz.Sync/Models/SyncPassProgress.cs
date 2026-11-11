namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// How far a sync pass has got through the changes it planned (uploads, downloads and deletes).
/// </summary>
/// <param name="done">How many changes are done, failed ones included.</param>
/// <param name="total">How many changes the pass planned.</param>
public class SyncPassProgress(int done, int total)
{
    /// <summary>
    /// Gets how many changes are done, failed ones included.
    /// </summary>
    public int Done { get; } = done;

    /// <summary>
    /// Gets how many changes the pass planned.
    /// </summary>
    public int Total { get; } = total;
}
