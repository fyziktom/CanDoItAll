using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Npgsql;

namespace CanDoItAll.Tests.Playwright.Flows;

[Collection(PlaywrightCollection.Name)]
public sealed class DatabaseSwitchWorkbenchPlaywrightTests
{
    [Fact]
    public async Task Switch_activation_requires_restart_and_keeps_running_routes_on_current_profile()
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
        var betaProfile = await host.CreatePostgreSqlProfileAsync();

        Assert.Contains($"/projects/{alphaProject.ProjectId:D}/structure", structurePage.Url, StringComparison.OrdinalIgnoreCase);

        var switchResult = await host.SwitchAsync(betaProfile.Id);

        Assert.True(switchResult.RequiresRestart);
        Assert.False(switchResult.RuntimeChangedInProcess);
        Assert.Equal(alphaProfile.Id, switchResult.RuntimeProfileId);
        Assert.Equal(betaProfile.Id, switchResult.PendingRestartProfileId);
        Assert.Contains("Restart", switchResult.Message, StringComparison.OrdinalIgnoreCase);

        var runtimeProfile = await host.GetCurrentProfileAsync();
        Assert.Equal(alphaProfile.Id, runtimeProfile.Id);
        Assert.Equal(alphaProfile.Id, runtimeProfile.RuntimeProfileId);
        Assert.Equal(betaProfile.Id, runtimeProfile.PendingRestartProfileId);
        Assert.True(runtimeProfile.HasPendingRestartActivation);
        Assert.Contains($"/projects/{alphaProject.ProjectId:D}/structure", structurePage.Url, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/projects", new Uri(secondPage.Url).AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.False(await structurePage.Locator("#blazor-error-ui").IsVisibleAsync());
        Assert.False(await secondPage.Locator("#blazor-error-ui").IsVisibleAsync());

        var storageKeys = await structurePage.EvaluateAsync<string[]>(
            "() => Object.keys(window.localStorage).filter(key => key.startsWith('candoitall.workbench.session:'))");

        Assert.Contains(storageKeys, key => key.EndsWith(alphaProfile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(storageKeys, key => key.EndsWith(betaProfile.Id.ToString("N"), StringComparison.OrdinalIgnoreCase));

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
    private int _profileCounter;

    private DatabaseSwitchPlaywrightHost(
        string repoRoot,
        string baseUrl,
        CanDoItAllTestEnvironment testEnvironment,
        FakeIpfsTestServer? fakeIpfsServer,
        IPlaywright playwright,
        HttpClient client,
        Process process,
        Task stdoutPump,
        Task stderrPump,
        ConcurrentQueue<string> logs)
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
        _logs = logs;
    }

    public string RepoRoot { get; }

    public string BaseUrl { get; }

    public CanDoItAllTestEnvironment TestEnvironment { get; }

    public FakeIpfsTestServer? FakeIpfsServer => _fakeIpfsServer;

    public IPlaywright Playwright { get; }

    public static async Task<DatabaseSwitchPlaywrightHost> CreateAsync(bool enableIpfs = false)
    {
        var repoRoot = PlaywrightTestHostPaths.RepositoryRoot;
        var baseUrl = ResolveBaseUrl();
        var testEnvironment = CanDoItAllTestEnvironment.Create("candoitall-playwright-switch");
        var activeProfile = testEnvironment.CreatePostgreSqlProfile("primary");
        await PersistActiveProfileAsync(testEnvironment, activeProfile);
        var fakeIpfsServer = enableIpfs ? await FakeIpfsTestServer.StartAsync() : null;
        var logs = new ConcurrentQueue<string>();
        var processStartInfo = new ProcessStartInfo(
            "dotnet",
            PlaywrightTestHostPaths.BuildDotnetRunArguments("src/App/CanDoItAll.Web", baseUrl))
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        processStartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        processStartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        foreach (var pair in CreateHostConfigurationValues(testEnvironment))
        {
            if (pair.Value is not null)
            {
                processStartInfo.Environment[pair.Key.Replace(":", "__", StringComparison.Ordinal)] = pair.Value;
            }
        }

        if (fakeIpfsServer is not null)
        {
            processStartInfo.Environment["ControlPlane__IpfsApiBaseUrl"] = fakeIpfsServer.ApiBaseUri.ToString();
        }

        var process = Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Failed to start CanDoItAll.Web for runtime-switch Playwright tests.");
        var stdoutPump = PumpAsync(process.StandardOutput, logs);
        var stderrPump = PumpAsync(process.StandardError, logs);

        using var readinessClient = CreateClient(baseUrl);
        await WaitForRuntimeReadyAsync(baseUrl, readinessClient, process, stdoutPump, stderrPump, logs);

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
            stderrPump,
            logs);
    }

    public async Task<DevDatabaseProfile> GetCurrentProfileAsync()
    {
        var response = await _client.GetFromJsonAsync<DevDatabaseProfile>("/_dev/database/selection");
        return response ?? throw new InvalidOperationException("The development selection endpoint returned no profile payload.");
    }

    public async Task<DevDatabaseProfile> CreatePostgreSqlProfileAsync()
    {
        var profile = TestEnvironment.CreatePostgreSqlProfile($"playwright-postgres-{Interlocked.Increment(ref _profileCounter)}");
        var builder = new NpgsqlConnectionStringBuilder(profile.ConnectionString);
        using var response = await _client.PostAsJsonAsync("/_dev/database/profiles/postgresql", new
        {
            DisplayName = $"PostgreSQL {profile.ProfileKey}",
            Host = builder.Host,
            Port = builder.Port,
            DatabaseName = builder.Database,
            Username = builder.Username,
            Password = builder.Password,
            AdminDatabaseName = builder.Database,
            WorkspaceRoot = profile.WorkspaceRootPath,
            Activate = false
        });
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"The development PostgreSQL profile endpoint returned {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<DevDatabaseProfile>();
        return payload ?? throw new InvalidOperationException("The PostgreSQL profile endpoint returned no payload.");
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

    public async Task<DevDatabaseSwitchResult> SwitchAsync(Guid profileId)
    {
        using var response = await _client.PostAsync($"/_dev/database/switch/{profileId:D}", content: null);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DevDatabaseSwitchResult>();
        return payload ?? throw new InvalidOperationException("The development switch endpoint returned no payload.");
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
        Task stderrPump,
        ConcurrentQueue<string> logs)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (process.HasExited)
            {
                await stdoutPump;
                await stderrPump;
                throw new InvalidOperationException(
                    $"The web app exited before becoming ready.{Environment.NewLine}{CreateLogSnapshot(logs)}");
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

        throw new TimeoutException(
            $"Timed out waiting for the runtime-switch Playwright host to become ready.{Environment.NewLine}{CreateLogSnapshot(logs)}");
    }

    private static HttpClient CreateClient(string baseUrl)
    {
        return new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static async Task PersistActiveProfileAsync(CanDoItAllTestEnvironment testEnvironment, TestDatabaseProfile activeProfile)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(CreateHostConfigurationValues(testEnvironment))
            .Build();

        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            testEnvironment.CreateHostEnvironment("CanDoItAll.Tests.Playwright"));

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var profileService = provider.GetRequiredService<IDatabaseProfileService>();
        var saveResult = await profileService.SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(
            activeProfile,
            "PostgreSQL primary"));
        if (saveResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to save the initial Playwright database profile. {string.Join(" ", saveResult.Errors.Select(error => error.Message))}");
        }

        var activateResult = await profileService.ActivateAsync(saveResult.Value);
        if (activateResult.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to activate the initial Playwright database profile. {string.Join(" ", activateResult.Errors.Select(error => error.Message))}");
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateHostConfigurationValues(CanDoItAllTestEnvironment testEnvironment)
        => new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Database:Provider"] = string.Empty,
            ["Database:ConnectionString"] = string.Empty,
            ["ControlPlane:RootPath"] = testEnvironment.ControlPlaneRootPath,
            ["Storage:ManagedFilesFolder"] = "managed-files",
            ["Storage:ExportsFolder"] = "exports",
            ["Storage:EvidenceFolder"] = "evidence",
            ["Storage:ManagerArtifactsFolder"] = Path.Combine(testEnvironment.RootPath, "manager-artifacts"),
            ["Workbench:MaxWarmTabs"] = "3",
            ["Workbench:SleepAfterMinutes"] = "15",
            ["Workbench:BrowserStorageKey"] = "candoitall.workbench.session",
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["DevelopmentManager:ReviewBeforeSend"] = "true",
            ["DevelopmentManager:ManagerBaseUrl"] = "http://127.0.0.1:6407",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
        };

    private static Task PumpAsync(StreamReader reader, ConcurrentQueue<string> logs)
        => Task.Run(async () =>
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                logs.Enqueue(line);
            }
        });

    private static string CreateLogSnapshot(ConcurrentQueue<string> logs)
        => string.Join(Environment.NewLine, logs.Reverse().Take(200).Reverse());

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

    public Guid? RuntimeProfileId { get; set; }

    public Guid? PendingRestartProfileId { get; set; }

    public bool HasPendingRestartActivation { get; set; }
}

internal sealed class DevDatabaseSwitchResult
{
    public Guid CurrentProfileId { get; set; }

    public Guid? RuntimeProfileId { get; set; }

    public Guid? PendingRestartProfileId { get; set; }

    public bool RequiresRestart { get; set; }

    public bool RuntimeChangedInProcess { get; set; }

    public string Message { get; set; } = string.Empty;
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
