namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeDiagnosticCodes
{
    public const string MissingRequiredInputArtifact = "process.runtime.missing_required_input_artifact";
    public const string MissingExpectedOutputArtifact = "process.runtime.missing_expected_output_artifact";
    public const string RunningClaimExpiredReplayUnsafe = "process.runtime.running_claim_expired_replay_unsafe";
    public const string InvalidBranchOutcomeSignal = "process.runtime.invalid_branch_outcome_signal";
}
