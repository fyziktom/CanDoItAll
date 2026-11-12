using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProcessLaunchDecisionOptions(
    [property: JsonRequired] int Version,
    [property: JsonRequired] ProcessDecisionExecutionContract Execution,
    DecisionDataPolicyOverride? Policy = null) {
    public const int CurrentVersion = 1;

    public void Validate() {
        if (Version != CurrentVersion || Execution is null) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.InvalidSelection);
        }
        try {
            Execution.Validate();
            if (Policy is not null) {
                _ = DecisionDataPolicyOverrideCodec.Serialize(Policy);
            }
        } catch (Exception exception) when (exception is InvalidDataException or ArgumentException) {
            throw new ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection.InvalidSelection);
        }
    }
}

public sealed record ProcessLaunchDecisionReview(ProcessDecisionExecutionContract Execution, DecisionDataPolicy Policy);

public enum ProcessLaunchDecisionRejection { InvalidSelection, UnsupportedContract, UnsupportedPolicy, UnsupportedPlan, MissingWorkspacePolicy }

public sealed class ProcessLaunchDecisionRejectedException(ProcessLaunchDecisionRejection reason) : InvalidOperationException(reason switch {
    ProcessLaunchDecisionRejection.InvalidSelection => "Select a supported, versioned Process decision contract and valid policy preferences.",
    ProcessLaunchDecisionRejection.UnsupportedContract => "The selected Process decision execution contract is not registered.",
    ProcessLaunchDecisionRejection.UnsupportedPolicy => "This Process planner supports starting-run or step snapshots, current publication and pause for review only.",
    ProcessLaunchDecisionRejection.UnsupportedPlan => "This Process planner requires one Agent activity permitted to publish Project tasks, without additional artifact, tool, host, branch or subprocess obligations.",
    ProcessLaunchDecisionRejection.MissingWorkspacePolicy => "Configure and save the Workspace decision-data policy before preparing this Process plan.",
    _ => throw new ArgumentOutOfRangeException(nameof(reason))
}) {
    public ProcessLaunchDecisionRejection Reason { get; } = reason;
}
