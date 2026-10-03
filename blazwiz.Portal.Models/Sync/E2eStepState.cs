namespace blazwiz.Portal.Models.Sync;

/// <summary>
/// How far a step of turning end to end encryption on or off has got, as the step list shows it.
/// </summary>
public enum E2eStepState
{
    /// <summary>
    /// Not started.
    /// </summary>
    Pending,

    /// <summary>
    /// Running now.
    /// </summary>
    Running,

    /// <summary>
    /// Done.
    /// </summary>
    Done,

    /// <summary>
    /// Stopped with an error.
    /// </summary>
    Failed,
}
