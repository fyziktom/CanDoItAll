using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionRunQueryBuilder
{
    internal const int DefaultTake = 20;

    internal static ProcessAutomationExecutionRunQuery ForCandidate(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        int take = DefaultTake)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return ForRunStep(candidate.Run.Id, candidate.StepRun.Id, take);
    }

    internal static ProcessAutomationExecutionRunQuery ForRunStep(
        Guid processRunId,
        Guid processStepRunId,
        int take = DefaultTake)
    {
        return new ProcessAutomationExecutionRunQuery(
            ProcessRunId: processRunId.ToString("D"),
            ProcessStepId: processStepRunId.ToString("D"),
            Take: take);
    }
}
