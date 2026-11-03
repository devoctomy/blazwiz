using System.Diagnostics;
using System.Reflection;
using Microsoft.Playwright.MSTest;

namespace MyApp.Portal.UITests;

/// <summary>
/// Base class for UI tests. Starts the portal once per test run (dotnet run, Release) and
/// gives each test a fresh browser context, so local storage (session, settings) starts empty.
/// </summary>
/// <remarks>
/// First time only, install the browsers:
/// <c>pwsh MyApp.Portal.UITests/bin/Debug/net10.0/playwright.ps1 install chromium</c>.
/// Set <c>MYAPP_UITEST_URL</c> to test an already running / deployed portal instead.
/// </remarks>
[TestClass]
public class TestBase : PageTest
{
    /// <summary>
    /// Maximum time for a single Playwright action or assertion. The app is tiny and served locally, so keep this short.
    /// </summary>
    protected const int StepTimeoutMs = 10000;

    private static readonly string ResultsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
    private static readonly string ScreenshotDirectory = Path.Combine(ResultsDirectory, "Screenshots");
    private static readonly TimeSpan ServerStartTimeout = TimeSpan.FromSeconds(90);
    private static readonly object ServerLogLock = new();
    private static readonly System.Text.RegularExpressions.Regex LoginUrlPattern = new("/login$");
    private static Process? serverProcess;
    private static StreamWriter? serverLog;
    private int screenshotCounter;

    /// <summary>
    /// Gets the URL of the portal under test.
    /// </summary>
    /// <remarks>
    /// A free port is picked for each run so the tests never collide with a portal you are running
    /// yourself, or with one left behind by an aborted test run.
    /// </remarks>
    protected static string ServerUrl { get; } = Environment.GetEnvironmentVariable("MYAPP_UITEST_URL") ?? $"http://localhost:{GetFreePort()}";

    [AssemblyInitialize]
    public static async Task AssemblyInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MYAPP_UITEST_URL") != null)
        {
            return;
        }

        var projectPath = GetPortalProjectPath();
        serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\" --configuration Release --no-launch-profile --urls=\"{ServerUrl}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };

        // Portal output goes to TestResults/portal.log rather than the test console. Writing it to the
        // console from background threads interferes with the test runner's output capture.
        Directory.CreateDirectory(ResultsDirectory);
        serverLog = new StreamWriter(Path.Combine(ResultsDirectory, "portal.log"), append: false) { AutoFlush = true };
        serverProcess.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        serverProcess.OutputDataReceived += (_, e) => WriteServerLog(e.Data);
        serverProcess.ErrorDataReceived += (_, e) => WriteServerLog(e.Data);
        serverProcess.Start();
        serverProcess.BeginOutputReadLine();
        serverProcess.BeginErrorReadLine();

        await WaitForServerAsync();
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        try
        {
            serverProcess?.Kill(true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
        }
        finally
        {
            serverProcess?.Dispose();
            lock (ServerLogLock)
            {
                serverLog?.Dispose();
                serverLog = null;
            }
        }
    }

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            ColorScheme = ColorScheme.Light,
        };
    }

    [TestInitialize]
    public void BaseTestInitialize()
    {
        screenshotCounter = 0;
        Directory.CreateDirectory(ScreenshotDirectory);

        // The app is small and served locally, so anything slower than this is a real problem.
        Page.SetDefaultTimeout(StepTimeoutMs);
        Assertions.SetDefaultExpectTimeout(StepTimeoutMs);
    }

    [TestCleanup]
    public async Task BaseTestCleanup()
    {
        if (TestContext.CurrentTestOutcome == UnitTestOutcome.Failed)
        {
            await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotDirectory, $"{TestContext.TestName}_FAILED.png") });
        }
    }

    /// <summary>
    /// Opens the portal and waits for the unauthenticated redirect to the login page.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task GoToLoginAsync()
    {
        await Page.GotoAsync(ServerUrl);
        await ExpectLoginPageAsync();
    }

    /// <summary>
    /// Asserts that the browser has been sent to the login page.
    /// </summary>
    /// <remarks>
    /// Blazor redirects with client-side (pushState) navigation, so use a polling URL assertion.
    /// <c>Page.WaitForURLAsync</c> can hang indefinitely (ignoring its timeout) when the redirect
    /// has already happened by the time it is called.
    /// </remarks>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task ExpectLoginPageAsync()
    {
        await Expect(Page).ToHaveURLAsync(LoginUrlPattern);
        await Expect(Page.Locator("#login-button")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Signs in with the placeholder authentication and waits for the dashboard.
    /// </summary>
    /// <param name="username">The username to sign in with.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task LoginAsync(string username = "bob.hoskins@example.com")
    {
        await GoToLoginAsync();
        await Page.Locator("#username").FillAsync(username);
        await Page.Locator("#password").FillAsync("password123");
        await Page.Locator("#login-button").ClickAsync();
        await Expect(Page.Locator("#dashboard-greeting")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Saves a screenshot to TestResults/Screenshots.
    /// </summary>
    /// <param name="name">A short description of what is being captured.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task ScreenshotAsync(string name)
    {
        screenshotCounter++;
        var fileName = $"{TestContext.TestName}_{screenshotCounter:D2}_{name}.png";
        await Page.ScreenshotAsync(new() { Path = Path.Combine(ScreenshotDirectory, fileName) });
    }

    private static void WriteServerLog(string? line)
    {
        if (line == null)
        {
            return;
        }

        lock (ServerLogLock)
        {
            serverLog?.WriteLine(line);
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static string GetPortalProjectPath()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "MyApp.Portal", "MyApp.Portal.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate MyApp.Portal/MyApp.Portal.csproj above the test output directory.");
    }

    private static async Task WaitForServerAsync()
    {
        using var client = new HttpClient();
        var timeout = ServerStartTimeout;
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            if (serverProcess?.HasExited == true)
            {
                throw new InvalidOperationException($"Portal process exited early with code {serverProcess.ExitCode}.");
            }

            try
            {
                var response = await client.GetAsync(ServerUrl);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException($"Portal at {ServerUrl} did not start within {timeout.TotalSeconds} seconds.");
    }
}
