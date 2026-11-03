namespace blazwiz.Portal.Application.Text.Interfaces;

/// <summary>
/// Describes errors for users, in their language. Errors with a code (<c>BlazwizException</c>) show the text resource
/// <c>Error.&lt;code&gt;</c>; anything else shows a general "Something went wrong" message, never the exception's own
/// text.
/// </summary>
public interface IErrorText
{
    /// <summary>
    /// Describes an exception.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <returns>The text to show.</returns>
    string Describe(Exception exception);

    /// <summary>
    /// Describes an error code, e.g. one recorded in a status.
    /// </summary>
    /// <param name="code">The error's code.</param>
    /// <param name="arguments">Values for the placeholders in its text. A count first selects the plural form, when
    /// the text has one.</param>
    /// <returns>The text to show.</returns>
    string Describe(string code, IReadOnlyList<object> arguments);
}
