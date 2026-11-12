using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ProcessAuthoringRestartHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("process-authoring-restart");
    private readonly ConcurrentQueue<string> log = new();
    private readonly Dictionary<string, string?> configuration = new() {
        ["DevelopmentManager:TuningModeEnabled"] = "false",
        ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
        ["Api:Enabled"] = "true",
        ["Api:Authorization:Enabled"] = "false",
        [$"{ProviderInitializationOptions.SectionName}:SeedDefaults"] = "false",
        [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
    };
    private TestDatabaseProfile profile = null!;
    private ServiceProvider services = null!;
    private Process? process;
    private Task[] pumps = [];
    internal string BaseUrl { get; private set; } = string.Empty;
    internal string DefinitionKey { get; private set; } = string.Empty;
    internal WorkflowDefinition Workflow { get; private set; } = null!;
    internal int ProcessId => process!.Id;
    internal string Evidence { get; } = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "processes-pc3", Guid.NewGuid().ToString("N"));

    internal async Task StartAsync() {
        Directory.CreateDirectory(Evidence);
        profile = environment.CreatePostgreSqlProfile("authoring-restart");
        services = await TestApplicationBootstrap.BuildServiceProviderAsync(profile, "ProcessAuthoringRestartSeed", TestSchemaBootstrapModules.Full, configuration);
        await ReadAsync(async provider => {
            Workflow = await provider.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(null, null, "Owned publication workflow",
                "Deterministic native publication proof", WorkflowLifecycleStatus.Active, ProcessNativeBrowserHost.CreateGraph(false),
                new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false)));
            var templates = provider.GetRequiredService<ProcessTemplatePackLoader>();
            DefinitionKey = templates.Load().Definitions[0].Key;
            var workspace = provider.GetRequiredService<ProcessAuthoringWorkspace>();
            var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.Global, new(DefinitionKey), default);
            var content = workspace.ReadTemplate(DefinitionKey);
            content.Definition.DisplayName = "Owned restart draft";
            content.Definition.RoleUsages = [new() { Key = "workflow-owner", DisplayName = "Workflow owner", PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow,
                IsRequired = true, WorkflowBinding = new(new(Workflow.Id.Value), new(Workflow.VersionId.Value)) }];
            content.Definition.Steps = [new() { Key = "outcome", Title = "Published workflow outcome", StepKind = "Start",
                AllowedOperations = ["ReadProcessContext"], OperationTargetScope = "ExternalProductTargetReadOnly",
                RoleAssignments = [new() { RoleKey = "workflow-owner", ResponsibilityKind = "Responsible", IsRequired = true }] }];
            content.Definition.LaunchDriverActivations.Clear();
            content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
            return await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("owned restart seed"), content, ProcessAuthoringLifecycle.Draft, false, null, default);
        });
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        BaseUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
        listener.Stop();
        await StartProcessAsync();
    }

    internal async Task<T> ReadAsync<T>(Func<IServiceProvider, Task<T>> action) {
        await using var scope = services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    internal async Task RestartAsync() {
        var original = ProcessId;
        await StopAsync();
        await StartProcessAsync();
        Assert.NotEqual(original, ProcessId);
    }

    private async Task StartProcessAsync() {
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "App", "CanDoItAll.Web"),
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(start.WorkingDirectory, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "CanDoItAll.Web.dll"));
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        foreach (var pair in profile.CreateEnvironmentVariables(configuration)) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        process = Process.Start(start) ?? throw new InvalidOperationException("The owned Process authoring host did not start.");
        log.Enqueue($"Owned host PID={ProcessId}; configuration={PlaywrightTestHostPaths.BuildConfiguration}; URL={BaseUrl}");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (true) {
            deadline.Token.ThrowIfCancellationRequested();
            if (process.HasExited) {
                throw new InvalidOperationException("The owned authoring host exited. Inspect its private server log.");
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

    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            log.Enqueue(line);
        }
    }

    private async Task StopAsync() {
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
        await StopAsync();
        Directory.CreateDirectory(Evidence);
        await File.WriteAllLinesAsync(Path.Combine(Evidence, "server.log"), log);
        if (services is not null) {
            await services.DisposeAsync();
        }
        await environment.DisposeAsync();
    }
}
