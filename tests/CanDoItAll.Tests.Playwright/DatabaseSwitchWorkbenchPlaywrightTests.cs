using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using CanDoItAll.Tests.Support;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class DatabaseSwitchWorkbenchPlaywrightTests
{
    [Fact]
    public async Task Switch_reloads_stale_artifact_routes_and_isolates_workbench_storage_per_profile()
    {
        await using var host = await DatabaseSwitchPlaywrightHost.CreateAsync();
        await using var browser = await host.Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });

        var alphaProject = await host.CreateProjectAsync("Runtime Switch Proof", "Execution");
        var structurePage = await context.NewPageAsync();
        await structurePage.GotoAsync($"{host.BaseUrl}{alphaProject.Route}");
        await structurePage.GetByTestId("project-structure-selection-window").WaitForAsync();

        var secondPage = await context.NewPageAsync();
        await secondPage.GotoAsync($"{host.BaseUrl}/projects");
        await secondPage.GetByTestId("projects-new-button").WaitForAsync();

        var alphaProfile = await host.GetCurrentProfileAsync();
        var betaProfile = await host.CreateManagedSqliteProfileAsync();

        Assert.Contains($"/projects/{alphaProject.ProjectId:D}/structure", structurePage.Url, StringComparison.OrdinalIgnoreCase);

        await host.SwitchAsync(betaProfile.Id);

        await structurePage.WaitForURLAsync("**/projects", new() { Timeout = 20_000 });
        await secondPage.WaitForURLAsync("**/projects", new() { Timeout = 20_000 });
        await structurePage.GetByTestId("database-switch-alert").WaitForAsync();
        await secondPage.GetByTestId("database-switch-alert").WaitForAsync();

        Assert.DoesNotContain("/structure", structurePage.Url, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/projects", new Uri(structurePage.Url).AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/projects", new Uri(secondPage.Url).AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(await structurePage.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.False(await secondPage.Locator("#blazor-error-ui").IsVisibleAsync());

        var storageKeys = await structurePage.EvaluateAsync<string[]>(
            "() => Object.keys(window.localStorage).filter(key => key.startsWith('candoitall.workbench.session:'))");

        Assert.Contains(storageKeys, key => key.EndsWith(alphaProfile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(storageKeys, key => key.EndsWith(betaProfile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));

        await SaveEvidenceAsync(structurePage, host.RepoRoot, "db-switch-stale-artifact-recovery-desktop.png");
        await SaveEvidenceAsync(secondPage, host.RepoRoot, "db-switch-cross-tab-desktop.png");

        await structurePage.SetViewportSizeAsync(1100, 900);
        await SaveEvidenceAsync(structurePage, host.RepoRoot, "db-switch-stale-artifact-responsive.png");
    }

    private static async Task SaveEvidenceAsync(IPage page, string repoRoot, string fileName)
    {
        var evidenceRoot = Path.Combine(repoRoot, "evidence");
        Directory.CreateDirectory(evidenceRoot);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceRoot, fileName),
            FullPage = true
        });
    }
}

