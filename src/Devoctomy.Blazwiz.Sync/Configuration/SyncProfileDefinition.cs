namespace Devoctomy.Blazwiz.Sync.Configuration;

/// <summary>
/// A sync profile defined as a class: what it is called and how it is configured. Derive from it and add it with
/// <see cref="SyncBuilder.AddProfile{TProfile}()"/>, or let <see cref="SyncBuilder.AddProfilesFromAssembly"/> find it.
/// </summary>
/// <example>
/// <code>
/// public sealed class PhotosSyncProfile : SyncProfileDefinition
/// {
///     public override string Name => "photos";
///
///     public override void Configure(SyncProfileBuilder profile) => profile
///         .UseRemote("photos/**/*")
///         .UseEncryption();
/// }
/// </code>
/// </example>
public abstract class SyncProfileDefinition
{
    /// <summary>
    /// Gets the profile name, e.g. <c>photos</c>. App code gets the profile's file store with this service key.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Configures the profile. <see cref="SyncProfileBuilder.UseRemote"/> is required.
    /// </summary>
    /// <param name="profile">The profile builder.</param>
    public abstract void Configure(SyncProfileBuilder profile);
}
