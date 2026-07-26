using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore :
    ISandboxWorkspaceStore,
    ISandboxWorkspaceChatQueryStore,
    ISandboxWorkspaceChatProjectionQueryStore,
    ISandboxWorkspaceChatSessionStore,
    ISandboxWorkspaceExecutionRunStore,
    ISandboxWorkspaceExecutionRunMutationStore,
    ISandboxWorkspaceExecutionRunReservationStore,
    IAgentRecruitingEvidenceStore
{
    private static readonly TimeSpan CatalogReadNormalizationLockTimeout = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly FileSandboxWorkspaceStorageLayout layout;
    private readonly FileSandboxWorkspaceJsonStore jsonStore;
    private readonly FileSandboxWorkspaceExecutionSliceStore executionSliceStore;
    private readonly FileSandboxWorkspaceChatProjectionStore chatProjectionStore;
    private readonly FileSandboxWorkspaceCrossProcessLock crossProcessLock;

    public FileSandboxWorkspaceStore(string workspaceRoot, WorkspaceScopeDescriptor? workspaceScope = null)
    {
        layout = new FileSandboxWorkspaceStorageLayout(workspaceRoot, workspaceScope);
        jsonStore = new FileSandboxWorkspaceJsonStore();
        executionSliceStore = new FileSandboxWorkspaceExecutionSliceStore(layout, jsonStore);
        chatProjectionStore = new FileSandboxWorkspaceChatProjectionStore(layout, jsonStore);
        crossProcessLock = new FileSandboxWorkspaceCrossProcessLock(layout.WorkspaceLockPath);
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

    public async Task SaveCatalogAsync(SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken = default)
    {
        if (executionSliceStore.ExecutionStorageExists())
        {
            await SaveCatalogOnlyAsync(catalog, expectedRevision: null, cancellationToken);
            return;
        }

        await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog),
                document.ToExecutionState()),
            cancellationToken);
    }

    public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (executionSliceStore.ExecutionStorageExists())
        {
            return await UpdateCatalogOnlyAsync(update, expectedRevision: null, cancellationToken);
        }

        var updatedDocument = await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                SandboxWorkspaceSeedFactory.NormalizeCatalog(update(document.ToCatalog())),
                document.ToExecutionState()),
            cancellationToken);

        return updatedDocument.ToCatalog();
    }

    public async Task<SandboxWorkspaceCatalog> UpdateCatalogAsync(
        Func<SandboxWorkspaceCatalog, SandboxWorkspaceCatalog> update,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (executionSliceStore.ExecutionStorageExists())
        {
            return await UpdateCatalogOnlyAsync(update, expectedRevision, cancellationToken);
        }

        var updatedDocument = await UpdateWorkspaceAsync(
            document => SandboxWorkspaceDocument.Combine(
                SandboxWorkspaceSeedFactory.NormalizeCatalog(update(document.ToCatalog())),
                document.ToExecutionState()),
            expectedRevision,
            cancellationToken);

        return updatedDocument.ToCatalog();
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

            var currentCatalog = await LoadNormalizedCatalogCoreAsync(cancellationToken);
            var updatedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(update(currentCatalog));
            SandboxWorkspaceDocumentInvariantValidator.Validate(
                SandboxWorkspaceDocument.Combine(updatedCatalog, SandboxWorkspaceExecutionState.Empty));

            var changed = await SaveCatalogCoreAsync(updatedCatalog, cancellationToken);
            var nextWorkspaceIndex = new WorkspaceStorageIndex(
                Revision: changed ? workspaceIndex.Revision + 1L : workspaceIndex.Revision,
                UpdatedAtUtc: changed ? DateTimeOffset.UtcNow : workspaceIndex.UpdatedAtUtc);
            if (changed || !File.Exists(layout.WorkspaceIndexPath) || jsonStore.RequiresSave(workspaceIndex, nextWorkspaceIndex))
            {
                await SaveWorkspaceIndexCoreAsync(nextWorkspaceIndex, cancellationToken);
            }

            return updatedCatalog;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<SandboxWorkspaceExecutionState> LoadExecutionAsync(CancellationToken cancellationToken = default)
    {
        if (CanReadExecutionWithoutWorkspaceLock())
        {
            return await LoadExecutionCoreAsync(cancellationToken);
        }

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
        if (CanReadExecutionProjectionWithoutWorkspaceLock())
        {
            return await LoadExecutionSummaryCoreAsync(cancellationToken);
        }

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
        if (CanReadExecutionProjectionWithoutWorkspaceLock())
        {
            return await LoadUsageProjectionCoreAsync(cancellationToken);
        }

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
        if (CanReadExecutionDetailsWithoutWorkspaceLock())
        {
            return await executionSliceStore.LoadRunDetailAsync(executionRunId, cancellationToken);
        }

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
        if (CanReadExecutionDetailsWithoutWorkspaceLock())
        {
            return await executionSliceStore.ListRunsAsync(cancellationToken);
        }

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
        if (CanReadExecutionDetailsWithoutWorkspaceLock())
        {
            return await executionSliceStore.LoadRunAsync(executionRunId, cancellationToken);
        }

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

            var catalog = await LoadCatalogCoreAsync(cancellationToken);
            ValidateExecutionRunDetail(catalog, detail);

            var previousDetail = await executionSliceStore.LoadRunDetailAsync(detail.Run.Id, cancellationToken);
            return await SaveExecutionRunDetailCoreAsync(previousDetail, detail, cancellationToken);
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

    public async Task SaveAsync(SandboxWorkspaceDocument document, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            var normalizedDocument = NormalizeAndValidateDocument(document);
            var currentRevision = await ResolveCurrentRevisionAsync(cancellationToken);
            var previousDocument = currentRevision == 0L
                ? SandboxWorkspaceDocument.Empty
                : (await LoadSnapshotCoreAsync(cancellationToken)).Document;
            await SaveCoreAsync(previousDocument, normalizedDocument, currentRevision, cancellationToken);
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
        if (CanReadChatProjectionWithoutWorkspaceLock())
        {
            return await chatProjectionStore.ListChatSessionSummariesAsync(agentId, cancellationToken);
        }

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
        if (CanReadChatProjectionWithoutWorkspaceLock())
        {
            return await chatProjectionStore.LoadChatWorkspaceProjectionAsync(agentId, cancellationToken);
        }

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
        if (executionSliceStore.ExecutionStorageExists())
        {
            return await chatProjectionStore.GetChatSessionAsync(chatSessionId, cancellationToken);
        }

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
        if (CanReadChatProjectionWithoutWorkspaceLock())
        {
            return await chatProjectionStore.ListChatRunSummariesAsync(agentId, chatSessionId, cancellationToken);
        }

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
        if (CanReadChatProjectionWithoutWorkspaceLock())
        {
            return await chatProjectionStore.LoadChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken);
        }

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
            var current = await LoadSnapshotCoreAsync(cancellationToken);
            if (expectedRevision.HasValue && current.Revision != expectedRevision.Value)
            {
                throw new SandboxWorkspaceConcurrencyException(expectedRevision.Value, current.Revision);
            }

            var updated = NormalizeAndValidateDocument(update(current.Document));
            await SaveCoreAsync(current.Document, updated, current.Revision, cancellationToken);
            return updated;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SaveCoreAsync(
        SandboxWorkspaceDocument previousDocument,
        SandboxWorkspaceDocument document,
        long currentRevision,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

        var catalogChanged = await SaveCatalogCoreAsync(document.ToCatalog(), cancellationToken);
        var executionChanged = await SaveExecutionCoreAsync(previousDocument.ToExecutionState(), document.ToExecutionState(), cancellationToken);
        var workspaceChanged = catalogChanged || executionChanged || !File.Exists(layout.WorkspaceIndexPath);
        if (workspaceChanged)
        {
            await SaveWorkspaceIndexCoreAsync(
                new WorkspaceStorageIndex(
                    Revision: Math.Max(1L, currentRevision + 1L),
                    UpdatedAtUtc: DateTimeOffset.UtcNow),
                cancellationToken);
        }
    }

    private async Task<ExecutionRunDetail> SaveExecutionRunDetailCoreAsync(
        ExecutionRunDetail? previousDetail,
        ExecutionRunDetail detail,
        CancellationToken cancellationToken)
    {
        var saveResult = await executionSliceStore.SaveRunDetailAsync(previousDetail, detail, cancellationToken);
        var workspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
        var nextWorkspaceIndex = new WorkspaceStorageIndex(
            Revision: saveResult.Changed ? workspaceIndex.Revision + 1L : workspaceIndex.Revision,
            UpdatedAtUtc: saveResult.Index.UpdatedAtUtc);

        if (saveResult.Changed || !File.Exists(layout.WorkspaceIndexPath) || jsonStore.RequiresSave(workspaceIndex, nextWorkspaceIndex))
        {
            await SaveWorkspaceIndexCoreAsync(nextWorkspaceIndex, cancellationToken);
        }

        await chatProjectionStore.SaveRunDetailAsync(previousDetail, saveResult.Detail, saveResult.Index, cancellationToken);
        return saveResult.Detail;
    }

    private async Task<SandboxWorkspaceDocumentSnapshot> LoadSnapshotCoreAsync(CancellationToken cancellationToken)
    {
        var normalizedDocument = NormalizeAndValidateDocument(SandboxWorkspaceDocument.Combine(
            await LoadCatalogCoreAsync(cancellationToken),
            await LoadExecutionCoreAsync(cancellationToken)));
        var index = await LoadWorkspaceIndexCoreAsync(cancellationToken);
        return new SandboxWorkspaceDocumentSnapshot(normalizedDocument, index.Revision);
    }

    private async Task EnsureSplitFilesCoreAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

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
        Directory.CreateDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

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
        Directory.CreateDirectory(Path.GetDirectoryName(layout.CatalogPath)!);

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
        if (EqualityComparer<SandboxWorkspaceCatalog>.Default.Equals(catalog, normalizedCatalog))
        {
            return normalizedCatalog;
        }

        return await TryPersistNormalizedCatalogReadAsync(cancellationToken) ?? normalizedCatalog;
    }

    private async Task<SandboxWorkspaceCatalog> LoadNormalizedCatalogCoreAsync(CancellationToken cancellationToken)
    {
        var catalog = await LoadCatalogCoreAsync(cancellationToken);
        var normalizedCatalog = SandboxWorkspaceSeedFactory.NormalizeCatalog(catalog);
        if (!EqualityComparer<SandboxWorkspaceCatalog>.Default.Equals(catalog, normalizedCatalog))
        {
            await SaveCatalogCoreAsync(normalizedCatalog, cancellationToken);
        }

        return normalizedCatalog;
    }

    private async Task<SandboxWorkspaceCatalog?> TryPersistNormalizedCatalogReadAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(CatalogReadNormalizationLockTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var gateAcquired = false;

        try
        {
            await gate.WaitAsync(linkedCancellation.Token);
            gateAcquired = true;

            await using var workspaceLock = await crossProcessLock.AcquireAsync(linkedCancellation.Token);
            await EnsureCatalogReadCoreAsync(cancellationToken);
            return await LoadNormalizedCatalogCoreAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            if (gateAcquired)
            {
                gate.Release();
            }
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

    private Task<bool> SaveCatalogCoreAsync(SandboxWorkspaceCatalog catalog, CancellationToken cancellationToken)
        => jsonStore.WriteJsonIfChangedAsync(layout.CatalogPath, catalog, cancellationToken);

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
               File.Exists(layout.WorkspaceIndexPath);
    }

    private bool CanReadExecutionWithoutWorkspaceLock()
    {
        return CanReadCatalogWithoutWorkspaceLock() &&
               executionSliceStore.ExecutionStorageExists();
    }

    private bool CanReadExecutionProjectionWithoutWorkspaceLock()
    {
        return CanReadExecutionWithoutWorkspaceLock() &&
               executionSliceStore.HasPersistedIndex();
    }

    private bool CanReadChatProjectionWithoutWorkspaceLock()
    {
        return executionSliceStore.ExecutionStorageExists() &&
               chatProjectionStore.HasPersistedChatIndex();
    }

    private bool CanReadExecutionDetailsWithoutWorkspaceLock()
    {
        return executionSliceStore.ExecutionStorageExists();
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
        var knownAgentIds = catalog.Agents
            .Select(item => item.Id)
            .ToHashSet();

        if (!knownAgentIds.Contains(detail.Run.AgentId))
        {
            throw new InvalidOperationException(
                $"Execution run '{detail.Run.Id:N}' references missing agent '{detail.Run.AgentId:N}'.");
        }

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
            if (!knownAgentIds.Contains(sessionAgentId))
            {
                throw new InvalidOperationException(
                    $"Chat session '{detail.ChatSession!.Id:N}' references missing agent '{sessionAgentId:N}'.");
            }
        }

        ValidateAgentScoped(detail.ExecutionLog.Select(item => (item.Id, item.AgentId, item.ChatSessionId)), detail, knownAgentIds, "Execution log entry");
        ValidateAgentScoped(detail.Metrics.Select(item => (item.Id, item.AgentId, item.ChatSessionId)), detail, knownAgentIds, "Execution metric");
        ValidateAgentScoped(
            detail.UsageObservations
                .Where(item => item.AgentId.HasValue)
                .Select(item => (item.Id, item.AgentId!.Value, item.ChatSessionId)),
            detail,
            knownAgentIds,
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
}
