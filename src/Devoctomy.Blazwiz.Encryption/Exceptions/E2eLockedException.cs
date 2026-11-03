using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Encryption.Exceptions;

/// <summary>
/// Thrown when end to end encryption is on but this device's keys are not unlocked.
/// </summary>
public class E2eLockedException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class, with the code <see cref="E2eErrorCodes.Locked"/>.
    /// </summary>
    public E2eLockedException()
        : base(E2eErrorCodes.Locked)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class, with the code <see cref="E2eErrorCodes.Locked"/>.
    /// </summary>
    /// <param name="innerException">What caused it.</param>
    public E2eLockedException(Exception innerException)
        : base(E2eErrorCodes.Locked, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eLockedException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eLockedException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eLockedException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
