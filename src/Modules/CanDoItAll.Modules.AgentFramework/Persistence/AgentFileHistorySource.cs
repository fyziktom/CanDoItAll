using System.Diagnostics;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentFileHistorySource(
    IDbContextFactory<AppDbContext> factory,
    IDatabaseProfileRuntimeAccessor profiles,
    IWorkspacePathResolver paths,
    AgentHistoryPublicationStore publications,
    ILogger<AgentFileHistorySource> logger) : IProviderHistorySource, IHistorySourceMaintenance, IDisposable {
    private HistoryPartition? activePartition;
    private string? activeRoot;
    private FileHistoryReadyQueue? queue;
    private WorkspaceScopeDescriptor? activeScope;
    private FileHistoryBackfill? backfill;

    public HistorySourceKind Kind => HistorySourceKind.AgentConversation;

    public async Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor,
        int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        var partition = context.Partition;
        var location = await context.DatabaseAsync(token => Task.FromResult((
            Root: paths.ResolveWorkspaceRoot(), Organization: WorkspaceScopeDescriptor.Organization(
                profiles.ResolveCurrentProfile().Profile.Id.ToString("N")))), cancellationToken);
        var root = location.Root;
        if (activePartition != partition || activeRoot != root) {
            Dispose();
            activePartition = partition;
            activeRoot = root;
            queue = new(root);
        }
        var position = cursor is null ? new Position(BackfillPhase.Organization, Guid.Empty)
            : JsonSerializer.Deserialize<Position>(cursor) ?? throw new InvalidDataException("Invalid agent history cursor.");
        var deleted = await TrackAsync(MaintenanceStage.ReconcileDeleted,
            () => context.DatabaseAsync(token => publications.ReconcileDeletedProjectsAsync(partition, maximumItems, token), cancellationToken));
        var ready = await queue!.NextAsync(partition, cancellationToken);
        if (await context.DatabaseAsync(token => ResolveScopeAsync(position, location.Organization, token), cancellationToken) is { } scope) {
            if (activeScope != scope) {
                backfill?.Dispose();
                activeScope = scope;
                backfill = new(root, scope, partition);
            }
            var progress = await TrackAsync(MaintenanceStage.Backfill, () => backfill!.ProcessAsync(maximumItems, cancellationToken));
            ready ??= scope;
            if (progress.AllSourceIntentsStaged) {
                position = position.Phase == BackfillPhase.Organization
                    ? new(BackfillPhase.Projects, Guid.Empty) : new(BackfillPhase.Projects, Guid.Parse(scope.Key));
            }
        } else {
            position = position with { Phase = BackfillPhase.Complete };
        }
        if (ready is not null) {
            await DrainAsync(root, ready, context, maximumItems, cancellationToken);
        }
        return new(JsonSerializer.Serialize(position),
            position.Phase == BackfillPhase.Complete && !queue.HasPending(partition) && deleted < maximumItems);
    }

    private async Task<WorkspaceScopeDescriptor?> ResolveScopeAsync(Position position, WorkspaceScopeDescriptor organization, CancellationToken cancellationToken) {
        if (position.Phase == BackfillPhase.Organization) {
            return organization;
        }
        if (position.Phase == BackfillPhase.Complete) {
            return null;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var project = await db.Set<Project>().AsNoTracking().Where(row => row.Id.CompareTo(position.AfterProject) > 0)
            .OrderBy(row => row.Id).Select(row => (Guid?)row.Id).FirstOrDefaultAsync(cancellationToken);
        return project is { } id ? WorkspaceScopeDescriptor.Project(id.ToString("D")) : null;
    }

    private async Task DrainAsync(string root, WorkspaceScopeDescriptor scope, HistoryMaintenanceContext context,
        int maximumItems, CancellationToken cancellationToken) {
        var partition = context.Partition;
        var journal = new FileProviderHistoryJournal(root, scope);
        var batch = await TrackAsync(MaintenanceStage.ReadJournal, () => journal.ReadBatchAsync(partition, maximumItems, cancellationToken));
        if (batch.Count > 0) {
            await TrackAsync(MaintenanceStage.Publish,
                () => context.DatabaseAsync(token => publications.PublishAsync(partition, scope, batch, token), cancellationToken));
            foreach (var item in batch) {
                await TrackAsync(MaintenanceStage.Acknowledge, () => journal.AcknowledgeAsync(item, cancellationToken));
            }
        }
        await journal.ClearReadyIfDrainedAsync(partition, cancellationToken);
    }

    public async Task<HistorySourceMutation?> ReadAsync(CanonicalEvidenceReference source, CancellationToken cancellationToken) {
        if (source.Kind != Kind || !Guid.TryParseExact(source.Owner.Value, "N", out var owner) ||
            !Guid.TryParseExact(source.Evidence.Value, "N", out var evidence)) {
            return null;
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var locator = await db.Set<AgentHistoryLocator>().AsNoTracking().SingleOrDefaultAsync(row =>
            row.PartitionId == source.Partition.StorageLineageId && row.EvidenceId == evidence && row.OwnerId == owner, cancellationToken);
        if (locator is null) {
            return null;
        }
        if (locator.IsDeleted) {
            return new(source, new(locator.SourceVersion), HistorySourceMutationKind.Delete, null, []);
        }
        if (locator.ProjectId is { } project && !await db.Set<Project>().AnyAsync(row => row.Id == project, cancellationToken)) {
            return new(source, new(checked(locator.SourceVersion + 1)), HistorySourceMutationKind.Delete, null, []);
        }
        var journal = new FileProviderHistoryJournal(paths.ResolveWorkspaceRoot(), new(locator.ScopeKind, locator.ScopeKey));
        return await journal.ReadAsync(source, cancellationToken);
    }

    public async Task<HistoryDetail> ReadDetailAsync(CanonicalEvidenceReference source, HistoryEntryId entryId,
        CancellationToken cancellationToken) {
        var evidence = await ReadAsync(source, cancellationToken);
        if (evidence is not { Kind: HistorySourceMutationKind.Upsert } ||
            evidence.Entry?.Id != entryId && !evidence.Attempts.Any(entry => entry.Id == entryId)) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, source.Partition, cancellationToken);
        var owner = Guid.ParseExact(source.Owner.Value, "N");
        var evidenceId = Guid.ParseExact(source.Evidence.Value, "N");
        var locator = await db.Set<AgentHistoryLocator>().AsNoTracking().SingleOrDefaultAsync(row =>
            row.PartitionId == source.Partition.StorageLineageId && row.EvidenceId == evidenceId &&
            row.OwnerId == owner && !row.IsDeleted, cancellationToken);
        if (locator is null) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        var store = new FileSandboxWorkspaceStore(paths.ResolveWorkspaceRoot(), new(locator.ScopeKind, locator.ScopeKey));
        var run = await store.GetExecutionRunAsync(owner, cancellationToken);
        if (run is null) {
            return new(entryId, HistoryDetailState.Unavailable);
        }
        var sections = new List<HistoryContentSection> {
            new("Run input summary", Capture(run.InputSummary)),
            new("Run result summary", Capture(run.ResultSummary))
        };
        if (run.ChatSessionId is { } sessionId) {
            var session = await store.GetChatSessionAsync(sessionId, cancellationToken);
            if (session is not null && session.AgentId == run.AgentId) {
                var messages = session.Messages.OrderBy(message => message.CreatedAtUtc).TakeLast(50);
                var transcript = string.Join("\n\n", messages.Select(message =>
                    $"{message.Role} · {message.CreatedAtUtc:u}\n{message.Content}"));
                sections.Insert(0, new("Linked conversation · latest 50 messages (may include other turns)", Capture(transcript)));
            } else {
                sections.Add(new("Linked conversation", Capture("The linked conversation is no longer available.")));
            }
        }
        return new(entryId, HistoryDetailState.Canonical) { Sections = sections };
    }

    private static HistoryCapturedText Capture(string text) => HistoryTextCapture.Capture(text, 32 * 1024, []);

    public void Dispose() {
        backfill?.Dispose();
        backfill = null;
        queue?.Dispose();
        queue = null;
        activeScope = null;
    }

    private async Task<T> TrackAsync<T>(MaintenanceStage stage, Func<Task<T>> action) {
        var started = Stopwatch.GetTimestamp();
        try {
            var result = await action();
            logger.LogDebug("Agent history stage {Stage} completed in {ElapsedMs} ms.",
                stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        } catch (Exception exception) {
            logger.LogWarning("Agent history stage {Stage} failed after {ElapsedMs} ms with {FailureType}.",
                stage, Stopwatch.GetElapsedTime(started).TotalMilliseconds, exception.GetType().Name);
            throw;
        }
    }

    private Task TrackAsync(MaintenanceStage stage, Func<Task> action) => TrackAsync(stage, async () => {
        await action();
        return true;
    });

    private enum MaintenanceStage { ReconcileDeleted, Backfill, ReadJournal, Publish, Acknowledge }
    private enum BackfillPhase { Organization, Projects, Complete }
    private sealed record Position(BackfillPhase Phase, Guid AfterProject);
}
