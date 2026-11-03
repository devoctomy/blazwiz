namespace blazwiz.Core.Configuration.Interfaces;

/// <summary>
/// Turns the short names used in code (e.g. <c>session</c>) into the names kept in the browser (e.g.
/// <c>acmeportal_session</c>). The browser storage clients apply it, so services only use short names.
/// </summary>
public interface IAppStorageKeys
{
    /// <summary>
    /// Gets the prefix given to every name, e.g. <c>acmeportal_</c>.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Gets the name kept in the browser for a short name.
    /// </summary>
    /// <param name="name">The short name, e.g. <c>session</c>.</param>
    /// <returns>The prefixed name, e.g. <c>acmeportal_session</c>.</returns>
    string For(string name);
}
