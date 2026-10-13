using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LiveProcess")]
[Trait("Category", "HostPlatform")]
public sealed class BlazorDeliveryLiveTests(ITestOutputHelper output) {
    private const string DefinitionKey = "blazor-app-delivery";
    private const string Model = "gpt-6-sol";

    [LiveBrowserQaFact]
    public Task Builtin_delivery_builds_Blazor_and_completes_browser_QA_and_structure_writeback()
        => RunDeliveryAsync(deliveryBlock: false);

    [LiveBrowserQaFact]
    public Task Delivery_block_launch_builds_a_playable_PWA_and_records_the_result_without_a_WorkItem()
        => RunDeliveryAsync(deliveryBlock: true);

    private async Task RunDeliveryAsync(bool deliveryBlock) {
        Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY")));
        var evidence = Environment.GetEnvironmentVariable("CANDOITALL_LIVE_PROOF_DIRECTORY");
        Assert.False(string.IsNullOrWhiteSpace(evidence));
        Directory.CreateDirectory(evidence);
        await using var environment = CanDoItAllTestEnvironment.Create("blazor-delivery-live");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var failures = new ConcurrentQueue<string>();
        await using var app = await TestApplication.CreateAsync(new() {
            TestEnvironment = environment, ActiveProfile = profile,
            ConfigureServices = services => services.AddSingleton<ILoggerProvider>(new FailureLogProvider(failures))
        });
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId,
            new() { Name = "Owned Blazor delivery regression" })).IsSuccess);
        var productRoot = Path.Combine(environment.RootPath, "product");
        Directory.CreateDirectory(productRoot);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var requirements = deliveryBlock
            ? "Build a complete playable client-only .NET 10 Blazor WebAssembly PWA falling-block game at /. " +
                "Use one C# game-state owner for a 10 by 20 playfield, seven tetromino shapes with distinct colors, " +
                "collision detection, locking/stacking, line clearing, score, increasing level/gravity speed and a next-piece preview using the same queue. " +
                "Support arrow-key movement, rotation and Space hard drop, with visible keyboard instructions, Start/Pause and Reset controls. " +
                "Persist only the best score in browser local storage; verify it survives reload. " +
                "No backend, API, database, identity or server-mediated gameplay. Keep UI rendering separate from the pure testable state model. " +
                "Provide deterministic bounded automated tests for movement/collision, locking, line clearing, scoring and queue progression. " +
                "Publish static output; QA must use a fresh real browser at 1920x1080 against the published output, " +
                "use native browser_click for Start/Pause, resume and Reset and browser_press_key for keyboard controls; " +
                "verify the board changes and pieces stack, pause stops it, and Reset clears it; inspect console/page errors, " +
                "inspect ordered screenshots, and verify PWA registration and offline reload after caching. " +
                "Stop the proof host, register a verified screenshot ImageAsset and record the accepted result under this selected delivery block."
            : "Build a small standalone .NET 10 Blazor WebAssembly application (not PWA). " +
            "At / show heading 'Delivery counter', counter initially 0, Increment adds one, Reset returns to 0. " +
            "Use a small independently testable C# counter model and meaningful automated tests for increment/reset. " +
            "No backend, authentication, database, persistence, offline behavior, external deployment or external services. " +
            "Publish and validate the real application through a disposable loopback static host. " +
            "QA must exercise both buttons in a real browser at 1920x1080, capture a screenshot and inspect console errors. " +
            "Stop the proof server, register the verified screenshot as a managed ImageAsset and write the accepted result below this work item. " +
            "Implementation hands off to the separate QA step; each step performs only its assigned work.";
        var block = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock,
            "Approved product folder", "Owned test output", deliveryBlock ? requirements : "The application belongs in this output root.",
            ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId), ObjectSubtype: "delivery",
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() { ProjectBlock = new() { OutputRoot = productRoot } })));
        var task = deliveryBlock ? block : await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.WorkItem,
            "Deliver tested Blazor counter", "Full production process regression", requirements, block.Id,
            ObjectSubtype: ProjectObjectSubtypePolicy.Task));
        var workspace = services.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>().GetOrganizationWorkspaceService();
        var store = services.GetRequiredService<ISandboxWorkspaceStore>();
        var catalog = await store.LoadCatalogAsync();
        var provider = catalog.Providers.First(item => item.Kind == ProviderKind.OpenAi && item.Transport == ProviderTransportKind.Responses);
        var configuration = AgentProjectStructureAccessMetadata.Write(
            AgentWorkspaceToolAccessMetadata.Write("{}", AgentWorkspaceToolAccessProfiles.CreateSettings(AgentWorkspaceToolProfileKind.SoftwareDevelopment)),
            new() { CanRead = true, CanWrite = true, CanWriteNonTaskStructure = true, AllowedProjectIds = [projectId] });
        string[] roles = ["blazor-delivery-manager", "blazor-solution-architect", "blazor-engineer", "blazor-qa-lead"];
        string[] seedKeys = ["delivery-manager", "dotnet-solution-architect", "blazor-application-developer", "dotnet-qa-review-lead"];
        var seeds = roles.Zip(seedKeys).ToDictionary(pair => pair.First,
            pair => catalog.Agents.Single(agent => agent.TemplateKey == pair.Second));
        var agents = roles.ToDictionary(role => role, role => seeds[role] with {
            Id = Guid.NewGuid(), Name = $"Owned {role}", RoleTitle = role, Status = AgentLifecycleStatus.Active,
            IsTemplate = false, TemplateKey = string.Empty, ProviderProfileId = provider.Id,
            Model = deliveryBlock && role == "blazor-qa-lead" ? "gpt-5.6-terra" : Model,
            Workload = AgentWorkloadKind.Programming, ChatHistoryMode = AgentChatHistoryMode.FrameworkManaged,
            EnableBackgroundResponses = false, Permissions = AgentPermissionsPolicy.Default, Tags = [.. seeds[role].Tags, role],
            ConfigurationJson = configuration
        });
        await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, .. agents.Values] });
        var variables = await services.GetRequiredService<ProjectStructureProcessNodeService>()
            .BuildProjectScopedLaunchVariablesAsync(new(projectId, task.Id, DefinitionKey, null,
                "live-delivery-regression", new Dictionary<string, string>()),
                services.GetRequiredService<IProcessLaunchVariablePreparer>());
        (string Step, string Role)[] assignments = [
            ("resolve-blazor-contract", roles[1]), ("implement-blazor-change", roles[2]),
            ("validate-blazor-runtime", roles[3]), ("repair-blazor-findings", roles[2]),
            ("revalidate-blazor-repair", roles[3]), ("record-blazor-results", roles[0]),
            ("record-blazor-results-after-repair", roles[0])
        ];
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(projectId, ProcessLaunchOperatorSurface.UserInterface);
        var launched = await services.GetRequiredService<ProcessLaunchApplicationService>().LaunchAsync(new(
            DefinitionKey, null, null, projectId, task.Id, "live-delivery-regression", variables, RunReadiness: true, Execute: false) {
            Authority = authority, ProjectAdmission = authority.ProjectAdmission, CallerIntentId = new(Guid.NewGuid()),
            ExecutorOverrides = [.. assignments.Select(item => new ProcessLaunchExecutorOverride(item.Step, item.Role,
                ProcessLaunchExecutorKinds.Agent, agents[item.Role].Id.ToString("D"), agents[item.Role].Name, "Owned live proof"))]
        });
        await File.WriteAllTextAsync(Path.Combine(evidence, "launch.json"), JsonSerializer.Serialize(launched, JsonSerializerOptions.Web));
        output.WriteLine($"Launch stage={launched.Stage}; warnings={string.Join("; ", launched.Warnings)}");
        Assert.NotNull(launched.RunId);
        var timings = new ConcurrentBag<(string Name, double Milliseconds)>();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name.StartsWith("CanDoItAll", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => timings.Add((activity.OperationName, activity.Duration.TotalMilliseconds))
        };
        ActivitySource.AddActivityListener(listener);
        var elapsed = Stopwatch.StartNew();
        await File.WriteAllTextAsync(Path.Combine(evidence, "lineage.json"), JsonSerializer.Serialize(new {
            projectId, runId = launched.RunId, models = agents.Values.Select(agent => agent.Model).Distinct(),
            fixture = environment.RootPath, productRoot, selectedNodeId = task.Id, deliveryBlock
        }, JsonSerializerOptions.Web));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(40));
        try {
            await services.GetRequiredService<ProcessRuntimeDispatchApplicationService>()
                .ExecuteReadyAsync(launched.RunId.Value, "live-delivery-regression", timeout.Token);
        } finally {
            elapsed.Stop();
            await File.WriteAllLinesAsync(Path.Combine(evidence, "runtime-errors.txt"), failures);
            var current = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(launched.RunId.Value);
            await File.WriteAllTextAsync(Path.Combine(evidence, "process-state.json"), JsonSerializer.Serialize(current, JsonSerializerOptions.Web));
            await File.WriteAllTextAsync(Path.Combine(evidence, "structure.json"),
                JsonSerializer.Serialize(await workbench.GetStructureAsync(projectId), JsonSerializerOptions.Web));
            var summaries = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(ProcessRunId: launched.RunId.Value.ToString()));
            foreach (var run in summaries) {
                var detail = await workspace.GetExecutionRunDetailAsync(run.Id);
                await File.WriteAllTextAsync(Path.Combine(evidence, $"execution-{run.Id:N}.json"), JsonSerializer.Serialize(detail, JsonSerializerOptions.Web));
                foreach (var artifact in detail.Artifacts.Where(item => item.RelativePath.Contains("/browser/", StringComparison.Ordinal) && item.RelativePath.EndsWith(".png", StringComparison.Ordinal))) {
                    File.Copy(Path.Combine(profile.WorkspaceRootPath, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)),
                        Path.Combine(evidence, $"{run.Id:N}-{Path.GetFileName(artifact.RelativePath)}"), true);
                }
            }
            await File.WriteAllTextAsync(Path.Combine(evidence, "timings.json"), JsonSerializer.Serialize(new {
                elapsedSeconds = elapsed.Elapsed.TotalSeconds,
                operations = timings.GroupBy(item => item.Name).Select(group => new {
                    name = group.Key, count = group.Count(), totalMs = group.Sum(item => item.Milliseconds),
                    averageMs = group.Average(item => item.Milliseconds), maxMs = group.Max(item => item.Milliseconds)
                }).OrderByDescending(item => item.totalMs)
            }, JsonSerializerOptions.Web));
        }
        var state = await services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(launched.RunId.Value);
        Assert.NotNull(state);
        output.WriteLine($"Run={launched.RunId}; Status={state.Status}; Elapsed={elapsed.Elapsed}");
        Assert.Equal(ProcessRuntimeStatus.Completed, state.Status);
        Assert.Equal(8, state.Steps.Count);
        var escalation = launched.LaunchPlan.Steps.Single(step => step.StepKey == "escalate-blazor-unresolved-repair");
        Assert.Equal(0, state.Steps.Single(step => step.StepInstanceId == escalation.StepInstanceId).AttemptNumber);
        var executions = await workspace.ListExecutionRunsAsync(new ExecutionRunQuery(ProcessRunId: launched.RunId.Value.ToString()));
        var details = await Task.WhenAll(executions.Select(run => workspace.GetExecutionRunDetailAsync(run.Id)));
        var receipts = details.SelectMany(detail => detail.ToolReceipts).ToArray();
        foreach (var tool in new[] { "workspace_dotnet_build", "workspace_dotnet_test", "workspace_static_serve", "browser_navigate", "browser_click", "browser_take_screenshot", "workspace_dotnet_stop", ProjectStructureToolPolicy.ProjectStructureAssetCreate }) {
            Assert.True(receipts.Any(receipt => receipt.ToolName == tool && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded),
                $"Missing successful native receipt: {tool}");
        }
        Assert.NotEmpty(Directory.GetFiles(productRoot, "*.razor", SearchOption.AllDirectories));
        foreach (var implementation in details.Where(detail => detail.Run.SourceId is "implement-blazor-change" or "repair-blazor-findings")) {
            Assert.DoesNotContain(implementation.ToolReceipts, receipt => receipt.ToolName.StartsWith("browser_", StringComparison.Ordinal));
        }
        Assert.NotEmpty(Directory.GetFiles(evidence, "*.png"));
        var structure = await workbench.GetStructureAsync(projectId);
        await File.WriteAllTextAsync(Path.Combine(evidence, "structure.json"), JsonSerializer.Serialize(structure, JsonSerializerOptions.Web));
        Assert.Contains(structure.Nodes, node => node.ParentId == task.Id && node.ObjectType == ProjectObjectType.Note);
        Assert.Contains(structure.Nodes, node => node.ObjectType == ProjectObjectType.ImageAsset);
        var runtimeNode = Assert.Single(structure.Nodes, node => node.ParentId == task.Id && node.ObjectType == ProjectObjectType.Environment);
        Assert.Equal("dotnet-runtime", runtimeNode.ObjectSubtype);
        var runtimeMetadata = ProjectObjectMetadataSerializer.Parse(runtimeNode.MetadataJson).Environment;
        Assert.NotNull(runtimeMetadata);
        Assert.Equal(ProjectEnvironmentKind.DotNetRuntime, runtimeMetadata.EnvironmentKind);
        Assert.Equal(".csproj", Path.GetExtension(runtimeMetadata.ProjectPath));
        Assert.True(File.Exists(runtimeMetadata.ProjectPath));
        Assert.Equal(Path.GetDirectoryName(runtimeMetadata.ProjectPath), runtimeMetadata.WorkingDirectory);
        var runtimeResolution = services.GetRequiredService<IProjectStructureRuntimeLauncher>().Resolve(
            runtimeNode.ObjectType, runtimeNode.ObjectSubtype, runtimeNode.Notes, runtimeNode.MetadataJson,
            ProjectStructureRuntimePathAuthorityMode.OperatorSelected);
        Assert.True(runtimeResolution.IsSuccess, runtimeResolution.Message);
        Assert.True(runtimeResolution.EffectiveCapabilities.Direct.IsAvailable);
        if (deliveryBlock) {
            Assert.True(receipts.Count(receipt => receipt.ToolName == "browser_click" && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded) >= 3,
                "PWA proof must exercise its start/pause/reset controls through native browser clicks.");
            Assert.Contains(receipts, receipt => receipt.ToolName == "browser_press_key" && receipt.InvocationOutcome == AgentToolInvocationOutcome.Succeeded);
            Assert.DoesNotContain(structure.Nodes, node => node.ObjectType == ProjectObjectType.WorkItem);
            Assert.NotEmpty(Directory.GetFiles(productRoot, "service-worker.published.js", SearchOption.AllDirectories));
        }
    }

    private sealed class FailureLogProvider(ConcurrentQueue<string> entries) : ILoggerProvider, ILogger {
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) {
            if (IsEnabled(logLevel) && entries.Count < 100) {
                entries.Enqueue(SensitiveTextRedactor.Redact($"{formatter(state, exception)}\n{exception}"));
            }
        }
    }
}
