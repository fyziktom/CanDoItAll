using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.SchedulerPlanner;

namespace CanDoItAll.Tests.Unit;

public sealed class SchedulerPreparedWorkflowLaunchTests {
    [Fact]
    public async Task Accepted_launch_observer_failure_keeps_the_exact_exception_and_prepared_run() {
        var fixture = new Fixture();
        var failure = new ArgumentException("Observer lost acknowledgement");
        fixture.Launcher.ObservationFailure = failure;
        var result = await fixture.Target.LaunchAsync(fixture.Plan, fixture.Context);
        Assert.Same(failure, result.ObservationException);
        Assert.Equal(fixture.Context.PreparedRunId!.Value.Value, result.TargetRunId);
        Assert.Equal(SchedulerPlanRunDispatchStatus.ObservationPending, result.DispatchStatus);
        Assert.True(result.RequiresObservation);
        var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(Assert.Single(fixture.Launcher.Intents).Origin);
        Assert.Equal(fixture.Context.PreparedRunId, origin.PreparedRunId);
        Assert.Same(fixture.Context.StructureAuthority, origin.StructureAuthority);
        Assert.Equal(WorkflowStructureAuthorityChannel.LocalOperator, origin.StructureAuthority!.Channel);
        Assert.Equal(WorkflowLaunchActorKind.User, origin.StructureAuthority.Principal.Kind);
    }

    [Fact]
    public async Task Exact_saved_run_is_observed_when_the_plan_can_no_longer_launch_and_event_failure_does_not_erase_identity() {
        var fixture = new Fixture();
        fixture.Runtime.Run = fixture.Launcher.CreateRun(fixture.Context);
        fixture.Runtime.EventFailure = new IOException("Event store unavailable");
        var result = await fixture.Target.LaunchAsync(fixture.Plan, fixture.Context with { MayLaunch = false });
        Assert.Empty(fixture.Launcher.Intents);
        Assert.Same(fixture.Runtime.EventFailure, result.ObservationException);
        Assert.Equal(fixture.Runtime.Run.RunId.Value, result.TargetRunId);
        Assert.Equal(new[] { fixture.Runtime.Run.RunId }, fixture.Runtime.Lookups);
    }

    [Fact]
    public async Task Mismatched_saved_origin_is_rejected_without_launching_or_broad_run_search() {
        var fixture = new Fixture();
        fixture.Runtime.Run = fixture.Launcher.CreateRun(fixture.Context) with { VersionId = WorkflowVersionId.New() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Target.LaunchAsync(fixture.Plan, fixture.Context));
        Assert.Empty(fixture.Launcher.Intents);
        Assert.Single(fixture.Runtime.Lookups);
    }

    private sealed class Fixture {
        public SchedulerPlan Plan { get; } = new() {
            Id = Guid.NewGuid(), TargetKind = SchedulerPlanTargetKind.Workflow, TargetId = Guid.NewGuid(), TargetVersionId = Guid.NewGuid(), InputJson = "{}"
        };
        public SchedulerTargetLaunchContext Context { get; }
        public LaunchStub Launcher { get; }
        public RuntimeProxy Runtime { get; }
        public SchedulerTargetLauncher Target { get; }

        public Fixture() {
            var runtime = DispatchProxy.Create<IWorkflowRuntimeManager, RuntimeProxy>();
            Runtime = (RuntimeProxy)(object)runtime;
            Context = new(Plan.Id, Guid.NewGuid(), new(Guid.NewGuid()), DateTimeOffset.UtcNow, new(Guid.NewGuid())) {
                PreparedRunId = WorkflowRunId.New(),
                StructureAuthority = new(WorkflowStructureAuthorityChannel.LocalOperator, new(WorkflowLaunchActorKind.User, "local-operator"),
                    Guid.NewGuid(), Guid.Empty, true, true, null, "fixture-policy") { AllProjects = true }
            };
            Launcher = new(Plan);
            Target = new(Launcher, runtime);
        }
    }

    private sealed class LaunchStub(SchedulerPlan plan) : IWorkflowLaunchService {
        public Exception? ObservationFailure { get; set; }
        public List<WorkflowLaunchIntent> Intents { get; } = [];

        public Task<WorkflowLaunchResult> LaunchAsync(WorkflowLaunchIntent intent, CancellationToken cancellationToken = default) {
            Intents.Add(intent);
            var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(intent.Origin);
            var now = DateTimeOffset.UtcNow;
            var definition = new WorkflowDefinition(new(plan.TargetId), new(plan.TargetVersionId!.Value), "Prepared target", "Fixture", WorkflowLifecycleStatus.Active,
                new(new("start"), [], []), new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), now, now);
            var resolved = new WorkflowResolvedRuntimeRequest(definition, intent.InputJson,
                new(WorkflowRuntimeBackendKind.InProcess, "Fixture", false, true, true, true, "Fixture"), intent.PreviewSimulationPlan,
                intent.Mode, origin, intent.CompletionPolicy, intent.Idempotency, now);
            var context = new SchedulerTargetLaunchContext(origin.PlanId, origin.PlanRunId, origin.FireId, origin.FiredAtUtc, origin.CorrelationId) {
                PreparedRunId = origin.PreparedRunId, StructureAuthority = origin.StructureAuthority
            };
            return Task.FromResult(new WorkflowLaunchResult(CreateRun(context), resolved, WorkflowLaunchIdempotencyDisposition.EnforcedNewRun) {
                ObservationException = ObservationFailure,
                Observation = ObservationFailure is null ? WorkflowLaunchObservation.Confirmed : WorkflowLaunchObservation.RecoveredAfterObserverFailure
            });
        }

        public WorkflowRunSnapshot CreateRun(SchedulerTargetLaunchContext context) => new(context.PreparedRunId!.Value,
            new(plan.TargetId), new(plan.TargetVersionId!.Value), WorkflowRunState.Completed, WorkflowRuntimeBackendKind.InProcess,
            "fixture", "Completed", context.FiredAtUtc, context.FiredAtUtc) {
            Origin = new WorkflowLaunchOrigin.SchedulerPlanRun(context.PlanId, context.PlanRunId, context.SchedulerFireId, context.FiredAtUtc, context.CorrelationId) {
                PreparedRunId = context.PreparedRunId, StructureAuthority = context.StructureAuthority
            }
        };
    }

    public class RuntimeProxy : DispatchProxy {
        public WorkflowRunSnapshot? Run { get; set; }
        public Exception? EventFailure { get; set; }
        public List<WorkflowRunId> Lookups { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IWorkflowRuntimeManager.GetRunAsync)) {
                Lookups.Add((WorkflowRunId)args![0]!);
                return Task.FromResult(Run);
            }
            if (targetMethod?.Name == nameof(IWorkflowRuntimeManager.ListEventsAsync)) {
                return EventFailure is null ? Task.FromResult<IReadOnlyList<WorkflowEventRecord>>([])
                    : Task.FromException<IReadOnlyList<WorkflowEventRecord>>(EventFailure);
            }
            throw new InvalidOperationException($"Unexpected Workflow runtime query '{targetMethod?.Name}'; only exact-run observation is allowed.");
        }
    }
}
