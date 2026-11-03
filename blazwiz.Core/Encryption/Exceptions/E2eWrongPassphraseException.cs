using blazwiz.Core.Exceptions;

namespace blazwiz.Core.Encryption.Exceptions;

/// <summary>
/// Thrown when the passphrase does not decrypt the keys.
/// </summary>
public class E2eWrongPassphraseException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class, with the code <see cref="E2eErrorCodes.WrongPassphrase"/>.
    /// </summary>
    public E2eWrongPassphraseException()
        : base(E2eErrorCodes.WrongPassphrase)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class, with the code <see cref="E2eErrorCodes.WrongPassphrase"/>.
    /// </summary>
    /// <param name="innerException">What caused it.</param>
    public E2eWrongPassphraseException(Exception innerException)
        : base(E2eErrorCodes.WrongPassphrase, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eWrongPassphraseException(string code, params object[] arguments)
        : base(code, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eWrongPassphraseException"/> class.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="innerException">What caused it.</param>
    /// <param name="arguments">Values for the placeholders in the error's text.</param>
    public E2eWrongPassphraseException(string code, Exception innerException, params object[] arguments)
        : base(code, innerException, arguments)
    {
    }
}
