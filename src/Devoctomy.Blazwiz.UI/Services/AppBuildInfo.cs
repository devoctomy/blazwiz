using System.Diagnostics;
using System.Reflection;
using Devoctomy.Blazwiz.UI.Services.Interfaces;

namespace Devoctomy.Blazwiz.UI.Services;

/// <summary>
/// Reads the app's build from its assembly: the entry assembly, unless another is given.
/// </summary>
public class AppBuildInfo : IAppBuildInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppBuildInfo"/> class for the app's entry assembly.
    /// </summary>
    public AppBuildInfo()
        : this(Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("The app has no entry assembly."))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AppBuildInfo"/> class.
    /// </summary>
    /// <param name="assembly">The app's assembly.</param>
    public AppBuildInfo(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Version = assembly.GetName().Version?.ToString() ?? string.Empty;
        IsDebugBuild = assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled ?? false;
        BuildTime = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildTimeUtc")?.Value;
    }

    /// <inheritdoc/>
    public string Version { get; }

    /// <inheritdoc/>
    public bool IsDebugBuild { get; }

    /// <inheritdoc/>
    public string? BuildTime { get; }
}
