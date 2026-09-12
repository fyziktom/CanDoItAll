using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Tests.Support;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessRunFileScopeProviderTests
{
    private static readonly WorkbenchOwnerInMemoryFixture Owner = new("process-file-scopes");
    private static readonly ProcessPreparedLaunch Preparation = ProcessPreparedLaunchFixture.Create(
        ProcessPreparedLaunchFixture.Local(Owner.Admissions.DatabaseProfileId));
    private static readonly Guid RunId = Preparation.InitialCommit.Mutation.State.RunId.Value;

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
        var provider = CreateProvider(stateStore, assignmentStore, catalog);

        ProcessRunFileScopeSet scopeSet = await provider.ResolveAsync(RunId);
        FileToolsSemanticScope productScope = Assert.Single(
            scopeSet.Scopes,
            scope => scope.DisplayName.StartsWith("Product output", StringComparison.Ordinal));
        IReadOnlyList<FileToolsStorageBinding> bindings = await ((IFileToolsStorageBindingSource)provider)
            .ResolveAsync(productScope);
        FileToolsStorageBinding binding = Assert.Single(bindings);

        Assert.Equal(WorkspaceScopeDescriptor.Organization(Owner.Admissions.DatabaseProfileId.ToString("N"))
            .CombineOutputPath("process-runs", RunId.ToString("D"), "calculator"), binding.Root.Value);
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
        var provider = CreateProvider(new StaticStateStore(CreateState()), assignmentStore, catalog);
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
        var provider = CreateProvider(
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
        var provider = CreateProvider(
            new StaticStateStore(CreateState()),
            assignments,
            new RecordingStorageCatalog());

        ProcessRunFileScopeSet scopeSet = await provider.ResolveAsync(RunId);

        Assert.Single(scopeSet.Scopes);
        Assert.Equal("Run artifacts", scopeSet.Scopes[0].DisplayName);

        var missingAssignments = new MutableAssignmentStore([]);
        var missingProvider = CreateProvider(
            new StaticStateStore(state: null),
            missingAssignments,
            new RecordingStorageCatalog());
        FileBrowserProviderException exception = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            missingProvider.ResolveAsync(RunId).AsTask());

        Assert.Equal(FileBrowserErrorCode.NotFound, exception.Error.Code);
        Assert.Equal(0, missingAssignments.LoadCalls);
    }

    [Fact]
    public async Task Provider_refuses_old_unbound_scope_keys_before_catalog_access() {
        var catalog = new RecordingStorageCatalog();
        var provider = CreateProvider(new StaticStateStore(CreateState()), new MutableAssignmentStore([]), catalog);
        var current = Assert.Single((await provider.ResolveAsync(RunId)).Scopes);
        Assert.Contains("run:v2:", current.Id.Value, StringComparison.Ordinal);
        var legacy = new FileToolsSemanticScope(FileToolsSemanticScopeKind.ProcessRun,
            new(current.Id.Value.Replace("run:v2:", "run:v1:", StringComparison.Ordinal)), current.DisplayName);
        var error = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            ((IFileToolsStorageBindingSource)provider).ResolveAsync(legacy).AsTask());
        Assert.Equal(FileBrowserErrorCode.InvalidOperation, error.Error.Code);
        Assert.Equal(0, catalog.EnsureCalls);
    }

    [Fact]
    public async Task Provider_binds_cached_scope_to_original_preparation_as_well_as_run_and_root() {
        var catalog = new RecordingStorageCatalog();
        var prepared = new PreparedStore();
        var provider = new ProcessRunFileScopeProvider(new StaticStateStore(CreateState()), new MutableAssignmentStore([]),
            catalog, prepared, Owner.Admissions);
        var current = Assert.Single((await provider.ResolveAsync(RunId)).Scopes);
        prepared.Fingerprint = "sha256:different-original-source";
        var error = await Assert.ThrowsAsync<FileBrowserProviderException>(() =>
            ((IFileToolsStorageBindingSource)provider).ResolveAsync(current).AsTask());
        Assert.Equal(FileBrowserErrorCode.Conflict, error.Error.Code);
        Assert.Equal(0, catalog.EnsureCalls);
    }

    private static ProcessRuntimeStateSnapshot CreateState()
        => Preparation.InitialCommit.Mutation.State with { Status = ProcessRuntimeStatus.Completed };

    private static ProcessRunFileScopeProvider CreateProvider(IProcessRuntimeStateStore states,
        IProcessRuntimeStepAssignmentStore assignments, IStorageCatalogService catalog)
        => new(states, assignments, catalog, new PreparedStore(), Owner.Admissions);

    private sealed class PreparedStore : IProcessPreparedLaunchStore {
        public string Fingerprint { get; set; } = "sha256:retained-owner-test";
        public Task<ProcessPreparedLaunchSnapshot?> GetAsync(ProcessLaunchAdmissionId admissionId,
            CancellationToken cancellationToken = default) => Task.FromResult<ProcessPreparedLaunchSnapshot?>(new(
                Preparation, Fingerprint, 1, ProcessLaunchContinuationState.Started,
                ProcessProjectAdmissionFixture.Now, true, ProcessLaunchLinkDeliveryState.NotRequested, null, null));
        public Task<ProcessPreparedLaunchSnapshot?> FindByIntentAsync(ProcessLaunchIntentId intentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessPreparedLaunchSnapshot?> FindByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessPreparedLaunchSnapshot> PrepareAsync(ProcessPreparedLaunch preparation, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<ProcessLaunchContinuationClaim?> ClaimContinuationAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> RenewContinuationAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task CompleteContinuationAsync(ProcessLaunchContinuationClaim claim, ProcessLaunchContinuationState state, string? publicFailure,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessLaunchAdmissionId>> ListPendingContinuationsAsync(int take, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

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

        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureCalls++;
            return Task.FromResult(storage);
        }

        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);

        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<StorageCatalogRecord?>(id == storage.Id ? storage : null);

        private Task<StorageCatalogRecord> SaveRecordAsync(
            StorageCatalogRecord record,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);

        private Task<StorageRoutingRule> SaveRoutingRecordAsync(
            StorageRoutingRule rule,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public async Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            (await ReadRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToSnapshot();

        public async Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToDriverInput();

        public async Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            var row = await ReadRecordAsync(id, cancellationToken);
            return row is null ? null : new(row.ToSnapshot(), StorageJson.ParseProviderConfiguration(row.ConfigJson));
        }

        public async Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
            (await ReadBootstrapRecordAsync(cancellationToken)).ToDriverInput();

        public async Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public async Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
            (await ReadRoutingRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRoutingRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
}
