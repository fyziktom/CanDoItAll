using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

public sealed partial class FileSandboxWorkspaceStore
{
    private const string AgentDeletionCommitJournalVersion = "1.0";

    public async Task<AgentWorkspaceDeletionResult> DeleteAgentWorkspaceDataAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent identifier is required.", nameof(agentId));
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var workspaceLock = await crossProcessLock.AcquireAsync(cancellationToken);
            await EnsureSplitFilesCoreAsync(cancellationToken);
            var currentCatalog = await LoadNormalizedCatalogCoreAsync(cancellationToken);
            var agent = currentCatalog.Agents.FirstOrDefault(item => item.Id == agentId);
            if (agent is null)
            {
                return new AgentWorkspaceDeletionResult(
                    Deleted: false,
                    DeletedChatSessionCount: 0,
                    DeletedExecutionRunCount: 0);
            }

            EnsureAgentCanBeDeleted(agent);
            var deletedAtUtc = DateTimeOffset.UtcNow;
            var currentExecutionIndex = await executionSliceStore
                .LoadIndexForAgentDeletionAsync(cancellationToken);
            var currentChatIndex = await chatProjectionStore.LoadCurrentIndexAsync(
                currentExecutionIndex,
                cancellationToken);
            var executionPlan = await executionSliceStore.PrepareAgentDeletionAsync(
                agentId,
                currentExecutionIndex,
                currentChatIndex,
                deletedAtUtc,
                cancellationToken);
            var targetChatIndex = CreateTargetChatIndex(
                currentChatIndex,
                executionPlan);
            var targetCatalog = CreateTargetCatalog(
                currentCatalog,
                agentId,
                deletedAtUtc);
            if (targetCatalog.Agents.Any(item => item.Id == agentId))
            {
                throw new AgentDeletionConflictException(
                    agentId,
                    AgentDeletionConflictKind.ManagedSeedAgent,
                    $"Managed seed agent '{agent.Name}' cannot be deleted.");
            }

            var currentWorkspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
            var targetWorkspaceIndex = new WorkspaceStorageIndex(
                Revision: currentWorkspaceIndex.Revision + 1L,
                UpdatedAtUtc: deletedAtUtc);
            var journal = new AgentDeletionCommitJournal(
                AgentDeletionCommitJournalVersion,
                agentId,
                deletedAtUtc,
                currentCatalog,
                targetCatalog,
                executionPlan,
                executionPlan.HasExecutionChanges ? currentChatIndex : null,
                executionPlan.HasExecutionChanges ? targetChatIndex : null,
                currentWorkspaceIndex,
                targetWorkspaceIndex);
            ValidateAgentDeletionCommitJournal(journal);
            jsonStore.EnsureDirectory(layout.ExecutionStorageRoot);
            await jsonStore.WriteJsonAtomicallyAsync(
                PendingAgentDeletionCommitJournalPath,
                journal,
                cancellationToken);
            await PersistAgentDeletionJournalAsync(
                journal,
                CancellationToken.None);

            return new AgentWorkspaceDeletionResult(
                Deleted: true,
                DeletedChatSessionCount: executionPlan.SessionIds.Count,
                DeletedExecutionRunCount: executionPlan.RunIds.Count);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task PersistAgentDeletionJournalAsync(
        AgentDeletionCommitJournal journal,
        CancellationToken cancellationToken)
    {
        ValidateAgentDeletionCommitJournal(journal);
        await executionSliceStore.PersistAgentDeletionAsync(
            journal.ExecutionPlan,
            cancellationToken);
        if (journal.ExecutionPlan.HasExecutionChanges)
        {
            await chatProjectionStore.PersistAgentDeletionIndexAsync(
                journal.TargetChatIndex!,
                cancellationToken);
        }

        agentDeletionCommitBoundary?.Invoke(
            AgentDeletionCommitStage.ExecutionSlicesPersisted);

        await SaveCatalogCoreAsync(journal.TargetCatalog, cancellationToken);
        agentDeletionCommitBoundary?.Invoke(
            AgentDeletionCommitStage.CatalogPersisted);
        await SaveWorkspaceIndexCoreAsync(
            journal.TargetWorkspaceIndex,
            cancellationToken);
        agentDeletionCommitBoundary?.Invoke(
            AgentDeletionCommitStage.WorkspaceIndexPersisted);
        await jsonStore.DeleteFileAsync(PendingAgentDeletionCommitJournalPath, cancellationToken);
    }

