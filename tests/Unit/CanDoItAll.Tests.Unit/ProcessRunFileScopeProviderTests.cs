using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRunFileScopeProviderTests
{
    private static readonly Guid RunId = Guid.Parse("1344fb1f-e6d9-4074-84c0-4c7b7d18a084");

    [Fact]
    public async Task Provider_resolves_current_artifact_and_product_roots_with_disabled_host_cache()
    {
        var stateStore = new StaticStateStore(CreateState());
        var assignmentStore = new MutableAssignmentStore(
            [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ManagedArtifactRoot"] = $"artifacts/process-runs/{RunId:D}",
                ["ProductRoot"] = $"output/process-runs/{RunId:D}/calculator"
            })]);
        var catalog = new RecordingStorageCatalog();
        var provider = new ProcessRunFileScopeProvider(stateStore, assignmentStore, catalog);

        ProcessRunFileScopeSet scopeSet = await provider.ResolveAsync(RunId);
        FileToolsSemanticScope productScope = Assert.Single(
            scopeSet.Scopes,
            scope => scope.DisplayName.StartsWith("Product output", StringComparison.Ordinal));
        IReadOnlyList<FileToolsStorageBinding> bindings = await ((IFileToolsStorageBindingSource)provider)
            .ResolveAsync(productScope);
        FileToolsStorageBinding binding = Assert.Single(bindings);

        Assert.Equal($"output/process-runs/{RunId:D}/calculator", binding.Root.Value);
        Assert.Equal(FileToolsHostBrowseCacheMode.Disabled, binding.HostCacheMode);
        Assert.Equal(2, scopeSet.Scopes.Count);
        Assert.Equal(1, catalog.EnsureCalls);
    }

    [Fact]
    public async Task Provider_re_resolves_current_launch_data_and_rejects_a_stale_root_before_catalog_access()
    {
        var assignmentStore = new MutableAssignmentStore(
            [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProductRoot"] = $"output/process-runs/{RunId:D}/calculator"
            })]);
        var catalog = new RecordingStorageCatalog();
        var provider = new ProcessRunFileScopeProvider(new StaticStateStore(CreateState()), assignmentStore, catalog);
        ProcessRunFileScopeSet initial = await provider.ResolveAsync(RunId);
        FileToolsSemanticScope staleScope = Assert.Single(
            initial.Scopes,
            scope => scope.DisplayName.StartsWith("Product output", StringComparison.Ordinal));
        assignmentStore.Assignments = [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal))];

        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            ((IFileToolsStorageBindingSource)provider).ResolveAsync(staleScope).AsTask());

        Assert.Equal(FileBrowserErrorCode.Conflict, exception.Error.Code);
        Assert.Equal(0, catalog.EnsureCalls);
        Assert.Equal(2, assignmentStore.LoadCalls);
    }

    [Fact]
    public async Task Provider_observes_a_new_product_root_on_the_next_scope_resolution()
    {
        var assignmentStore = new MutableAssignmentStore(
            [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal))]);
        var provider = new ProcessRunFileScopeProvider(
            new StaticStateStore(CreateState()),
            assignmentStore,
            new RecordingStorageCatalog());
        ProcessRunFileScopeSet initial = await provider.ResolveAsync(RunId);
        assignmentStore.Assignments = [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ProductRoot"] = $"output/process-runs/{RunId:D}/calculator"
        })];

        ProcessRunFileScopeSet refreshed = await provider.ResolveAsync(RunId);

        Assert.Single(initial.Scopes);
        Assert.Equal(2, refreshed.Scopes.Count);
        Assert.NotEqual(initial.Fingerprint, refreshed.Fingerprint);
    }

    [Fact]
    public async Task Provider_ignores_absolute_external_target_and_fails_missing_run_before_assignments()
    {
        var assignments = new MutableAssignmentStore(
            [CreateAssignment(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ExternalTargetRoot"] = @"C:\products\outside"
            })]);
        var provider = new ProcessRunFileScopeProvider(
            new StaticStateStore(CreateState()),
            assignments,
            new RecordingStorageCatalog());

        ProcessRunFileScopeSet scopeSet = await provider.ResolveAsync(RunId);

        Assert.Single(scopeSet.Scopes);
        Assert.Equal("Run artifacts", scopeSet.Scopes[0].DisplayName);

        var missingAssignments = new MutableAssignmentStore([]);
        var missingProvider = new ProcessRunFileScopeProvider(
            new StaticStateStore(state: null),
            missingAssignments,
            new RecordingStorageCatalog());
        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            missingProvider.ResolveAsync(RunId).AsTask());

        Assert.Equal(FileBrowserErrorCode.NotFound, exception.Error.Code);
        Assert.Equal(0, missingAssignments.LoadCalls);
    }

    private static ProcessRuntimeStateSnapshot CreateState()
        => new(
            new ProcessRunId(RunId),
            new ProcessRunId(RunId),
            ProcessInstancePlanId.New(),
            "plan-hash",
            ProcessRuntimeStatus.Active,
            [],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);

    private static ProcessRuntimeStepAssignment CreateAssignment(IReadOnlyDictionary<string, string> variables)
        => new(
            new ProcessRunId(RunId),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "step",
            "role",
            "role-resource",
            "Role",
            "executor",
            "executor-id",
            "Executor",
            "Prompt",
            "readiness",
            "test",
            [],
            [],
            [],
            "run",
            variables,
            BranchGate: null,
            DateTimeOffset.UtcNow);

    private sealed class StaticStateStore(ProcessRuntimeStateSnapshot? state) : IProcessRuntimeStateStore
    {
        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(state);
    }

    private sealed class MutableAssignmentStore(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments) : IProcessRuntimeStepAssignmentStore
    {
        public IReadOnlyList<ProcessRuntimeStepAssignment> Assignments { get; set; } = assignments;

        public int LoadCalls { get; private set; }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return ValueTask.FromResult(Assignments);
        }

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingStorageCatalog : IStorageCatalogService
    {
        private readonly StorageCatalogRecord storage = new()
        {
            Id = Guid.NewGuid(),
            Name = "Managed files",
            ProviderKind = StorageProviderKind.FileSystem,
            EndpointOrRoot = "workspace"
        };

        public int EnsureCalls { get; private set; }

        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureCalls++;
            return Task.FromResult(storage);
        }

        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(id == storage.Id ? storage : null);

        public Task<StorageCatalogRecord> SaveAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        public Task<StorageRoutingRule> SaveRuleAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
