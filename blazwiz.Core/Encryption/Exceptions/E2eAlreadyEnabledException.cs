using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when turning end to end encryption on, but it was already turned on from another device.
/// </summary>
public class E2eAlreadyEnabledException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class, with the code <see cref="E2eErrorCodes.AlreadyEnabled"/>.
    /// </summary>
    public E2eAlreadyEnabledException()
        : base(E2eErrorCodes.AlreadyEnabled)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class, with the code <see cref="E2eErrorCodes.AlreadyEnabled"/>.
    /// </summary>
    /// <param name="innerException">What caused it.</param>
    public E2eAlreadyEnabledException(Exception innerException)
        : base(E2eErrorCodes.AlreadyEnabled, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eAlreadyEnabledException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eAlreadyEnabledException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eAlreadyEnabledException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
