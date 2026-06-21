using CanDoItAll.Processes.Contracts;
using CoreExecutionEvidenceDescriptor = global::CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor;
using CoreExecutionEvidenceDescriptorRules = global::CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules;
using CoreExecutionRunEvidenceDescriptor = global::CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExecutionEvidenceDescriptorAdapter
{
    public static CoreExecutionRunEvidenceDescriptor DescribeRun(
        ProcessAutomationExecutionRunRecord run)
    {
        return CoreExecutionEvidenceDescriptorRules.DescribeRun(run);
    }

    public static CoreExecutionEvidenceDescriptor Describe(
        ProcessAutomationExecutionRunRecord run,
        ProcessExecutionPostAttemptFacts postAttemptFacts,
        int attemptNumber)
    {
        ArgumentNullException.ThrowIfNull(postAttemptFacts);

        return new CoreExecutionEvidenceDescriptor(
            CoreExecutionEvidenceDescriptorRules.DescribeRun(run),
            CoreExecutionEvidenceDescriptorRules.DescribeAttempt(
                run.Id,
                attemptNumber,
                postAttemptFacts.CompletionStatus,
                postAttemptFacts.CompletionReason,
                postAttemptFacts.MissingRequiredTools,
                postAttemptFacts.UnresolvedCriticalToolFailures.Count,
                postAttemptFacts.SelectedBranchOutcomeId),
            CoreExecutionEvidenceDescriptorRules.DescribeCarriedProof(
                postAttemptFacts.CarriedImplementationProof.HasConcreteImplementationProof,
                postAttemptFacts.CarriedImplementationProof.HasRunnableApplicationProof,
                postAttemptFacts.CarriedImplementationProof.HasConcreteProductMutation));
    }
}
