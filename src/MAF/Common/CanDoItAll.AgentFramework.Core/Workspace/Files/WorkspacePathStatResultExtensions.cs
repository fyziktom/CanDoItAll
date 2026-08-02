using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class WorkspacePathStatResultExtensions
{
    private const string MissingPathKind = "missing";
    private const string FailedOutcome = "Failed";

    public static bool IsKnownMissing(this WorkspacePathStatResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return !result.Succeeded &&
               !result.Exists &&
               string.Equals(result.PathKind, MissingPathKind, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(result.Receipt.Operation, ToolContractCatalog.WorkspaceStatPath, StringComparison.Ordinal) &&
               string.Equals(result.Receipt.Outcome, FailedOutcome, StringComparison.OrdinalIgnoreCase);
    }
}
