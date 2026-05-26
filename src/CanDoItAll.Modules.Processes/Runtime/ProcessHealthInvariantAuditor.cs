namespace CanDoItAll.Modules.Processes;

internal static class ProcessHealthInvariantAuditor
{
    public static ProcessStepRunHealthViewModel BuildStepHealth(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessArtifactExpectationSatisfactionViewModel> artifactLedger,
        string manualRecoveryDirective)
    {
        ArgumentNullException.ThrowIfNull(stepRun);
        ArgumentNullException.ThrowIfNull(artifactLedger);

        var missingArtifacts = stepRun.Status == ProcessStepRunStatus.Skipped
            ? new List<string>()
            : artifactLedger
                .Where(item => item.IsRequired)
                .Where(item => item.Status is ProcessArtifactExpectationSatisfactionStatus.Missing or ProcessArtifactExpectationSatisfactionStatus.ProjectionFailed)
                .Select(item => item.Title)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        var recoveryOptions = ProcessStepRunBlockState.ResolveRecoveryOptions(stepRun);
        return ProcessStepRunHealthViewModel.Empty with
        {
            RecoveryClassification = ResolveRecoveryClassification(stepRun, missingArtifacts, manualRecoveryDirective),
            ActionableReason = BuildActionableReason(stepRun, missingArtifacts, manualRecoveryDirective),
            CanManualRerun = CanManualRerun(stepRun),
            NextRecoveryAction = stepRun.NextRecoveryAction,
            RecoveryOptions = recoveryOptions
        };
    }

    private static ProcessRecoveryClassification ResolveRecoveryClassification(
        ProcessStepRun stepRun,
        IReadOnlyCollection<string> missingArtifacts,
        string manualRecoveryDirective)
    {
        if (!string.IsNullOrWhiteSpace(manualRecoveryDirective))
        {
            return ProcessRecoveryClassification.ManualRerun;
        }

        if (missingArtifacts.Count > 0)
        {
            return ProcessRecoveryClassification.MissingArtifact;
        }

        return stepRun.Status switch
        {
            ProcessStepRunStatus.InProgress when !string.IsNullOrWhiteSpace(stepRun.ExceptionSummary) => ProcessRecoveryClassification.CrashRecovery,
            ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed => ProcessRecoveryClassification.AutomaticRetry,
            _ => ProcessRecoveryClassification.None
        };
    }

    private static string BuildActionableReason(
        ProcessStepRun stepRun,
        IReadOnlyCollection<string> missingArtifacts,
        string manualRecoveryDirective)
    {
        if (!string.IsNullOrWhiteSpace(manualRecoveryDirective))
        {
            return manualRecoveryDirective.Trim();
        }

        if (missingArtifacts.Count > 0)
        {
            return $"Missing required artifacts: {string.Join(", ", missingArtifacts.Take(3))}.";
        }

        if (!string.IsNullOrWhiteSpace(stepRun.BlockedReason))
        {
            return stepRun.BlockedReason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(stepRun.ExceptionSummary))
        {
            return stepRun.ExceptionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(stepRun.DecisionSummary))
        {
            return stepRun.DecisionSummary.Trim();
        }

        return string.Empty;
    }

    private static bool CanManualRerun(ProcessStepRun stepRun)
    {
        return stepRun.CurrentExecutorPartyId.HasValue &&
               stepRun.Status is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed;
    }
}
