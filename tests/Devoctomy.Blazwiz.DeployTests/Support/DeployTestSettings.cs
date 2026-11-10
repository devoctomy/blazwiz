namespace Devoctomy.Blazwiz.DeployTests.Support;

/// <summary>
/// What the deploy tests are given, from environment variables: the deployed portal and the test user. Everything else
/// is read from the portal's own settings (<see cref="PortalSettings"/>).
/// </summary>
public sealed class DeployTestSettings
{
    public const string PortalUrlVariable = "BLAZWIZ_DEPLOYTEST_PORTAL_URL";
    public const string EmailVariable = "BLAZWIZ_DEPLOYTEST_EMAIL";
    public const string PasswordVariable = "BLAZWIZ_DEPLOYTEST_PASSWORD";
    public const string SignOutEverywhereVariable = "BLAZWIZ_DEPLOYTEST_SIGN_OUT_EVERYWHERE";

    public required Uri PortalUrl { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public bool SignOutEverywhere { get; init; }

    /// <summary>
    /// Reads the settings, or null when any is missing (the scenarios are then skipped).
    /// </summary>
    /// <returns>The settings, or null.</returns>
    public static DeployTestSettings? FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable(PortalUrlVariable);
        var email = Environment.GetEnvironmentVariable(EmailVariable);
        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        return new DeployTestSettings
        {
            PortalUrl = new Uri(url.TrimEnd('/') + "/"),
            Email = email,
            Password = password,
            SignOutEverywhere = string.Equals(Environment.GetEnvironmentVariable(SignOutEverywhereVariable), "true", StringComparison.OrdinalIgnoreCase),
        };
    }
}
