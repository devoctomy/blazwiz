namespace Devoctomy.Blazwiz.Sync.Models;

/// <summary>
/// The actions for every key in one pass of one profile.
/// </summary>
public class SyncPlan
{
    /// <summary>
    /// Gets or sets the actions, one per key, in key order.
    /// </summary>
    public IReadOnlyList<SyncAction> Actions { get; set; } = [];
}
