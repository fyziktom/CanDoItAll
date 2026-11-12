using System.Collections.Immutable;

namespace CanDoItAll.Modules.Projects;

public interface IProjectDeletionPendingInventory {
    ProjectDeletionParticipantId Id { get; }
    Task<bool> HasPendingAsync(CancellationToken cancellationToken = default);
    Task<ProjectDeletionPendingPage> ReadPageAsync(Guid databaseProfileId, Guid? after,
        CancellationToken cancellationToken = default);
}

public sealed record ProjectDeletionPendingBinding(ProjectDeletionPreparationContext Original, ProjectDeletionObligation Obligation);

public sealed class ProjectDeletionPendingPage {
    public const int MaximumCount = 128;

    public ProjectDeletionPendingPage(IEnumerable<ProjectDeletionPendingBinding> items) {
        Items = items.Take(MaximumCount + 1).ToImmutableArray();
        if (Items.Length > MaximumCount || Items.Any(item => item is null || item.Original is null || item.Obligation is null) ||
                Items.Select(item => item.Obligation.RecoveryId).Distinct().Count() != Items.Length) {
            throw new InvalidDataException("The pending deletion inventory page is invalid or exceeds its bound.");
        }
    }

    public ImmutableArray<ProjectDeletionPendingBinding> Items { get; }
}
