using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ProcessNativeBrowserHost : IAsyncDisposable {
    internal const string CompleteDefinition = "pc1-workflow-outcome";
    internal const string WaitingDefinition = "pc1-workflow-wait";
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("process-native-browser");
    private NativeApplication? app;
    private HttpClient? client;
    private readonly AcceptedRunReadFault readFault = new();
    private ProcessAuthoringBrowserControl? authoring;
    internal Task ReadFailure => readFault.Observed.Task;
    internal int ReadFailures => readFault.Count;
    internal Guid ProjectId { get; private set; }
    internal Guid AgentId { get; private set; }
    internal string BaseUrl => app!.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single().TrimEnd('/');
    internal string Evidence { get; } = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "processes-pc1", "native-" + Guid.NewGuid().ToString("N"));
    internal IReadOnlyDictionary<string, WorkflowDefinition> Workflows { get; private set; } = new Dictionary<string, WorkflowDefinition>();

    internal static async Task<ProcessNativeBrowserHost> StartAsync(string upstream, bool allowVoice = false, bool failFirstRunRead = false,
        ProcessAuthoringBrowserControl? authoring = null) {
        var host = new ProcessNativeBrowserHost { authoring = authoring };
        try {
            await host.StartCoreAsync(upstream, allowVoice);
            host.readFault.Armed = failFirstRunRead;
            return host;
        } catch {
            await host.DisposeAsync();
            throw;
        }
    }
    private async Task StartCoreAsync(string upstream, bool allowVoice) {
        Directory.CreateDirectory(Evidence);
        var profile = environment.CreatePostgreSqlProfile("native");
        await using (var seed = await TestApplicationBootstrap.BuildServiceProviderAsync(profile, "ProcessNativeBrowserSeed", TestSchemaBootstrapModules.Full)) {
            await using var scope = seed.CreateAsyncScope();
            var services = scope.ServiceProvider;
            ProjectId = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(ProjectId, new() { Name = "PC1 native workflow project" })).IsSuccess);
            var workflows = new Dictionary<string, WorkflowDefinition>();
            foreach (var key in new[] { CompleteDefinition, WaitingDefinition }) {
                workflows.Add(key, await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(
                    null, null, key, "Task-owned native process browser proof.", WorkflowLifecycleStatus.Active,
                    CreateGraph(key == WaitingDefinition), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false))));
            }
            Workflows = workflows;
            var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                Name = "PC1 synthetic upstream credential", Kind = SecretKind.ApiKey, SecretValue = "local-fixture-credential", Scope = "workspace"
            });
            Assert.True(secret.IsSuccess);
            var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var provider = await workspace.SaveProviderAsync(new ProviderProfileEditorModel {
                Name = "PC1 loopback upstream", Kind = ProviderKind.OpenAi, Transport = ProviderTransportKind.Responses,
                BaseUrl = upstream, ApiKeyEnvironmentVariable = $"secret:{secret.Value:D}", DefaultModel = "pc1-fixture",
                SuggestedModels = ["pc1-fixture"], SupportsStreaming = true, SupportsTools = true,
                ModelPrices = [new() { Model = "pc1-fixture", TariffKind = ProviderTariffKind.ExplicitFree }]
            });
            AgentId = await workspace.SaveAgentAsync(new AgentEditorModel {
                Name = "PC1 process manager", ProviderProfileId = provider, Model = "pc1-fixture", Status = AgentLifecycleStatus.Active,
                Instructions = "Answer with the supplied deterministic result. Do not request tools.",
                WorkspaceToolAccess = new() { Profile = AgentWorkspaceToolProfileKind.Custom },
                VoiceAccess = new() { CanUseVoiceMode = allowVoice },
                ProjectStructureAccess = new() { CanRead = true, AllowedProjectIds = [ProjectId] }
            });
        }
        var pack = WritePack();
        app = new(profile, pack, readFault, authoring);
        app.UseKestrel(0);
        client = app.CreateClient(new() { AllowAutoRedirect = false });
    }
    internal async Task<T> ReadAsync<T>(Func<IServiceProvider, Task<T>> action) {
        await using var scope = app!.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }
    private string WritePack() {
        var root = Path.Combine(environment.RootPath, "templates");
        Directory.CreateDirectory(root);
        var manifest = new ProcessTemplatePackManifest {
            PackKey = "pc1-browser", Name = "PC1 native browser fixtures", Version = "1.0.0", GeneratedAtUtc = DateTimeOffset.UtcNow,
            Processes = Workflows.Keys.Select(key => new ProcessTemplateManifestProcessEntry { Key = key, RelativePath = key }).ToList()
        };
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest));
        if (authoring is not null) {
            Directory.CreateDirectory(Path.Combine(root, "toolbox"));
            File.WriteAllText(Path.Combine(root, "toolbox", "role-templates.json"), """
                [{ "ActionId": "role-template.workflow-owner", "Label": "Reviewer", "TemplateRoleKey": "workflow-owner",
                   "KeyPrefix": "reviewer", "DisplayNameTemplate": "Reviewer {ordinal}", "PreferredExecutorKind": "person-or-agent", "DefaultAllocationPercent": 50 }]
                """);
        }
        foreach (var (key, workflow) in Workflows) {
            var directory = Path.Combine(root, key);
            Directory.CreateDirectory(directory);
            var definition = new ProcessTemplateDefinitionDocument {
                Key = key, DisplayName = key, Summary = "A real saved workflow produces a typed process outcome.",
                Criticality = "Low", OperatingMode = "GovernedLive", AutonomyLevel = "Guarded",
                RoleUsages = [new() { Key = "workflow-owner", RoleResourceKey = "workflow-owner", DisplayName = "Workflow owner",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Workflow, IsRequired = true,
                    WorkflowBinding = new(new(workflow.Id.Value), new(workflow.VersionId.Value)) }],
                Steps = [new() { Key = "outcome", Title = "Native workflow outcome", StepKind = "Start",
                    Notes = "Return the typed outcome of the saved workflow.", AllowedOperations = [ProcessOperationContractNames.ReadProcessContext],
                    OperationTargetScope = ProcessOperationContractNames.ExternalProductTargetReadOnly,
                    RoleAssignments = [new() { RoleKey = "workflow-owner", ResponsibilityKind = "Responsible", IsRequired = true }] }]
            };
            if (authoring is not null) {
                definition.Steps[0].DecisionRoleKey = "workflow-owner";
                definition.RoleUsages.Add(new() { Key = "second-owner", DisplayName = "Second owner", PreferredExecutorKind = "person-or-agent", DefaultAllocationPercent = 50 });
                definition.Steps.Add(new() { Key = "second-step", Title = "Second step", StepKind = "Work", DecisionRoleKey = "second-owner",
                    OperationTargetScope = ProcessOperationContractNames.ManagedProcessArtifactsOnly,
                    AllowedOperations = [ProcessOperationContractNames.ReadProcessContext] });
            }
            File.WriteAllText(Path.Combine(directory, "definition.json"), JsonSerializer.Serialize(definition));
        }
        return root;
    }
    private static WorkflowGraph CreateGraph(bool wait) {
        var descriptor = BuiltInWorkflowExecutorDescriptors.JsonTransform;
        var outcome = JsonSerializer.Serialize(new ProcessStepOutcomeResult {
            Status = ProcessStepOutcomeStatus.Completed, Reason = "PC1 native workflow completed.",
            EvidenceRefs = ["workflow-executor:pc1-outcome"], HumanReadableSummaryMarkdown = "PC1 real workflow output."
        }, AgentOutputJson.SerializerOptions);
        var middle = Node("outcome", wait ? WorkflowNodeKind.HumanInput : WorkflowNodeKind.Executor) with {
            Settings = wait ? new(null, null, null, WorkflowExternalRequestKind.HumanInput, "Wait for PC1 operator input.", descriptor.InputShape, descriptor.ResultShape)
                : new(null, null, null, null, string.Empty, descriptor.InputShape, descriptor.ResultShape) {
                    ExecutorId = descriptor.Id,
                    ExecutorSettingsJson = JsonSerializer.Serialize(new WorkflowJsonTransformExecutorSettings {
                        Operations = [new() { Operation = WorkflowJsonTransformOperation.Set, DestinationPath = "$", ValueJson = outcome }]
                    }, AgentOutputJson.SerializerOptions),
                    ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
                }
        };
        return new(new("start"), [Node("start", WorkflowNodeKind.Start), middle, Node("end", WorkflowNodeKind.End)],
            [Edge("start", "outcome"), Edge("outcome", "end")]);
    }
    private static WorkflowNode Node(string id, WorkflowNodeKind kind) => new(new(id), kind, id, [],
        new(null, null, null, null, string.Empty,
            kind == WorkflowNodeKind.End ? BuiltInWorkflowExecutorDescriptors.JsonTransform.ResultShape : WorkflowValueShape.Text,
            kind == WorkflowNodeKind.Start ? WorkflowValueShape.Text : BuiltInWorkflowExecutorDescriptors.JsonTransform.ResultShape));
    private static WorkflowEdge Edge(string source, string target) => new(new(source + "-" + target), new(source), null,
        new(target), null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always };
    public async ValueTask DisposeAsync() {
        client?.Dispose();
        if (app is not null) {
            await app.DisposeAsync();
        }
        await environment.DisposeAsync();
    }
    private sealed class NativeApplication(TestDatabaseProfile profile, string pack, AcceptedRunReadFault readFault,
        ProcessAuthoringBrowserControl? authoring) : WebApplicationFactory<CanDoItAll.Web.Components.App> {
        protected override void ConfigureWebHost(IWebHostBuilder builder) {
            builder.UseEnvironment("Development");
            builder.UseContentRoot(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "App", "CanDoItAll.Web"));
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(profile.CreateConfigurationValues(new Dictionary<string, string?> {
                ["DevelopmentManager:TuningModeEnabled"] = "false",
                ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
                [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind
            })));
            builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
            builder.ConfigureServices(services => {
                services.Replace(ServiceDescriptor.Singleton(new ProcessTemplatePackLoader(pack)));
                services.Replace(ServiceDescriptor.Scoped<IProcessWorkspaceProjectionClient>(provider =>
                    new ObservedProjectionClient(ActivatorUtilities.CreateInstance<ProcessWorkspaceProjectionClient>(provider), readFault, authoring,
                        provider.GetRequiredService<ProcessDefinitionRoleEditorProjectionService>(), provider.GetRequiredService<ProcessDefinitionStepEditorProjectionService>())));
            });
        }
    }
    private sealed class AcceptedRunReadFault {
        private int count;
        internal bool Armed { get; set; }
        internal int Count => Volatile.Read(ref count);
        internal TaskCompletionSource Observed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal void AfterRead(ProcessWorkspaceShellRequest request) {
            if (Armed && request.Selection.RunId is not null && Interlocked.CompareExchange(ref count, 1, 0) == 0) {
                Observed.TrySetResult();
                throw new TimeoutException("PC1 accepted-run read unavailable.");
            }
        }
    }
    private sealed class ObservedProjectionClient(ProcessWorkspaceProjectionClient native, AcceptedRunReadFault fault,
        ProcessAuthoringBrowserControl? authoring, ProcessDefinitionRoleEditorProjectionService roles,
        ProcessDefinitionStepEditorProjectionService steps) : IProcessWorkspaceProjectionClient {
        public async Task<ProcessWorkspaceShellProjection> GetShellAsync(ProcessWorkspaceShellRequest request, CancellationToken cancellationToken = default) {
            var result = await native.GetShellAsync(request, cancellationToken);
            fault.AfterRead(request);
            if (authoring is not null && result.DefinitionCatalog.SelectedEditor is { } editor) {
                Interlocked.Increment(ref authoring.ReadCount);
                editor = editor with {
                    RoleEditor = editor.RoleEditor is null ? null : await roles.GetEditorAsync(request.Scope, editor.DefinitionKey, cancellationToken),
                    StepEditor = editor.StepEditor is null ? null : await steps.GetEditorAsync(request.Scope, editor.DefinitionKey, cancellationToken)
                };
                result = result with { DefinitionCatalog = result.DefinitionCatalog with { SelectedEditor = editor } };
            }
            return result;
        }
        public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(ProcessDefinitionFeedDefaultsCommand command, CancellationToken cancellationToken = default)
            => native.FeedDefaultDefinitionsAsync(command, cancellationToken);
        public async Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommand command, CancellationToken cancellationToken = default) {
            var result = await native.ExecuteDefinitionEditorCommandAsync(command, cancellationToken);
            if (authoring is not null) {
                Interlocked.Increment(ref authoring.DefinitionCommandCount);
                authoring.DefinitionCommand = command;
                authoring.DefinitionResult = result;
                authoring.DefinitionStarted.TrySetResult();
                await authoring.ReleaseDefinition.Task;
            }
            return result;
        }
        public async Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(ProcessDefinitionRoleEditorCommand command, CancellationToken cancellationToken = default) {
            if (authoring is null) {
                return await native.ExecuteDefinitionRoleEditorCommandAsync(command, cancellationToken);
            }
            var result = await roles.ExecuteCommandAsync(command, cancellationToken);
            authoring.RoleResult = result;
            return result;
        }
        public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(ProcessDefinitionCanvasCommand command, CancellationToken cancellationToken = default)
            => native.ExecuteDefinitionCanvasCommandAsync(command, cancellationToken);
        public async Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(ProcessDefinitionStepEditorCommand command, CancellationToken cancellationToken = default) {
            if (authoring is null) {
                return await native.ExecuteDefinitionStepEditorCommandAsync(command, cancellationToken);
            }
            authoring.StepCommand = command;
            var result = await steps.ExecuteCommandAsync(command, cancellationToken);
            authoring.StepResult = result;
            return result;
        }
        public Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(ProcessTemplateImportCommand command, CancellationToken cancellationToken = default)
            => native.ExecuteTemplateImportCommandAsync(command, cancellationToken);
        public Task<ProcessRuntimeOperatorActionResult> ExecuteRuntimeOperatorActionAsync(ProcessRuntimeOperatorActionCommand command, CancellationToken cancellationToken = default)
            => native.ExecuteRuntimeOperatorActionAsync(command, cancellationToken);
        public Task<ProcessRuntimeRunCancellationResult> RequestRunCancellationAsync(ProcessRuntimeRunCancellationCommand command, CancellationToken cancellationToken = default)
            => native.RequestRunCancellationAsync(command, cancellationToken);
    }
}
