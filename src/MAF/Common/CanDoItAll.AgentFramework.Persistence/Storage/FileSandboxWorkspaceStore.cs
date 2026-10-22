using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore :
    ISandboxWorkspaceStore,
    IIndexedWorkspaceReadGuard,
    IBoundedAgentProviderUsageEvidenceStore,
    ISandboxWorkspaceChatQueryStore,
    ISandboxWorkspaceChatProjectionQueryStore,
    ISandboxWorkspaceChatSessionStore,
    ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceProcessRunPageStore,
    ISandboxWorkspaceProcessRunIndexPreparation,
    ISandboxWorkspaceChatRunStartStore,
    ISandboxWorkspaceExecutionRunMutationStore,
    ISandboxWorkspaceExecutionRunLeaseStore,
    ISandboxWorkspaceExecutionRunReservationStore,
    IAgentRecruitingEvidenceStore
{
    private const string ChatBackedRunCommitJournalVersion = "1.0";
    private const string GenericNewRunCommitJournalVersion = "1.0";
    private const string ExistingRunDetailCommitJournalVersion = "1.0";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileSandboxWorkspaceJsonStore jsonStore;
    private readonly FileSandboxWorkspaceCatalogNormalizer catalogNormalizer;
    private readonly FileSandboxWorkspaceExecutionSliceStore executionSliceStore;
    private readonly FileSandboxWorkspaceChatProjectionStore chatProjectionStore;
    private readonly FileSandboxWorkspaceCrossProcessLock crossProcessLock;
    private readonly DurableFileWriter runLeaseWriter;
    private readonly IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory;
    private readonly Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary;
    private readonly Action<GenericNewRunCommitStage>? genericNewRunCommitBoundary;
    private readonly Action<ExistingRunDetailCommitStage>? existingRunDetailCommitBoundary;
    private readonly Action<AgentDeletionCommitStage>? agentDeletionCommitBoundary;
    private readonly IAgentCatalogMutationPolicy? catalogMutationPolicy;

    public FileSandboxWorkspaceStore(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null,
        IAgentCatalogMutationPolicy? catalogMutationPolicy = null)
        : this(
            workspaceRoot,
            workspaceScope,
            chatBackedRunCommitBoundary: null,
            existingRunDetailCommitBoundary: null,
            genericNewRunCommitBoundary: null,
            jsonReadDiagnostics: null,
            catalogMutationPolicy: catalogMutationPolicy) {
    }

    internal FileSandboxWorkspaceStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary)
        : this(
            workspaceRoot,
            workspaceScope,
            chatBackedRunCommitBoundary,
            existingRunDetailCommitBoundary: null)
    {
    }

    internal FileSandboxWorkspaceStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary,
        Action<ExistingRunDetailCommitStage>? existingRunDetailCommitBoundary)
        : this(
            workspaceRoot,
            workspaceScope,
            chatBackedRunCommitBoundary,
            existingRunDetailCommitBoundary,
            jsonReadDiagnostics: null)
    {
    }

    internal FileSandboxWorkspaceStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary,
        Action<ExistingRunDetailCommitStage>? existingRunDetailCommitBoundary,
        FileSandboxWorkspaceJsonReadDiagnostics? jsonReadDiagnostics)
        : this(
            workspaceRoot,
            workspaceScope,
            chatBackedRunCommitBoundary,
            existingRunDetailCommitBoundary,
            genericNewRunCommitBoundary: null,
            jsonReadDiagnostics)
    {
    }

    internal FileSandboxWorkspaceStore(
        string workspaceRoot,
        WorkspaceScopeDescriptor? workspaceScope,
        Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary,
        Action<ExistingRunDetailCommitStage>? existingRunDetailCommitBoundary,
        Action<GenericNewRunCommitStage>? genericNewRunCommitBoundary,
        FileSandboxWorkspaceJsonReadDiagnostics? jsonReadDiagnostics,
        Action<AgentDeletionCommitStage>? agentDeletionCommitBoundary = null,
        Action<FileHistoryCommitStage>? historyCommitBoundary = null,
        IAgentCatalogMutationPolicy? catalogMutationPolicy = null,
        DurableFileWriter? fileWriter = null) {
        layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        physicalPathPolicyFactory = new PhysicalFileSystemPathPolicyFactory(PhysicalFileSystemCaseSensitivity.Sensitive);
        var durableFileWriter = fileWriter ?? new DurableFileWriter(physicalPathPolicyFactory);
        runLeaseWriter = durableFileWriter;
        jsonStore = new FileSandboxWorkspaceJsonStore(
            jsonReadDiagnostics,
            physicalPathPolicyFactory,
            durableFileWriter,
            layout.RootPath,
            new FileProviderHistoryJournal(layout, historyCommitBoundary));
        catalogNormalizer = new(jsonStore.RequiresSave, jsonStore.CanRetainCatalogSnapshot);
        executionSliceStore = new FileSandboxWorkspaceExecutionSliceStore(layout, jsonStore);
        jsonStore.UsageIndex = new FileProviderUsageIndex(layout, jsonStore);
        chatProjectionStore = new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
        crossProcessLock = new FileSandboxWorkspaceCrossProcessLock(
            layout.RootPath,
            layout.WorkspaceLockPath,
            durableFileWriter);
        this.chatBackedRunCommitBoundary = chatBackedRunCommitBoundary;
        this.genericNewRunCommitBoundary = genericNewRunCommitBoundary;
        this.existingRunDetailCommitBoundary = existingRunDetailCommitBoundary;
        this.agentDeletionCommitBoundary = agentDeletionCommitBoundary;
        this.catalogMutationPolicy = catalogMutationPolicy;
    }

    public async Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default)
        => (await LoadSnapshotAsync(cancellationToken)).Document;

    public async Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await LoadSnapshotCoreAsync(operation, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (catalogMutationPolicy is null && CanReadCatalogWithoutWorkspaceLock())
        {
            return await LoadCatalogWithoutWorkspaceLockAsync(cancellationToken);
        }

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken);
            return await LoadNormalizedCatalogCoreAsync(operation, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken);
            var catalog = await LoadNormalizedCatalogCoreAsync(operation, cancellationToken);
            return new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public Task<SandboxWorkspaceCatalog> SaveCatalogAsync(
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken = default)
        => SaveCatalogOnlyAsync(catalog, expectedRevision: null, cancellationToken);

    public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return await UpdateCatalogOnlyAsync(update, expectedRevision: null, cancellationToken);
    }

    public Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        Action<SandboxWorkspaceCatalog> validatePublication,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(validatePublication);
        return UpdateCatalogOnlyAsync(update, expectedRevision: null, cancellationToken, validatePublication);
    }

    public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return await UpdateCatalogOnlyAsync(update, expectedRevision, cancellationToken);
    }

    public async Task<AgentDecisionDataPolicySaveOutcome> ReplaceAgentDecisionDataPolicyAsync(
        ReplaceAgentDecisionDataPolicyCommand command, Action validateCurrentContext,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(validateCurrentContext);
        var publication = new AgentPolicyPublication(command, DateTimeOffset.UtcNow);
        try {
            if (command.Expectation.Workspace.WorkspaceScope != layout.Scope) {
                throw new AgentDecisionDataPolicyContextChangedException();
            }
            validateCurrentContext();
            await using (var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(
                gate, crossProcessLock, layout, runLeaseWriter, cancellationToken)) {
                try {
                    await EnsureCatalogReadCoreAsync(operation, cancellationToken);
                    var current = await LoadCatalogCoreAsync(cancellationToken);
                    if (current.CatalogDataRevision != command.Expectation.CatalogRevision) {
                        throw new SandboxWorkspaceCatalogConcurrencyException(
                            command.Expectation.CatalogRevision, current.CatalogDataRevision);
                    }
                    var original = current.Agents.SingleOrDefault(agent => agent.Id == command.Expectation.AgentId)
                        ?? throw new KeyNotFoundException("The original Agent no longer exists.");
                    var target = current with {
                        Agents = current.Agents.Select(agent => agent.Id == original.Id
                            ? agent with { UpdatedAtUtc = AgentConfigurationVersion.NextRevision(original.UpdatedAtUtc, DateTimeOffset.UtcNow) }
                            : agent).ToArray()
                    };
                    await SaveCatalogCoreAsync(operation, current, catalogExists: true, target, cancellationToken,
                        _ => validateCurrentContext(), publication, CatalogPublicationDependencies.CatalogOnly);
                    var index = await LoadWorkspaceIndexCoreAsync(cancellationToken);
                    await SaveWorkspaceIndexCoreAsync(new WorkspaceStorageIndex(
                        checked(index.Revision + 1L), DateTimeOffset.UtcNow), cancellationToken);
                } catch (Exception failure) {
                    operation.RetainFailure(failure);
                    throw;
                }
            }
            return publication.Acknowledgement
                ?? throw new InvalidOperationException("Agent policy publication returned without acknowledgement.");
        } catch (Exception failure) {
            var diagnostic = AgentDecisionDataPolicyFailure.Capture(AgentDecisionDataPolicyFailureStage.Publication, failure);
            if (publication.Acknowledgement is { } accepted) {
                return new AgentDecisionDataPolicySaveOutcome.Accepted(command, accepted.Snapshot, followupRequired: true, diagnostic);
            }
            if (publication.WriteEntered) {
                return new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command, diagnostic);
            }
            var reason = failure switch {
                AgentDecisionDataPolicyContextChangedException => AgentDecisionDataPolicyRejection.ContextChanged,
                SandboxWorkspaceCatalogConcurrencyException or SandboxWorkspacePublicationConflictException
                    => AgentDecisionDataPolicyRejection.Conflict,
                KeyNotFoundException => AgentDecisionDataPolicyRejection.NotFound,
                AgentDecisionDataPolicyMetadataException => AgentDecisionDataPolicyRejection.InvalidMetadata,
                AgentDecisionDataPolicyReceiptCapacityException => AgentDecisionDataPolicyRejection.ReceiptCapacityExceeded,
                AgentDecisionDataPolicyReceiptConflictException => AgentDecisionDataPolicyRejection.Conflict,
                OperationCanceledException => AgentDecisionDataPolicyRejection.Cancelled,
                _ => AgentDecisionDataPolicyRejection.Unavailable
            };
            return new AgentDecisionDataPolicySaveOutcome.Rejected(reason, diagnostic);
        }
    }

    private async Task<SandboxWorkspaceCatalog> SaveCatalogOnlyAsync(
        SandboxWorkspaceCatalog catalog,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        catalog = FileSandboxWorkspacePreparedValues.Freeze(catalog);
        return await UpdateCatalogOnlyAsync(
            _ => catalog,
            expectedRevision,
            cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> UpdateCatalogOnlyAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long? expectedRevision,
        CancellationToken cancellationToken,
        Action<SandboxWorkspaceCatalog>? validatePublication = null)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken);
            var workspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
            if (expectedRevision.HasValue && workspaceIndex.Revision != expectedRevision.Value)
            {
                throw new SandboxWorkspaceConcurrencyException(expectedRevision.Value, workspaceIndex.Revision);
            }

            var currentCatalog = await LoadCatalogCoreAsync(cancellationToken);
            var currentForUpdate = SandboxWorkspaceSeedFactory.NormalizeCatalog(currentCatalog) with
            {
                CatalogDataRevision = currentCatalog.CatalogDataRevision.IsAssigned
                    ? currentCatalog.CatalogDataRevision
                    : CatalogDataRevision.Initial
            };
            var updatedCatalog = FileSandboxWorkspacePreparedValues.Freeze(
                SandboxWorkspaceSeedFactory.NormalizeCatalog(update(FileSandboxWorkspacePreparedValues.Freeze(currentForUpdate))));
            SandboxWorkspaceDocumentInvariantValidator.Validate(
                SandboxWorkspaceDocument.Combine(updatedCatalog, SandboxWorkspaceExecutionState.Empty));

            var catalogSave = await SaveCatalogCoreAsync(operation,
                currentCatalog,
                File.Exists(layout.CatalogPath),
                updatedCatalog,
                cancellationToken,
                validatePublication,
                dependencies: expectedRevision.HasValue ? CatalogPublicationDependencies.WholeWorkspace : CatalogPublicationDependencies.CatalogOnly);
            workspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
            var nextWorkspaceIndex = new WorkspaceStorageIndex(
                Revision: catalogSave.Changed ? workspaceIndex.Revision + 1L : workspaceIndex.Revision,
                UpdatedAtUtc: catalogSave.Changed ? DateTimeOffset.UtcNow : workspaceIndex.UpdatedAtUtc);
            if (catalogSave.Changed ||
                !File.Exists(layout.WorkspaceIndexPath) ||
                jsonStore.RequiresSave(workspaceIndex, nextWorkspaceIndex))
            {
                await SaveWorkspaceIndexCoreAsync(nextWorkspaceIndex, cancellationToken);
            }

            return catalogSave.Catalog;
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return (await LoadSnapshotCoreAsync(operation, cancellationToken)).Document.ToExecutionState();
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureExecutionSummaryReadCoreAsync(operation, cancellationToken);
            return await LoadExecutionSummaryCoreAsync(cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureExecutionSummaryReadCoreAsync(operation, cancellationToken);
            return await LoadUsageProjectionCoreAsync(cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<AgentProviderUsageEvidence> LoadProviderUsageEvidenceAsync(
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureExecutionSummaryReadCoreAsync(operation, cancellationToken);
            return await executionSliceStore.LoadProviderUsageEvidenceAsync(cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<AgentProviderUsageWindowEvidence> LoadProviderUsageWindowAsync(
        CanDoItAll.AgentFramework.Usage.ProviderUsageWindow window, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(window);
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await RecoverPendingExecutionCommitAsync(operation, cancellationToken);
            return await jsonStore.UsageIndex!.ReadAsync(window, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task EnsureReadyAsync(bool requireSummaryIndex = false, CancellationToken cancellationToken = default) {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await RecoverPendingExecutionCommitAsync(operation, cancellationToken);
            if (!layout.ExecutionStorageExists() && (File.Exists(layout.CatalogPath) || File.Exists(layout.LegacyExecutionPath))) {
                throw new InvalidOperationException("Legacy workspace storage needs explicit migration. Run usage-index maintenance with --migrate-legacy.");
            }
            if (requireSummaryIndex && layout.ExecutionStorageExists() && !await executionSliceStore.HasCurrentSummaryIndexAsync(cancellationToken)) {
                throw new InvalidOperationException("The runtime summary index needs explicit repair. Run usage-index maintenance with --migrate-legacy.");
            }
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task SaveExecutionAsync(SandboxWorkspaceExecutionState executionState, CancellationToken cancellationToken = default)
    {
        executionState = FileSandboxWorkspacePreparedValues.Freeze(executionState);
        await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                document.ToCatalog(),
                SandboxWorkspaceSeedFactory.NormalizeExecutionState(executionState)),
            cancellationToken);
    }

    public async Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
        Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var updatedDocument = await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                document.ToCatalog(),
                SandboxWorkspaceSeedFactory.NormalizeExecutionState(update(document.ToExecutionState()))),
            cancellationToken);

        return updatedDocument.ToExecutionState();
    }

    public async Task<SandboxWorkspaceExecutionState> UpdateExecutionAsync(
        Func<SandboxWorkspaceExecutionState, SandboxWorkspaceExecutionState> update,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        var updatedDocument = await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                document.ToCatalog(),
                SandboxWorkspaceSeedFactory.NormalizeExecutionState(update(document.ToExecutionState()))),
            expectedRevision,
            cancellationToken);

        return updatedDocument.ToExecutionState();
    }

    public async Task<ExecutionRunDetail?> GetExecutionRunDetailAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await executionSliceStore.LoadRunDetailAsync(executionRunId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ExecutionRunAdmissionSnapshot?> GetExecutionRunAdmissionSnapshotAsync(
        Guid executionRunId, CancellationToken cancellationToken = default) {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await executionSliceStore.LoadRunAdmissionSnapshotAsync(executionRunId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await executionSliceStore.ListRunsAsync(cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ProcessExecutionRunPage> ReadProcessExecutionRunPageAsync(
        ProcessExecutionRunPageQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            if (PendingCommitJournalPaths.Any(File.Exists)) {
                return ProcessExecutionRunPage.Pending(ProcessExecutionRunPageStatus.PublicationPending);
            }
            return await new FileProcessExecutionRunPageReader(layout, jsonStore).ReadAsync(query, cancellationToken).ConfigureAwait(false);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ProcessExecutionRunPageStatus> PrepareProcessExecutionRunIndexAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            if (PendingCommitJournalPaths.Any(File.Exists)) {
                return ProcessExecutionRunPageStatus.PublicationPending;
            }
            return await new FileProcessExecutionIndex(layout, jsonStore).PrepareAsync(cancellationToken).ConfigureAwait(false);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunCandidatesAsync(
        ExecutionRunQuery query, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            if (query.ExecutionRunId is { } runId) {
                var run = await executionSliceStore.LoadRunAsync(runId, cancellationToken);
                return run is null ? [] : [run];
            }
            if (string.IsNullOrWhiteSpace(query.ProcessRunId) && query.ProcessRunIds.Count == 0) {
                return await executionSliceStore.ListRunsAsync(cancellationToken);
            }
            var candidates = await chatProjectionStore.FindProcessRunCandidateIdsAsync(query, cancellationToken);
            var runs = await jsonStore.ReadJsonBatchAsync<ExecutionRunRecord>(
                candidates.Select(layout.RunPath).ToArray(), cancellationToken);
            if (runs.Any(run => run is null)) {
                throw new InvalidDataException("The execution index references a missing run record.");
            }
            return runs.Select(run => run!).ToArray();
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ExecutionRunRecord?> GetExecutionRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await executionSliceStore.LoadRunAsync(executionRunId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
        ExecutionRunDetail detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);
        detail = FileSandboxWorkspacePreparedValues.Freeze(detail);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var previousDetail = await executionSliceStore
                .LoadRunDetailAsync(detail.Run.Id, cancellationToken);
            SandboxWorkspaceCatalog? validatedCatalog = null;
            if (previousDetail is null)
            {
                validatedCatalog = await LoadCatalogCoreAsync(cancellationToken);
                ValidateExecutionRunDetail(validatedCatalog, detail);
            }
            else
            {
                ValidateExecutionRunDetailConsistency(detail);
            }

            return await SaveExecutionRunDetailCoreAsync(operation,
                previousDetail,
                detail,
                validatedCatalog,
                cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public Task<ChatBackedRunStartResult> BeginChatBackedRunAsync(
        ChatBackedRunStartRequest request,
        Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create,
        CancellationToken cancellationToken = default)
        => BeginChatBackedRunCoreAsync(request, create, reportAdmissionKnowledge: false, cancellationToken);

    public Task<ChatBackedRunStartResult> BeginChatBackedRunWithAdmissionKnowledgeAsync(
        ChatBackedRunStartRequest request,
        Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create,
        CancellationToken cancellationToken = default)
        => BeginChatBackedRunCoreAsync(request, create, reportAdmissionKnowledge: true, cancellationToken);

    private async Task<ChatBackedRunStartResult> BeginChatBackedRunCoreAsync(
        ChatBackedRunStartRequest request,
        Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create,
        bool reportAdmissionKnowledge,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(create);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var catalog = FileSandboxWorkspacePreparedValues.Freeze(await LoadNormalizedCatalogCoreAsync(operation, cancellationToken));
            if (catalog.CatalogDataRevision != request.ExpectedCatalogRevision)
            {
                throw new SandboxWorkspaceCatalogConcurrencyException(
                    request.ExpectedCatalogRevision,
                    catalog.CatalogDataRevision);
            }

            var agent = catalog.Agents.FirstOrDefault(item => item.Id == request.AgentId)
                ?? throw new InvalidOperationException(
                    $"Agent '{request.AgentId:N}' was not found in the current catalog.");
            if (agent.ProviderProfileId != request.ExpectedAgentProviderProfileId)
            {
                throw new InvalidOperationException(
                    $"Agent '{request.AgentId:N}' no longer uses the expected provider profile '{request.ExpectedAgentProviderProfileId:N}'.");
            }

            var catalogSnapshot = new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
            var session = await LoadChatRunStartSessionCoreAsync(request, cancellationToken);
            var existingMessages = session?.Messages.ToArray() ?? [];
            var sessionSnapshot = session is null
                ? null
                : session with
                {
                    Messages = Array.AsReadOnly(existingMessages)
                };
            var blockingRun = sessionSnapshot is null
                ? null
                : await LoadBlockingSessionRunAsync(
                    request.AgentId,
                    sessionSnapshot,
                    cancellationToken);
            if (blockingRun is not null)
            {
                return new ChatBackedRunBlocked(
                    catalogSnapshot,
                    agent,
                    sessionSnapshot!,
                    blockingRun);
            }

            sessionSnapshot = sessionSnapshot is null ? null : FileSandboxWorkspacePreparedValues.Freeze(sessionSnapshot);
            var context = new ChatBackedRunStartContext(
                catalogSnapshot,
                agent,
                sessionSnapshot);
            var created = create(context)
                ?? throw new InvalidOperationException(
                    "The chat-backed run start factory returned no mutation.");
            var mutation = created with { Detail = FileSandboxWorkspacePreparedValues.Freeze(created.Detail) };

            ValidateChatBackedRunStartMutation(
                request,
                context,
                existingMessages,
                mutation);
            ValidateExecutionRunDetail(catalog, mutation.Detail);
            if (await executionSliceStore.LoadRunAsync(
                    mutation.Detail.Run.Id,
                    cancellationToken) is not null)
            {
                throw new InvalidOperationException(
                    $"Execution run '{mutation.Detail.Run.Id:N}' already exists.");
            }

            IAsyncDisposable acquired;
            try {
                var witness = await CapturePublicationWitnessAsync(cancellationToken,
                    mutation.Detail.Run.ChatSessionId is { } publicationChatId ? [layout.SessionPath(publicationChatId)] : [],
                    [layout.RunRoot(mutation.Detail.Run.Id)]);
                acquired = await operation.AcquireAsync(
                    FileSandboxWorkspaceWriteSet.Run(null, mutation.Detail.Run, readsAgent: true), witness, cancellationToken);
            } catch (Exception conflict) when (reportAdmissionKnowledge &&
                    conflict is SandboxWorkspaceConcurrencyException or SandboxWorkspacePublicationConflictException) {
                throw new AgentChatAdmissionConflictException(request.AgentId, request.ChatSessionId, conflict);
            }
            await using var publication = acquired;
            try {

                var persistencePlan = await executionSliceStore.PrepareNewRunAsync(
                    mutation.Detail,
                    sessionSnapshot,
                    cancellationToken);
                var previousWorkspaceIndex = await LoadWorkspaceIndexCoreAsync(
                    cancellationToken);
                var targetWorkspaceIndex = new WorkspaceStorageIndex(
                    previousWorkspaceIndex.Revision + 1L,
                    persistencePlan.TargetIndex.UpdatedAtUtc);
                var journal = new ChatBackedRunCommitJournal(
                    ChatBackedRunCommitJournalVersion,
                    persistencePlan.Detail.Run.Id,
                    mutation.UserMessage.Id,
                    persistencePlan,
                    previousWorkspaceIndex,
                    targetWorkspaceIndex);
                ValidateChatBackedRunCommitJournal(catalog, journal);

                await jsonStore.WriteJsonAtomicallyAsync(
                    PendingChatBackedRunCommitJournalPath,
                    journal,
                    cancellationToken);
                NotifyChatBackedRunCommitBoundary(
                    ChatBackedRunCommitStage.JournalPersisted);
                var persistedDetail = await CommitChatBackedRunJournalAsync(
                    journal,
                    catalog,
                    CancellationToken.None);
                return new ChatBackedRunStarted(
                    catalogSnapshot,
                    agent,
                    persistedDetail,
                    mutation.UserMessage);
            } catch (Exception failure) {
                operation.RetainFailure(failure);
                throw;
            }
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ExecutionRunSourceReservationResult> ReserveExecutionRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunDetail candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        candidate = FileSandboxWorkspacePreparedValues.Freeze(candidate);
        if (source.RequiresBackgroundAdmission) {
            throw new InvalidOperationException("Use the explicitly admitted background reservation operation.");
        }
        if (!source.Matches(candidate.Run))
        {
            throw new InvalidOperationException(
                "The candidate execution run does not match the requested source key.");
        }

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var sameSourceRuns = (await executionSliceStore.ListRunsAsync(cancellationToken))
                .Where(source.Matches)
                .OrderByDescending(run => run.UpdatedAtUtc)
                .ToArray();
            var completedRun = sameSourceRuns.FirstOrDefault(run =>
                run.State == ExecutionState.Completed &&
                run.Outcome == RunOutcome.Succeeded);
            if (completedRun is not null)
            {
                return new ExecutionRunSourceReservationResult(
                    ExecutionRunSourceDisposition.ReusedCompleted,
                    completedRun);
            }

            var activeRun = sameSourceRuns.FirstOrDefault(run =>
                run.State is not ExecutionState.Completed and not ExecutionState.Failed);
            if (activeRun is not null)
            {
                return new ExecutionRunSourceReservationResult(
                    ExecutionRunSourceDisposition.ExistingActive,
                    activeRun);
            }

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateExecutionRunDetail(catalog, candidate);
            var persisted = await SaveExecutionRunDetailCoreAsync(operation,
                previousDetail: null,
                candidate,
                catalog,
                cancellationToken);
            return new ExecutionRunSourceReservationResult(
                ExecutionRunSourceDisposition.Created,
                persisted.Run);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public ValueTask<IAsyncDisposable> AcquireToolDispatchLeaseAsync(
        Guid executionRunId, CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        return runLeaseWriter.AcquireCoordinationAsync(layout.RootPath,
            Path.Combine(layout.RunRoot(executionRunId), "tool-dispatch.lock"),
            TimeSpan.FromMinutes(1), requirePrivateUnixMode: false, cancellationToken);
    }

    public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default)
        => UpdateExecutionRunDetailCoreAsync(executionRunId, update, null, cancellationToken);

    public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update, Func<CancellationToken, Task> requireCurrentAuthority,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(requireCurrentAuthority);
        return UpdateExecutionRunDetailCoreAsync(executionRunId, update, requireCurrentAuthority, cancellationToken);
    }

    private async Task<ExecutionRunDetail> UpdateExecutionRunDetailCoreAsync(Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update, Func<CancellationToken, Task>? requireCurrentAuthority,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(update);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var observation = await executionSliceStore
                .LoadRunDetailForUpdateAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Execution run '{executionRunId:N}' was not found.");
            var previousDetail = observation.Detail;
            var currentForUpdate = FileSandboxWorkspacePreparedValues.Freeze(previousDetail);
            var updatedDetail = update(currentForUpdate)
                ?? throw new InvalidOperationException(
                    "Execution run update cannot return null.");
            if (updatedDetail.Run.Id != executionRunId) {
                throw new InvalidOperationException(
                    "Execution run update cannot change the run identity.");
            }

            if (ReferenceEquals(currentForUpdate, updatedDetail) && requireCurrentAuthority is null) {
                return previousDetail;
            }

            ValidateExecutionRunDetailConsistency(updatedDetail);
            return await SaveExecutionRunDetailCoreAsync(operation,
                previousDetail,
                updatedDetail,
                cancellationToken,
                observation.Snapshot,
                requireCurrentAuthority);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(update);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            var observation = await executionSliceStore.LoadRunDetailForUpdateAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException($"Execution run '{executionRunId:N}' was not found.");
            var previousDetail = observation.Detail;
            var currentForUpdate = FileSandboxWorkspacePreparedValues.Freeze(previousDetail);
            var updatedDetail = update(FileSandboxWorkspacePreparedValues.Freeze(catalog), currentForUpdate)
                ?? throw new InvalidOperationException("Execution run update cannot return null.");
            if (updatedDetail.Run.Id != executionRunId) {
                throw new InvalidOperationException("Execution run update cannot change the run identity.");
            }

            if (ReferenceEquals(currentForUpdate, updatedDetail)) {
                return previousDetail;
            }

            ValidateExecutionRunDetail(catalog, updatedDetail);
            return await SaveExecutionRunDetailCoreAsync(operation, previousDetail, updatedDetail, catalog, cancellationToken, observation.Snapshot);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceDocument> SaveAsync(
        SandboxWorkspaceDocument document,
        CancellationToken cancellationToken = default)
    {
        document = FileSandboxWorkspacePreparedValues.Freeze(document);
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            var normalizedDocument = FileSandboxWorkspacePreparedValues.Freeze(NormalizeAndValidateDocument(document));
            var currentRevision = await ResolveCurrentRevisionAsync(operation, cancellationToken);
            var previousDocument = currentRevision == 0L
                ? SandboxWorkspaceDocument.Empty
                : (await LoadMutationSnapshotCoreAsync(cancellationToken)).Document;
            return await SaveCoreAsync(operation,
                previousDocument,
                normalizedDocument,
                currentRevision,
                cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        CancellationToken cancellationToken = default)
        => await UpdateWorkspaceCoreAsync(update, expectedRevision: null, cancellationToken);

    public async Task<SandboxWorkspaceDocument> UpdateWorkspaceAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        long expectedRevision,
        CancellationToken cancellationToken = default)
        => await UpdateWorkspaceCoreAsync(update, expectedRevision: expectedRevision, cancellationToken);

    public async Task<IReadOnlyList<ChatSessionSummaryRecord>> ListChatSessionSummariesAsync(
        Guid? agentId = null,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await chatProjectionStore.ListChatSessionSummariesAsync(agentId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ChatWorkspaceProjectionSnapshot> LoadChatWorkspaceProjectionAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await chatProjectionStore.LoadChatWorkspaceProjectionAsync(agentId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ChatSessionRecord?> GetChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await chatProjectionStore.GetChatSessionAsync(chatSessionId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListChatRunSummariesAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await chatProjectionStore.ListChatRunSummariesAsync(agentId, chatSessionId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            return await chatProjectionStore.LoadChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ChatSessionRecord> CreateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        session = FileSandboxWorkspacePreparedValues.Freeze(session);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateChatSession(catalog, session);

            var existingSession = await chatProjectionStore.GetChatSessionAsync(session.Id, cancellationToken);
            if (existingSession is not null)
            {
                throw new InvalidOperationException($"Chat session '{session.Id:N}' already exists.");
            }

            return await SaveChatSessionCoreAsync(operation, previousSession: null, session, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    public async Task<ChatSessionRecord> UpdateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        session = FileSandboxWorkspacePreparedValues.Freeze(session);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateChatSession(catalog, session);

            var existingSession = await chatProjectionStore.GetChatSessionAsync(session.Id, cancellationToken)
                                  ?? throw new InvalidOperationException($"Chat session '{session.Id:N}' was not found.");
            if (existingSession.AgentId != session.AgentId)
            {
                throw new InvalidOperationException($"Chat session '{session.Id:N}' cannot be moved to a different agent.");
            }

            return await SaveChatSessionCoreAsync(operation, existingSession, session, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<SandboxWorkspaceDocument> UpdateWorkspaceCoreAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureSplitFilesCoreAsync(operation, cancellationToken);
            var current = await LoadMutationSnapshotCoreAsync(cancellationToken);
            if (expectedRevision.HasValue && current.Revision != expectedRevision.Value)
            {
                throw new SandboxWorkspaceConcurrencyException(expectedRevision.Value, current.Revision);
            }

            var updated = FileSandboxWorkspacePreparedValues.Freeze(
                NormalizeAndValidateDocument(update(FileSandboxWorkspacePreparedValues.Freeze(current.Document))));
            return await SaveCoreAsync(operation,
                current.Document,
                updated,
                current.Revision,
                cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<SandboxWorkspaceDocument> SaveCoreAsync(FileSandboxWorkspacePublicationScope operation,
        SandboxWorkspaceDocument previousDocument,
        SandboxWorkspaceDocument document,
        long currentRevision,
        CancellationToken cancellationToken)
    {
        document = FileSandboxWorkspacePreparedValues.Freeze(document);
        var witness = await CapturePublicationWitnessAsync(cancellationToken, directories: [layout.ExecutionStorageRoot]);
        var catalogExists = File.Exists(layout.CatalogPath);
        var currentCatalog = await LoadCatalogCoreAsync(cancellationToken);
        var preparedCatalog = await PrepareCatalogCoreAsync(currentCatalog, catalogExists, document.ToCatalog(), cancellationToken);
        document = SandboxWorkspaceDocument.Combine(preparedCatalog.Catalog, document.ToExecutionState());
        await using var publication = await operation.AcquireAsync(
            FileSandboxWorkspaceWriteSet.Workspace(previousDocument, document), witness, cancellationToken);
        try {
            jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

            var finalCatalog = await PrepareCatalogCoreAsync(currentCatalog, catalogExists, preparedCatalog.Catalog, cancellationToken);
            if (finalCatalog.Changed != preparedCatalog.Changed || !HasSamePayload(finalCatalog.Catalog, preparedCatalog.Catalog)) {
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.WriteSetChanged);
            }
            if (preparedCatalog.Changed) {
                await jsonStore.WriteJsonAtomicallyAsync(layout.CatalogPath, preparedCatalog.Catalog, cancellationToken);
            }
            var executionChanged = await SaveExecutionCoreAsync(operation, previousDocument.ToExecutionState(), document.ToExecutionState(), cancellationToken);
            var workspaceChanged = preparedCatalog.Changed || executionChanged || !File.Exists(layout.WorkspaceIndexPath);
            if (workspaceChanged)
            {
                await SaveWorkspaceIndexCoreAsync(
                    new WorkspaceStorageIndex(
                        Revision: Math.Max(1L, currentRevision + 1L),
                        UpdatedAtUtc: DateTimeOffset.UtcNow),
                    cancellationToken);
            }

            return SandboxWorkspaceDocument.Combine(
                preparedCatalog.Catalog,
                document.ToExecutionState());
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<ExecutionRunDetail> SaveExecutionRunDetailCoreAsync(FileSandboxWorkspacePublicationScope operation,
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken,
        FileSandboxWorkspaceRunReadSnapshot? snapshot = null, Func<CancellationToken, Task>? requireCurrentAuthority = null) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.run.save");
        return await SaveExecutionRunDetailCoreAsync(operation,
            previousDetail,
            detail,
            validatedCatalog: null,
            cancellationToken,
            snapshot,
            requireCurrentAuthority);
    }

    private async Task<ExecutionRunDetail> SaveExecutionRunDetailCoreAsync(FileSandboxWorkspacePublicationScope operation,
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        SandboxWorkspaceCatalog? validatedCatalog,
        CancellationToken cancellationToken,
        FileSandboxWorkspaceRunReadSnapshot? snapshot = null, Func<CancellationToken, Task>? requireCurrentAuthority = null) {
        detail = FileSandboxWorkspacePreparedValues.Freeze(detail);
        var unchanged = previousDetail is not null && executionSliceStore.HasSameRunDetailPayload(previousDetail, detail);
        if (unchanged && requireCurrentAuthority is null) {
            return previousDetail!;
        }
        var chatPaths = new[] { previousDetail?.Run.ChatSessionId, detail.Run.ChatSessionId }
            .OfType<Guid>().Select(layout.SessionPath).Distinct().ToArray();
        var witness = previousDetail is not null && validatedCatalog is null
            ? await FileSandboxWorkspaceMutationWitness.CaptureAsync(layout,
                PendingCommitJournalPaths.Concat(chatPaths).Append(layout.RunPath(detail.Run.Id)),
                [layout.RunRoot(detail.Run.Id)], PendingCommitJournalPaths, physicalPathPolicyFactory, cancellationToken,
                pendingPublicationTargets: SharedPublicationPaths)
            : await CapturePublicationWitnessAsync(cancellationToken, chatPaths, [layout.RunRoot(detail.Run.Id)]);
        await using var publication = await operation.AcquireAsync(
            FileSandboxWorkspaceWriteSet.Run(previousDetail?.Run, detail.Run, validatedCatalog is not null || previousDetail is null),
            witness, cancellationToken, requireCurrentAuthority);
        if (unchanged) {
            return previousDetail!;
        }
        try {
            if (previousDetail is not null) {
                var persistencePlan =
                    await executionSliceStore.PrepareExistingRunUpdateAsync(
                        previousDetail,
                        detail,
                        cancellationToken);
                var chatProjectionPlan =
                    await chatProjectionStore.PrepareExistingRunUpdateAsync(
                        persistencePlan,
                        cancellationToken);
                var previousWorkspaceIndex =
                    await LoadWorkspaceIndexCoreAsync(cancellationToken);
                var targetWorkspaceIndex = new WorkspaceStorageIndex(
                    previousWorkspaceIndex.Revision + 1L,
                    persistencePlan.TargetIndex.UpdatedAtUtc);
                var journal = new ExistingRunDetailCommitJournal(
                    ExistingRunDetailCommitJournalVersion,
                    detail.Run.Id,
                    persistencePlan,
                    chatProjectionPlan,
                    previousWorkspaceIndex,
                    targetWorkspaceIndex);
                ValidateExistingRunDetailCommitJournal(journal);

                await jsonStore.WriteJsonAtomicallyAsync(
                    PendingExistingRunDetailCommitJournalPath,
                    journal,
                    cancellationToken);
                NotifyExistingRunDetailCommitBoundary(
                    ExistingRunDetailCommitStage.JournalPersisted);
                return await CommitExistingRunDetailJournalAsync(
                    journal,
                    ExistingRunDetailCommitOrigin.Prepared,
                    CancellationToken.None,
                    snapshot);
            }

            var genericPersistencePlan =
                await executionSliceStore.PrepareGenericNewRunAsync(
                    detail,
                    cancellationToken);
            var genericChatProjectionPlan =
                await chatProjectionStore.PrepareGenericNewRunAsync(
                    genericPersistencePlan,
                    cancellationToken);
            var genericPreviousWorkspaceIndex =
                await LoadWorkspaceIndexCoreAsync(cancellationToken);
            var genericTargetWorkspaceIndex = new WorkspaceStorageIndex(
                genericPreviousWorkspaceIndex.Revision + 1L,
                genericPersistencePlan.TargetIndex.UpdatedAtUtc);
            var genericJournal = new GenericNewRunCommitJournal(
                GenericNewRunCommitJournalVersion,
                detail.Run.Id,
                genericPersistencePlan,
                genericChatProjectionPlan,
                genericPreviousWorkspaceIndex,
                genericTargetWorkspaceIndex);
            var catalog = validatedCatalog ??
                          await LoadCatalogCoreAsync(cancellationToken);
            ValidateGenericNewRunCommitJournal(catalog, genericJournal);

            await jsonStore.WriteJsonAtomicallyAsync(
                PendingGenericNewRunCommitJournalPath,
                genericJournal,
                cancellationToken);
            NotifyGenericNewRunCommitBoundary(
                GenericNewRunCommitStage.JournalPersisted);
            return await CommitGenericNewRunJournalAsync(
                genericJournal,
                validatedCatalog: catalog,
                validatePersistedState: false,
                cancellationToken: CancellationToken.None);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<ExecutionRunDetail> CommitExistingRunDetailJournalAsync(
        ExistingRunDetailCommitJournal journal,
        ExistingRunDetailCommitOrigin origin,
        CancellationToken cancellationToken,
        FileSandboxWorkspaceRunReadSnapshot? snapshot = null) {
        using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("workspace.run.commit");
        ValidateExistingRunDetailCommitJournal(journal);

        var currentWorkspaceIndex = await LoadWorkspaceIndexCoreAsync(
            cancellationToken);
        if (!HasSamePayload(
                currentWorkspaceIndex,
                journal.PreviousWorkspaceIndex) &&
            !HasSamePayload(
                currentWorkspaceIndex,
                journal.TargetWorkspaceIndex)) {
            throw new InvalidDataException(
                $"Pending execution-run update '{journal.RunId:N}' found an unexpected workspace index.");
        }

        await executionSliceStore.PersistExistingRunSessionAsync(
            journal.PersistencePlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.SessionPersisted);

        await executionSliceStore.PersistExistingRunApprovalRecordsAsync(
            journal.PersistencePlan,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.ApprovalRecordsPersisted);

        await executionSliceStore.PersistExistingRunRemainingRecordsAsync(
            journal.PersistencePlan,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.RemainingRecordsPersisted);

        await Task.WhenAll(
            PersistStageAsync(() => executionSliceStore.PersistExistingRunRecordAsync(
                journal.PersistencePlan, origin, cancellationToken, snapshot), ExistingRunDetailCommitStage.RunPersisted),
            PersistStageAsync(() => executionSliceStore.PersistExistingRunExecutionIndexAsync(
                journal.PersistencePlan, origin, cancellationToken), ExistingRunDetailCommitStage.ExecutionIndexPersisted),
            PersistStageAsync(() => executionSliceStore.PersistExistingRunUsageIndexAsync(
                journal.PersistencePlan, origin, cancellationToken), ExistingRunDetailCommitStage.UsageIndexPersisted),
            PersistStageAsync(() => SaveWorkspaceIndexCoreAsync(journal.TargetWorkspaceIndex, cancellationToken),
                ExistingRunDetailCommitStage.WorkspaceIndexPersisted),
            chatProjectionStore.PersistExistingRunUpdateAsync(
                journal.PersistencePlan, journal.ChatProjectionPlan, origin, cancellationToken));
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.ChatIndexPersisted);

        await jsonStore.DeleteFileAsync(PendingExistingRunDetailCommitJournalPath, cancellationToken);
        return journal.PersistencePlan.TargetDetail;

        async Task PersistStageAsync(Func<Task> persist, ExistingRunDetailCommitStage stage) {
            await persist();
            NotifyExistingRunDetailCommitBoundary(stage);
        }

    }

    private async Task RecoverPendingExistingRunDetailCommitAsync(
        CancellationToken cancellationToken)
    {
        if (!HasPendingExistingRunDetailCommit)
        {
            return;
        }

        var journal = await jsonStore.ReadJsonAsync<ExistingRunDetailCommitJournal>(
            PendingExistingRunDetailCommitJournalPath,
            cancellationToken)
            ?? throw new InvalidDataException(
                $"Pending execution-run update journal '{PendingExistingRunDetailCommitJournalPath}' is empty.");
        await CommitExistingRunDetailJournalAsync(
            journal,
            ExistingRunDetailCommitOrigin.RecoveredJournal,
            CancellationToken.None);
    }

    private async Task<ExecutionRunDetail> CommitGenericNewRunJournalAsync(
        GenericNewRunCommitJournal journal,
        SandboxWorkspaceCatalog? validatedCatalog,
        bool validatePersistedState,
        CancellationToken cancellationToken)
    {
        var catalog = validatedCatalog ??
                      await LoadCatalogCoreAsync(cancellationToken);
        ValidateGenericNewRunCommitJournal(catalog, journal);

        if (validatePersistedState)
        {
            await executionSliceStore
                .ValidateGenericNewRunPersistedStateAsync(
                    journal.PersistencePlan,
                    cancellationToken);
            await chatProjectionStore.ValidateGenericNewRunPlanAsync(
                journal.PersistencePlan,
                journal.ChatProjectionPlan,
                cancellationToken);

            var currentWorkspaceIndex =
                await LoadWorkspaceIndexCoreAsync(cancellationToken);
            if (!HasSamePayload(
                    currentWorkspaceIndex,
                    journal.PreviousWorkspaceIndex) &&
                !HasSamePayload(
                    currentWorkspaceIndex,
                    journal.TargetWorkspaceIndex))
            {
                throw new InvalidDataException(
                    $"Pending generic execution-run creation '{journal.RunId:N}' found an unexpected workspace index.");
            }
        }

        var persistedDetail =
            await executionSliceStore.PersistGenericNewRunSlicesAsync(
                journal.PersistencePlan,
                validatePersistedState,
                cancellationToken);
        NotifyGenericNewRunCommitBoundary(
            GenericNewRunCommitStage.ExecutionSlicesPersisted);

        await executionSliceStore.PersistGenericNewRunExecutionIndexAsync(
            journal.PersistencePlan,
            validatePersistedState,
            cancellationToken);
        NotifyGenericNewRunCommitBoundary(
            GenericNewRunCommitStage.ExecutionIndexPersisted);

        await executionSliceStore.PersistGenericNewRunUsageIndexAsync(
            journal.PersistencePlan,
            validatePersistedState,
            cancellationToken);
        NotifyGenericNewRunCommitBoundary(
            GenericNewRunCommitStage.UsageIndexPersisted);

        await SaveWorkspaceIndexCoreAsync(
            journal.TargetWorkspaceIndex,
            cancellationToken);
        NotifyGenericNewRunCommitBoundary(
            GenericNewRunCommitStage.WorkspaceIndexPersisted);

        await chatProjectionStore.PersistGenericNewRunAsync(
            journal.PersistencePlan,
            journal.ChatProjectionPlan,
            validatePersistedState,
            cancellationToken);
        NotifyGenericNewRunCommitBoundary(
            GenericNewRunCommitStage.ChatIndexPersisted);

        await jsonStore.DeleteFileAsync(PendingGenericNewRunCommitJournalPath, cancellationToken);
        return persistedDetail;
    }

    private async Task RecoverPendingGenericNewRunCommitAsync(
        CancellationToken cancellationToken)
    {
        if (!HasPendingGenericNewRunCommit)
        {
            return;
        }

        var journal =
            await jsonStore.ReadJsonAsync<GenericNewRunCommitJournal>(
                PendingGenericNewRunCommitJournalPath,
                cancellationToken)
            ?? throw new InvalidDataException(
                $"Pending generic execution-run creation journal '{PendingGenericNewRunCommitJournalPath}' is empty.");
        await CommitGenericNewRunJournalAsync(
            journal,
            validatedCatalog: null,
            validatePersistedState: true,
            cancellationToken: CancellationToken.None);
    }

    private async Task<ExecutionRunDetail> CommitChatBackedRunJournalAsync(
        ChatBackedRunCommitJournal journal,
        SandboxWorkspaceCatalog? validatedCatalog,
        CancellationToken cancellationToken)
    {
        var catalog = validatedCatalog ??
                      await LoadCatalogCoreAsync(cancellationToken);
        ValidateChatBackedRunCommitJournal(catalog, journal);

        var currentWorkspaceIndex = await LoadWorkspaceIndexCoreAsync(
            cancellationToken);
        if (!HasSamePayload(
                currentWorkspaceIndex,
                journal.PreviousWorkspaceIndex) &&
            !HasSamePayload(
                currentWorkspaceIndex,
                journal.TargetWorkspaceIndex))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{journal.RunId:N}' found an unexpected workspace index.");
        }

        var persistedDetail = await executionSliceStore.PersistNewRunSlicesAsync(
            journal.PersistencePlan,
            validatePersistedState: validatedCatalog is null,
            cancellationToken: cancellationToken);
        NotifyChatBackedRunCommitBoundary(
            ChatBackedRunCommitStage.ExecutionSlicesPersisted);

        await executionSliceStore.PersistNewRunIndexesAsync(
            journal.PersistencePlan,
            cancellationToken);
        NotifyChatBackedRunCommitBoundary(
            ChatBackedRunCommitStage.ExecutionIndexesPersisted);

        await SaveWorkspaceIndexCoreAsync(
            journal.TargetWorkspaceIndex,
            cancellationToken);
        NotifyChatBackedRunCommitBoundary(
            ChatBackedRunCommitStage.WorkspaceIndexPersisted);

        await chatProjectionStore.SaveRunDetailAsync(
            previousDetail: null,
            persistedDetail,
            journal.PersistencePlan.TargetIndex,
            cancellationToken);
        NotifyChatBackedRunCommitBoundary(
            ChatBackedRunCommitStage.ChatProjectionPersisted);

        await jsonStore.DeleteFileAsync(PendingChatBackedRunCommitJournalPath, cancellationToken);
        return persistedDetail;
    }

    private async Task RecoverPendingChatBackedRunCommitAsync(
        CancellationToken cancellationToken)
    {
        if (!HasPendingChatBackedRunCommit)
        {
            return;
        }

        var journal = await jsonStore.ReadJsonAsync<ChatBackedRunCommitJournal>(
            PendingChatBackedRunCommitJournalPath,
            cancellationToken)
            ?? throw new InvalidDataException(
                $"Pending chat-run transaction journal '{PendingChatBackedRunCommitJournalPath}' is empty.");
        await CommitChatBackedRunJournalAsync(
            journal,
            validatedCatalog: null,
            cancellationToken: CancellationToken.None);
    }

    private async Task RecoverPendingExecutionCommitAsync(FileSandboxWorkspacePublicationScope operation,
        CancellationToken cancellationToken)
    {
        var pendingJournalCount =
            (HasPendingChatBackedRunCommit ? 1 : 0) +
            (HasPendingGenericNewRunCommit ? 1 : 0) +
            (HasPendingExistingRunDetailCommit ? 1 : 0) +
            (HasPendingAgentDeletionCommit ? 1 : 0);
        if (pendingJournalCount > 1)
        {
            throw new InvalidDataException(
                "The workspace contains conflicting pending execution transaction journals.");
        }
        if (pendingJournalCount == 0) {
            return;
        }
        var witness = await CapturePublicationWitnessAsync(cancellationToken, directories: [layout.ExecutionStorageRoot]);
        FileSandboxWorkspaceWriteSet writeSet;
        if (HasPendingChatBackedRunCommit) {
            var journal = await jsonStore.ReadJsonAsync<ChatBackedRunCommitJournal>(PendingChatBackedRunCommitJournalPath, cancellationToken)
                ?? throw new InvalidDataException("The pending chat-backed run journal is empty.");
            ValidateChatBackedRunCommitJournal(await LoadCatalogCoreAsync(cancellationToken), journal);
            writeSet = FileSandboxWorkspaceWriteSet.Run(null, journal.PersistencePlan.Detail.Run, readsAgent: true);
        } else if (HasPendingGenericNewRunCommit) {
            var journal = await jsonStore.ReadJsonAsync<GenericNewRunCommitJournal>(PendingGenericNewRunCommitJournalPath, cancellationToken)
                ?? throw new InvalidDataException("The pending new-run journal is empty.");
            ValidateGenericNewRunCommitJournal(await LoadCatalogCoreAsync(cancellationToken), journal);
            writeSet = FileSandboxWorkspaceWriteSet.Run(null, journal.PersistencePlan.Detail.Run, readsAgent: true);
        } else if (HasPendingExistingRunDetailCommit) {
            var journal = await jsonStore.ReadJsonAsync<ExistingRunDetailCommitJournal>(PendingExistingRunDetailCommitJournalPath, cancellationToken)
                ?? throw new InvalidDataException("The pending run-detail journal is empty.");
            ValidateExistingRunDetailCommitJournal(journal);
            writeSet = FileSandboxWorkspaceWriteSet.Run(journal.PersistencePlan.PreviousDetail.Run,
                journal.PersistencePlan.TargetDetail.Run, readsAgent: false);
        } else {
            var journal = await jsonStore.ReadJsonAsync<AgentDeletionCommitJournal>(PendingAgentDeletionCommitJournalPath, cancellationToken)
                ?? throw new InvalidDataException("The pending Agent deletion journal is empty.");
            ValidateAgentDeletionCommitJournal(journal);
            writeSet = FileSandboxWorkspaceWriteSet.Deletion(journal);
        }
        await using var publication = await operation.AcquireAsync(writeSet, witness, cancellationToken);
        try {
            await RecoverPendingChatBackedRunCommitAsync(cancellationToken);
            await RecoverPendingGenericNewRunCommitAsync(cancellationToken);
            await RecoverPendingExistingRunDetailCommitAsync(cancellationToken);
            await RecoverPendingAgentDeletionAsync(operation, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        var normalizedDocument = NormalizeAndValidateDocument(SandboxWorkspaceDocument.Combine(
            await LoadNormalizedCatalogCoreAsync(operation, cancellationToken),
            await LoadExecutionCoreAsync(cancellationToken)));
        var index = await LoadWorkspaceIndexCoreAsync(cancellationToken);
        return new SandboxWorkspaceDocumentSnapshot(normalizedDocument, index.Revision);
    }

    private async Task<SandboxWorkspaceDocumentSnapshot> LoadMutationSnapshotCoreAsync(
        CancellationToken cancellationToken)
    {
        var normalizedDocument = NormalizeAndValidateDocument(SandboxWorkspaceDocument.Combine(
            await LoadCatalogCoreAsync(cancellationToken),
            await LoadExecutionCoreAsync(cancellationToken)));
        var index = await LoadWorkspaceIndexCoreAsync(cancellationToken);
        return new SandboxWorkspaceDocumentSnapshot(normalizedDocument, index.Revision);
    }

    private async Task EnsureSplitFilesCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(operation, cancellationToken);

        if (!File.Exists(layout.CatalogPath) &&
            !File.Exists(layout.LegacyExecutionPath) &&
            !executionSliceStore.ExecutionStorageExists())
        {
            var seeded = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create());
            await SaveCoreAsync(operation, SandboxWorkspaceDocument.Empty, seeded, currentRevision: 0L, cancellationToken);
            return;
        }

        if (executionSliceStore.ExecutionStorageExists())
        {
            if (!File.Exists(layout.CatalogPath))
            {
                var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
                await SaveCatalogCoreAsync(operation, seededCatalog, cancellationToken);
            }

            await EnsureWorkspaceIndexCoreAsync(cancellationToken);
            return;
        }

        if (File.Exists(layout.CatalogPath) && !File.Exists(layout.LegacyExecutionPath))
        {
            var legacyDocument = await TryLoadLegacyWorkspaceDocumentAsync(cancellationToken);
            if (legacyDocument is not null)
            {
                var normalized = SandboxWorkspaceSeedFactory.Normalize(legacyDocument);
                await SaveCoreAsync(operation, SandboxWorkspaceDocument.Empty, normalized, currentRevision: 0L, cancellationToken);
                return;
            }
        }

        if (!File.Exists(layout.CatalogPath))
        {
            var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
            await SaveCatalogCoreAsync(operation, seededCatalog, cancellationToken);
        }

        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        var executionState = await executionSliceStore.TryLoadLegacyExecutionStateAsync(cancellationToken)
            ?? new SandboxWorkspaceExecutionState(
                Version: catalog.Version,
                ChatSessions: [],
                ExecutionLog: [],
                Metrics: []);

        await SaveExecutionCoreAsync(operation,
            SandboxWorkspaceExecutionState.Empty,
            SandboxWorkspaceSeedFactory.NormalizeExecutionState(executionState),
            cancellationToken);
        await EnsureWorkspaceIndexCoreAsync(cancellationToken);
        await PersistNormalizedWorkspaceDocumentCoreAsync(operation, cancellationToken);
    }

    private async Task EnsureCatalogReadCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(operation, cancellationToken);

        if (executionSliceStore.ExecutionStorageExists())
        {
            if (!File.Exists(layout.CatalogPath))
            {
                var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
                await SaveCatalogCoreAsync(operation, seededCatalog, cancellationToken);
            }

            await EnsureWorkspaceIndexCoreAsync(cancellationToken);
            return;
        }

        await EnsureSplitFilesCoreAsync(operation, cancellationToken);
    }

    private async Task EnsureExecutionSummaryReadCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(operation, cancellationToken);

        if (executionSliceStore.ExecutionStorageExists())
        {
            await EnsureWorkspaceIndexCoreAsync(cancellationToken);
            return;
        }

        await EnsureSplitFilesCoreAsync(operation, cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> LoadCatalogCoreAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.CatalogPath))
        {
            return SandboxWorkspaceCatalog.Empty;
        }

        return await jsonStore.ReadJsonAsync<SandboxWorkspaceCatalog>(layout.CatalogPath, cancellationToken)
            ?? SandboxWorkspaceCatalog.Empty;
    }

    private async Task<SandboxWorkspaceCatalog> LoadCatalogWithoutWorkspaceLockAsync(CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        if (catalogNormalizer.TryNormalizeUnchanged(catalog, out var normalizedCatalog))
        {
            return normalizedCatalog;
        }

        return await PersistNormalizedCatalogReadAsync(cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> LoadNormalizedCatalogCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        var catalogSave = await SaveCatalogCoreAsync(operation,
            catalog,
            File.Exists(layout.CatalogPath),
            catalog,
            cancellationToken);
        if (catalogSave.Changed)
        {
            var workspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
            await SaveWorkspaceIndexCoreAsync(
                new WorkspaceStorageIndex(
                    Revision: workspaceIndex.Revision + 1L,
                    UpdatedAtUtc: DateTimeOffset.UtcNow),
                cancellationToken);
        }

        return catalogSave.Catalog;
    }

    private async Task<SandboxWorkspaceCatalog> PersistNormalizedCatalogReadAsync(
        CancellationToken cancellationToken)
    {
        await using var operation = await FileSandboxWorkspacePublicationScope.EnterAsync(gate, crossProcessLock, layout, runLeaseWriter, cancellationToken);
        try {
            await EnsureCatalogReadCoreAsync(operation, cancellationToken);
            return await LoadNormalizedCatalogCoreAsync(operation, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private Task<SandboxWorkspaceExecutionState> LoadExecutionCoreAsync(CancellationToken cancellationToken)
        => executionSliceStore.LoadAsync(cancellationToken);

    private Task<AgentUsageProjection> LoadUsageProjectionCoreAsync(CancellationToken cancellationToken)
        => executionSliceStore.LoadUsageProjectionAsync(cancellationToken);

    private async Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryCoreAsync(CancellationToken cancellationToken)
    {
        var executionIndex = await executionSliceStore.LoadIndexAsync(cancellationToken);

        return new SandboxWorkspaceExecutionSummary(
            SessionCount: executionIndex.SessionCount,
            ActiveRuns: executionIndex.ActiveRunCount,
            FailedRuns: executionIndex.FailedRunCount);
    }

    private async Task<CatalogSaveResult> SaveCatalogCoreAsync(FileSandboxWorkspacePublicationScope operation,
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken)
    {
        var catalogExists = File.Exists(layout.CatalogPath);
        var currentCatalog = await LoadCatalogCoreAsync(cancellationToken);
        return await SaveCatalogCoreAsync(operation,
            currentCatalog,
            catalogExists,
            catalog,
            cancellationToken);
    }

    private async Task<CatalogSaveResult> SaveCatalogCoreAsync(FileSandboxWorkspacePublicationScope operation,
        SandboxWorkspaceCatalog currentCatalog,
        bool catalogExists,
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken,
        Action<SandboxWorkspaceCatalog>? validatePublication = null,
        AgentPolicyPublication? policyPublication = null,
        CatalogPublicationDependencies dependencies = CatalogPublicationDependencies.WholeWorkspace)
    {
        var prepared = await PrepareCatalogCoreAsync(currentCatalog, catalogExists, catalog, cancellationToken, policyPublication);
        if (!prepared.Changed && validatePublication is null) {
            return prepared;
        }
        var witness = dependencies == CatalogPublicationDependencies.CatalogOnly
            ? await FileSandboxWorkspaceMutationWitness.CaptureAsync(layout,
                PendingCommitJournalPaths.Append(layout.CatalogPath), [], PendingCommitJournalPaths,
                physicalPathPolicyFactory, cancellationToken, pendingPublicationTargets: SharedPublicationPaths)
            : await CapturePublicationWitnessAsync(cancellationToken);
        await using var publication = await operation.AcquireAsync(
            FileSandboxWorkspaceWriteSet.Catalog(currentCatalog, prepared.Catalog), witness, cancellationToken);
        try {
            var final = await PrepareCatalogCoreAsync(currentCatalog, catalogExists, prepared.Catalog, cancellationToken, policyPublication);
            if (final.Changed != prepared.Changed || !HasSamePayload(final.Catalog, prepared.Catalog)) {
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.WriteSetChanged);
            }
            validatePublication?.Invoke(prepared.Catalog);
            if (prepared.Changed) {
                policyPublication?.EnterWrite(prepared.Catalog);
                await jsonStore.WriteJsonAtomicallyAsync(layout.CatalogPath, prepared.Catalog, cancellationToken);
                policyPublication?.Acknowledge();
            }
            return prepared;
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private enum CatalogPublicationDependencies { WholeWorkspace, CatalogOnly }

    private async Task<CatalogSaveResult> PrepareCatalogCoreAsync(SandboxWorkspaceCatalog currentCatalog,
        bool catalogExists, SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken,
        AgentPolicyPublication? policyPublication = null) {
        _ = catalogNormalizer.TryNormalizeUnchanged(catalog, out var normalized);
        var normalizedCatalog = FileSandboxWorkspacePreparedValues.Freeze(normalized);
        if (catalogMutationPolicy is not null) {
            normalizedCatalog = await catalogMutationPolicy.ApplyAsync(
                FileSandboxWorkspacePreparedValues.Freeze(currentCatalog), normalizedCatalog, cancellationToken);
            SandboxWorkspaceDocumentInvariantValidator.Validate(
                SandboxWorkspaceDocument.Combine(normalizedCatalog, SandboxWorkspaceExecutionState.Empty));
        }
        normalizedCatalog = normalizedCatalog with {
            Agents = AgentDecisionDataPolicyMetadata.Preserve(currentCatalog.Agents, normalizedCatalog.Agents),
            AgentDecisionDataPolicyReceipts = currentCatalog.AgentDecisionDataPolicyReceipts
        };
        if (policyPublication is not null) {
            normalizedCatalog = policyPublication.Apply(normalizedCatalog);
        }
        var payloadChanged = !catalogExists ||
                             catalogNormalizer.RequiresSave(currentCatalog, normalizedCatalog);
        var revision = currentCatalog.CatalogDataRevision.IsAssigned
            ? payloadChanged
                ? currentCatalog.CatalogDataRevision.Next()
                : currentCatalog.CatalogDataRevision
            : CatalogDataRevision.Initial;
        var savedCatalog = FileSandboxWorkspacePreparedValues.Freeze(normalizedCatalog) with
        {
            CatalogDataRevision = revision
        };
        var changed = !catalogExists ||
                      jsonStore.RequiresSave(currentCatalog, savedCatalog);
        return new CatalogSaveResult(savedCatalog, changed);
    }

    private async Task<bool> SaveExecutionCoreAsync(FileSandboxWorkspacePublicationScope operation,
        SandboxWorkspaceExecutionState previousExecutionState,
        SandboxWorkspaceExecutionState executionState,
        CancellationToken cancellationToken) {
        executionState = FileSandboxWorkspacePreparedValues.Freeze(executionState);
        var witness = await CapturePublicationWitnessAsync(cancellationToken, directories: [layout.ExecutionStorageRoot]);
        await using var publication = await operation.AcquireAsync(FileSandboxWorkspaceWriteSet.Workspace(
            SandboxWorkspaceDocument.Combine(SandboxWorkspaceCatalog.Empty, previousExecutionState),
            SandboxWorkspaceDocument.Combine(SandboxWorkspaceCatalog.Empty, executionState)), witness, cancellationToken);
        try {
            return await executionSliceStore.SaveAsync(previousExecutionState, executionState, cancellationToken);
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task EnsureWorkspaceIndexCoreAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(layout.WorkspaceIndexPath))
        {
            return;
        }

        var executionIndex = await jsonStore.ReadJsonAsync<ExecutionStorageIndex>(layout.ExecutionIndexPath, cancellationToken);
        await SaveWorkspaceIndexCoreAsync(
            new WorkspaceStorageIndex(
                Revision: executionIndex?.Revision ?? 1L,
                UpdatedAtUtc: executionIndex?.UpdatedAtUtc ?? DateTimeOffset.UtcNow),
            cancellationToken);
    }

    private async Task<WorkspaceStorageIndex> LoadWorkspaceIndexCoreAsync(CancellationToken cancellationToken)
    {
        return await jsonStore.ReadJsonAsync<WorkspaceStorageIndex>(layout.WorkspaceIndexPath, cancellationToken)
            ?? new WorkspaceStorageIndex(1L, DateTimeOffset.UtcNow);
    }

    private async Task PersistNormalizedWorkspaceDocumentCoreAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        var currentDocument = SandboxWorkspaceDocument.Combine(
            await LoadCatalogCoreAsync(cancellationToken),
            await LoadExecutionCoreAsync(cancellationToken));
        var normalizedDocument = NormalizeAndValidateDocument(currentDocument);
        if (EqualityComparer<SandboxWorkspaceDocument>.Default.Equals(currentDocument, normalizedDocument))
        {
            return;
        }

        var currentRevision = File.Exists(layout.WorkspaceIndexPath)
            ? (await LoadWorkspaceIndexCoreAsync(cancellationToken)).Revision
            : 0L;
        await SaveCoreAsync(operation, currentDocument, normalizedDocument, currentRevision, cancellationToken);
    }

    private Task SaveWorkspaceIndexCoreAsync(WorkspaceStorageIndex index, CancellationToken cancellationToken)
        => jsonStore.WriteJsonAtomicallyAsync(layout.WorkspaceIndexPath, index, cancellationToken);

    private async Task<ChatSessionRecord> SaveChatSessionCoreAsync(FileSandboxWorkspacePublicationScope operation,
        ChatSessionRecord? previousSession,
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        var normalizedSession = FileSandboxWorkspacePreparedValues.Freeze(NormalizeChatSession(session));
        var witness = await CapturePublicationWitnessAsync(cancellationToken, [layout.SessionPath(normalizedSession.Id)]);
        await using var publication = await operation.AcquireAsync(
            FileSandboxWorkspaceWriteSet.Session(previousSession, normalizedSession), witness, cancellationToken);
        try {
            var sessionExistedBefore = previousSession is not null;
            var changed = await jsonStore.WriteJsonIfChangedAsync(
                layout.SessionPath(normalizedSession.Id),
                normalizedSession,
                cancellationToken);

            var currentIndex = await executionSliceStore.LoadIndexAsync(cancellationToken);
            var nextIndex = currentIndex with
            {
                Revision = changed ? currentIndex.Revision + 1L : currentIndex.Revision,
                UpdatedAtUtc = changed ? DateTimeOffset.UtcNow : currentIndex.UpdatedAtUtc,
                SessionCount = currentIndex.SessionCount + (sessionExistedBefore ? 0 : 1)
            };

            if (changed || !File.Exists(layout.ExecutionIndexPath) || jsonStore.RequiresSave(currentIndex, nextIndex))
            {
                await jsonStore.WriteJsonAtomicallyAsync(layout.ExecutionIndexPath, nextIndex, cancellationToken);
            }

            var workspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
            var nextWorkspaceIndex = new WorkspaceStorageIndex(
                Revision: changed ? workspaceIndex.Revision + 1L : workspaceIndex.Revision,
                UpdatedAtUtc: nextIndex.UpdatedAtUtc);
            if (changed || !File.Exists(layout.WorkspaceIndexPath) || jsonStore.RequiresSave(workspaceIndex, nextWorkspaceIndex))
            {
                await SaveWorkspaceIndexCoreAsync(nextWorkspaceIndex, cancellationToken);
            }

            await chatProjectionStore.SaveSessionAsync(previousSession, normalizedSession, nextIndex, cancellationToken);
            if (changed) {
                await executionSliceStore.AdvanceUsageProjectionForSessionChangeAsync(
                    currentIndex,
                    nextIndex,
                    cancellationToken);
            }
            return normalizedSession;
        } catch (Exception failure) {
            operation.RetainFailure(failure);
            throw;
        }
    }

    private async Task<long> ResolveCurrentRevisionAsync(FileSandboxWorkspacePublicationScope operation, CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.CatalogPath) &&
            !File.Exists(layout.LegacyExecutionPath) &&
            !executionSliceStore.ExecutionStorageExists())
        {
            return 0L;
        }

        await EnsureSplitFilesCoreAsync(operation, cancellationToken);
        return (await LoadWorkspaceIndexCoreAsync(cancellationToken)).Revision;
    }

    private bool CanReadCatalogWithoutWorkspaceLock()
    {
        return File.Exists(layout.CatalogPath) &&
               File.Exists(layout.WorkspaceIndexPath) &&
               !HasPendingAgentDeletionCommit;
    }

    private Task<FileSandboxWorkspaceMutationWitness> CapturePublicationWitnessAsync(CancellationToken cancellationToken,
        IEnumerable<string>? paths = null, IEnumerable<string>? directories = null) =>
        FileSandboxWorkspaceMutationWitness.CaptureAsync(layout,
            SharedPublicationPaths.Concat(paths ?? []), directories ?? [], PendingCommitJournalPaths,
            physicalPathPolicyFactory, cancellationToken);

    private string[] PendingCommitJournalPaths =>
        [PendingChatBackedRunCommitJournalPath, PendingGenericNewRunCommitJournalPath,
            PendingExistingRunDetailCommitJournalPath, PendingAgentDeletionCommitJournalPath];

    private IEnumerable<string> SharedPublicationPaths =>
        new[] { layout.CatalogPath, layout.WorkspaceIndexPath, layout.LegacyExecutionPath,
            layout.ExecutionIndexPath, layout.ExecutionChatIndexPath, layout.ExecutionProcessIndexPath, layout.ExecutionUsageIndexPath }
            .Concat(PendingCommitJournalPaths);

    private string PendingChatBackedRunCommitJournalPath
        => Path.Combine(
            layout.ExecutionStorageRoot,
            "pending-chat-run-start.json");

    private bool HasPendingChatBackedRunCommit
        => File.Exists(PendingChatBackedRunCommitJournalPath);

    private string PendingGenericNewRunCommitJournalPath
        => Path.Combine(
            layout.ExecutionStorageRoot,
            "pending-run-start.json");

    private bool HasPendingGenericNewRunCommit
        => File.Exists(PendingGenericNewRunCommitJournalPath);

    private string PendingExistingRunDetailCommitJournalPath
        => Path.Combine(
            layout.ExecutionStorageRoot,
            "pending-run-detail-update.json");

    private bool HasPendingExistingRunDetailCommit
        => File.Exists(PendingExistingRunDetailCommitJournalPath);

    private bool HasSamePayload<T>(T left, T right)
    {
        return !jsonStore.RequiresSave(left, right);
    }

    private void NotifyChatBackedRunCommitBoundary(
        ChatBackedRunCommitStage stage)
    {
        chatBackedRunCommitBoundary?.Invoke(stage);
    }

    private void NotifyGenericNewRunCommitBoundary(
        GenericNewRunCommitStage stage)
    {
        genericNewRunCommitBoundary?.Invoke(stage);
    }

    private void NotifyExistingRunDetailCommitBoundary(
        ExistingRunDetailCommitStage stage)
    {
        existingRunDetailCommitBoundary?.Invoke(stage);
    }

    private void ValidateExistingRunDetailCommitJournal(
        ExistingRunDetailCommitJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.PersistencePlan is null ||
            journal.ChatProjectionPlan is null ||
            journal.PreviousWorkspaceIndex is null ||
            journal.TargetWorkspaceIndex is null)
        {
            throw new InvalidDataException(
                "A pending execution-run update journal is incomplete.");
        }

        if (!string.Equals(
                journal.Version,
                ExistingRunDetailCommitJournalVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{journal.RunId:N}' uses unsupported journal version '{journal.Version}'.");
        }

        executionSliceStore.ValidateExistingRunUpdatePlan(
            journal.PersistencePlan);
        if (journal.RunId == Guid.Empty ||
            journal.RunId != journal.PersistencePlan.TargetDetail.Run.Id)
        {
            throw new InvalidDataException(
                "A pending execution-run update has an invalid execution-run identity.");
        }

        ValidateExecutionRunDetailConsistency(
            journal.PersistencePlan.PreviousDetail);
        ValidateExecutionRunDetailConsistency(
            journal.PersistencePlan.TargetDetail);

        var expectedWorkspaceIndex = new WorkspaceStorageIndex(
            journal.PreviousWorkspaceIndex.Revision + 1L,
            journal.PersistencePlan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(
                expectedWorkspaceIndex,
                journal.TargetWorkspaceIndex))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{journal.RunId:N}' contains an invalid target workspace index.");
        }

        if (journal.ChatProjectionPlan.PreviousIndex.Revision !=
                journal.PersistencePlan.PreviousIndex.Revision ||
            journal.ChatProjectionPlan.TargetIndex.Revision !=
                journal.PersistencePlan.TargetIndex.Revision ||
            !string.Equals(
                journal.ChatProjectionPlan.TargetIndex.Version,
                journal.PersistencePlan.TargetIndex.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending execution-run update '{journal.RunId:N}' contains invalid chat-index revisions.");
        }
    }

    private void ValidateGenericNewRunCommitJournal(
        SandboxWorkspaceCatalog catalog,
        GenericNewRunCommitJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.PersistencePlan is null ||
            journal.ChatProjectionPlan is null ||
            journal.PreviousWorkspaceIndex is null ||
            journal.TargetWorkspaceIndex is null)
        {
            throw new InvalidDataException(
                "A pending generic execution-run creation journal is incomplete.");
        }

        if (!string.Equals(
                journal.Version,
                GenericNewRunCommitJournalVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{journal.RunId:N}' uses unsupported journal version '{journal.Version}'.");
        }

        executionSliceStore.ValidateGenericNewRunPlan(
            journal.PersistencePlan);
        var detail = journal.PersistencePlan.Detail;
        if (journal.RunId == Guid.Empty ||
            journal.RunId != detail.Run.Id)
        {
            throw new InvalidDataException(
                "A pending generic execution-run creation has an invalid execution-run identity.");
        }

        ValidateExecutionRunDetail(catalog, detail);
        var expectedWorkspaceIndex = new WorkspaceStorageIndex(
            journal.PreviousWorkspaceIndex.Revision + 1L,
            journal.PersistencePlan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(
                expectedWorkspaceIndex,
                journal.TargetWorkspaceIndex))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{journal.RunId:N}' contains an invalid target workspace index.");
        }

        if (journal.ChatProjectionPlan.PreviousIndex.Revision !=
                journal.PersistencePlan.PreviousIndex.Revision ||
            journal.ChatProjectionPlan.TargetIndex.Revision !=
                journal.PersistencePlan.TargetIndex.Revision ||
            !string.Equals(
                journal.ChatProjectionPlan.TargetIndex.Version,
                journal.PersistencePlan.TargetIndex.Version,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending generic execution-run creation '{journal.RunId:N}' contains invalid chat-index revisions.");
        }
    }

    private void ValidateChatBackedRunCommitJournal(
        SandboxWorkspaceCatalog catalog,
        ChatBackedRunCommitJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.PersistencePlan is null ||
            journal.PreviousWorkspaceIndex is null ||
            journal.TargetWorkspaceIndex is null)
        {
            throw new InvalidDataException(
                "A pending chat-run transaction journal is incomplete.");
        }

        if (!string.Equals(
                journal.Version,
                ChatBackedRunCommitJournalVersion,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{journal.RunId:N}' uses unsupported journal version '{journal.Version}'.");
        }

        executionSliceStore.ValidateNewRunPlan(journal.PersistencePlan);
        var detail = journal.PersistencePlan.Detail;
        if (journal.RunId == Guid.Empty ||
            journal.RunId != detail.Run.Id)
        {
            throw new InvalidDataException(
                "A pending chat-run transaction has an invalid execution-run identity.");
        }

        ValidateExecutionRunDetail(catalog, detail);
        var matchingUserMessages = detail.ChatSession!.Messages
            .Where(message => message.Id == journal.UserMessageId)
            .ToArray();
        if (journal.UserMessageId == Guid.Empty ||
            matchingUserMessages.Length != 1 ||
            matchingUserMessages[0].Role != ChatMessageRole.User)
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{journal.RunId:N}' does not contain its declared user message.");
        }

        var expectedWorkspaceIndex = new WorkspaceStorageIndex(
            journal.PreviousWorkspaceIndex.Revision + 1L,
            journal.PersistencePlan.TargetIndex.UpdatedAtUtc);
        if (!HasSamePayload(
                expectedWorkspaceIndex,
                journal.TargetWorkspaceIndex))
        {
            throw new InvalidDataException(
                $"Pending chat-run transaction '{journal.RunId:N}' contains an invalid target workspace index.");
        }
    }

    private static SandboxWorkspaceDocument NormalizeAndValidateDocument(SandboxWorkspaceDocument document)
    {
        var normalized = SandboxWorkspaceSeedFactory.Normalize(document);
        SandboxWorkspaceDocumentInvariantValidator.Validate(normalized);
        return normalized;
    }

    private static void ValidateExecutionRunDetail(
        SandboxWorkspaceCatalog catalog,
        ExecutionRunDetail detail)
    {
        if (!catalog.Agents.Any(item => item.Id == detail.Run.AgentId))
        {
            throw new InvalidOperationException(
                $"Execution run '{detail.Run.Id:N}' references missing agent '{detail.Run.AgentId:N}'.");
        }

        ValidateExecutionRunDetailConsistency(detail);
    }

    private static void ValidateExecutionRunDetailConsistency(
        ExecutionRunDetail detail)
    {
        if (detail.Run.Id == Guid.Empty ||
            detail.Run.AgentId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Execution run and agent identities are required.");
        }

        var runAgentIds = new HashSet<Guid>
        {
            detail.Run.AgentId
        };
        if (detail.Run.ChatSessionId.HasValue)
        {
            if (detail.ChatSession is null)
            {
                throw new InvalidOperationException(
                    $"Execution run '{detail.Run.Id:N}' requires chat session '{detail.Run.ChatSessionId.Value:N}'.");
            }

            if (detail.ChatSession.Id != detail.Run.ChatSessionId.Value)
            {
                throw new InvalidOperationException(
                    $"Execution run '{detail.Run.Id:N}' references chat session '{detail.Run.ChatSessionId.Value:N}', but the supplied session was '{detail.ChatSession.Id:N}'.");
            }

            if (detail.ChatSession.AgentId != detail.Run.AgentId)
            {
                throw new InvalidOperationException(
                    $"Execution run '{detail.Run.Id:N}' and chat session '{detail.ChatSession.Id:N}' do not share the same agent.");
            }
        }
        else if (detail.ChatSession is not null)
        {
            throw new InvalidOperationException(
                $"Execution run '{detail.Run.Id:N}' cannot persist a chat session when the run is not chat-backed.");
        }

        foreach (var sessionAgentId in detail.ChatSession is null ? [] : new[] { detail.ChatSession.AgentId })
        {
            if (!runAgentIds.Contains(sessionAgentId))
            {
                throw new InvalidOperationException(
                    $"Chat session '{detail.ChatSession!.Id:N}' does not belong to execution run '{detail.Run.Id:N}'.");
            }
        }

        ValidateAgentScoped(
            detail.ExecutionLog.Select(item =>
                (item.Id, item.AgentId, item.ChatSessionId)),
            detail,
            runAgentIds,
            "Execution log entry");
        ValidateAgentScoped(
            detail.Metrics.Select(item =>
                (item.Id, item.AgentId, item.ChatSessionId)),
            detail,
            runAgentIds,
            "Execution metric");
        ValidateAgentScoped(
            detail.UsageObservations
                .Where(item => item.AgentId.HasValue)
                .Select(item => (item.Id, item.AgentId!.Value, item.ChatSessionId)),
            detail,
            runAgentIds,
            "Provider usage observation");
    }

    private static void ValidateAgentScoped(
        IEnumerable<(Guid RecordId, Guid AgentId, Guid? ChatSessionId)> records,
        ExecutionRunDetail detail,
        IReadOnlySet<Guid> knownAgentIds,
        string label)
    {
        foreach (var record in records)
        {
            if (!knownAgentIds.Contains(record.AgentId))
            {
                throw new InvalidOperationException(
                    $"{label} '{record.RecordId:N}' references missing agent '{record.AgentId:N}'.");
            }

            if (record.AgentId != detail.Run.AgentId)
            {
                throw new InvalidOperationException(
                    $"{label} '{record.RecordId:N}' does not belong to execution run '{detail.Run.Id:N}'.");
            }

            if (record.ChatSessionId != detail.Run.ChatSessionId)
            {
                throw new InvalidOperationException(
                    $"{label} '{record.RecordId:N}' does not match the chat session linked to execution run '{detail.Run.Id:N}'.");
            }
        }
    }

    private static ChatSessionRecord NormalizeChatSession(ChatSessionRecord session)
    {
        var now = DateTimeOffset.UtcNow;
        return session with
        {
            Title = string.IsNullOrWhiteSpace(session.Title) ? "New exploration thread" : session.Title.Trim(),
            CreatedAtUtc = session.CreatedAtUtc == default ? now : session.CreatedAtUtc,
            UpdatedAtUtc = session.UpdatedAtUtc == default ? now : session.UpdatedAtUtc,
            Messages = session.Messages ?? []
        };
    }

    private static void ValidateChatSession(
        SandboxWorkspaceCatalog catalog,
        ChatSessionRecord session)
    {
        if (session.Id == Guid.Empty)
        {
            throw new InvalidOperationException("Chat session id is required.");
        }

        if (session.AgentId == Guid.Empty)
        {
            throw new InvalidOperationException("Chat session agent id is required.");
        }

        if (!catalog.Agents.Any(agent => agent.Id == session.AgentId))
        {
            throw new InvalidOperationException($"Chat session '{session.Id:N}' references missing agent '{session.AgentId:N}'.");
        }
    }

    private async Task<ChatSessionRecord?> LoadChatRunStartSessionCoreAsync(
        ChatBackedRunStartRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.ChatSessionId.HasValue)
        {
            return null;
        }

        if (request.ChatSessionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "A selected chat session identifier cannot be empty.",
                nameof(request));
        }

        var session = await chatProjectionStore.GetChatSessionAsync(
            request.ChatSessionId.Value,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Chat session '{request.ChatSessionId.Value:N}' was not found.");
        if (session.AgentId != request.AgentId)
        {
            throw new InvalidOperationException(
                $"Chat session '{session.Id:N}' does not belong to agent '{request.AgentId:N}'.");
        }

        return session;
    }

    private async Task<ExecutionRunRecord?> LoadBlockingSessionRunAsync(
        Guid agentId,
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        if (session.LatestExecutionRunId is { } latestExecutionRunId)
        {
            var latestRun = await executionSliceStore.LoadRunAsync(
                latestExecutionRunId,
                cancellationToken);
            if (latestRun is not null &&
                latestRun.AgentId == agentId &&
                latestRun.ChatSessionId == session.Id &&
                ExecutionRunSessionConcurrencyPolicy.BlocksSession(latestRun))
            {
                return latestRun;
            }
        }

        var summaries = await chatProjectionStore.ListChatRunSummariesAsync(
            agentId,
            session.Id,
            cancellationToken);
        var blockingCandidates = summaries
            .Where(summary =>
                summary.ExecutionRunId !=
                    session.LatestExecutionRunId &&
                summary.ChatSessionId == session.Id &&
                ExecutionRunSessionConcurrencyPolicy.BlocksSession(
                    summary.State))
            .OrderByDescending(summary => summary.UpdatedAtUtc)
            .Select(summary => summary.ExecutionRunId)
            .Distinct()
            .Take(2)
            .ToArray();
        if (blockingCandidates.Length > 1)
        {
            throw new InvalidDataException(
                $"Chat session '{session.Id:N}' has multiple blocking execution summaries. The chat projection must be rebuilt before another run can start.");
        }

        if (blockingCandidates.Length == 0)
        {
            return null;
        }

        var blockingRunId = blockingCandidates[0];
        var candidate = await executionSliceStore.LoadRunAsync(
            blockingRunId,
            cancellationToken);
        return candidate is not null &&
               candidate.AgentId == agentId &&
               candidate.ChatSessionId == session.Id &&
               ExecutionRunSessionConcurrencyPolicy.BlocksSession(candidate)
            ? candidate
            : throw new InvalidDataException(
                $"Chat session '{session.Id:N}' contains a stale blocking summary for execution run '{blockingRunId:N}'. The chat projection must be rebuilt before another run can start.");
    }

    private static void ValidateChatBackedRunStartMutation(
        ChatBackedRunStartRequest request,
        ChatBackedRunStartContext context,
        IReadOnlyList<ChatMessageRecord> existingMessages,
        ChatBackedRunStartMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation.Detail);
        ArgumentNullException.ThrowIfNull(mutation.UserMessage);
        if (mutation.UserMessage.Id == Guid.Empty ||
            mutation.UserMessage.Role != ChatMessageRole.User)
        {
            throw new InvalidOperationException(
                "A chat-backed run start requires a new identified user message.");
        }

        if (existingMessages.Any(message => message.Id == mutation.UserMessage.Id))
        {
            throw new InvalidOperationException(
                $"Chat message '{mutation.UserMessage.Id:N}' already exists in the selected session.");
        }

        var session = mutation.Detail.ChatSession
            ?? throw new InvalidOperationException(
                "A chat-backed execution run requires a chat session.");
        if (session.Id == Guid.Empty ||
            session.AgentId != request.AgentId)
        {
            throw new InvalidOperationException(
                "The chat-backed run mutation returned an invalid session identity.");
        }

        if (context.Session is not null &&
            session.Id != context.Session.Id)
        {
            throw new InvalidOperationException(
                $"Chat session '{context.Session.Id:N}' cannot be replaced by '{session.Id:N}' while starting a run.");
        }

        var expectedMessages = existingMessages
            .Append(mutation.UserMessage)
            .ToArray();
        if (!session.Messages.SequenceEqual(expectedMessages))
        {
            throw new InvalidOperationException(
                "The chat-backed run mutation must preserve the current message history and append exactly its new user message.");
        }

        if (mutation.Detail.Run.Id == Guid.Empty ||
            mutation.Detail.Run.AgentId != request.AgentId ||
            mutation.Detail.Run.ChatSessionId != session.Id)
        {
            throw new InvalidOperationException(
                "The chat-backed run mutation returned an invalid execution run identity.");
        }
    }

    private async Task<SandboxWorkspaceDocument?> TryLoadLegacyWorkspaceDocumentAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.CatalogPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(layout.CatalogPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("chatSessions", out _) ||
            !document.RootElement.TryGetProperty("executionLog", out _) ||
            !document.RootElement.TryGetProperty("metrics", out _))
        {
            return null;
        }

        stream.Position = 0;
        return await JsonSerializer.DeserializeAsync<SandboxWorkspaceDocument>(stream, jsonStore.SerializerOptions, cancellationToken);
    }

    private sealed record CatalogSaveResult(
        SandboxWorkspaceCatalog Catalog,
        bool Changed);
}

internal enum ChatBackedRunCommitStage
{
    JournalPersisted,
    ExecutionSlicesPersisted,
    ExecutionIndexesPersisted,
    WorkspaceIndexPersisted,
    ChatProjectionPersisted
}

internal sealed record ChatBackedRunCommitJournal(
    string Version,
    Guid RunId,
    Guid UserMessageId,
    NewExecutionRunPersistencePlan PersistencePlan,
    WorkspaceStorageIndex PreviousWorkspaceIndex,
    WorkspaceStorageIndex TargetWorkspaceIndex);

internal enum GenericNewRunCommitStage
{
    JournalPersisted,
    ExecutionSlicesPersisted,
    ExecutionIndexPersisted,
    UsageIndexPersisted,
    WorkspaceIndexPersisted,
    ChatIndexPersisted
}

internal sealed record GenericNewRunCommitJournal(
    string Version,
    Guid RunId,
    GenericNewExecutionRunPersistencePlan PersistencePlan,
    GenericNewExecutionRunChatProjectionPlan ChatProjectionPlan,
    WorkspaceStorageIndex PreviousWorkspaceIndex,
    WorkspaceStorageIndex TargetWorkspaceIndex);

internal enum ExistingRunDetailCommitOrigin {
    Prepared,
    RecoveredJournal
}

internal enum ExistingRunDetailCommitStage
{
    JournalPersisted,
    SessionPersisted,
    RunPersisted,
    ApprovalRecordsPersisted,
    RemainingRecordsPersisted,
    ExecutionIndexPersisted,
    UsageIndexPersisted,
    WorkspaceIndexPersisted,
    ChatIndexPersisted
}

internal sealed record ExistingRunDetailCommitJournal(
    string Version,
    Guid RunId,
    ExistingExecutionRunPersistencePlan PersistencePlan,
    ExistingExecutionRunChatProjectionPlan ChatProjectionPlan,
    WorkspaceStorageIndex PreviousWorkspaceIndex,
    WorkspaceStorageIndex TargetWorkspaceIndex);
