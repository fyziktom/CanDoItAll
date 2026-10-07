using CanDoItAll.Modules.Projects;
using CanDoItAll.TestLab.UI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.TestLab;

public sealed class TestLabWorkspaceOwner(
    TestLabService plans,
    ProjectWriteSelectionQuery projects,
    IProjectPartyIntegrationBridge parties,
    ILogger<TestLabWorkspaceOwner> logger) : ITestLabWorkspaceOwner {
    public Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken) => plans.ListAsync(cancellationToken);

    public async Task<TestPlanEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) {
        var plan = await plans.GetAsync(id, cancellationToken);
        return plan.Id == id ? plan : null;
    }

    public async Task<IReadOnlyList<TestLabProjectOption>> ProjectsAsync(CancellationToken cancellationToken) =>
        (await projects.ListAsync(cancellationToken: cancellationToken)).Select(item => new TestLabProjectOption(item.Id, item.Name, item.Admission)).ToArray();

    public async Task<IReadOnlyList<TestLabPartyOption>> PartiesAsync(Guid projectId, CancellationToken cancellationToken) =>
        (await parties.ListPartyOptionsAsync(projectId, cancellationToken)).Select(item => new TestLabPartyOption(item.PartyId, item.DisplayName)).ToArray();

    public async Task<TestLabPartyOption?> PartyAsync(Guid partyId, CancellationToken cancellationToken) {
        var party = await parties.GetPartyOptionAsync(partyId, cancellationToken);
        return party is null ? null : new(party.PartyId, party.DisplayName);
    }

    public async Task<TestLabWriteResult> SaveAsync(TestPlanEditorModel submission) {
        try {
            var result = await plans.SaveAsync(submission);
            return result.IsSuccess
                ? new(TestLabWriteOutcome.Committed, result.Value, "Plan saved.")
                : new(TestLabWriteOutcome.Refused, null, string.Join(" ", result.Errors.Select(error => error.Message)));
        } catch (TestPlanCommittedSaveException exception) {
            return new(TestLabWriteOutcome.CommittedWithWarning, exception.TestPlanId, "Plan saved; a subsequent operation failed. Retry refresh to review the committed state.");
        } catch (ProjectWriteAdmissionRejectedException) {
            return new(TestLabWriteOutcome.Refused, null, "The selected project admission is no longer valid. Select a current project explicitly before saving.");
        } catch (TestPlanBindingChangedException exception) {
            return new(TestLabWriteOutcome.Refused, null, exception.Message);
        } catch (Exception exception) {
            logger.LogError("TestLab write outcome is unknown. PlanId={PlanId} ProjectId={ProjectId} FailureType={FailureType}",
                submission.Id, submission.ProjectId, exception.GetType().Name);
            return new(TestLabWriteOutcome.Unknown, null, "Save outcome is unknown. Submission is locked. Refresh and review the stored plans; select a verified plan or explicitly start a new draft. Refresh does not retry this write.");
        }
    }
}

public sealed class TestPlanBindingChangedException() : InvalidOperationException("The test plan project binding changed. Reload Test Lab before saving.");
