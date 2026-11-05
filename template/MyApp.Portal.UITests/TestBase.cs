using System.Diagnostics;
using System.Reflection;
using Microsoft.Playwright.MSTest;

namespace MyApp.Portal.UITests;

/// <summary>
/// Base class for UI tests. Builds and starts the portal once per test run (Release) and
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

    /// <summary>
    /// Maximum time for the app to start after a page is loaded or reloaded. Each test has a fresh browser context, so
    /// the first load downloads and starts the WebAssembly runtime with an empty cache, which takes far longer than a
    /// step on a busy CI runner.
    /// </summary>
    protected const int AppStartTimeoutMs = 60000;

//#if (IncludeSync)
    /// <summary>
    /// The passphrase the tests protect their files with.
    /// </summary>
    protected const string Passphrase = "correct horse battery staple";

//#endif
    private static readonly string ResultsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "TestResults");
    private static readonly string ScreenshotDirectory = Path.Combine(ResultsDirectory, "Screenshots");
    private static readonly TimeSpan PortalBuildTimeout = TimeSpan.FromMinutes(10);
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

        // Portal output goes to TestResults/portal.log rather than the test console. Writing it to the
        // console from background threads interferes with the test runner's output capture.
        Directory.CreateDirectory(ResultsDirectory);
        serverLog = new StreamWriter(Path.Combine(ResultsDirectory, "portal.log"), append: false) { AutoFlush = true };

        // Build first, with its own (generous) time limit: a cold Release build, as on a CI runner, takes minutes, and
        // only the portal starting should count against ServerStartTimeout.
        await BuildPortalAsync(projectPath);

        serverProcess = StartDotnet($"run --project \"{projectPath}\" --configuration Release --no-build --no-launch-profile --urls=\"{ServerUrl}\"");
        try
        {
            await WaitForServerAsync();
        }
        catch
        {
            // Stop the portal, or its output keeps the test run open and it never finishes.
            StopServer();
            throw;
        }
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        StopServer();
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
        await GoToAsync();
        await ExpectLoginPageAsync();
    }

    /// <summary>
    /// Opens a page of the portal and waits for the app to start.
    /// </summary>
    /// <param name="path">The path to open, e.g. <c>/settings</c>; the home page if empty.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected Task GoToAsync(string path = "")
    {
        return GoToAsync(Page, path);
    }

    /// <summary>
    /// Opens a page of the portal in the given browser page (e.g. a second tab) and waits for the app to start.
    /// </summary>
    /// <param name="page">The browser page.</param>
    /// <param name="path">The path to open, e.g. <c>/settings</c>; the home page if empty.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task GoToAsync(IPage page, string path = "")
    {
        await page.GotoAsync($"{ServerUrl}{path}");
        await WaitForAppAsync(page);
    }

    /// <summary>
    /// Reloads the page and waits for the app to start again.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task ReloadAsync()
    {
        await Page.ReloadAsync();
        await WaitForAppAsync(Page);
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

//#if (IncludeSync)
    /// <summary>
    /// Sets the passphrase of the sync profiles in Settings, which unlocks them, and returns to the page it started on.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task SetPassphraseAsync()
    {
        var startUrl = Page.Url;
        await Page.Locator("#nav-settings").ClickAsync();
        await Page.Locator("#e2e-enable").ClickAsync();
        await Page.Locator("#e2e-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-confirm-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-acknowledge").CheckAsync();
        await Page.Locator("#e2e-confirm").ClickAsync(new() { Timeout = AppStartTimeoutMs });
        await CloseFinishedDialogAsync(startUrl);
    }

    /// <summary>
    /// Unlocks the sync profiles with their passphrase from the banner shown after a reload, and returns to the page
    /// it started on.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    protected async Task UnlockAsync()
    {
        var startUrl = Page.Url;
        await Page.Locator("#e2e-setup-button").ClickAsync();
        await Page.Locator("#e2e-setup").ClickAsync();
        await Page.Locator("#e2e-passphrase").FillAsync(Passphrase);
        await Page.Locator("#e2e-confirm").ClickAsync(new() { Timeout = AppStartTimeoutMs });
        await CloseFinishedDialogAsync(startUrl);
    }
//#endif

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

//#if (IncludeSync)
    /// <summary>
    /// Waits for the passphrase to be derived, which blocks the page for a while with the Release settings, closes the
    /// dialog and goes back to the page it started on, in the app so the keys are kept.
    /// </summary>
    private async Task CloseFinishedDialogAsync(string startUrl)
    {
        await Expect(Page.Locator("#e2e-finished")).ToBeVisibleAsync(new() { Timeout = AppStartTimeoutMs });
        await Page.Locator("#e2e-close").ClickAsync();
        await Expect(Page.Locator("#e2e-banner")).ToHaveCountAsync(0);
        if (Page.Url != startUrl)
        {
            await Page.GoBackAsync();
            await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"^{System.Text.RegularExpressions.Regex.Escape(startUrl)}$"));
        }
    }
//#endif

    /// <summary>
    /// Waits until the app has started: it replaces the loading indicator in <c>index.html</c> when it first renders.
    /// </summary>
    private async Task WaitForAppAsync(IPage page)
    {
        await Expect(page.Locator("#app > .loading-progress")).ToHaveCountAsync(0, new() { Timeout = AppStartTimeoutMs });
    }

    private static Process StartDotnet(string arguments)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        // Build in fresh MSBuild processes. Reusing the ones left by building the tests intermittently fails the static
        // web assets compression step ("An item with the same key has already been added").
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.OutputDataReceived += (_, e) => WriteServerLog(e.Data);
        process.ErrorDataReceived += (_, e) => WriteServerLog(e.Data);
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static async Task BuildPortalAsync(string projectPath)
    {
        using var build = StartDotnet($"build \"{projectPath}\" --configuration Release --nologo");
        using var timeout = new CancellationTokenSource(PortalBuildTimeout);
        try
        {
            await build.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            build.Kill(true);
            throw new TimeoutException($"The portal did not build within {PortalBuildTimeout.TotalMinutes} minutes (see TestResults/portal.log).");
        }

        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException($"The portal did not build (exit code {build.ExitCode}, see TestResults/portal.log).");
        }
    }

    private static void StopServer()
    {
        try
        {
            if (serverProcess is { HasExited: false })
            {
                serverProcess.Kill(true);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping server: {ex.Message}");
        }
        finally
        {
            serverProcess?.Dispose();
            serverProcess = null;
            lock (ServerLogLock)
            {
                serverLog?.Dispose();
                serverLog = null;
            }
        }
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
