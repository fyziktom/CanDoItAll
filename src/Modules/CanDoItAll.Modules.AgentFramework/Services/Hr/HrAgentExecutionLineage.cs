using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public static class HrAgentExecutionLineage
{
    public const string ManagerReviewSourceKind = HrAgentExecutionSourceKinds.ManagerReview;
    public const string ProcessStepSourceKind = "process-step";

    public static bool IsManagerReview(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return string.Equals(
            run.SourceKind,
            ManagerReviewSourceKind,
            StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProcessStep(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return string.Equals(
                   run.SourceKind,
                   ProcessStepSourceKind,
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(run.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}
