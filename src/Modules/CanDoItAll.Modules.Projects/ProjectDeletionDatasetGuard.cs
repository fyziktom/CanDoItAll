using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectDeletionDatasetGuard {
    private readonly Guid databaseProfileId;
    private readonly DbContextOptions<ProjectsDbContext> options;
    private readonly CoordinatedDatabaseTransaction transactions;
    private readonly ImmutableArray<IProjectDeletionPendingInventory> inventories;

    public ProjectDeletionDatasetGuard(Guid databaseProfileId, DbContextOptions<ProjectsDbContext> options,
        CoordinatedDatabaseTransaction transactions, IEnumerable<IProjectDeletionPendingInventory> inventories) {
        ArgumentOutOfRangeException.ThrowIfEqual(databaseProfileId, Guid.Empty);
        this.databaseProfileId = databaseProfileId;
        this.options = options;
        this.transactions = transactions;
        this.inventories = inventories.ToImmutableArray();
        if (this.inventories.IsEmpty || this.inventories.Any(inventory => inventory is null) ||
                this.inventories.Select(inventory => inventory.Id).Distinct().Count() != this.inventories.Length) {
            throw new InvalidOperationException("Dataset readiness requires distinct installed native deletion inventories.");
        }
    }

    public async Task RequireReadyEnlistedAsync(DatasetActivationBinding binding, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new ProjectsDbContext(configured), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        foreach (var inventory in inventories) {
            Guid? after = null;
            while (true) {
                var page = await inventory.ReadPageAsync(databaseProfileId, after, cancellationToken);
                if (page.Items.IsEmpty) {
                    break;
                }
                foreach (var item in page.Items) {
                    if (item.Obligation.Participant != inventory.Id || item.Original.Completion.DatabaseProfileId != databaseProfileId ||
                            item.Original.Completion.Namespace != binding.Namespace || after is { } previous && item.Obligation.RecoveryId.CompareTo(previous) <= 0) {
                        throw new InvalidDataException("The pending deletion inventory has an inconsistent binding or cursor.");
                    }
                    after = item.Obligation.RecoveryId;
                }
                foreach (var batch in page.Items.Chunk(64)) {
                    var rows = await ProjectDeletionCompletionReader.ReadBatchAsync(context, binding.Namespace.Value,
                        batch.Select(item => item.Original.Completion.Id.Value).Distinct().ToArray(), cancellationToken);
                    var completions = rows.Where(row => row.CompletionId is not null).ToDictionary(row => row.CompletionId!.Value);
                    foreach (var item in batch) {
                        if (!completions.TryGetValue(item.Original.Completion.Id.Value, out var row) || !row.Matches(binding)) {
                            throw new InvalidDataException("Pending native deletion has no matching completion in the active dataset.");
                        }
                        if (row.Interpret(item.Original, item.Obligation) is ProjectDeletionCompletionObservation.Unavailable unavailable) {
                            throw new InvalidDataException($"Pending native deletion completion is unavailable: {unavailable.Reason}.");
                        }
                    }
                }
            }
        }
    }

    public async Task RequireRotationAllowedEnlistedAsync(DatasetActivationBinding binding, CancellationToken cancellationToken = default) {
        await using var context = await transactions.CreateEnlistedAsync(options, static configured => new ProjectsDbContext(configured), cancellationToken);
        await DatasetLifecycleStore.RequireActiveForMutationAsync(context, binding, cancellationToken);
        string query = $"""
            SELECT COUNT(*) > 0 AS "Value" FROM (
                SELECT "CompletionId" FROM "Projects_DeletionCompletions"
                WHERE {ProjectDeletionCompletionRecord.AwaitingFilter} ORDER BY "CompletionId" LIMIT 1
            ) AS awaiting
            """;
        if (await context.Database.SqlQueryRaw<bool>(query).SingleAsync(cancellationToken)) {
            throw new InvalidOperationException("Dataset reactivation requires release of every Awaiting project deletion completion.");
        }
        foreach (var inventory in inventories) {
            if (await inventory.HasPendingAsync(cancellationToken)) {
                throw new InvalidOperationException($"Dataset reactivation requires completion of pending native deletion work for {inventory.Id}.");
            }
        }
    }
}