    private async Task RecoverPendingAgentDeletionAsync(
        CancellationToken cancellationToken)
    {
        if (!HasPendingAgentDeletionCommit)
        {
            return;
        }

        var journal = await jsonStore.ReadJsonAsync<AgentDeletionCommitJournal>(
                PendingAgentDeletionCommitJournalPath,
                cancellationToken)
            ?? throw new InvalidDataException(
                $"Pending agent deletion journal '{PendingAgentDeletionCommitJournalPath}' is empty.");
        ValidateAgentDeletionCommitJournal(journal);

        var currentExecutionIndex = await executionSliceStore.LoadIndexAsync(cancellationToken);
        if (!HasSamePayload(currentExecutionIndex, journal.ExecutionPlan.SourceIndex) &&
            !HasSamePayload(currentExecutionIndex, journal.ExecutionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                $"Pending agent deletion '{journal.AgentId:N}' found an unexpected execution index.");
        }

        if (journal.ExecutionPlan.HasExecutionChanges)
        {
            var currentUsageProjection = await jsonStore.ReadJsonAsync<AgentUsageProjection>(
                    layout.ExecutionUsageIndexPath,
                    cancellationToken)
                ?? throw new InvalidDataException(
                    $"Pending agent deletion '{journal.AgentId:N}' found no usage projection.");
            if (!HasSamePayload(
                    currentUsageProjection,
                    journal.ExecutionPlan.SourceUsageProjection) &&
                !HasSamePayload(
                    currentUsageProjection,
                    journal.ExecutionPlan.TargetUsageProjection))
            {
                throw new InvalidDataException(
                    $"Pending agent deletion '{journal.AgentId:N}' found an unexpected usage projection.");
            }
        }

        if (journal.ExecutionPlan.HasExecutionChanges)
        {
            var currentChatIndex = await jsonStore.ReadJsonAsync<ExecutionChatIndex>(
                    layout.ExecutionChatIndexPath,
                    cancellationToken)
                ?? throw new InvalidDataException(
                    $"Pending agent deletion '{journal.AgentId:N}' found no chat index.");
            if (!HasSamePayload(currentChatIndex, journal.SourceChatIndex) &&
                !HasSamePayload(currentChatIndex, journal.TargetChatIndex))
            {
                throw new InvalidDataException(
                    $"Pending agent deletion '{journal.AgentId:N}' found an unexpected chat index.");
            }
        }

        var currentWorkspaceIndex = await LoadWorkspaceIndexCoreAsync(cancellationToken);
        if (!HasSamePayload(currentWorkspaceIndex, journal.SourceWorkspaceIndex) &&
            !HasSamePayload(currentWorkspaceIndex, journal.TargetWorkspaceIndex))
        {
            throw new InvalidDataException(
                $"Pending agent deletion '{journal.AgentId:N}' found an unexpected workspace index.");
        }

        var currentCatalog = await LoadCatalogCoreAsync(cancellationToken);
        if (!HasSamePayload(currentCatalog, journal.SourceCatalog) &&
            !HasSamePayload(currentCatalog, journal.TargetCatalog))
        {
            throw new InvalidDataException(
                $"Pending agent deletion '{journal.AgentId:N}' found an unexpected catalog.");
        }

        await PersistAgentDeletionJournalAsync(
            journal,
            CancellationToken.None);
    }

