using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore :
    ISandboxWorkspaceStore,
    IAgentProviderUsageEvidenceStore,
    ISandboxWorkspaceChatQueryStore,
    ISandboxWorkspaceChatProjectionQueryStore,
    ISandboxWorkspaceChatSessionStore,
    ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceChatRunStartStore,
    ISandboxWorkspaceExecutionRunMutationStore,
    ISandboxWorkspaceExecutionRunReservationStore,
    IAgentRecruitingEvidenceStore
{
    private const string ChatBackedRunCommitJournalVersion = "1.0";
    private const string GenericNewRunCommitJournalVersion = "1.0";
    private const string ExistingRunDetailCommitJournalVersion = "1.0";
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileSandboxWorkspaceJsonStore jsonStore;
    private readonly FileSandboxWorkspaceExecutionSliceStore executionSliceStore;
    private readonly FileSandboxWorkspaceChatProjectionStore chatProjectionStore;
    private readonly FileSandboxWorkspaceCrossProcessLock crossProcessLock;
    private readonly Action<ChatBackedRunCommitStage>? chatBackedRunCommitBoundary;
    private readonly Action<GenericNewRunCommitStage>? genericNewRunCommitBoundary;
    private readonly Action<ExistingRunDetailCommitStage>? existingRunDetailCommitBoundary;
    private readonly Action<AgentDeletionCommitStage>? agentDeletionCommitBoundary;

    public FileSandboxWorkspaceStore(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
        : this(
            workspaceRoot,
            workspaceScope,
            chatBackedRunCommitBoundary: null,
            existingRunDetailCommitBoundary: null)
    {
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
        Action<FileHistoryCommitStage>? historyCommitBoundary = null)
    {
        layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        var physicalPathPolicyFactory = new PhysicalFileSystemPathPolicyFactory();
        var durableFileWriter = new DurableFileWriter(physicalPathPolicyFactory);
        jsonStore = new FileSandboxWorkspaceJsonStore(
            jsonReadDiagnostics,
            physicalPathPolicyFactory,
            durableFileWriter,
            layout.RootPath,
            new FileProviderHistoryJournal(layout, historyCommitBoundary));
        executionSliceStore = new FileSandboxWorkspaceExecutionSliceStore(layout, jsonStore);
        chatProjectionStore = new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
        crossProcessLock = new FileSandboxWorkspaceCrossProcessLock(
            layout.RootPath,
            layout.WorkspaceLockPath,
            durableFileWriter);
        this.chatBackedRunCommitBoundary = chatBackedRunCommitBoundary;
        this.genericNewRunCommitBoundary = genericNewRunCommitBoundary;
        this.existingRunDetailCommitBoundary = existingRunDetailCommitBoundary;
        this.agentDeletionCommitBoundary = agentDeletionCommitBoundary;
    }

    public async Task<SandboxWorkspaceDocument> LoadAsync(CancellationToken cancellationToken = default)
        => (await LoadSnapshotAsync(cancellationToken)).Document;

    public async Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await LoadSnapshotCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceCatalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (CanReadCatalogWithoutWorkspaceLock())
        {
            return await LoadCatalogWithoutWorkspaceLockAsync(cancellationToken);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureCatalogReadCoreAsync(cancellationToken);
            return await LoadNormalizedCatalogCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceCatalogSnapshot> LoadCatalogSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureCatalogReadCoreAsync(cancellationToken);
            var catalog = await LoadNormalizedCatalogCoreAsync(cancellationToken);
            return new SandboxWorkspaceCatalogSnapshot(
                catalog,
                catalog.CatalogDataRevision);
        }
        finally
        {
            gate.Release();
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

    public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        return await UpdateCatalogOnlyAsync(update, expectedRevision, cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> SaveCatalogOnlyAsync(
        SandboxWorkspaceCatalog catalog,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        return await UpdateCatalogOnlyAsync(
            _ => catalog,
            expectedRevision,
            cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> UpdateCatalogOnlyAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureCatalogReadCoreAsync(cancellationToken);
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
            var updatedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(update(currentForUpdate));
            SandboxWorkspaceDocumentInvariantValidator.Validate(
                SandboxWorkspaceDocument.Combine(updatedCatalog, SandboxWorkspaceExecutionState.Empty));

            var catalogSave = await SaveCatalogCoreAsync(
                currentCatalog,
                File.Exists(layout.CatalogPath),
                updatedCatalog,
                cancellationToken);
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
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return (await LoadSnapshotCoreAsync(cancellationToken)).Document.ToExecutionState();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceExecutionSummary> LoadExecutionSummaryAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureExecutionSummaryReadCoreAsync(cancellationToken);
            return await LoadExecutionSummaryCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AgentUsageProjection> LoadUsageProjectionAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureExecutionSummaryReadCoreAsync(cancellationToken);
            return await LoadUsageProjectionCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<AgentProviderUsageEvidence> LoadProviderUsageEvidenceAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureExecutionSummaryReadCoreAsync(cancellationToken);
            return await executionSliceStore.LoadProviderUsageEvidenceAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveExecutionAsync(SandboxWorkspaceExecutionState executionState, CancellationToken cancellationToken = default)
    {
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
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await executionSliceStore.LoadRunDetailAsync(executionRunId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await executionSliceStore.ListRunsAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionRunRecord?> GetExecutionRunAsync(
        Guid executionRunId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await executionSliceStore.LoadRunAsync(executionRunId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionRunDetail> SaveExecutionRunDetailAsync(
        ExecutionRunDetail detail,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detail);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

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

            return await SaveExecutionRunDetailCoreAsync(
                previousDetail,
                detail,
                validatedCatalog,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatBackedRunStartResult> BeginChatBackedRunAsync(
        ChatBackedRunStartRequest request,
        Func<ChatBackedRunStartContext, ChatBackedRunStartMutation> create,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(create);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            var catalog = await LoadNormalizedCatalogCoreAsync(cancellationToken);
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

            var context = new ChatBackedRunStartContext(
                catalogSnapshot,
                agent,
                sessionSnapshot);
            var mutation = create(context)
                ?? throw new InvalidOperationException(
                    "The chat-backed run start factory returned no mutation.");

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
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionRunSourceReservationResult> ReserveExecutionRunAsync(
        ExecutionRunSourceKey source,
        ExecutionRunDetail candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(candidate);
        if (!source.Matches(candidate.Run))
        {
            throw new InvalidOperationException(
                "The candidate execution run does not match the requested source key.");
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

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
            var persisted = await SaveExecutionRunDetailCoreAsync(
                previousDetail: null,
                candidate,
                catalog,
                cancellationToken);
            return new ExecutionRunSourceReservationResult(
                ExecutionRunSourceDisposition.Created,
                persisted.Run);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock =
                await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            var previousDetail = await executionSliceStore
                .LoadRunDetailAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Execution run '{executionRunId:N}' was not found.");
            var updatedDetail = update(previousDetail)
                ?? throw new InvalidOperationException(
                    "Execution run update cannot return null.");
            if (updatedDetail.Run.Id != executionRunId)
            {
                throw new InvalidOperationException(
                    "Execution run update cannot change the run identity.");
            }

            if (ReferenceEquals(previousDetail, updatedDetail))
            {
                return previousDetail;
            }

            ValidateExecutionRunDetailConsistency(updatedDetail);
            return await SaveExecutionRunDetailCoreAsync(
                previousDetail,
                updatedDetail,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(
        Guid executionRunId,
        Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            var previousDetail = await executionSliceStore.LoadRunDetailAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException($"Execution run '{executionRunId:N}' was not found.");
            var updatedDetail = update(catalog, previousDetail)
                ?? throw new InvalidOperationException("Execution run update cannot return null.");
            if (updatedDetail.Run.Id != executionRunId)
            {
                throw new InvalidOperationException("Execution run update cannot change the run identity.");
            }

            if (ReferenceEquals(previousDetail, updatedDetail))
            {
                return previousDetail;
            }

            ValidateExecutionRunDetail(catalog, updatedDetail);
            return await SaveExecutionRunDetailCoreAsync(previousDetail, updatedDetail, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceDocument> SaveAsync(
        SandboxWorkspaceDocument document,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            var normalizedDocument = NormalizeAndValidateDocument(document);
            var currentRevision = await ResolveCurrentRevisionAsync(cancellationToken);
            var previousDocument = currentRevision == 0L
                ? SandboxWorkspaceDocument.Empty
                : (await LoadMutationSnapshotCoreAsync(cancellationToken)).Document;
            return await SaveCoreAsync(
                previousDocument,
                normalizedDocument,
                currentRevision,
                cancellationToken);
        }
        finally
        {
            gate.Release();
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
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await chatProjectionStore.ListChatSessionSummariesAsync(agentId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatWorkspaceProjectionSnapshot> LoadChatWorkspaceProjectionAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await chatProjectionStore.LoadChatWorkspaceProjectionAsync(agentId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatSessionRecord?> GetChatSessionAsync(
        Guid chatSessionId,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await chatProjectionStore.GetChatSessionAsync(chatSessionId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyList<ChatRunSummaryRecord>> ListChatRunSummariesAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await chatProjectionStore.ListChatRunSummariesAsync(agentId, chatSessionId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatRuntimeSnapshot> LoadChatRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId = null,
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            return await chatProjectionStore.LoadChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatSessionRecord> CreateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateChatSession(catalog, session);

            var existingSession = await chatProjectionStore.GetChatSessionAsync(session.Id, cancellationToken);
            if (existingSession is not null)
            {
                throw new InvalidOperationException($"Chat session '{session.Id:N}' already exists.");
            }

            return await SaveChatSessionCoreAsync(previousSession: null, session, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ChatSessionRecord> UpdateChatSessionAsync(
        ChatSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateChatSession(catalog, session);

            var existingSession = await chatProjectionStore.GetChatSessionAsync(session.Id, cancellationToken)
                                  ?? throw new InvalidOperationException($"Chat session '{session.Id:N}' was not found.");
            if (existingSession.AgentId != session.AgentId)
            {
                throw new InvalidOperationException($"Chat session '{session.Id:N}' cannot be moved to a different agent.");
            }

            return await SaveChatSessionCoreAsync(existingSession, session, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SandboxWorkspaceDocument> UpdateWorkspaceCoreAsync(
        Func<SandboxWorkspaceDocument, SandboxWorkspaceDocument> update,
        long? expectedRevision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            var current = await LoadMutationSnapshotCoreAsync(cancellationToken);
            if (expectedRevision.HasValue && current.Revision != expectedRevision.Value)
            {
                throw new SandboxWorkspaceConcurrencyException(expectedRevision.Value, current.Revision);
            }

            var updated = NormalizeAndValidateDocument(update(current.Document));
            return await SaveCoreAsync(
                current.Document,
                updated,
                current.Revision,
                cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<SandboxWorkspaceDocument> SaveCoreAsync(
        SandboxWorkspaceDocument previousDocument,
        SandboxWorkspaceDocument document,
        long currentRevision,
        CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

        var catalogSave = await SaveCatalogCoreAsync(document.ToCatalog(), cancellationToken);
        var executionChanged = await SaveExecutionCoreAsync(previousDocument.ToExecutionState(), document.ToExecutionState(), cancellationToken);
        var workspaceChanged = catalogSave.Changed || executionChanged || !File.Exists(layout.WorkspaceIndexPath);
        if (workspaceChanged)
        {
            await SaveWorkspaceIndexCoreAsync(
                new WorkspaceStorageIndex(
                    Revision: Math.Max(1L, currentRevision + 1L),
                    UpdatedAtUtc: DateTimeOffset.UtcNow),
                cancellationToken);
        }

        return SandboxWorkspaceDocument.Combine(
            catalogSave.Catalog,
            document.ToExecutionState());
    }

    private async Task<ExecutionRunDetail> SaveExecutionRunDetailCoreAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        return await SaveExecutionRunDetailCoreAsync(
            previousDetail,
            detail,
            validatedCatalog: null,
            cancellationToken);
    }

    private async Task<ExecutionRunDetail> SaveExecutionRunDetailCoreAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        SandboxWorkspaceCatalog? validatedCatalog,
        CancellationToken cancellationToken)
    {
        if (previousDetail is not null)
        {
            if (executionSliceStore.HasSameRunDetailPayload(
                    previousDetail,
                    detail))
            {
                return previousDetail;
            }

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
                CancellationToken.None);
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
    }

    private async Task<ExecutionRunDetail> CommitExistingRunDetailJournalAsync(
        ExistingRunDetailCommitJournal journal,
        ExistingRunDetailCommitOrigin origin,
        CancellationToken cancellationToken) {
        ValidateExistingRunDetailCommitJournal(journal);

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
                $"Pending execution-run update '{journal.RunId:N}' found an unexpected workspace index.");
        }

        await executionSliceStore.PersistExistingRunSessionAsync(
            journal.PersistencePlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.SessionPersisted);

        await executionSliceStore.PersistExistingRunRecordAsync(
            journal.PersistencePlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.RunPersisted);

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

        await executionSliceStore.PersistExistingRunExecutionIndexAsync(
            journal.PersistencePlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.ExecutionIndexPersisted);

        await executionSliceStore.PersistExistingRunUsageIndexAsync(
            journal.PersistencePlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.UsageIndexPersisted);

        await SaveWorkspaceIndexCoreAsync(
            journal.TargetWorkspaceIndex,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.WorkspaceIndexPersisted);

        await chatProjectionStore.PersistExistingRunUpdateAsync(
            journal.PersistencePlan,
            journal.ChatProjectionPlan,
            origin,
            cancellationToken);
        NotifyExistingRunDetailCommitBoundary(
            ExistingRunDetailCommitStage.ChatIndexPersisted);

        await jsonStore.DeleteFileAsync(PendingExistingRunDetailCommitJournalPath, cancellationToken);
        return journal.PersistencePlan.TargetDetail;
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

    private async Task RecoverPendingExecutionCommitAsync(
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

        await RecoverPendingChatBackedRunCommitAsync(cancellationToken);
        await RecoverPendingGenericNewRunCommitAsync(cancellationToken);
        await RecoverPendingExistingRunDetailCommitAsync(cancellationToken);
        await RecoverPendingAgentDeletionAsync(cancellationToken);
    }

    private async Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotCoreAsync(CancellationToken cancellationToken)
    {
        var normalizedDocument = NormalizeAndValidateDocument(SandboxWorkspaceDocument.Combine(
            await LoadNormalizedCatalogCoreAsync(cancellationToken),
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

    private async Task EnsureSplitFilesCoreAsync(CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(cancellationToken);

        if (!File.Exists(layout.CatalogPath) &&
            !File.Exists(layout.LegacyExecutionPath) &&
            !executionSliceStore.ExecutionStorageExists())
        {
            var seeded = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create());
            await SaveCoreAsync(SandboxWorkspaceDocument.Empty, seeded, currentRevision: 0L, cancellationToken);
            return;
        }

        if (executionSliceStore.ExecutionStorageExists())
        {
            if (!File.Exists(layout.CatalogPath))
            {
                var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
                await SaveCatalogCoreAsync(seededCatalog, cancellationToken);
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
                await SaveCoreAsync(SandboxWorkspaceDocument.Empty, normalized, currentRevision: 0L, cancellationToken);
                return;
            }
        }

        if (!File.Exists(layout.CatalogPath))
        {
            var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
            await SaveCatalogCoreAsync(seededCatalog, cancellationToken);
        }

        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        var executionState = await executionSliceStore.TryLoadLegacyExecutionStateAsync(cancellationToken)
            ?? new SandboxWorkspaceExecutionState(
                Version: catalog.Version,
                ChatSessions: [],
                ExecutionLog: [],
                Metrics: []);

        await SaveExecutionCoreAsync(
            SandboxWorkspaceExecutionState.Empty,
            SandboxWorkspaceSeedFactory.NormalizeExecutionState(executionState),
            cancellationToken);
        await EnsureWorkspaceIndexCoreAsync(cancellationToken);
        await PersistNormalizedWorkspaceDocumentCoreAsync(cancellationToken);
    }

    private async Task EnsureCatalogReadCoreAsync(CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(cancellationToken);

        if (executionSliceStore.ExecutionStorageExists())
        {
            if (!File.Exists(layout.CatalogPath))
            {
                var seededCatalog = SandboxWorkspaceSeedFactory.Normalize(SandboxWorkspaceSeedFactory.Create()).ToCatalog();
                await SaveCatalogCoreAsync(seededCatalog, cancellationToken);
            }

            await EnsureWorkspaceIndexCoreAsync(cancellationToken);
            return;
        }

        await EnsureSplitFilesCoreAsync(cancellationToken);
    }

    private async Task EnsureExecutionSummaryReadCoreAsync(CancellationToken cancellationToken)
    {
        jsonStore.EnsureDirectory(Path.GetDirectoryName(layout.CatalogPath)!);
        await RecoverPendingExecutionCommitAsync(cancellationToken);

        if (executionSliceStore.ExecutionStorageExists())
        {
            await EnsureWorkspaceIndexCoreAsync(cancellationToken);
            return;
        }

        await EnsureSplitFilesCoreAsync(cancellationToken);
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
        var normalizedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        if (catalog.CatalogDataRevision.IsAssigned &&
            !CatalogPayloadRequiresSave(catalog, normalizedCatalog))
        {
            return normalizedCatalog;
        }

        return await PersistNormalizedCatalogReadAsync(cancellationToken);
    }

    private async Task<SandboxWorkspaceCatalog> LoadNormalizedCatalogCoreAsync(CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        var normalizedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var catalogSave = await SaveCatalogCoreAsync(
            catalog,
            File.Exists(layout.CatalogPath),
            normalizedCatalog,
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
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureCatalogReadCoreAsync(cancellationToken);
            return await LoadNormalizedCatalogCoreAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
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

    private async Task<CatalogSaveResult> SaveCatalogCoreAsync(
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken)
    {
        var catalogExists = File.Exists(layout.CatalogPath);
        var currentCatalog = await LoadCatalogCoreAsync(cancellationToken);
        return await SaveCatalogCoreAsync(
            currentCatalog,
            catalogExists,
            catalog,
            cancellationToken);
    }

    private async Task<CatalogSaveResult> SaveCatalogCoreAsync(
        SandboxWorkspaceCatalog currentCatalog,
        bool catalogExists,
        SandboxWorkspaceCatalog catalog,
        CancellationToken cancellationToken)
    {
        var normalizedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        var payloadChanged = !catalogExists ||
                             CatalogPayloadRequiresSave(currentCatalog, normalizedCatalog);
        var revision = currentCatalog.CatalogDataRevision.IsAssigned
            ? payloadChanged
                ? currentCatalog.CatalogDataRevision.Next()
                : currentCatalog.CatalogDataRevision
            : CatalogDataRevision.Initial;
        var savedCatalog = normalizedCatalog with
        {
            CatalogDataRevision = revision
        };
        var changed = !catalogExists ||
                      jsonStore.RequiresSave(currentCatalog, savedCatalog);
        if (changed)
        {
            await jsonStore.WriteJsonAtomicallyAsync(
                layout.CatalogPath,
                savedCatalog,
                cancellationToken);
        }

        return new CatalogSaveResult(savedCatalog, changed);
    }

    private bool CatalogPayloadRequiresSave(
        SandboxWorkspaceCatalog currentCatalog,
        SandboxWorkspaceCatalog candidateCatalog)
    {
        return jsonStore.RequiresSave(
            currentCatalog with
            {
                CatalogDataRevision = CatalogDataRevision.Unassigned
            },
            candidateCatalog with
            {
                CatalogDataRevision = CatalogDataRevision.Unassigned
            });
    }

    private Task<bool> SaveExecutionCoreAsync(
        SandboxWorkspaceExecutionState previousExecutionState,
        SandboxWorkspaceExecutionState executionState,
        CancellationToken cancellationToken)
        => executionSliceStore.SaveAsync(previousExecutionState, executionState, cancellationToken);

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

    private async Task PersistNormalizedWorkspaceDocumentCoreAsync(CancellationToken cancellationToken)
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
        await SaveCoreAsync(currentDocument, normalizedDocument, currentRevision, cancellationToken);
    }

    private Task SaveWorkspaceIndexCoreAsync(WorkspaceStorageIndex index, CancellationToken cancellationToken)
        => jsonStore.WriteJsonAtomicallyAsync(layout.WorkspaceIndexPath, index, cancellationToken);

    private async Task<ChatSessionRecord> SaveChatSessionCoreAsync(
        ChatSessionRecord? previousSession,
        ChatSessionRecord session,
        CancellationToken cancellationToken)
    {
        var normalizedSession = NormalizeChatSession(session);
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
        return normalizedSession;
    }

    private async Task<long> ResolveCurrentRevisionAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(layout.CatalogPath) &&
            !File.Exists(layout.LegacyExecutionPath) &&
            !executionSliceStore.ExecutionStorageExists())
        {
            return 0L;
        }

        await EnsureSplitFilesCoreAsync(cancellationToken);
        return (await LoadWorkspaceIndexCoreAsync(cancellationToken)).Revision;
    }

    private bool CanReadCatalogWithoutWorkspaceLock()
    {
        return File.Exists(layout.CatalogPath) &&
               File.Exists(layout.WorkspaceIndexPath) &&
               !HasPendingAgentDeletionCommit;
    }

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
