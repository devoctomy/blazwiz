using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Encryption.Exceptions;

/// <summary>
/// Thrown when a keys document or the index of protected profiles is written conditionally and was changed, created
/// or deleted since it was read (by another device or tab), with the code <see cref="E2eErrorCodes.DocumentChanged"/>.
/// The write did not happen.
/// </summary>
public class E2eDocumentChangedException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eDocumentChangedException"/> class.
    /// </summary>
    public E2eDocumentChangedException()
        : base(E2eErrorCodes.DocumentChanged)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="E2eDocumentChangedException"/> class.
    /// </summary>
    /// <param name="innerException">What caused it.</param>
    public E2eDocumentChangedException(Exception innerException)
        : base(E2eErrorCodes.DocumentChanged, innerException)
    {
    }
}
