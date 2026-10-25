using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentInheritedPlanningRunReader(ISandboxWorkspaceExecutionRunStore runs,
    AgentToolAdmissionJournal journal, IEnumerable<IAgentInheritedPlanningParticipant> participants) {
    private readonly AgentInheritedPlanningReadAuthority authority = new(journal, participants);

    public async Task<ExecutionRunDetail> ReadAsync(Guid executionRunId, CancellationToken cancellationToken = default) {
        ExecutionRunDetail detail;
        using (AgentFrameworkTelemetry.ActivitySource.StartActivity("agent.planning.read.detail")) {
            detail = await runs.GetExecutionRunDetailAsync(executionRunId, cancellationToken)
                ?? throw new InvalidOperationException("Execution run was not found.");
        }
        using (AgentFrameworkTelemetry.ActivitySource.StartActivity("agent.planning.read.authority")) {
            await authority.RequireCurrentAsync(detail.Run, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return detail;
    }
}
