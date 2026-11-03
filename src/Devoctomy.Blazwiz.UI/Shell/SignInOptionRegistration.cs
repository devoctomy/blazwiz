namespace Devoctomy.Blazwiz.UI.Shell;

/// <summary>
/// The component showing a sign-in option (see <see cref="Interfaces.ISignInOption"/>).
/// </summary>
public sealed class SignInOptionRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SignInOptionRegistration"/> class.
    /// </summary>
    /// <param name="optionType">The option's type.</param>
    /// <param name="componentType">The component that shows it.</param>
    public SignInOptionRegistration(Type optionType, Type componentType)
    {
        OptionType = optionType ?? throw new ArgumentNullException(nameof(optionType));
        ComponentType = componentType ?? throw new ArgumentNullException(nameof(componentType));
    }

    /// <summary>
    /// Gets the option's type.
    /// </summary>
    public Type OptionType { get; }

    /// <summary>
    /// Gets the component that shows it.
    /// </summary>
    public Type ComponentType { get; }
}
