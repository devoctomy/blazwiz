using System.Runtime.CompilerServices;

namespace blazwiz.Portal.Application.Helpers;

/// <summary>
/// Provides helper methods for validation and argument checking.
/// </summary>
public static class ValidationHelpers
{
    /// <summary>
    /// Returns the given value if it is not <see langword="null"/>, otherwise throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value being verified.</typeparam>
    /// <param name="value">The value to verify.</param>
    /// <param name="parameterName">The name of the parameter, captured automatically from the call site.</param>
    /// <returns>The non-null <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static T VerifiedAssignment<T>(
        T value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }
}
