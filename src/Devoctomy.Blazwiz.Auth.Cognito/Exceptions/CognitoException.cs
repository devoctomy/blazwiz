namespace Devoctomy.Blazwiz.Auth.Cognito.Exceptions;

/// <summary>
/// An error returned by the Cognito user pool API, e.g. <c>NotAuthorizedException</c> for a wrong password.
/// </summary>
public class CognitoException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CognitoException"/> class.
    /// </summary>
    /// <param name="errorType">The Cognito error type, e.g. <c>NotAuthorizedException</c>.</param>
    /// <param name="message">The error message.</param>
    public CognitoException(string errorType, string message)
        : base(message)
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Gets the Cognito error type, e.g. <c>NotAuthorizedException</c>.
    /// </summary>
    public string ErrorType { get; }
}
