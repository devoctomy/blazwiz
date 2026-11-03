namespace Devoctomy.Blazwiz.UI.Services.Interfaces;

/// <summary>
/// The app's build: its version, whether it is a debug build and when it was built. Shown in Settings > About.
/// </summary>
public interface IAppBuildInfo
{
    /// <summary>
    /// Gets the app's version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the version of the blazwiz framework the app is built on, e.g. <c>1.2.0</c> (every
    /// <c>Devoctomy.Blazwiz</c> package has the same version).
    /// </summary>
    string FrameworkVersion { get; }

    /// <summary>
    /// Gets a value indicating whether the app is a debug build.
    /// </summary>
    bool IsDebugBuild { get; }

    /// <summary>
    /// Gets when the app was built (UTC, e.g. <c>2026-10-02 14:31</c>), or null for a build that was not stamped
    /// (Debug). Set by the app's project file, as <c>BuildTimeUtc</c> assembly metadata.
    /// </summary>
    string? BuildTime { get; }
}
