using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessAcceptedStepRequirementsTests(ITestOutputHelper output) {
    [Fact]
    public void Capture_preserves_compiled_and_runtime_obligations_without_transient_availability() {
        var initial = Initial();
        var assignment = initial.InitialAssignments!.Single();
        var expected = new ProcessAcceptedStepRequirementsCapture(initial).Capture(assignment);
        Assert.Equal(initial.InitialPlan!.Steps[0].StepDefinitionId, expected.StepDefinitionId);
        Assert.Equal(ProcessStepKind.Branch, expected.Kind);
        Assert.True(expected.StartsSubprocess);
        Assert.Equal(["browser_navigate", "workspace_read"], expected.RequiredRuntimeToolNames);
        Assert.Equal(["host.browser", "host.workspace"], expected.RequiredHostCapabilities.Select(item => item.Value));
        Assert.Equal(["a", "b", "a"], expected.ConfiguredBranchOutcomeIds.Select(item => item.Value));
        Assert.Equal(initial.Mutation.State.Steps[0].ArtifactDescriptors, expected.ArtifactDescriptors);
        Assert.Equal(initial.Mutation.State.Steps[0].SubprocessArtifactMappings, expected.SubprocessArtifactMappings);
        var changed = initial with { Mutation = initial.Mutation with { State = initial.Mutation.State with {
            ConnectedInputArtifacts = [new(assignment.StepInstanceId, assignment.RequiredArtifactSlotIds[0],
                ProcessArtifactInputAvailability.Available, null, ArtifactInstanceId.New(), "changed", "changed")]
        } } };
        Assert.Equal(JsonSerializer.Serialize(expected),
            JsonSerializer.Serialize(new ProcessAcceptedStepRequirementsCapture(changed).Capture(assignment)));
        var header = "sha256:" + new string('0', 64);
        var metadata = ProcessAcceptedSourceCodec.EncodeAssignment(ProcessAssignmentSource.From(assignment), header);
        var encoded = ProcessAcceptedExecutionInputCodec.Capture(assignment, expected, header, metadata);
        var decoded = ProcessAcceptedExecutionInputCodec.Read(ProcessAcceptedExecutionInputVersion.OriginalStepRequirements, encoded, header, metadata);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(decoded.Requirements));
    }

    [Theory]
    [InlineData(Mismatch.Run)]
    [InlineData(Mismatch.Plan)]
    [InlineData(Mismatch.PlanHash)]
    [InlineData(Mismatch.Step)]
    [InlineData(Mismatch.Definition)]
    [InlineData(Mismatch.Key)]
    [InlineData(Mismatch.NonExecutablePlan)]
    [InlineData(Mismatch.NonExecutableRuntime)]
    [InlineData(Mismatch.MissingTool)]
    [InlineData(Mismatch.MissingHost)]
    [InlineData(Mismatch.MissingInput)]
    [InlineData(Mismatch.MissingOutput)]
    [InlineData(Mismatch.DuplicateInput)]
    [InlineData(Mismatch.DuplicatePlanStep)]
    [InlineData(Mismatch.DuplicateRuntimeStep)]
    public void Capture_refuses_inconsistent_original_evidence(Mismatch change) {
        var initial = Initial();
        var assignment = initial.InitialAssignments!.Single();
        var plan = initial.InitialPlan!;
        var state = initial.Mutation.State;
        var planned = plan.Steps[0];
        var runtime = state.Steps[0];
        assignment = change switch {
            Mismatch.Run => assignment with { RunId = ProcessRunId.New() },
            Mismatch.Plan => assignment with { PlanId = ProcessInstancePlanId.New() },
            Mismatch.Step => assignment with { StepInstanceId = ProcessStepInstanceId.New() },
            Mismatch.Key => assignment with { StepKey = "different" },
            Mismatch.MissingInput => assignment with { RequiredArtifactSlotIds = [] },
            Mismatch.MissingOutput => assignment with { ProducedArtifactSlotIds = [] },
            Mismatch.DuplicateInput => assignment with { RequiredArtifactSlotIds = [assignment.RequiredArtifactSlotIds[0], assignment.RequiredArtifactSlotIds[0]] },
            _ => assignment
        };
        runtime = change switch {
            Mismatch.Definition => runtime with { StepDefinitionId = ProcessStepDefinitionId.New() },
            Mismatch.NonExecutableRuntime => runtime with { IsExecutable = false },
            Mismatch.MissingTool => runtime with { RequiredRuntimeToolNames = ["workspace_read"] },
            Mismatch.MissingHost => runtime with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId>() },
            _ => runtime
        };
        if (change == Mismatch.NonExecutablePlan) {
            planned = planned with { IsExecutable = false };
        }
        plan = plan with { Steps = change == Mismatch.DuplicatePlanStep ? [planned, planned] : [planned] };
        state = state with {
            PlanHash = change == Mismatch.PlanHash ? "different" : state.PlanHash,
            Steps = change == Mismatch.DuplicateRuntimeStep ? [runtime, runtime] : [runtime]
        };
        initial = initial with { InitialPlan = plan, Mutation = initial.Mutation with { State = state } };
        Assert.Throws<InvalidDataException>(() => new ProcessAcceptedStepRequirementsCapture(initial).Capture(assignment));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    [InlineData(1024)]
    public void Each_assignment_uses_indexes_without_reenumerating_the_whole_route_table(int count) {
        var initial = ProcessPreparedLaunchScalingFixture.Create(count, 256).InitialCommit;
        var routes = new ObservedRoutes(initial.InitialPlan!.Steps.Select(step => Route(step.StepDefinitionId, "a", "b")).ToArray());
        initial = initial with { InitialPlan = initial.InitialPlan with { Branches = new(routes) } };
        var capture = new ProcessAcceptedStepRequirementsCapture(initial);
        var initialEnumerations = routes.Enumerations;
        Assert.InRange(initialEnumerations, 1, 2);
        foreach (var assignment in initial.InitialAssignments!) {
            var requirements = capture.Capture(assignment);
            Assert.Equal("b", Assert.Single(requirements.ConfiguredBranchOutcomeIds).Value);
        }
        Assert.Equal(initialEnumerations, routes.Enumerations);
    }

    [Theory]
    [InlineData(1, 256)]
    [InlineData(128, 256)]
    [InlineData(1024, 256)]
    [InlineData(128, 1_048_576)]
    public void Combined_projection_stays_within_the_shared_capacity_budget(int count, int neighboringPromptCharacters) {
        var preparation = ProcessPreparedLaunchScalingFixture.Create(count, 256);
        var assignments = preparation.InitialCommit.InitialAssignments!.ToArray();
        assignments[0] = assignments[0] with { Prompt = new('x', neighboringPromptCharacters) };
        preparation = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = assignments } };
        var saved = ProcessPreparedLaunchCodec.Read(ProcessPreparedLaunchCodec.ToEntity(preparation));
        _ = ProcessAcceptedSourceCodec.Capture(saved);
        for (var round = 0; round < 3; round++) {
            var allocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            var projection = ProcessAcceptedSourceCodec.Capture(saved);
            var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            var bytes = Encoding.UTF8.GetByteCount(projection.Header.PayloadJson) +
                projection.Assignments.Values.Sum(item => Encoding.UTF8.GetByteCount(item.PayloadJson)) +
                projection.ExecutionInputs.Values.Sum(item => Encoding.UTF8.GetByteCount(item.PayloadJson));
            Assert.InRange(bytes, 1, ProcessAcceptedSourceCodec.MaximumGovernedProjectionUtf8Bytes);
            Assert.Equal(count, projection.ExecutionInputs.Count);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "accepted-requirements-capture", Steps = count, NeighborPromptCharacters = neighboringPromptCharacters,
                Round = round, ElapsedMilliseconds = elapsed, AllocatedBytes = allocated, TotalProjectionUtf8Bytes = bytes
            }));
        }
    }

    private static ProcessRuntimeCommitRequest Initial() {
        var initial = ProcessPreparedLaunchScalingFixture.Create(1, 256).InitialCommit;
        var input = ArtifactSlotId.New();
        var output = ArtifactSlotId.New();
        var planned = initial.InitialPlan!.Steps[0] with {
            Kind = ProcessStepKind.Branch, StartsSubprocess = true, RequiredRuntimeToolNames = ["browser_navigate"],
            RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { new("host.browser") }
        };
        var runtime = initial.Mutation.State.Steps[0] with {
            RequiredRuntimeToolNames = ["workspace_read", "browser_navigate"],
            RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { new("host.workspace"), new("host.browser") },
            RequiredArtifactSlots = new HashSet<ArtifactSlotId> { input },
            ProducedArtifactSlots = new HashSet<ArtifactSlotId> { output },
            ArtifactDescriptors = [new(output, "output", planned.StepKey, "report", "Report", "text", "report",
                ProcessArtifactMaterializationMode.AgentWritten) { PayloadSchema = "schema" }],
            SubprocessArtifactMappings = [new(output, "report", "child",
                [new("child-step", "report", "Child report", "accepted")], [new("child-step", "report", "Child report", "no-go")])]
        };
        var plan = initial.InitialPlan with {
            Steps = [planned],
            Branches = new([Route(planned.StepDefinitionId, "second", "a"), Route(planned.StepDefinitionId, "first", "b"),
                Route(planned.StepDefinitionId, "first", "a")])
        };
        plan = plan with { PlanHash = ProcessPlanHasher.Compute(plan) };
        var state = initial.Mutation.State with { PlanHash = plan.PlanHash, Steps = [runtime] };
        return initial with {
            InitialPlan = plan, OriginalState = state, Mutation = initial.Mutation with { State = state },
            InitialAssignments = [initial.InitialAssignments![0] with { RequiredArtifactSlotIds = [input], ProducedArtifactSlotIds = [output] }]
        };
    }

    private static BranchRoutePlan Route(ProcessStepDefinitionId step, string family, string outcome)
        => new(step, new(family), new(outcome), BranchOutcomeCategory.Continue, new(ProcessRouteTargetKind.NextStep), null);

    private sealed class ObservedRoutes(IReadOnlyList<BranchRoutePlan> routes) : IReadOnlyList<BranchRoutePlan> {
        public int Enumerations { get; private set; }
        public int Count => routes.Count;
        public BranchRoutePlan this[int index] => routes[index];
        public IEnumerator<BranchRoutePlan> GetEnumerator() {
            Enumerations++;
            return routes.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public enum Mismatch {
        Run, Plan, PlanHash, Step, Definition, Key, NonExecutablePlan, NonExecutableRuntime,
        MissingTool, MissingHost, MissingInput, MissingOutput, DuplicateInput, DuplicatePlanStep, DuplicateRuntimeStep
    }
}
