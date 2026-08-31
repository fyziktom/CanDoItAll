using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentHistoryPublicationStore(IDbContextFactory<AppDbContext> factory) {
    public async Task PublishAsync(HistoryPartition partition, WorkspaceScopeDescriptor scope,
        IReadOnlyList<FileHistoryPublication> publications, CancellationToken cancellationToken) {
        if (publications.Count is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(publications));
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var ids = publications.Select(item => item.EvidenceId).ToArray();
        if (ids.Distinct().Count() != ids.Length) {
            throw new InvalidDataException("A file publication batch contains duplicate source identities.");
        }
        var locators = await db.Set<AgentHistoryLocator>()
            .Where(row => row.PartitionId == partition.StorageLineageId && ids.Contains(row.EvidenceId))
            .ToDictionaryAsync(row => row.EvidenceId, cancellationToken);
        var projectId = scope.Kind == WorkspaceScopeKind.Project ? Guid.Parse(scope.Key) : (Guid?)null;
        var deletedProject = projectId is { } project && !await db.Set<Project>().AnyAsync(row => row.Id == project, cancellationToken);
        foreach (var publication in publications) {
            var mutation = publication.Mutation;
            if (mutation.Source.Kind != HistorySourceKind.AgentConversation || mutation.Source.Partition != partition ||
                mutation.Source.Evidence.Value != publication.EvidenceId.ToString("N") ||
                mutation.Version.Value != publication.Version) {
                throw new InvalidDataException("The file publication does not belong to its history partition and identity.");
            }
            var ownerId = Guid.ParseExact(mutation.Source.Owner.Value, "N");
            if (!locators.TryGetValue(publication.EvidenceId, out var locator)) {
                locator = new() {
                    PartitionId = partition.StorageLineageId, EvidenceId = publication.EvidenceId,
                    OwnerId = ownerId, ScopeKind = scope.Kind, ScopeKey = scope.Key, ProjectId = projectId
                };
                db.Add(locator);
                locators.Add(locator.EvidenceId, locator);
            }
            if (locator.OwnerId != ownerId || locator.ScopeKind != scope.Kind || locator.ScopeKey != scope.Key) {
                throw new ProviderHistoryException(HistoryFailure.Conflict, "A canonical file identity was reused in another scope.");
            }
            if (locator.SourceVersion > mutation.Version.Value) {
                continue;
            }
            if (deletedProject) {
                mutation = Delete(mutation.Source, checked(Math.Max(locator.SourceVersion, mutation.Version.Value) + 1));
            }
            await HistoryProjectionWriter.StageAsync(db, mutation, cancellationToken);
            locator.SourceVersion = mutation.Version.Value;
            locator.IsDeleted = mutation.Kind == HistorySourceMutationKind.Delete;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<int> ReconcileDeletedProjectsAsync(HistoryPartition partition, int maximumItems, CancellationToken cancellationToken) {
        if (maximumItems is < 1 or > 1000) {
            throw new ArgumentOutOfRangeException(nameof(maximumItems));
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await HistoryPartitionStore.RequireAsync(db, partition, cancellationToken);
        var locators = await db.Set<AgentHistoryLocator>().Where(row =>
                row.PartitionId == partition.StorageLineageId && row.ProjectId != null && !row.IsDeleted &&
                !db.Set<Project>().Any(project => project.Id == row.ProjectId))
            .OrderBy(row => row.EvidenceId).Take(maximumItems).ToArrayAsync(cancellationToken);
        foreach (var locator in locators) {
            var source = new CanonicalEvidenceReference(partition, HistorySourceKind.AgentConversation,
                new(locator.OwnerId.ToString("N")), new(locator.EvidenceId.ToString("N")));
            var mutation = Delete(source, checked(locator.SourceVersion + 1));
            await HistoryProjectionWriter.StageAsync(db, mutation, cancellationToken);
            locator.SourceVersion = mutation.Version.Value;
            locator.IsDeleted = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return locators.Length;
    }

    private static HistorySourceMutation Delete(CanonicalEvidenceReference source, long version)
        => new(source, new(version), HistorySourceMutationKind.Delete, null, []);
}
