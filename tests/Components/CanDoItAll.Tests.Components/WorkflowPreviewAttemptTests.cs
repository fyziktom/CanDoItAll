using Bunit;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.WorkflowAuthoring.UI;
using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Fixture = CanDoItAll.Tests.Components.AgentFramework.WorkflowOwnershipTests.Fixture;

namespace CanDoItAll.Tests.Components.AgentFramework;

[Trait("Category", "HostPlatform")]
public sealed class WorkflowPreviewAttemptTests {
    public enum Followup { DedicatedReservation, AuthorityRefusal, Success }

    [Theory]
    [InlineData(false, Followup.DedicatedReservation)]
    [InlineData(true, Followup.DedicatedReservation)]
    [InlineData(false, Followup.AuthorityRefusal)]
    [InlineData(true, Followup.AuthorityRefusal)]
    [InlineData(false, Followup.Success)]
    [InlineData(true, Followup.Success)]
    public async Task Both_native_preview_consumers_keep_followup_identity_and_authority_local_to_the_attempt(bool canvas, Followup followup) {
        await using var fixture = await Fixture.CreateAsync();
        var requests = new List<WorkflowTestRunRequest>();
        var second = fixture.FirstRun with { RunId = WorkflowRunId.New() };
        fixture.Probe.Test = async request => {
            requests.Add(request);
            if (requests.Count == 2 && followup == Followup.DedicatedReservation) {
                throw new WorkflowLaunchAdmissionObservationException(second.RunId, new IOException("Admission"), new IOException("Observation"));
            }
            var run = requests.Count == 1 ? fixture.FirstRun : second;
            await fixture.Store.SaveRunAsync(run);
            return new(true, new([]), run, [], [], [], "");
        };
        await fixture.Tab(canvas ? WorkflowTab.Editor : WorkflowTab.History);
        if (canvas) {
            fixture.Cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        }
        async Task Preview() {
            if (canvas) {
                await fixture.Cut.InvokeAsync(() => fixture.Cut.Find("[data-testid='workflow-canvas-run-preview']").ClickAsync());
            } else {
                await fixture.Emit(WorkflowAction.RunTest);
            }
        }
        await Preview();
        if (followup == Followup.AuthorityRefusal) {
            fixture.Probe.Authority = _ => throw new InvalidOperationException("Authority refused");
        }
        await Preview();
        Assert.Equal(followup == Followup.AuthorityRefusal ? 1 : 2, requests.Count);
        var accepted = canvas ? fixture.Cut.FindComponent<WorkflowCanvasEditor>().Instance.PreviewOwner.Accepted : fixture.Cut.Instance.AcceptedPreviewRun;
        Assert.Equal(followup == Followup.Success ? second.RunId : fixture.FirstRun.RunId, accepted?.RunId);
        var reservation = canvas ? fixture.Cut.FindComponent<WorkflowCanvasEditor>().Instance.PreviewOwner.ReservedRunId : fixture.Cut.Instance.UnconfirmedPreviewRunId;
        if (followup == Followup.DedicatedReservation) {
            Assert.Equal(second.RunId, reservation);
            await Preview();
            Assert.Equal(2, requests.Count);
        } else if (followup == Followup.AuthorityRefusal) {
            Assert.Null(reservation);
        }
        Assert.NotNull(await fixture.Store.GetRunAsync(fixture.FirstRun.RunId));
        Assert.All(requests, request => Assert.NotNull(request.StructureAuthority));
        if (requests.Count == 2) {
            if (canvas) {
                Assert.NotEqual(requests[0].DraftDefinition!.VersionId, requests[1].DraftDefinition!.VersionId);
                Assert.Equal(fixture.First.Id, requests[1].DraftDefinition!.Id);
            } else {
                Assert.Equal(fixture.First.VersionId, requests[1].VersionId);
            }
        }
    }

    [Fact]
    public async Task Native_owner_returns_attempt_local_outcomes_while_pending_and_keeps_independent_owners_separate() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Tab(WorkflowTab.Editor);
        fixture.Cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        var owner = fixture.Cut.FindComponent<WorkflowCanvasEditor>().Instance.PreviewOwner;
        var requests = new List<WorkflowTestRunRequest>();
        fixture.Probe.Test = request => {
            requests.Add(request);
            return Task.FromResult(new WorkflowTestRunResult(true, new([]), fixture.FirstRun, [], [], [], ""));
        };
        var first = new WorkflowPreviewSubmission(fixture.First, "{\"attempt\":\"A\"}", WorkflowPreviewSimulationPlan.Empty);
        var accepted = Assert.IsType<WorkflowPreviewOutcome.Completed>(await owner.Operations.Run(first, _ => Task.CompletedTask, CancellationToken.None));
        Assert.Equal(fixture.FirstRun.RunId, accepted.Run!.RunId);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<WorkflowTestRunResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Probe.Test = request => {
            requests.Add(request);
            entered.SetResult();
            return release.Task;
        };
        var second = first with { InputJson = "{\"attempt\":\"B\"}" };
        var pending = owner.Operations.Run(second, _ => Task.CompletedTask, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var duplicate = Assert.IsType<WorkflowPreviewOutcome.Unknown>(await owner.Operations.Run(second, _ => Task.CompletedTask, CancellationToken.None));
        Assert.Null(duplicate.ReservedRunId);
        Assert.Equal(2, requests.Count);
        Assert.Equal(first.InputJson, requests[0].InputJson);
        Assert.Equal(second.InputJson, requests[1].InputJson);
        Assert.NotEqual(requests[0].DraftDefinition!.VersionId, requests[1].DraftDefinition!.VersionId);
        release.SetException(new IOException("Lost acknowledgement"));
        Assert.Null(Assert.IsType<WorkflowPreviewOutcome.Unknown>(await pending).ReservedRunId);
        Assert.Null(Assert.IsType<WorkflowPreviewOutcome.Unknown>(await owner.Operations.Run(second, _ => Task.CompletedTask, CancellationToken.None)).ReservedRunId);
        Assert.Equal(2, requests.Count);
        Assert.Equal(fixture.FirstRun.RunId, owner.Accepted!.RunId);
        await using var other = await Fixture.CreateAsync();
        await other.Tab(WorkflowTab.Editor);
        other.Cut.WaitForElement("[data-testid='workflow-canvas-run-preview']");
        other.Probe.Test = _ => Task.FromResult(new WorkflowTestRunResult(true, new([]), other.FirstRun, [], [], [], ""));
        var otherOwner = other.Cut.FindComponent<WorkflowCanvasEditor>().Instance.PreviewOwner;
        var otherOutcome = Assert.IsType<WorkflowPreviewOutcome.Completed>(await otherOwner.Operations.Run(
            new(other.First, first.InputJson, WorkflowPreviewSimulationPlan.Empty), _ => Task.CompletedTask, CancellationToken.None));
        Assert.Equal(other.FirstRun.RunId, otherOutcome.Run!.RunId);
        Assert.NotEqual(owner.Accepted.RunId, otherOwner.Accepted!.RunId);
        Assert.NotNull(await fixture.Store.GetRunAsync(owner.Accepted.RunId));
    }
}
