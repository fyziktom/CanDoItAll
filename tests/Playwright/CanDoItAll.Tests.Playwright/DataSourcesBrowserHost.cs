using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright;

internal sealed class DataSourcesBrowserHost : IAsyncDisposable {
    public CanDoItAllTestEnvironment Environment { get; } = CanDoItAllTestEnvironment.Create("data-sources-browser");
    private readonly ConcurrentQueue<string> logs = new();
    private readonly Dictionary<string, string?> configuration = new() {
        ["Database:Provider"] = string.Empty, ["Database:ConnectionString"] = string.Empty,
        ["DevelopmentManager:TuningModeEnabled"] = "false", ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
        [$"{ProviderInitializationOptions.SectionName}:SeedDefaults"] = "false",
        [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
    };
    private Process? process;
    private Task[] pumps = [];
    private PostgresTestDatabaseLease? targetLease;
    public TestDatabaseProfile A { get; private set; } = null!;
    public TestDatabaseProfile B { get; private set; } = null!;
    public ServiceProvider Services { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;
    public Guid ProfileA { get; private set; }
    public Guid DefaultProviderId { get; } = Guid.NewGuid();
    public Guid ProjectA { get; private set; }
    public Guid SecretA { get; private set; }
    public Guid ResourceA { get; private set; }
    public Guid AccountId { get; } = Guid.NewGuid();
    public Guid TokenId { get; } = Guid.NewGuid();

    public async Task StartAsync() {
        A = Environment.CreatePostgreSqlProfile("data-sources-a");
        targetLease = PostgresTestDatabaseLease.Create("data-sources-b");
        B = Environment.CreatePostgreSqlProfile("data-sources-b", targetLease.ConnectionString);
        await targetLease.DisposeAsync();
        Services = BuildServices(A, configuration);
        var saved = await Services.GetRequiredService<IDatabaseProfileService>().SaveAsync(TestDatabaseProfileEditorFactory.CreatePostgreSqlEditor(A, "Owned workspace A"));
        Assert.True(saved.IsSuccess);
        ProfileA = saved.Value;
        await TestApplicationBootstrap.InitializeSchemaAsync(Services, TestSchemaBootstrapModules.Full);
        await using (var scope = Services.CreateAsyncScope()) {
            await scope.ServiceProvider.GetRequiredService<WorkspaceService>().SaveSettingsAsync(new() {
                WorkspaceName = "Marker A", DefaultProviderProfileId = DefaultProviderId, Notes = "Original A marker"
            });
            var project = await scope.ServiceProvider.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Owned source project A" });
            Assert.True(project.IsSuccess);
            ProjectA = project.Value;
            var secret = await scope.ServiceProvider.GetRequiredService<SecretService>().SaveAsync(new() { Name = "A-only secret", SecretValue = "synthetic-profile-marker" });
            Assert.True(secret.IsSuccess);
            SecretA = secret.Value;
            var admission = Assert.Single(await scope.ServiceProvider.GetRequiredService<ProjectWriteSelectionQuery>().ListAsync(), item => item.Id == ProjectA).Admission;
            var resourceModel = new ResourceEditorModel {
                Name = "A-only resource", ProjectId = ProjectA, ExpectedProjectAdmission = admission,
                ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink, LocationOrIdentifier = "https://example.invalid/workspace-a"
            };
            resourceModel.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, resourceModel.LocationOrIdentifier);
            var resource = await scope.ServiceProvider.GetRequiredService<ResourcesService>().SaveAsync(resourceModel);
            Assert.True(resource.IsSuccess);
            ResourceA = resource.Value;
            var clock = DateTimeOffset.UtcNow;
            await scope.ServiceProvider.GetRequiredService<IApiUserStore>().SaveAsync(new(AccountId, "owned-instance-user", "OWNED-INSTANCE-USER", "Owned instance marker", true,
                scope.ServiceProvider.GetRequiredService<ApiPasswordService>().Hash("Synthetic account password 42!"), [], 1, 1, clock, clock), null);
            scope.ServiceProvider.GetRequiredService<IApiTokenRegistry>().Register(new(TokenId, "owned-instance-marker", "Owned instance marker", clock, clock.AddHours(1), [ApiAccessScopeNames.Api]));
        }
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        listener.Stop();
        await StartProcessAsync();
    }

    public async Task RestartAsync() {
        await StopProcessAsync();
        await StartProcessAsync();
    }
    public ServiceProvider BuildTargetServices() => BuildServices(B, new Dictionary<string, string?>(configuration) {
        ["Database:Provider"] = "Postgres", ["Database:ConnectionString"] = B.ConnectionString
    });
    public async Task<Guid> TargetProfileIdAsync() => Assert.Single(await Services.GetRequiredService<IDatabaseProfileService>().ListAsync(), item => item.DisplayName == "Owned workspace B").Id;

    public async Task<DataSourcesRuntimeProof> ReadRuntimeAsync() {
        using var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        return await client.GetFromJsonAsync<DataSourcesRuntimeProof>("/_dev/database/selection") ?? throw new InvalidOperationException("No canonical runtime proof.");
    }

    private ServiceProvider BuildServices(TestDatabaseProfile profile, IReadOnlyDictionary<string, string?> overrides) {
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(services, TestApplicationBootstrap.BuildConfiguration(profile, overrides), Environment.CreateHostEnvironment("DataSources.Browser.Proof"));
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
    private async Task StartProcessAsync() {
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "App", "CanDoItAll.Web"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(start.WorkingDirectory, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "CanDoItAll.Web.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        foreach (var pair in A.CreateEnvironmentVariables(configuration)) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        process = Process.Start(start) ?? throw new InvalidOperationException("Owned Data Sources host did not start.");
        logs.Enqueue($"Owned Data Sources PID={process.Id}; URL={BaseUrl}; profiles={ProfileA}");
        pumps = [Pump(process.StandardOutput), Pump(process.StandardError)];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            if (process.HasExited) {
                throw new InvalidOperationException("Owned Data Sources host exited. Inspect its retained safe log.");
            }
            try {
                if ((await client.GetStringAsync(BaseUrl + "/_dev/runtime", deadline.Token)).Contains("\"isReady\":true", StringComparison.Ordinal)) {
                    return;
                }
            } catch (HttpRequestException) {
            } catch (OperationCanceledException) when (!deadline.IsCancellationRequested) {
            }
            await Task.Delay(100, deadline.Token);
        }
    }
    private async Task Pump(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
        }
    }
    private async Task StopProcessAsync() {
        if (process is null) {
            return;
        }
        if (!process.HasExited) {
            process.Kill(entireProcessTree: true);
        }
        await process.WaitForExitAsync();
        await Task.WhenAll(pumps);
        process.Dispose();
        process = null;
    }
    public async ValueTask DisposeAsync() {
        await StopProcessAsync();
        var output = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "artifacts", "workspace-completion", "data-sources");
        Directory.CreateDirectory(output);
        await File.WriteAllLinesAsync(Path.Combine(output, "production-host.log"), logs);
        if (Services is not null) {
            await Services.DisposeAsync();
        }
        if (targetLease is not null) {
            await targetLease.DisposeAsync();
        }
        await Environment.DisposeAsync();
    }
}

internal sealed record DataSourcesRuntimeProof(Guid Id, Guid? RuntimeProfileId, Guid? PendingRestartProfileId, bool HasPendingRestartActivation, string WorkspaceRoot);
