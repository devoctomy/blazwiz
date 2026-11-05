using Devoctomy.Blazwiz.Core.Exceptions;

namespace Devoctomy.Blazwiz.Encryption.Exceptions;

/// <summary>
/// Thrown when the signed in user has no protected profile with the given id, with the code
/// <see cref="E2eErrorCodes.ProfileNotFound"/> and the id as its argument.
/// </summary>
public class E2eProfileNotFoundException : BlazwizException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="E2eProfileNotFoundException"/> class.
    /// </summary>
    /// <param name="profileId">The profile's id.</param>
    public E2eProfileNotFoundException(string profileId)
        : base(E2eErrorCodes.ProfileNotFound, profileId)
    {
    }
}
