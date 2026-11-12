using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionSingleActivityAdmissionTests {
    [Fact]
    public void Original_single_activity_is_supported() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        ProcessDecisionSingleActivityAdmission.Require(fixture.State, fixture.Input, fixture.Contract);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Managed_execution_port_is_preserved_but_cannot_authorize_an_additional_host_obligation(bool additional) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        ProcessHostCapabilityId[] required = additional
            ? [ProcessHostCapabilityIds.ManagedProcessAdapter, new("host.workspace")]
            : [ProcessHostCapabilityIds.ManagedProcessAdapter];
        var step = fixture.State.Steps[0] with { RequiredHostCapabilities = required.ToHashSet() };
        var input = fixture.Input with { Requirements = fixture.Input.RequireRequirements() with { RequiredHostCapabilities = required } };
        Assert.Equal(!additional, ProcessDecisionReviewCompletion.SupportsRuntimeContract(step));
        if (additional) {
            Assert.Throws<NotSupportedException>(() => ProcessDecisionSingleActivityAdmission.Require(fixture.State with { Steps = [step] }, input, fixture.Contract));
        } else {
            ProcessDecisionSingleActivityAdmission.Require(fixture.State with { Steps = [step] }, input, fixture.Contract);
        }
        Assert.Equal(required, input.RequireRequirements().RequiredHostCapabilities);
        Assert.True(step.RequiredHostCapabilities.SetEquals(required));
    }

    [Fact]
    public void Legacy_input_without_compiled_requirements_refuses_explicitly() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        Assert.Throws<ProcessAcceptedStepRequirementsRequiredException>(() =>
            ProcessDecisionSingleActivityAdmission.Require(fixture.State, fixture.Input with { Requirements = null }, fixture.Contract));
    }

    [Theory]
    [InlineData(Unsupported.Branch)]
    [InlineData(Unsupported.Subprocess)]
    [InlineData(Unsupported.Tool)]
    [InlineData(Unsupported.Host)]
    [InlineData(Unsupported.Outcome)]
    [InlineData(Unsupported.Descriptor)]
    [InlineData(Unsupported.Mapping)]
    [InlineData(Unsupported.Input)]
    [InlineData(Unsupported.Output)]
    [InlineData(Unsupported.Gate)]
    [InlineData(Unsupported.RuntimeInput)]
    [InlineData(Unsupported.RuntimeHost)]
    public void Unsupported_original_or_runtime_obligations_are_never_silently_dropped(Unsupported kind) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var requirements = fixture.Input.RequireRequirements();
        var assignment = fixture.Input.Assignment;
        ArtifactSlotId slot = new(Id(81));
        requirements = kind switch {
            Unsupported.Branch => requirements with { Kind = ProcessStepKind.Branch },
            Unsupported.Subprocess => requirements with { StartsSubprocess = true },
            Unsupported.Tool => requirements with { RequiredRuntimeToolNames = ["workspace_read"] },
            Unsupported.Host => requirements with { RequiredHostCapabilities = [new("host.workspace")] },
            Unsupported.Outcome => requirements with { ConfiguredBranchOutcomeIds = [new("accepted")] },
            Unsupported.Descriptor => requirements with { ArtifactDescriptors = [
                new(slot, "output", "review-step", "report", "Report", "text", "report",
                    ProcessArtifactMaterializationMode.AgentWritten) { PayloadSchema = "schema" }
            ] },
            Unsupported.Mapping => requirements with { SubprocessArtifactMappings = [
                new(slot, "report", "child", [new("child-step", "report", "Child report", "accepted")], [])
            ] },
            _ => requirements
        };
        assignment = kind switch {
            Unsupported.Input => assignment with { RequiredArtifactSlotIds = [slot] },
            Unsupported.Output => assignment with { ProducedArtifactSlotIds = [slot] },
            Unsupported.Gate => assignment with { BranchGate = new("earlier-step", "accepted") },
            _ => assignment
        };
        var step = fixture.State.Steps[0];
        step = kind switch {
            Unsupported.RuntimeInput => step with { RequiredArtifactSlots = new HashSet<ArtifactSlotId> { slot } },
            Unsupported.RuntimeHost => step with { RequiredHostCapabilities = new HashSet<ProcessHostCapabilityId> { new("host.workspace") } },
            _ => step
        };
        Assert.Throws<NotSupportedException>(() => ProcessDecisionSingleActivityAdmission.Require(
            fixture.State with { Steps = [step] },
            fixture.Input with { Assignment = assignment, Requirements = requirements }, fixture.Contract));
    }

    [Theory]
    [InlineData(Identity.Root)]
    [InlineData(Identity.Run)]
    [InlineData(Identity.Plan)]
    [InlineData(Identity.Admission)]
    [InlineData(Identity.Step)]
    [InlineData(Identity.Definition)]
    [InlineData(Identity.PolicyStep)]
    [InlineData(Identity.MissingPolicy)]
    public void Input_from_a_different_original_execution_is_refused(Identity changed) {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        var input = fixture.Input;
        var assignment = input.Assignment;
        input = changed switch {
            Identity.Root => input with { RootRunId = new(Id(90)) },
            Identity.Admission => input with { AdmissionId = new(Id(90)) },
            Identity.Definition => input with { Requirements = input.RequireRequirements() with { StepDefinitionId = new(Id(90)) } },
            _ => input
        };
        assignment = changed switch {
            Identity.Run => assignment with { RunId = new(Id(90)) },
            Identity.Plan => assignment with { PlanId = new(Id(90)) },
            Identity.Step => assignment with { StepInstanceId = new(Id(90)) },
            Identity.PolicyStep => assignment with { DecisionDataPolicy = assignment.DecisionDataPolicy! with { StepInstanceId = new(Id(90)) } },
            Identity.MissingPolicy => assignment with { DecisionDataPolicy = null },
            _ => assignment
        };
        Assert.Throws<InvalidDataException>(() =>
            ProcessDecisionSingleActivityAdmission.Require(fixture.State, input with { Assignment = assignment }, fixture.Contract));
    }

    [Fact]
    public void A_different_registered_contract_cannot_borrow_the_original_admission() {
        var fixture = new ProcessDecisionReviewCoordinatorFixture();
        Assert.Throws<InvalidDataException>(() => ProcessDecisionSingleActivityAdmission.Require(
            fixture.State, fixture.Input, fixture.Contract with { Version = fixture.Contract.Version + 1 }));
    }

    public enum Unsupported { Branch, Subprocess, Tool, Host, Outcome, Descriptor, Mapping, Input, Output, Gate, RuntimeInput, RuntimeHost }
    public enum Identity { Root, Run, Plan, Admission, Step, Definition, PolicyStep, MissingPolicy }
}
