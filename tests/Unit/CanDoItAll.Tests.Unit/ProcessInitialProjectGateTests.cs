using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessInitialProjectGateTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void An_initial_executable_Project_is_locked_even_without_a_run_Project(bool global) {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = ProcessProjectAdmissionFixture.Initial();
        request = request with { InitialPlan = request.InitialPlan! with {
            ExecutableDefinitions = new(project.DatabaseProfileId, global ? Guid.Empty : project.ProjectId,
                global ? Guid.Empty : project.LifetimeId, "definition", new Dictionary<string, CanDoItAll.Processes.Builder.ProcessExecutableDefinitionSource>())
        } };
        Assert.Equal(global ? [] : new[] { project }, ProcessInitialProjectGates.Capture(request));
    }

    [Fact]
    public void Conflicting_executable_and_run_lifetimes_are_rejected_before_acquisition() {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = ProcessProjectAdmissionFixture.Initial(project);
        request = request with { InitialPlan = request.InitialPlan! with {
            ExecutableDefinitions = new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid(), "definition",
                new Dictionary<string, CanDoItAll.Processes.Builder.ProcessExecutableDefinitionSource>())
        } };
        Assert.Throws<InvalidOperationException>(() => ProcessInitialProjectGates.Capture(request));
    }

    [Fact]
    public void Existing_runtime_commands_do_not_acquire_Project_gates() {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var request = ProcessProjectAdmissionFixture.Cancel(ProcessProjectAdmissionFixture.Initial(project).OriginalState);
        Assert.Empty(ProcessInitialProjectGates.Capture(request));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Prepared_target_must_match_its_original_admission(bool mismatch) {
        var project = new ProcessProjectAdmission(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var prepared = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project),
            target: new(mismatch ? Guid.NewGuid() : project.ProjectId, "original-node", "original-binding"));
        if (mismatch) {
            Assert.Throws<InvalidOperationException>(() => ProcessInitialProjectGates.Capture(prepared.InitialCommit, prepared));
        } else {
            Assert.Equal(project, Assert.Single(ProcessInitialProjectGates.Capture(prepared.InitialCommit, prepared)));
        }
    }
}
