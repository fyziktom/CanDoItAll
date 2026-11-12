using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatasetActivationRestoration {
    public DatasetActivationRestoration(DatasetActivationPlan abandonedPlan) {
        ArgumentNullException.ThrowIfNull(abandonedPlan);
        if (abandonedPlan.Kind != DatasetActivationKind.Reactivate) {
            throw new ArgumentException("Only an exact reactivation plan has a restorable previous selection.");
        }
        AbandonedPlan = abandonedPlan;
    }

    public DatasetActivationPlan AbandonedPlan { get; }

    internal static DatasetActivationFileRecord Select(DatasetActivationFileRecord current,
        Guid expectedPlanId, DatasetNamespaceId expectedNamespace) {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(expectedNamespace);
        ArgumentOutOfRangeException.ThrowIfEqual(expectedPlanId, Guid.Empty);
        var abandoned = current.Restoration?.AbandonedPlan ?? current.Plan;
        if (abandoned.Kind != DatasetActivationKind.Reactivate || abandoned.PlanId != expectedPlanId ||
                abandoned.ExpectedPreviousSqlBinding?.Namespace != expectedNamespace ||
                current.Restoration is null && current.State != DatasetActivationFileState.Prepared) {
            throw new InvalidOperationException("Restoration requires the exact retained reactivation and previous namespace.");
        }
        if (current.Restoration is not null) {
            return current;
        }
        var previous = abandoned.ExpectedPreviousSqlBinding!;
        return new(DatasetActivationFileRecord.CurrentVersion, current.ProfileId, DatasetActivationFileState.Prepared,
            new(Guid.NewGuid(), DatasetActivationKind.Attach, previous, previous), new(abandoned));
    }
}
