using System.Collections.Concurrent;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.SharedKernel.Streaming;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Unit;

[Trait("Category", "UnixRuntimePortability")]
public sealed class AgentFrameworkWorkspaceProcessLeaseCleanupTests
{
    [Fact]
    public async Task Persisted_completed_execution_cleans_process_leases_once()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        using var context = await CreateContextAsync(
            new StubAgentRuntime(CreateCompletedResponse),
            cleaner);

        var result = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(result.ExecutionRunId));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(ExecutionState.Completed, persistedRun.State);
        Assert.Equal(
            result.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    [Fact]
    public async Task Persisted_failed_execution_cleans_process_leases_once()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        var runtimeFailure = new InvalidOperationException(
            "The runtime failed before producing a response.");
        using var context = await CreateContextAsync(
            new StubAgentRuntime(() => throw runtimeFailure),
            cleaner);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(
            () => context.Service.ExecuteRunAsync(
                CreateRequest(context.Agent.Id)));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(exception.ExecutionRunId));

        Assert.Same(runtimeFailure, exception.InnerException);
        Assert.Equal(ExecutionState.Failed, persistedRun.State);
        Assert.Equal(
            exception.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    [Fact]
    public async Task Waiting_on_tool_execution_and_nonterminal_continuation_do_not_clean_process_leases()
    {
        var cleaner = new RecordingProcessLeaseCleaner();
        var runtime = new StubAgentRuntime(
            () => CreateWaitingResponse("approval-initial"),
            () => CreateWaitingResponse("approval-continuation"));
        using var context = await CreateContextAsync(runtime, cleaner);

        var initialResult = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var continuationResult =
            await context.Service.ContinueExecutionRunAsync(
                initialResult.ExecutionRunId,
                AgentExecutionOperationId.New(),
                decisions: [new PendingToolApprovalDecision("approval-initial", Approved: true)]);
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(
                continuationResult.ExecutionRunId));

        Assert.Equal(ExecutionState.WaitingOnTool, initialResult.State);
        Assert.Equal(ExecutionState.WaitingOnTool, continuationResult.State);
        Assert.Equal(ExecutionState.WaitingOnTool, persistedRun.State);
        Assert.Equal(1, runtime.ContinuationCallCount);
        Assert.Empty(cleaner.ExecutionRunIds);
    }

    [Fact]
    public async Task Cleanup_failure_does_not_mask_persisted_completed_execution_outcome()
    {
        var cleanupFailure = new InvalidOperationException(
            "The process lease cleaner failed.");
        var cleaner = new RecordingProcessLeaseCleaner(
            _ => Task.FromException<WorkspaceExecutionRunProcessCleanupResult>(
                cleanupFailure));
        using var context = await CreateContextAsync(
            new StubAgentRuntime(CreateCompletedResponse),
            cleaner);

        var result = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var persistedRun = Assert.IsType<ExecutionRunRecord>(
            await context.Store.GetExecutionRunAsync(result.ExecutionRunId));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.Equal(ExecutionState.Completed, persistedRun.State);
        Assert.Equal(
            result.ExecutionRunId,
            Assert.Single(cleaner.ExecutionRunIds));
    }

    [Fact]
    public async Task Organization_execution_cleans_a_real_project_scoped_process_lease()
    {
        var organizationScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        LeaseProcessHost? processHost = null;
        WorkspaceCommandExecutionService? projectCommands = null;
        string? startupReceiptPath = null;
        var runtime = new StubAgentRuntime(() =>
        {
            var launch = projectCommands!.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:5128/",
                    keepAlive: true)
                .GetAwaiter()
                .GetResult();
            Assert.True(launch.Succeeded, launch.Message);
            startupReceiptPath = Assert.Single(
                launch.Receipt.TargetPaths,
                path => path.EndsWith("/startup.json", StringComparison.OrdinalIgnoreCase));
            return CreateCompletedResponse();
        });
        using var context = await CreateContextAsync(
            runtime,
            organizationScope,
            (workspaceRoot, store) =>
            {
                processHost = new LeaseProcessHost(workspaceRoot);
                projectCommands = TestWorkspaceServices.CreateCommandExecutionService(
                    workspaceRoot,
                    processHost,
                    projectScope);
                return new WorkspaceExecutionRunProcessLeaseCleaner(
                    store,
                    new WorkspaceExecutionScope(workspaceRoot, organizationScope),
                    new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                        () => processHost));
            });
        await CreateSampleWebProjectAsync(context.Workspace.Path);
        var result = await context.Service.ExecuteRunAsync(
            CreateScopedRequest(context, projectScope));

        Assert.Equal(ExecutionState.Completed, result.State);
        Assert.False(string.IsNullOrWhiteSpace(startupReceiptPath));
        var projectLeaseStore = new WorkspaceExecutionRunProcessLeaseStore(
            context.Workspace.Path,
            projectScope);
        Assert.False(projectLeaseStore.HasLease(result.ExecutionRunId, startupReceiptPath!));
        Assert.Contains(processHost!.Requests, request => request.ToolName == "workspace_dotnet_stop");
        Assert.True(processHost.IsDisposed);
    }

    [Fact]
    public async Task Approval_continuation_cleans_a_real_project_scoped_process_lease()
    {
        var organizationScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        LeaseProcessHost? processHost = null;
        WorkspaceCommandExecutionService? projectCommands = null;
        string? startupReceiptPath = null;
        var runtime = new StubAgentRuntime(
            () =>
            {
                var launch = projectCommands!.DotnetRun(
                        "apps/SampleWeb/SampleWeb.csproj",
                        url: "http://127.0.0.1:5129/",
                        keepAlive: true)
                    .GetAwaiter()
                    .GetResult();
                Assert.True(launch.Succeeded, launch.Message);
                startupReceiptPath = Assert.Single(
                    launch.Receipt.TargetPaths,
                    path => path.EndsWith("/startup.json", StringComparison.OrdinalIgnoreCase));
                return CreateWaitingResponse("approval-project-lease");
            },
            CreateCompletedResponse);
        using var context = await CreateContextAsync(
            runtime,
            organizationScope,
            (workspaceRoot, store) =>
            {
                processHost = new LeaseProcessHost(workspaceRoot);
                projectCommands = TestWorkspaceServices.CreateCommandExecutionService(
                    workspaceRoot,
                    processHost,
                    projectScope);
                return new WorkspaceExecutionRunProcessLeaseCleaner(
                    store,
                    new WorkspaceExecutionScope(workspaceRoot, organizationScope),
                    new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                        () => processHost));
            });
        await CreateSampleWebProjectAsync(context.Workspace.Path);

        var initial = await context.Service.ExecuteRunAsync(
            CreateScopedRequest(context, projectScope));
        var result = await context.Service.ContinueExecutionRunAsync(
            initial.ExecutionRunId,
            AgentExecutionOperationId.New(),
            decisions:
            [
                new PendingToolApprovalDecision(
                    "approval-project-lease",
                    Approved: true)
            ]);

        Assert.Equal(ExecutionState.WaitingOnTool, initial.State);
        Assert.Equal(ExecutionState.Completed, result.State);
        var leaseStore = new WorkspaceExecutionRunProcessLeaseStore(
            context.Workspace.Path,
            projectScope);
        Assert.False(leaseStore.HasLease(result.ExecutionRunId, startupReceiptPath!));
        Assert.Single(
            processHost!.Requests,
            request => request.ToolName == "workspace_dotnet_stop");
        Assert.True(processHost.IsDisposed);
    }

    [Fact]
    public async Task Failed_terminal_execution_cleans_a_real_project_scoped_process_lease()
    {
        var organizationScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var projectScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        LeaseProcessHost? processHost = null;
        WorkspaceCommandExecutionService? projectCommands = null;
        string? startupReceiptPath = null;
        var runtimeFailure = new InvalidOperationException("Fail after launching the project process.");
        var runtime = new StubAgentRuntime(() =>
        {
            var launch = projectCommands!.DotnetRun(
                    "apps/SampleWeb/SampleWeb.csproj",
                    url: "http://127.0.0.1:5130/",
                    keepAlive: true)
                .GetAwaiter()
                .GetResult();
            Assert.True(launch.Succeeded, launch.Message);
            startupReceiptPath = Assert.Single(
                launch.Receipt.TargetPaths,
                path => path.EndsWith("/startup.json", StringComparison.OrdinalIgnoreCase));
            throw runtimeFailure;
        });
        using var context = await CreateContextAsync(
            runtime,
            organizationScope,
            (workspaceRoot, store) =>
            {
                processHost = new LeaseProcessHost(workspaceRoot);
                projectCommands = TestWorkspaceServices.CreateCommandExecutionService(
                    workspaceRoot,
                    processHost,
                    projectScope);
                return new WorkspaceExecutionRunProcessLeaseCleaner(
                    store,
                    new WorkspaceExecutionScope(workspaceRoot, organizationScope),
                    new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                        () => processHost));
            });
        await CreateSampleWebProjectAsync(context.Workspace.Path);

        var exception = await Assert.ThrowsAsync<AgentRunFailedException>(
            () => context.Service.ExecuteRunAsync(
                CreateScopedRequest(context, projectScope)));

        Assert.Same(runtimeFailure, exception.InnerException);
        var leaseStore = new WorkspaceExecutionRunProcessLeaseStore(
            context.Workspace.Path,
            projectScope);
        Assert.False(leaseStore.HasLease(exception.ExecutionRunId, startupReceiptPath!));
        Assert.Single(
            processHost!.Requests,
            request => request.ToolName == "workspace_dotnet_stop");
        Assert.True(processHost.IsDisposed);
    }

    [Fact]
    public async Task Conflicting_metadata_and_governance_scope_retains_the_lease_for_retry()
    {
        var organizationScope = WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N"));
        var metadataScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var governanceScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        using var context = await CreateContextAsync(
            new StubAgentRuntime(CreateCompletedResponse),
            new RecordingProcessLeaseCleaner());
        var completed = await context.Service.ExecuteRunAsync(
            CreateRequest(context.Agent.Id));
        var detail = Assert.IsType<ExecutionRunDetail>(
            await context.Store.GetExecutionRunDetailAsync(completed.ExecutionRunId));
        var conflictingRun = detail.Run with
        {
            MetadataJson = CreateGovernedMetadata(
                context,
                metadataScope,
                governanceScope),
            Revision = detail.Run.Revision + 1
        };
        await context.Store.SaveExecutionRunDetailAsync(
            detail with
            {
                Run = conflictingRun
            });
        var startupReceiptPath =
            "artifacts/process-runs/dotnet-run/scope-conflict/startup.json";
        var leaseStore = new WorkspaceExecutionRunProcessLeaseStore(
            context.Workspace.Path,
            metadataScope);
        leaseStore.Register(completed.ExecutionRunId, startupReceiptPath);
        var cleanupFactoryCallCount = 0;
        var cleaner = new WorkspaceExecutionRunProcessLeaseCleaner(
            context.Store,
            new WorkspaceExecutionScope(
                context.Workspace.Path,
                organizationScope),
            new TestWorkspaceExecutionRunProcessLeaseCleanupScopeFactory(
                () =>
                {
                    cleanupFactoryCallCount++;
                    return new LeaseProcessHost(context.Workspace.Path);
                }));

        var result = await cleaner.CleanupAsync(completed.ExecutionRunId);

        Assert.Empty(result.CleanedStartupReceiptPaths);
        var failure = Assert.Single(result.Failures);
        Assert.Contains("conflicting workspace scope", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, cleanupFactoryCallCount);
        Assert.True(leaseStore.HasLease(completed.ExecutionRunId, startupReceiptPath));
    }

    private static ExecutionRunRequest CreateRequest(Guid agentId)
    {
        return new(
            agentId,
            "Return the terminal process lease test response.",
            AgentExecutionOperationId.New());
    }

    private static ExecutionRunRequest CreateScopedRequest(
        TestContext context,
        WorkspaceScopeDescriptor scope)
    {
        var transientContext = new AgentRuntimeTransientContext(
            "Trusted project context for process lease cleanup.",
            scope);
        var metadata = CreateGovernedMetadata(context, scope, scope);
        return new ExecutionRunRequest(
            context.Agent.Id,
            "Return the terminal process lease test response.",
            AgentExecutionOperationId.New(),
            Context: new ExecutionInvocationContext(
                SourceKind: "project-structure",
                SourceId: scope.Key,
                CorrelationId: Guid.NewGuid().ToString("N"),
                CausationId: string.Empty,
                RequestedBy: "unit-test",
                RequestedByKind: "interactive",
                MetadataJson: metadata))
        {
            TransientContext = transientContext
        };
    }

    private static string CreateGovernedMetadata(
        TestContext context,
        WorkspaceScopeDescriptor metadataScope,
        WorkspaceScopeDescriptor governanceScope)
    {
        var authority = new AgentExecutionAuthorityRecord(
            AgentExecutionAuthorityId.Create(),
            context.Agent.Id,
            context.WorkspaceIdentity.DatabaseProfileId,
            context.WorkspaceIdentity.DatabaseProfileGeneration,
            governanceScope,
            readAllowed: true,
            mutationAllowed: true,
            policyVersion: "process-lease-cleanup-v1",
            policyFingerprint: "process-lease-cleanup-policy",
            resolvedAtUtc: DateTimeOffset.UtcNow);
        var turnReference = new AgentTurnContextReference(
            AgentTurnContextId.Create(),
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(metadataScope.Key),
            surface: "project-structure",
            view: "hierarchy",
            observationVersion: 1,
            modelContextDigest: "process-lease-cleanup-context",
            capturedAtUtc: DateTimeOffset.UtcNow);
        return AgentTurnContextMetadata.Apply(
            ExecutionInvocationMetadata.ApplyContextWorkspaceScope(
                "{}",
                metadataScope),
            turnReference,
            authority);
    }

    private static async Task CreateSampleWebProjectAsync(string workspaceRoot)
    {
        var projectDirectory = Path.Combine(workspaceRoot, "apps", "SampleWeb");
        Directory.CreateDirectory(projectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(projectDirectory, "SampleWeb.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
    }

    private static AgentRuntimeResponse CreateCompletedResponse()
    {
        return new(
            "The execution completed.",
            InputTokens: 8,
            OutputTokens: 4,
            ToolCalls: 0,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static AgentRuntimeResponse CreateWaitingResponse(
        string approvalId)
    {
        return new(
            "Approval is required.",
            InputTokens: 8,
            OutputTokens: 4,
            ToolCalls: 1,
            RuntimeSessionKey: "runtime-session",
            SerializedSessionStateJson: "{}",
            PendingApprovals:
            [
                new PendingToolApprovalRecord(
                    approvalId,
                    $"call-{approvalId}",
                    "workspace_test_operation",
                    "function",
                    "Approve the unit-test operation.",
                    "{}")
            ]);
    }

    private static async Task<TestContext> CreateContextAsync(
        IFakeAgentRuntime runtime,
        IWorkspaceExecutionRunProcessLeaseCleaner cleaner)
        => await CreateContextAsync(
            runtime,
            WorkspaceScopeDescriptor.Project($"process-lease-cleanup-{Guid.NewGuid():N}"),
            (_, _) => cleaner);

    private static async Task<TestContext> CreateContextAsync(
        IFakeAgentRuntime runtime,
        WorkspaceScopeDescriptor workspaceScope,
        Func<string, FileSandboxWorkspaceStore, IWorkspaceExecutionRunProcessLeaseCleaner> createCleaner)
    {
        var workspace = new TemporaryWorkspace();
        var workspaceIdentity = new AgentExecutionActivityWorkspaceIdentity(
            Guid.NewGuid(),
            workspaceScope,
            new DatabaseProfileGeneration(0));
        var store = new FileSandboxWorkspaceStore(
            workspace.Path,
            workspaceScope);
        var provider = CreateProvider();
        var agent = CreateAgent(provider.Id);
        await store.SaveCatalogAsync(
            new SandboxWorkspaceCatalog(
                Version: "1.0",
                Agents: [agent],
                Providers: [provider],
                Capabilities: [],
                Memory: []));
        var preparationCache = new AgentExecutionPreparationCache(
            AgentExecutionPreparationCachePolicy.Default);
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the fake runtime for all four ports.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime);
        var cleaner = createCleaner(workspace.Path, store);
        var service = new AgentFrameworkWorkspaceService(
            store,
            CreateUnexpectedDependency<IAgentPackageService>(),
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            CreateUnexpectedDependency<ICapabilityProofService>(),
            NullLogger<AgentFrameworkWorkspaceService>.Instance,
            CreateActivityCoordinator(),
            workspaceIdentity,
            preparationCache,
            new FixedAgentExecutionProfileGenerationSource(default),
            cleaner,
            new ExternalTargetPathRegistryFactory(),
            providerCredentialResolver:
                FixedAgentProviderCredentialResolver.Instance);

        return new(
            workspace,
            store,
            agent,
            workspaceIdentity,
            preparationCache,
            service);
    }

    private static AgentExecutionActivityCoordinator
        CreateActivityCoordinator()
    {
        return new(
            new PartitionedSequencedStream<
                AgentExecutionActivityStreamId,
                AgentExecutionActivity>(
                PartitionedSequencedStreamPolicy.Default,
                TimeProvider.System),
            TimeProvider.System);
    }

    private static ProviderProfile CreateProvider()
    {
        return new(
            Guid.NewGuid(),
            "Process lease cleanup test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "CANDOITALL_PROCESS_LEASE_CLEANUP_TEST_API_KEY",
            "test-model",
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: ["test-model"]);
    }

    private static AgentDefinition CreateAgent(Guid providerProfileId)
    {
        var now = DateTimeOffset.UtcNow;
        return new(
            Guid.NewGuid(),
            "Process lease cleanup test agent",
            "Test agent",
            "Exercises terminal process lease cleanup.",
            "Return a concise response.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static T CreateUnexpectedDependency<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnexpectedCallProxy>();
    }

    private sealed record TestContext(
        TemporaryWorkspace Workspace,
        FileSandboxWorkspaceStore Store,
        AgentDefinition Agent,
        AgentExecutionActivityWorkspaceIdentity WorkspaceIdentity,
        AgentExecutionPreparationCache PreparationCache,
        AgentFrameworkWorkspaceService Service) : IDisposable
    {
        public void Dispose()
        {
            Service.Dispose();
            PreparationCache.Dispose();
            Workspace.Dispose();
        }
    }

    private sealed class RecordingProcessLeaseCleaner :
        IWorkspaceExecutionRunProcessLeaseCleaner
    {
        private readonly ConcurrentQueue<Guid> executionRunIds = new();
        private readonly Func<
            Guid,
            Task<WorkspaceExecutionRunProcessCleanupResult>> cleanup;

        public RecordingProcessLeaseCleaner(
            Func<
                Guid,
                Task<WorkspaceExecutionRunProcessCleanupResult>>? cleanup = null)
        {
            this.cleanup = cleanup
                ?? (executionRunId => Task.FromResult(
                    WorkspaceExecutionRunProcessCleanupResult.Empty(
                        executionRunId)));
        }

        public IReadOnlyList<Guid> ExecutionRunIds =>
            executionRunIds.ToArray();

        public Task<WorkspaceExecutionRunProcessCleanupResult> CleanupAsync(
            Guid executionRunId)
        {
            executionRunIds.Enqueue(executionRunId);
            return cleanup(executionRunId);
        }
    }

    private sealed class LeaseProcessHost(string workspaceRoot) :
        IWorkspaceLongRunningProcessHost,
        IAsyncDisposable
    {
        private readonly ConcurrentQueue<WorkspaceProcessExecutionRequest> requests = new();

        public IReadOnlyList<WorkspaceProcessExecutionRequest> Requests => requests.ToArray();

        public string StartupReceiptPath { get; private set; } = string.Empty;

        public bool IsDisposed { get; private set; }

        public ExecutionBoundaryDescriptor DescribeBoundary()
            => new(
                Mode: "Test",
                FilesystemScope: "Workspace",
                NetworkScope: "None",
                CredentialScope: "None",
                HostLabel: "Lease topology test",
                IsEnforcedByHost: false,
                Notes: "Deterministic in-process process host.");

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(
            WorkspaceProcessExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            requests.Enqueue(request);
            if (request.ToolName == "workspace_dotnet_run")
            {
                var fileIndex = request.Arguments.ToList().IndexOf("-File");
                Assert.True(fileIndex >= 0 && fileIndex + 1 < request.Arguments.Count);
                var scriptDirectory = Path.GetDirectoryName(request.Arguments[fileIndex + 1]);
                Assert.False(string.IsNullOrWhiteSpace(scriptDirectory));
                var receiptPath = Path.Combine(scriptDirectory!, "startup.json");
                File.WriteAllText(
                    receiptPath,
                    """{"succeeded":true,"appProcessTreeIds":[12345]}""");
                StartupReceiptPath = Path.GetRelativePath(workspaceRoot, receiptPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
            }

            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(
                Started: true,
                ExitCode: 0,
                Stdout: "ok",
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                StartedAtUtc: now,
                CompletedAtUtc: now,
                TimedOut: false,
                Boundary: DescribeBoundary(),
                FailureMessage: string.Empty));
        }

        public Task<IWorkspaceProcessSession> StartSessionAsync(
            WorkspaceProcessSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            requests.Enqueue(new WorkspaceProcessExecutionRequest(
                request.ToolName,
                request.RecipeId,
                request.ExecutablePath,
                request.Arguments,
                request.WorkingDirectory,
                request.EnvironmentVariables,
                TimeoutSeconds: 0,
                StdoutLimitCharacters: request.StdoutLimitCharacters,
                StderrLimitCharacters: request.StderrLimitCharacters,
                StandardInput: request.StandardInput));
            return Task.FromResult<IWorkspaceProcessSession>(new LeaseProcessSession(DescribeBoundary()));
        }

        public Task<WorkspaceProcessTerminationResult> TerminateOwnedProcessAsync(
            WorkspaceOwnedProcessIdentity identity,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            requests.Enqueue(new WorkspaceProcessExecutionRequest(
                "workspace_dotnet_stop",
                "dotnet_stop",
                "dotnet",
                [],
                workspaceRoot,
                new Dictionary<string, string?>(),
                TimeoutSeconds: 0,
                StdoutLimitCharacters: 0,
                StderrLimitCharacters: 0));
            return Task.FromResult(new WorkspaceProcessTerminationResult(
                WorkspaceProcessTerminationStatus.Terminated,
                ResidualProcessPossible: false,
                "Owned process terminated."));
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }

        private sealed class LeaseProcessSession(ExecutionBoundaryDescriptor boundary) : IWorkspaceProcessSession
        {
            private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;

            public WorkspaceOwnedProcessIdentity Identity { get; } = new(
                12345,
                DateTimeOffset.UtcNow,
                new string('a', 64),
                new WorkspaceOwnedProcessBoundary(
                    WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup,
                    12345,
                    Guid.Empty));

            public bool HasExited => false;

            public WorkspaceProcessOutputSnapshot CaptureOutput()
                => new(
                    "Now listening on: http://127.0.0.1",
                    string.Empty,
                    StdoutTruncated: false,
                    StderrTruncated: false);

            public Task<WorkspaceProcessExecutionResult> WaitForExitAsync(
                CancellationToken cancellationToken = default)
                => Task.FromResult(CreateResult(WorkspaceProcessTerminationReason.Completed));

            public Task<WorkspaceProcessExecutionResult> TerminateAsync(
                WorkspaceProcessTerminationReason reason,
                string failureMessage,
                CancellationToken cancellationToken = default)
                => Task.FromResult(CreateResult(reason, failureMessage));

            public WorkspaceOwnedProcessIdentity Detach()
                => Identity;

            public ValueTask DisposeAsync()
                => ValueTask.CompletedTask;

            private WorkspaceProcessExecutionResult CreateResult(
                WorkspaceProcessTerminationReason reason,
                string failureMessage = "")
            {
                var output = CaptureOutput();
                return new WorkspaceProcessExecutionResult(
                    Started: true,
                    ExitCode: 0,
                    output.Stdout,
                    output.Stderr,
                    output.StdoutTruncated,
                    output.StderrTruncated,
                    startedAtUtc,
                    DateTimeOffset.UtcNow,
                    TimedOut: false,
                    boundary,
                    failureMessage,
                    reason,
                    ResidualProcessPossible: false);
            }
        }
    }

    private sealed class StubAgentRuntime : IFakeAgentRuntime
    {
        private readonly Func<AgentRuntimeResponse> run;
        private readonly Func<AgentRuntimeResponse>? continuation;
        private int continuationCallCount;

        public StubAgentRuntime(
            Func<AgentRuntimeResponse> run,
            Func<AgentRuntimeResponse>? continuation = null)
        {
            this.run = run;
            this.continuation = continuation;
        }

        public int ContinuationCallCount =>
            Volatile.Read(ref continuationCallCount);

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(run());
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref continuationCallCount);
            var response = continuation
                ?? throw new InvalidOperationException(
                    "The test did not configure an approval continuation.");
            return Task.FromResult(response());
        }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ProviderModelMaintenanceEditorResult>
            CreateOrUpdateProviderModelAsync(
                ProviderProfile provider,
                ProviderModelMaintenanceEditorRequest request,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedAgentProviderCredentialResolver :
        IAgentProviderCredentialResolver
    {
        public static FixedAgentProviderCredentialResolver Instance { get; } =
            new();

        private FixedAgentProviderCredentialResolver()
        {
        }

        public ProviderCredentialResolution Resolve(ProviderProfile provider)
        {
            return new(
                "unit-test-api-key",
                "unit-test credential resolver",
                string.Empty);
        }
    }

    private class UnexpectedCallProxy : DispatchProxy
    {
        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            throw new InvalidOperationException(
                $"Dependency member '{targetMethod?.Name}' was not expected.");
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        public TemporaryWorkspace()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"candoitall-process-lease-cleanup-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
