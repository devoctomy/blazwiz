using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when the passkey cannot unlock this device's keys, so the passphrase is needed.
/// </summary>
public class E2ePassphraseRequiredException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class, with the code <see cref="E2eErrorCodes.PassphraseRequired"/>.
    /// </summary>
    public E2ePassphraseRequiredException()
        : base(E2eErrorCodes.PassphraseRequired)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class, with the code <see cref="E2eErrorCodes.PassphraseRequired"/>.
    /// </summary>
    /// <param name="innerException">What caused it.</param>
    public E2ePassphraseRequiredException(Exception innerException)
        : base(E2eErrorCodes.PassphraseRequired, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2ePassphraseRequiredException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2ePassphraseRequiredException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2ePassphraseRequiredException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
