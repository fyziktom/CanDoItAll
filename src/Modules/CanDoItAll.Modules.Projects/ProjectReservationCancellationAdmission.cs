using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Projects;

public sealed partial class ProjectWriteAdmissionService {
    public async Task<ProjectReservationCancellationRequest> CaptureReservationCancellationAsync(ProjectCreationReservation original,
        CancellationToken cancellationToken = default) {
        ValidateReservation(original);
        var binding = await CaptureCreationBindingAsync(cancellationToken);
        ProjectReservationCancellationRequest? result = null;
        await ReadCreationAuthorityAsync(binding, dbContextFactory.CreateDbContextAsync, async context => {
            var row = await context.Set<ProjectCreationReservationRecord>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == original.Id, cancellationToken);
            if (row is null || !Matches(row, original) || row.NamespaceId is not { } ns || ns == Guid.Empty ||
                    row.State is not (ProjectCreationReservationState.Reserved or ProjectCreationReservationState.Cancelled) ||
                    row.ConsumedAtUtc is not null) {
                throw new InvalidOperationException("Cancellation requires the retained exact native never-consumed reservation.");
            }
            RequireReservationNamespace(row, binding.Binding.Namespace);
            result = new(new(ns), original);
        }, cancellationToken);
        return result ?? throw new InvalidOperationException("The original reservation was not captured.");
    }

    public async Task RequireReservedCancellationForMutationAsync(ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        var binding = await CaptureCreationBindingAsync(cancellationToken);
        if (binding.Binding.Namespace != original.Completion.Completion.Namespace) {
            throw new InvalidOperationException("The original reserved cancellation belongs to another dataset namespace.");
        }
        await ReadCreationAuthorityAsync(binding, token => coordinatedTransaction.CreateEnlistedAsync(contextOptions,
            static options => new ProjectsDbContext(options), token), async context => {
            await RequireReservationCancellationRowAsync(context, original, ProjectCreationReservationState.Reserved, cancellationToken);
            if (original.Target.Parent is { } parent) {
                await RequireAsync(context, parent, cancellationToken);
            }
        }, cancellationToken);
    }

    internal async Task CancelReservedCreationAsync(ProjectsDbContext context, ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken) {
        var row = await RequireReservationCancellationRowAsync(context, original, ProjectCreationReservationState.Reserved, cancellationToken);
        row.State = ProjectCreationReservationState.Cancelled;
        row.CancelledAtUtc = DateTimeOffset.UtcNow;
    }

    internal async Task RequireCancelledReservationAsync(ProjectsDbContext context, ProjectReservationCancellationPreparationContext original,
        CancellationToken cancellationToken) {
        if (context.ChangeTracker.HasChanges()) {
            throw new InvalidOperationException("Final reservation cancellation admission requires the saved owner transaction.");
        }
        await RequireReservationCancellationRowAsync(context, original, ProjectCreationReservationState.Cancelled, cancellationToken);
    }

    private async Task<ProjectCreationReservationRecord> RequireReservationCancellationRowAsync(ProjectsDbContext context,
        ProjectReservationCancellationPreparationContext original, ProjectCreationReservationState state,
        CancellationToken cancellationToken) {
        var target = original.Target;
        if (!context.Database.IsNpgsql() || context.Database.CurrentTransaction is null ||
                target.Project.DatabaseProfileId != DatabaseProfileId) {
            throw new InvalidOperationException("Reserved cancellation requires its original PostgreSQL owner transaction.");
        }
        var row = await context.Set<ProjectCreationReservationRecord>()
            .SingleOrDefaultAsync(value => value.Id == target.ReservationId, cancellationToken);
        if (row is null || row.ImportedHistory is not null || row.NamespaceId != original.Completion.Completion.Namespace.Value ||
                row.DatabaseProfileId != target.Project.DatabaseProfileId || row.ProjectId != target.Project.ProjectId ||
                row.LifetimeId != target.Project.LifetimeId || row.RequesterId != target.RequesterId ||
                row.ParentProjectId != target.Parent?.ProjectId || row.ParentLifetimeId != target.Parent?.LifetimeId ||
                row.State != state || row.ConsumedAtUtc is not null ||
                state == ProjectCreationReservationState.Reserved && row.CancelledAtUtc is not null ||
                state == ProjectCreationReservationState.Cancelled && row.CancelledAtUtc is null ||
                await context.Set<Project>().AnyAsync(value => value.Id == target.Project.ProjectId, cancellationToken)) {
            throw new InvalidOperationException("The exact original never-created reservation no longer permits cancellation.");
        }
        return row;
    }
}
