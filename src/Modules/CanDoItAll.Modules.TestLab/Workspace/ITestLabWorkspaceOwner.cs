using CanDoItAll.TestLab.UI;

namespace CanDoItAll.Modules.TestLab;

public enum TestLabWriteOutcome { Refused, Committed, CommittedWithWarning, Unknown }
public sealed record TestLabWriteResult(TestLabWriteOutcome Outcome, Guid? PlanId, string Message);

public interface ITestLabWorkspaceOwner {
    Task<IReadOnlyList<TestPlanSummary>> ListAsync(CancellationToken cancellationToken);
    Task<TestPlanEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<TestLabProjectOption>> ProjectsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<TestLabPartyOption>> PartiesAsync(Guid projectId, CancellationToken cancellationToken);
    Task<TestLabPartyOption?> PartyAsync(Guid partyId, CancellationToken cancellationToken);
    Task<TestLabWriteResult> SaveAsync(TestPlanEditorModel submission);
}
