using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectOperationMaintenanceTests {
    private static readonly DateTimeOffset Start = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly DatasetActivationBinding Binding = new(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
        DatasetLifecycleRecord.CurrentSchemaVersion, DatasetGuardProtocol.SharedRows);

    [Theory]
    [InlineData(4, 300, false)]
    [InlineData(5, 300, true)]
    [InlineData(60, 300, true)]
    [InlineData(61, 300, false)]
    [InlineData(10, 19, false)]
    [InlineData(10, 20, true)]
    [InlineData(10, 3600, true)]
    [InlineData(10, 3601, false)]
    public void Scheduling_bounds_are_independent_of_issued_replay_promises(int intervalSeconds, int warningSeconds, bool valid) {
        var options = new ProjectDefinitionOperationOptions {
            MaintenanceInterval = TimeSpan.FromSeconds(intervalSeconds),
            MaintenanceWarningAfter = TimeSpan.FromSeconds(warningSeconds)
        };

        Assert.Equal(valid, options.IsValid());
        Assert.Equal(TimeSpan.FromDays(7), options.ReplayRetention);
        Assert.Equal(TimeSpan.FromMinutes(1), new ProjectDefinitionOperationOptions().MaintenanceInterval);
        if (!valid) {
            Assert.Throws<InvalidOperationException>(() => options.GetLimits());
        }
    }

    [Fact]
    public void Persistent_eligible_work_without_progress_warns_once_then_reports_observed_relief() {
        var health = new FiniteOperationMaintenanceHealth(TimeSpan.FromMinutes(5));
        var stalled = Result(FiniteOperationMaintenanceStop.Contention);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, Observe(health, stalled, 0).Transition);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.Warning, Observe(health, stalled, 5).Transition);
        Assert.Equal(FiniteOperationMaintenanceHealthState.NoProgress, Observe(health, stalled, 6).State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, Observe(health, stalled, 7).Transition);
        var resumed = Observe(health, Result(FiniteOperationMaintenanceStop.Empty, rows: 1,
            eligible: FiniteOperationEligibleWork.NoneObserved), 8);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.Recovery, resumed.Transition);
        Assert.Equal(TimeSpan.Zero, resumed.SinceProgress);
    }

    [Fact]
    public void Repeated_capped_progress_is_pressure_and_each_successful_batch_does_not_clear_it() {
        var health = new FiniteOperationMaintenanceHealth(TimeSpan.FromMinutes(5));
        var capped = Result(FiniteOperationMaintenanceStop.BatchLimit, rows: 128);
        Observe(health, capped, 0);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.Warning, Observe(health, capped, 5).Transition);
        var stillCapped = Observe(health, capped with { Stop = FiniteOperationMaintenanceStop.Budget }, 6);
        Assert.Equal(FiniteOperationMaintenanceHealthState.CapacityPressure, stillCapped.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, stillCapped.Transition);
        Assert.Equal(TimeSpan.Zero, stillCapped.SinceProgress);
        var blockedRetirement = Observe(health, Result(FiniteOperationMaintenanceStop.Contention,
            eligible: FiniteOperationEligibleWork.NoneObserved) with { Retirement = FiniteOperationRetirementStatus.Busy }, 7);
        Assert.Equal(FiniteOperationMaintenanceHealthState.CapacityPressure, blockedRetirement.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, blockedRetirement.Transition);
        var relief = Observe(health, Result(FiniteOperationMaintenanceStop.Empty,
            eligible: FiniteOperationEligibleWork.NoneObserved), 8);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.Recovery, relief.Transition);
    }

    [Fact]
    public void Unknown_observation_and_failure_preserve_an_existing_pressure_warning() {
        var health = new FiniteOperationMaintenanceHealth(TimeSpan.FromMinutes(5));
        Observe(health, Result(FiniteOperationMaintenanceStop.Contention), 0);
        Observe(health, Result(FiniteOperationMaintenanceStop.Contention), 5);
        var unknown = Result(FiniteOperationMaintenanceStop.Failed, eligible: FiniteOperationEligibleWork.Unknown);
        var observed = Observe(health, unknown, 6);
        Assert.Equal(FiniteOperationMaintenanceHealthState.NoProgress, observed.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, observed.Transition);
        Assert.Null(observed.SinceProgress);
        var partiallyFailed = Observe(health, Result(FiniteOperationMaintenanceStop.Failed, rows: 128), 7);
        Assert.Equal(FiniteOperationMaintenanceHealthState.NoProgress, partiallyFailed.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, partiallyFailed.Transition);
        Assert.Equal(TimeSpan.Zero, partiallyFailed.SinceProgress);
    }

    [Fact]
    public void Profile_or_namespace_change_resets_bounded_warning_state_without_claiming_recovery() {
        var health = new FiniteOperationMaintenanceHealth(TimeSpan.FromMinutes(5));
        var stalled = Result(FiniteOperationMaintenanceStop.Contention);
        Observe(health, stalled, 0);
        Observe(health, stalled, 5);
        var switched = health.Observe(Guid.NewGuid(), Binding, stalled, Start.AddMinutes(6));
        Assert.Equal(FiniteOperationMaintenanceHealthState.Healthy, switched.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, switched.Transition);
        var replaced = new DatasetActivationBinding(new DatasetNamespaceId(Guid.NewGuid()), Guid.NewGuid(),
            Binding.SchemaVersion, Binding.GuardProtocol);
        var renewed = health.Observe(Profile, replaced, stalled, Start.AddMinutes(7));
        Assert.Equal(FiniteOperationMaintenanceHealthState.Healthy, renewed.State);
        Assert.Equal(FiniteOperationMaintenanceHealthTransition.None, renewed.Transition);
    }

    private static FiniteOperationMaintenanceHealthObservation Observe(FiniteOperationMaintenanceHealth health,
        FiniteOperationMaintenanceResult result, int minutes) => health.Observe(Profile, Binding, result, Start.AddMinutes(minutes));

    private static FiniteOperationMaintenanceResult Result(FiniteOperationMaintenanceStop stop, int rows = 0,
        FiniteOperationEligibleWork eligible = FiniteOperationEligibleWork.Present) => new(1, rows, rows * 3L,
            TimeSpan.FromMilliseconds(20), FiniteOperationRetirementStatus.Unchanged, eligible,
            eligible == FiniteOperationEligibleWork.Present ? Start.AddDays(-1) : null, stop, null);
}
