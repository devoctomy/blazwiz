using System.Reflection;

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

    /// <summary>
    /// Gets when this build was made (UTC, e.g. <c>2026-10-02 14:31</c>), or null for a build that was not stamped
    /// (Debug). Set by the project file.
    /// </summary>
    public static string? BuildTime => typeof(Program).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value;
}
