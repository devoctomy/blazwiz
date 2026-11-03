namespace blazwiz.Portal.Application.Shell.Interfaces;

/// <summary>
/// An option under "Advanced options" on the sign-in page, such as starting with sync turned off. Add one with
/// <see cref="Extensions.ShellServiceCollectionExtensions.AddSignInOption{TOption, TComponent}"/>. The component shows
/// the option and binds to the same instance, which it gets from dependency injection by its own type.
/// </summary>
public interface ISignInOption
{
    /// <summary>
    /// Gets the option's position: lower comes first.
    /// </summary>
    int Order => 0;

    /// <summary>
    /// Gets a value indicating whether the option is set to something other than its default. When any option is,
    /// the advanced options are shown expanded.
    /// </summary>
    bool IsSet { get; }

    /// <summary>
    /// Loads the option's current value. Called when the sign-in page opens.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task LoadAsync();

    /// <summary>
    /// Applies the option. Called after a successful sign in, before anything else happens.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task ApplyAsync();
}