    private static SandboxWorkspaceCatalog CreateTargetCatalog(
        SandboxWorkspaceCatalog currentCatalog,
        Guid agentId,
        DateTimeOffset updatedAtUtc)
    {
        var nextRevision = currentCatalog.CatalogDataRevision.IsAssigned
            ? currentCatalog.CatalogDataRevision.Next()
            : CatalogDataRevision.Initial;
        var candidate = currentCatalog with
        {
            CatalogDataRevision = nextRevision,
            Agents = currentCatalog.Agents
                .Where(item => item.Id != agentId)
                .ToList(),
            AgentExternalBindings = currentCatalog.AgentExternalBindings
                .Where(item => item.AgentId != agentId)
                .ToList(),
            Memory = currentCatalog.Memory
                .Where(item => item.AgentId != agentId)
                .ToList(),
            AgentTeams = currentCatalog.AgentTeams
                .Select(team => team.AgentIds.Contains(agentId)
                    ? team with
                    {
                        AgentIds = team.AgentIds
                            .Where(item => item != agentId)
                            .ToList(),
                        UpdatedAtUtc = updatedAtUtc
                    }
                    : team)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        return SandboxWorkspaceSeedFactory.NormalizeCatalog(candidate);
    }

    private static ExecutionChatIndex CreateTargetChatIndex(
        ExecutionChatIndex currentChatIndex,
        AgentExecutionDeletionPlan executionPlan)
    {
        if (!executionPlan.HasExecutionChanges)
        {
            return currentChatIndex;
        }

        var runIds = executionPlan.RunIds.ToHashSet();
        var sessionIds = executionPlan.SessionIds.ToHashSet();
        return currentChatIndex with
        {
            Revision = executionPlan.TargetIndex.Revision,
            UpdatedAtUtc = executionPlan.TargetIndex.UpdatedAtUtc,
            SessionSummaries = currentChatIndex.SessionSummaries
                .Where(item => !sessionIds.Contains(item.Id))
                .ToList(),
            RunSummaries = currentChatIndex.RunSummaries
                .Where(item => !runIds.Contains(item.ExecutionRunId))
                .ToList()
        };
    }

    private static void EnsureAgentCanBeDeleted(AgentDefinition agent)
    {
        if (!ManagedSeedProviderFallbacks.IsManagedSeedAgent(agent))
        {
            return;
        }

        throw new AgentDeletionConflictException(
            agent.Id,
            AgentDeletionConflictKind.ManagedSeedAgent,
            $"Managed seed agent '{agent.Name}' cannot be deleted.");
    }

    private void ValidateAgentDeletionCommitJournal(
        AgentDeletionCommitJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        if (journal.SourceCatalog is null ||
            journal.TargetCatalog is null ||
            journal.ExecutionPlan is null ||
            journal.SourceWorkspaceIndex is null ||
            journal.TargetWorkspaceIndex is null ||
            !string.Equals(
                journal.Version,
                AgentDeletionCommitJournalVersion,
                StringComparison.Ordinal) ||
            journal.AgentId == Guid.Empty ||
            journal.ExecutionPlan.AgentId != journal.AgentId ||
            !journal.SourceCatalog.Agents.Any(item => item.Id == journal.AgentId) ||
            journal.TargetCatalog.Agents.Any(item => item.Id == journal.AgentId) ||
            journal.TargetWorkspaceIndex.Revision != journal.SourceWorkspaceIndex.Revision + 1L ||
            journal.TargetWorkspaceIndex.UpdatedAtUtc != journal.DeletedAtUtc ||
            journal.ExecutionPlan.SourceIndex is null ||
            journal.ExecutionPlan.TargetIndex is null ||
            journal.ExecutionPlan.TargetIndex.Revision is < 1L)
        {
            throw new InvalidDataException("The pending agent deletion journal is invalid.");
        }

        var expectedCatalog = CreateTargetCatalog(
            journal.SourceCatalog,
            journal.AgentId,
            journal.DeletedAtUtc);
        var expectedExecutionRevision = journal.ExecutionPlan.HasExecutionChanges
            ? journal.ExecutionPlan.SourceIndex.Revision + 1L
            : journal.ExecutionPlan.SourceIndex.Revision;
        if (journal.ExecutionPlan.TargetIndex.Revision != expectedExecutionRevision ||
            journal.ExecutionPlan.HasExecutionChanges !=
            (journal.ExecutionPlan.TargetUsageProjection is not null) ||
            journal.ExecutionPlan.HasExecutionChanges !=
            (journal.ExecutionPlan.SourceUsageProjection is not null) ||
            journal.ExecutionPlan.HasExecutionChanges !=
            (journal.SourceChatIndex is not null) ||
            journal.ExecutionPlan.HasExecutionChanges !=
            (journal.TargetChatIndex is not null) ||
            !HasSamePayload(expectedCatalog, journal.TargetCatalog) ||
            journal.ExecutionPlan.HasExecutionChanges &&
            !HasValidAgentDeletionChatIndexes(journal))
        {
            throw new InvalidDataException(
                "The pending agent deletion journal contains inconsistent source or target state.");
        }

        if (journal.ExecutionPlan.RunIds.Any(item => item == Guid.Empty) ||
            journal.ExecutionPlan.RunIds.Distinct().Count() !=
            journal.ExecutionPlan.RunIds.Count ||
            journal.ExecutionPlan.SessionIds.Any(item => item == Guid.Empty) ||
            journal.ExecutionPlan.SessionIds.Distinct().Count() !=
            journal.ExecutionPlan.SessionIds.Count ||
            journal.ExecutionPlan.TargetIndex.RunCount !=
            journal.ExecutionPlan.SourceIndex.RunCount -
            journal.ExecutionPlan.RunIds.Count ||
            journal.ExecutionPlan.TargetIndex.SessionCount !=
            journal.ExecutionPlan.SourceIndex.SessionCount -
            journal.ExecutionPlan.SessionIds.Count)
        {
            throw new InvalidDataException(
                "The pending agent deletion journal contains inconsistent execution ownership data.");
        }

        if (journal.ExecutionPlan.HasExecutionChanges &&
            (journal.ExecutionPlan.TargetIndex.UpdatedAtUtc != journal.DeletedAtUtc ||
             journal.ExecutionPlan.TargetUsageProjection!.Revision != expectedExecutionRevision ||
             journal.ExecutionPlan.SourceUsageProjection!.Revision !=
             journal.ExecutionPlan.SourceIndex.Revision))
        {
            throw new InvalidDataException(
                "The pending agent deletion journal contains inconsistent usage state.");
        }

        if (!journal.ExecutionPlan.HasExecutionChanges &&
            !HasSamePayload(
                journal.ExecutionPlan.SourceIndex,
                journal.ExecutionPlan.TargetIndex))
        {
            throw new InvalidDataException(
                "The pending agent deletion journal changes execution state without an execution mutation.");
        }
    }

    private bool HasValidAgentDeletionChatIndexes(
        AgentDeletionCommitJournal journal)
    {
        var source = journal.SourceChatIndex!;
        var target = journal.TargetChatIndex!;
        var expected = CreateTargetChatIndex(source, journal.ExecutionPlan);
        return source.Revision == journal.ExecutionPlan.SourceIndex.Revision &&
               source.SessionSummaries.Count ==
               journal.ExecutionPlan.SourceIndex.SessionCount &&
               source.RunSummaries.Count ==
               journal.ExecutionPlan.SourceIndex.RunCount &&
               target.Revision == journal.ExecutionPlan.TargetIndex.Revision &&
               HasSamePayload(expected, target);
    }

    private string PendingAgentDeletionCommitJournalPath
        => Path.Combine(
            layout.ExecutionStorageRoot,
            "pending-agent-deletion.json");

    private bool HasPendingAgentDeletionCommit
        => File.Exists(PendingAgentDeletionCommitJournalPath);
}

internal sealed record AgentDeletionCommitJournal(
    string Version,
    Guid AgentId,
    DateTimeOffset DeletedAtUtc,
    SandboxWorkspaceCatalog SourceCatalog,
    SandboxWorkspaceCatalog TargetCatalog,
    AgentExecutionDeletionPlan ExecutionPlan,
    ExecutionChatIndex? SourceChatIndex,
    ExecutionChatIndex? TargetChatIndex,
    WorkspaceStorageIndex SourceWorkspaceIndex,
    WorkspaceStorageIndex TargetWorkspaceIndex);

internal enum AgentDeletionCommitStage
{
    ExecutionSlicesPersisted,
    CatalogPersisted,
    WorkspaceIndexPersisted
}
