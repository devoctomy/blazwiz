namespace blazwiz.Portal;

/// <summary>
/// Global constants and utilities for the portal.
/// </summary>
public static class Globals
{
#if DEBUG
    /// <summary>
    /// Indicates whether this is a debug build.
    /// </summary>
    public const bool IsDebugBuild = true;
#else
    /// <summary>
    /// Indicates whether this is a debug build.
    /// </summary>
    public const bool IsDebugBuild = false;
#endif

    /// <summary>
    /// Gets the portal version.
    /// </summary>
    public static string Version => typeof(Program).Assembly.GetName().Version!.ToString();
}
