namespace blazwiz.Core.Exceptions;

/// <summary>
/// An error the user may need to see, identified by a code rather than by English text. The UI shows it with the text
/// resource <c>Error.&lt;code&gt;</c>, formatted with <see cref="Arguments"/>, so it can be reworded and translated.
/// <see cref="Exception.Message"/> is for developers and logs only.
/// </summary>
public class BlazwizException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlazwizException"/> class.
    /// </summary>
    /// <param name="code">The error's code, e.g. <c>Sync.Storage.FolderNotEmpty</c>.</param>
    /// <param name="arguments">Values for the placeholders in the error's text, e.g. a file count.</param>
    public BlazwizException(string code, params object[] arguments)
        : this(code, null, arguments)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlazwizException"/> class.
    /// </summary>
    /// <param name="code">The error's code, e.g. <c>Sync.Storage.FolderNotEmpty</c>.</param>
    /// <param name="innerException">What caused it, if anything.</param>
    /// <param name="arguments">Values for the placeholders in the error's text, e.g. a file count.</param>
    public BlazwizException(string code, Exception? innerException, params object[] arguments)
        : base(code, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Arguments = arguments ?? [];
    }

    /// <summary>
    /// Gets the error's code. The UI shows the text resource <c>Error.&lt;code&gt;</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the values for the placeholders in the error's text.
    /// </summary>
    public IReadOnlyList<object> Arguments { get; }
}
