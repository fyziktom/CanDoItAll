using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectCreationDatasetBinding(ResolvedDatabaseProfile Profile, long Generation,
    DatasetActivationBinding Binding);

public sealed partial class ProjectWriteAdmissionService {
    internal async Task<ProjectCreationDatasetBinding> CaptureCreationBindingAsync(CancellationToken cancellationToken) {
        var profile = canonicalDatabase.Profile;
        var generation = canonicalDatabase.Generation;
        if (profile.Profile.ProviderKind != DatabaseProviderKind.PostgreSql) {
            throw new NotSupportedException("Namespace-bound project creation requires PostgreSQL.");
        }
        var observed = await activation.ObserveActiveBindingAsync(profile.Profile.Id, cancellationToken);
        var original = new ProjectCreationDatasetBinding(profile, generation, observed.Record.Plan.Binding);
        RequireCreationOwner(original);
        return original;
    }

    internal void RequireCreationOwner(ProjectCreationDatasetBinding original) {
        if (canonicalDatabase.Profile != original.Profile || canonicalDatabase.Generation != original.Generation) {
            throw new InvalidOperationException("The original project creation profile changed.");
        }
    }

    internal async Task RequireCreationBindingAsync(ProjectCreationDatasetBinding original, CancellationToken cancellationToken) {
        RequireCreationOwner(original);
        var observed = await activation.ObserveActiveBindingAsync(original.Profile.Profile.Id, cancellationToken);
        RequireCreationOwner(original);
        if (observed.Record.Plan.Binding != original.Binding) {
            throw new DatasetActivationMismatchException(original.Binding);
        }
    }

    internal static void RequireReservationNamespace(ProjectCreationReservationRecord record, DatasetNamespaceId original) {
        ArgumentNullException.ThrowIfNull(original);
        if (record.ImportedHistory is not null || record.NamespaceId is null || record.NamespaceId != original.Value) {
            throw new InvalidOperationException("The reservation has no native authority in the original dataset namespace.");
        }
    }

    private async Task ReadCreationAuthorityAsync(ProjectCreationDatasetBinding original,
        Func<CancellationToken, Task<ProjectsDbContext>> open, Func<ProjectsDbContext, Task> observe,
        CancellationToken cancellationToken) {
        ProjectsDbContext? context = null;
        Exception? failure = null;
        try {
            RequireCreationOwner(original);
            context = await open(cancellationToken);
            RequireCreationOwner(original);
            CoordinatedDatabaseTransaction.ForProfile(original.Profile).RequireOwnerProfile(context);
            await observe(context);
            RequireCreationOwner(original);
        } catch (Exception readFailure) {
            failure = readFailure;
        }
        if (context is not null) {
            try {
                await context.DisposeAsync();
            } catch (Exception cleanupFailure) {
                failure = failure is null ? cleanupFailure : new AggregateException(
                    "Creation authority observation and context disposal both failed.", failure, cleanupFailure);
            }
        }
        if (failure is not null) {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
        await RequireCreationBindingAsync(original, cancellationToken);
    }
}