internal sealed class DatabaseSwitchPlaywrightHost : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _logs = new();
    private readonly HttpClient _client;
    private readonly Process _process;
    private readonly Task _stdoutPump;
    private readonly Task _stderrPump;
    private readonly FakeIpfsTestServer? _fakeIpfsServer;

    private DatabaseSwitchPlaywrightHost(
        string repoRoot,
        string baseUrl,
        CanDoItAllTestEnvironment testEnvironment,
        FakeIpfsTestServer? fakeIpfsServer,
        IPlaywright playwright,
        HttpClient client,
        Process process,
        Task stdoutPump,
        Task stderrPump)
    {
        RepoRoot = repoRoot;
        BaseUrl = baseUrl;
        TestEnvironment = testEnvironment;
        _fakeIpfsServer = fakeIpfsServer;
        Playwright = playwright;
        _client = client;
        _process = process;
        _stdoutPump = stdoutPump;
        _stderrPump = stderrPump;
    }

    public string RepoRoot { get; }

    public string BaseUrl { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public FakeIpfsTestServer? FakeIpfsServer => _fakeIpfsServer;

    public IPlaywright Playwright { get; }

    public static async Task<DatabaseSwitchPlaywrightHost> CreateAsync(bool enableIpfs = false)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var baseUrl = ResolveBaseUrl();
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-playwright-switch");
        var fakeIpfsServer = enableIpfs ? await FakeIpfsTestServer.StartAsync() : null;
        var processStartInfo = new ProcessStartInfo("dotnet", $"run --no-build --no-launch-profile --project src/CanDoItAll.Web --urls {baseUrl}")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["ControlPlane__RootPath"] = testEnvironment.ControlPlaneRootPath;
        processStartInfo.Environment["Storage__ManagedFilesFolder"] = "managed-files";
        processStartInfo.Environment["Storage__ExportsFolder"] = "exports";
        processStartInfo.Environment["Storage__EvidenceFolder"] = "evidence";
        processStartInfo.Environment["Storage__ManagerArtifactsFolder"] = Path.Combine(testEnvironment.RootPath, "manager-artifacts");
        processStartInfo.Environment["Workbench__BrowserStorageKey"] = "candoitall.workbench.session";
        processStartInfo.Environment["DevelopmentManager__TuningModeEnabled"] = "false";
        if (fakeIpfsServer is not null)
        {
            processStartInfo.Environment["ControlPlane__IpfsApiBaseUrl"] = fakeIpfsServer.ApiBaseUri.ToString();
        }

        var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start CanDoItAll.Web for runtime-switch Playwright tests.");
        var stdoutPump = PumpAsync(process.StandardOutput, static (queue, line) => queue.Enqueue(line));
        var stderrPump = PumpAsync(process.StandardError, static (queue, line) => queue.Enqueue(line));

        using var readinessClient = CreateClient(baseUrl);
        await WaitForRuntimeReadyAsync(baseUrl, readinessClient, process, stdoutPump, stderrPump);

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var client = CreateClient(baseUrl);
        return new DatabaseSwitchPlaywrightHost(
            repoRoot,
            baseUrl,
            testEnvironment,
            fakeIpfsServer,
            playwright,
            client,
            process,
            stdoutPump,
            stderrPump);
    }

    public async Task<DevDatabaseProfile> GetCurrentProfileAsync()
    {
        var response = await _client.GetFromJsonAsync<DevDatabaseProfile>("/_dev/database/selection");
        return response ?? throw new InvalidOperationException("The development selection endpoint returned no profile payload.");
    }

    public async Task<DevDatabaseProfile> CreateManagedSqliteProfileAsync()
    {
        using var response = await _client.PostAsync("/_dev/database/profiles/managed-sqlite", content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevDatabaseProfile>();
        return payload ?? throw new InvalidOperationException("The managed SQLite profile endpoint returned no payload.");
    }

    public async Task<DevProjectRoute> CreateProjectAsync(string projectName, string phase)
    {
        using var response = await _client.PostAsync(
            $"/_dev/projects?name={Uri.EscapeDataString(projectName)}&phase={Uri.EscapeDataString(phase)}",
            content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevProjectRoute>();
        return payload ?? throw new InvalidOperationException("The development project endpoint returned no payload.");
    }

    public async Task<DevSeedResult> SeedCurrentProfileAsync(string label)
    {
        using var response = await _client.PostAsync(
            $"/_dev/database/seed-profile?label={Uri.EscapeDataString(label)}",
            content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevSeedResult>();
        return payload ?? throw new InvalidOperationException("The profile seed endpoint returned no payload.");
    }

    public async Task SwitchAsync(Guid profileId)
    {
        using var response = await _client.PostAsync($"/_dev/database/switch/{profileId:D}", content: null);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        Playwright.Dispose();

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        await _stdoutPump;
        await _stderrPump;
        if (_fakeIpfsServer is not null)
        {
            await _fakeIpfsServer.DisposeAsync();
        }
        await TestEnvironment.DisposeAsync();
    }

    private static async Task WaitForRuntimeReadyAsync(
        string baseUrl,
        HttpClient client,
        Process process,
        Task stdoutPump,
        Task stderrPump)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (process.HasExited)
            {
                await stdoutPump;
                await stderrPump;
                throw new InvalidOperationException("The web app exited before becoming ready.");
            }

            try
            {
                var payload = await client.GetStringAsync($"{baseUrl}/_dev/runtime");
                if (payload.Contains("\"isReady\":true", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch
            {
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for the runtime-switch Playwright host to become ready.");
    }

    private static HttpClient CreateClient(string baseUrl)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    private static Task PumpAsync(StreamReader reader, Action<ConcurrentQueue<string>, string> onLine)
    {
        var lines = new ConcurrentQueue<string>();
        return Task.Run(async () =>
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                onLine(lines, line);
            }
        });
    }

    private static string ResolveBaseUrl()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return $"http://127.0.0.1:{port}";
    }
}

internal sealed class DevDatabaseProfile
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Fingerprint { get; set; } = string.Empty;

    public string WorkspaceRoot { get; set; } = string.Empty;

    public string ConnectionString { get; set; } = string.Empty;
}

internal sealed class DevProjectRoute
{
    public Guid ProjectId { get; set; }

    public string Route { get; set; } = string.Empty;
}

internal sealed class DevSeedResult
{
    public Guid Value { get; set; }

    public string ProjectName { get; set; } = string.Empty;

    public string ManagedFileRelativePath { get; set; } = string.Empty;

    public string ManagedFileFullPath { get; set; } = string.Empty;

    public string ManagedFileContent { get; set; } = string.Empty;
}
