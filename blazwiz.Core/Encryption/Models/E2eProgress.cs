namespace blazwiz.Core.Encryption.Models;

/// <summary>
/// Progress of one <see cref="E2eStep"/>.
/// </summary>
/// <param name="step">The step.</param>
/// <param name="completed">False when the step starts (or moves on, for files), true when it is done.</param>
/// <param name="done">For file steps, how many files are done.</param>
/// <param name="total">For file steps, how many files there are.</param>
public class E2eProgress(E2eStep step, bool completed, int done = 0, int total = 0)
{
    /// <summary>
    /// Gets the step.
    /// </summary>
    public E2eStep Step { get; } = step;

    /// <summary>
    /// Gets a value indicating whether the step is done.
    /// </summary>
    public bool Completed { get; } = completed;

    /// <summary>
    /// Gets how many files are done, for file steps.
    /// </summary>
    public int Done { get; } = done;

    /// <summary>
    /// Gets how many files there are, for file steps.
    /// </summary>
    public int Total { get; } = total;
}
