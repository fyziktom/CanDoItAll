using System.Xml.Linq;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowRuntimeManagerContract = CanDoItAll.AgentFramework.Workflows.Abstractions.IWorkflowRuntimeManager;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowRuntimeExtractionTests
{
    [Fact]
    public void WorkflowRuntimeProjectDoesNotReferenceForbiddenImplementationProjects()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            "CanDoItAll.AgentFramework.Workflows.Runtime.csproj");
        var forbiddenReferences = new[]
        {
            "CanDoItAll.AgentFramework.Maf",
            "CanDoItAll.Modules.AgentFramework",
            "CanDoItAll.Modules.Plugins",
            "CanDoItAll.Plugins.Abstractions",
            "CanDoItAll.AgentFramework.Persistence",
            "CanDoItAll.Web"
        };

        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Concat(project
                .Descendants("PackageReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty))
            .ToArray();

        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(
                references,
                reference => reference.Contains(forbiddenReference, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void WorkflowRuntimeImplementationFilesMovedOutOfAgentFrameworkCoreProject()
    {
        var root = FindRepositoryRoot();
        var movedFiles = new[]
        {
            "WorkflowCheckpointFactory.cs",
            "WorkflowRuntimeManager.cs",
            "WorkflowExternalRequestRuntime.cs",
            "WorkflowArtifactContentStores.cs",
            "WorkflowEventPayloads.cs",
            "WorkflowNodeExecutionProgress.cs"
        };

        foreach (var movedFile in movedFiles)
        {
            Assert.False(
                File.Exists(Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Core", "Workflows", movedFile)),
                $"{movedFile} must not remain in AgentFramework.Core.");
            Assert.True(
                File.Exists(Path.Combine(root, "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Runtime", movedFile)),
                $"{movedFile} must exist in Workflows.Runtime.");
        }

        Assert.False(File.Exists(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.Runtime",
            "WorkflowContracts.cs")));
    }

    [Fact]
    public void WorkflowRuntimeRegistrationExtensionOwnsRuntimeServiceRegistrations()
    {
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"candoitall-workflow-runtime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            var services = new ServiceCollection();
            services.AddWorkflowRuntimeServices();
            services.AddInMemoryWorkflowRuntimeStores(workspaceRoot);

            using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
            using var scope = provider.CreateScope();
            using var secondScope = provider.CreateScope();

            Assert.IsType<WorkflowRuntimeManager>(
                scope.ServiceProvider.GetRequiredService<WorkflowRuntimeManagerContract>());
            Assert.IsType<InMemoryWorkflowRunStore>(scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>());
            Assert.IsType<FileWorkflowArtifactContentStore>(scope.ServiceProvider.GetRequiredService<IWorkflowArtifactContentStore>());
            Assert.IsType<WorkflowCheckpointFactory>(scope.ServiceProvider.GetRequiredService<IWorkflowCheckpointFactory>());
            Assert.IsType<NullWorkflowEventSink>(scope.ServiceProvider.GetRequiredService<IWorkflowEventSink>());
            Assert.IsType<WorkflowExternalRequestApprovalGate>(scope.ServiceProvider.GetRequiredService<IWorkflowExecutorApprovalGate>());
            Assert.Same(
                scope.ServiceProvider.GetRequiredService<IWorkflowActiveRunRegistry>(),
                secondScope.ServiceProvider.GetRequiredService<IWorkflowActiveRunRegistry>());
            Assert.Same(TimeProvider.System, scope.ServiceProvider.GetRequiredService<TimeProvider>());
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ActiveRunRegistryContractDoesNotExposeConcreteLease()
    {
        var tryRegister = typeof(IWorkflowActiveRunRegistry).GetMethod(nameof(IWorkflowActiveRunRegistry.TryRegister));

        Assert.NotNull(tryRegister);
        var leaseParameter = Assert.Single(
            tryRegister!.GetParameters(),
            parameter => parameter.IsOut);
        Assert.Equal(typeof(IWorkflowActiveRunLease).MakeByRefType(), leaseParameter.ParameterType);
    }

    [Fact]
    public void HostAndModuleRegistrationUseWorkflowRuntimeExtension()
    {
        var root = FindRepositoryRoot();
        var hostingSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Common",
            "CanDoItAll.AgentFramework.Hosting",
            "AgentFrameworkServiceCollectionExtensions.cs"));
        var moduleSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Modules",
            "CanDoItAll.Modules.AgentFramework",
            "Services",
            "AgentFrameworkModuleServiceCollectionExtensions.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "MAF",
            "Workflows",
            "CanDoItAll.AgentFramework.Workflows.MafAdapter",
            "MafWorkflowAdapterServiceCollectionExtensions.cs"));

        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Singleton)", hostingSource, StringComparison.Ordinal);
        Assert.Contains("AddInMemoryWorkflowRuntimeStores", hostingSource, StringComparison.Ordinal);
        Assert.Contains("AddMafWorkflowAdapterServices(ServiceLifetime.Scoped)", moduleSource, StringComparison.Ordinal);
        Assert.Contains("AddFileWorkflowArtifactContentStore", moduleSource, StringComparison.Ordinal);
        Assert.Contains("AddWorkflowRuntimeServices()", adapterSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowRuntimeManager", hostingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddScoped<IWorkflowRuntimeManager", moduleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryAddSingleton<InMemoryWorkflowRunStore>", hostingSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileWorkflowArtifactContentStore", moduleSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeManagerRejectsUnregisteredBackendWithTypedDiagnostics()
    {
        var definition = CreateDefinition();
        var manager = new WorkflowRuntimeManager([], new InMemoryWorkflowRunStore());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.DurableTask,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)));
        var diagnostic = Assert.Single(WorkflowRuntimeFailureDiagnosticMapper.GetDiagnostics(exception));

        Assert.Equal(WorkflowFailureKind.Runtime, diagnostic.Kind);
        Assert.Equal(WorkflowFailureRetryability.RetryableAfterRepair, diagnostic.Retryability);
        Assert.Equal(WorkflowFailureSourceKind.RuntimeBackend, diagnostic.Source.Kind);
        Assert.Equal(WorkflowRuntimeBackendKind.DurableTask, diagnostic.Source.BackendKind);
        Assert.Equal(definition.Id, diagnostic.WorkflowId);
        Assert.Contains("Register", diagnostic.RepairHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RuntimeManagerDoesNotFabricateCancellationForInactiveRun()
    {
        var store = new InMemoryWorkflowRunStore();
        var manager = new WorkflowRuntimeManager([], store);
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(
            WorkflowRunId.New(),
            WorkflowId.New(),
            WorkflowVersionId.New(),
            WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess,
            BackendRunId: "in-process-test",
            Summary: "Running",
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
        await store.SaveRunAsync(run);

        var result = await manager.RequestCancellationAsync(run.RunId);
        var persisted = await manager.GetRunAsync(run.RunId);
        var events = await store.ListEventsAsync(run.RunId);

        Assert.Equal(WorkflowRunCancellationOutcome.NotActive, result.Outcome);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowRunState.Running, persisted.State);
        Assert.Empty(events);
    }

    [Fact]
    public async Task RuntimeManagerPropagatesStoreFailureWithoutInMemoryFallback()
    {
        var definition = CreateDefinition();
        var expected = new InvalidOperationException("runtime store write failed connectionString=[REDACTED]");
        var store = new ThrowingSaveRunStore(expected);
        var manager = new WorkflowRuntimeManager([new CompletedBackend()], store);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAsync(
            definition,
            new WorkflowRunStartRequest(
                definition.Id,
                definition.VersionId,
                "{}",
                WorkflowRuntimeBackendKind.InProcess,
                SourceProcessRunId: null,
                SourceProcessAssignmentId: null)));

        Assert.Same(expected, actual);
    }

    private static WorkflowDefinition CreateDefinition()
    {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Runtime extraction workflow",
            "Runtime extraction test workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(
                start,
                [
                    CreateNode(start, WorkflowNodeKind.Start),
                    CreateNode(end, WorkflowNodeKind.End)
                ],
                [
                    new(
                        new WorkflowEdgeId("start-end"),
                        start,
                        SourcePortId: null,
                        end,
                        TargetPortId: null,
                        WorkflowEdgeKind.Direct,
                        ConditionExpression: string.Empty)
                    {
                        Routing = WorkflowEdgeRouting.Always
                    }
                ]),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private static WorkflowNode CreateNode(
        WorkflowNodeId id,
        WorkflowNodeKind kind)
        => new(
            id,
            kind,
            id.Value,
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowValueShape.Text));

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed class CompletedBackend : IWorkflowExecutionBackend
    {
        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(
            WorkflowRuntimeBackendKind.InProcess,
            "In-process test backend",
            IsDurable: false,
            SupportsStreaming: false,
            SupportsExternalRequests: false,
            SupportsDashboardObservability: false,
            OperationalNotes: "Test backend.");

        public Task<WorkflowBackendStartResult> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(
                runId,
                definition.Id,
                definition.VersionId,
                WorkflowRunState.Completed,
                WorkflowRuntimeBackendKind.InProcess,
                BackendRunId: runId.ToString(),
                Summary: "Completed.",
                CreatedAtUtc: now,
                UpdatedAtUtc: now);

            return Task.FromResult(new WorkflowBackendStartResult(run, [], [], []));
        }
    }

    private sealed class ThrowingSaveRunStore(Exception saveRunException) : IWorkflowRunStore
    {
        public Task CreateRunWithStartedEventAsync(
            WorkflowRunSnapshot run,
            WorkflowEventRecord startedEvent,
            CancellationToken cancellationToken = default)
            => Task.FromException(saveRunException);

        public Task<WorkflowRunTransitionResult> TryTransitionRunAsync(
            WorkflowRunId runId,
            IReadOnlyCollection<WorkflowRunState> expectedStates,
            WorkflowRunSnapshot updatedRun,
            WorkflowEventRecord? transitionEvent = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkflowRunTransitionResult(false, Run: null));

        public Task<WorkflowExternalResponseAcceptanceResult> TryAcceptExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            DateTimeOffset respondedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkflowExternalResponseAcceptanceResult(
                WorkflowExternalResponseAcceptanceOutcome.NotFound,
                Request: null));

        public Task SaveRunAsync(
            WorkflowRunSnapshot run,
            CancellationToken cancellationToken = default)
            => Task.FromException(saveRunException);

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowRunSnapshot?>(null);

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowRunSnapshot>>([]);

        public Task<WorkflowListPage<WorkflowRunSnapshot>> ListRunPageAsync(
            WorkflowRunPageRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkflowListPage<WorkflowRunSnapshot>([], request.PageIndex, request.PageSize, 0));

        public Task SaveEventAsync(
            WorkflowEventRecord workflowEvent,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([]);

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkflowListPage<WorkflowEventRecord>([], request.PageIndex, request.PageSize, 0));

        public Task SaveExternalRequestAsync(
            WorkflowExternalRequestRecord request,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<WorkflowExternalRequestRecord?> GetExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowExternalRequestRecord?>(null);

        public Task<IReadOnlyList<WorkflowExternalRequestRecord>> ListPendingExternalRequestsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowExternalRequestRecord>>([]);

        public Task SaveArtifactAsync(
            WorkflowArtifactRecord artifact,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<WorkflowArtifactRecord>> ListArtifactsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowArtifactRecord>>([]);

        public Task<WorkflowCheckpointRecord> SaveCheckpointAsync(
            WorkflowCheckpointRecord checkpoint,
            CancellationToken cancellationToken = default)
            => Task.FromResult(checkpoint);

        public Task<WorkflowCheckpointRecord?> GetCheckpointAsync(
            WorkflowCheckpointId checkpointId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowCheckpointRecord?>(null);

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowCheckpointRecord>>([]);

        public Task<WorkflowCheckpointRecord> MarkCheckpointResumedAsync(
            WorkflowCheckpointId checkpointId,
            DateTimeOffset resumedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromException<WorkflowCheckpointRecord>(
                new KeyNotFoundException($"Workflow checkpoint '{checkpointId}' was not found."));
    }
}
