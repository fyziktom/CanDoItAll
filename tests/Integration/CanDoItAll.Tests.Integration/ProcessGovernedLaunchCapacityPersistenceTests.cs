using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "LongRunning")]
public sealed class ProcessGovernedLaunchCapacityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Aggregate_input_or_metadata_overflow_cannot_partially_accept_a_prepared_launch(bool metadata) {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var source = await fixture.Services.GetRequiredService<ProjectProcessLaunchAuthorityService>()
            .CaptureLocalAsync(fixture.Project.ProjectId, ProcessLaunchOperatorSurface.UserInterface);
        var preparation = ProcessPreparedLaunchScalingFixture.Create(18, 256, source);
        var payload = new string('x', 1_000_000);
        var assignments = preparation.InitialCommit.InitialAssignments!.Select((item, index) => metadata
            ? index == 0 ? item : item with {
                DecisionDataPolicy = null, LaunchVariables = new Dictionary<string, string> { ["capacity-payload"] = payload }
            }
            : item with { Prompt = payload }).ToArray();
        preparation = preparation with { InitialCommit = preparation.InitialCommit with { InitialAssignments = assignments } };
        var store = fixture.Services.GetRequiredService<IProcessPreparedLaunchStore>();
        var saved = await store.PrepareAsync(preparation);
        await using (var accepting = Context(fixture.Services)) {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => ProcessRootMaterialFixture.Owner(accepting, fixture.Services)
                .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
            Assert.Equal("The accepted governed Process source exceeds its total UTF-8 byte limit.", failure.Message);
        }
        await using var observed = Context(fixture.Services);
        var run = preparation.InitialCommit.OriginalState.RunId.Value;
        Assert.False(await observed.RuntimeStates.AnyAsync(item => item.RunId == run));
        Assert.False(await observed.DecisionBudgets.AnyAsync(item => item.RunId == run));
        Assert.False(await observed.AcceptedAssignmentSources.AnyAsync(item => item.AdmissionId == preparation.AdmissionId.Value));
        var retained = await store.GetAsync(preparation.AdmissionId);
        Assert.NotNull(retained);
        Assert.Null(retained.AcceptedAtUtc);
        Assert.Equal(saved.PreparationFingerprint, retained.PreparationFingerprint);
        Assert.Equal(saved.Preparation.InitialCommit.InitialAssignments!.Count, retained.Preparation.InitialCommit.InitialAssignments!.Count);
    }
}
