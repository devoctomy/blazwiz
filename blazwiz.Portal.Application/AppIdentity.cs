namespace blazwiz.Portal.Application;

/// <summary>
/// Identifies this application. Each app created from the template gets its own name and a new ID.
/// </summary>
public static class AppIdentity
{
    /// <summary>
    /// The application name.
    /// </summary>
    public const string Name = "blazwiz";

    /// <summary>
    /// The application's unique ID. Keys derived from WebAuthn are salted with it, so changing it makes everything
    /// encrypted with them (such as remembered passwords) unreadable.
    /// </summary>
    public const string Id = "a78e68c7-90e0-4f90-aa3d-73f7b2c9f2c8";
}
