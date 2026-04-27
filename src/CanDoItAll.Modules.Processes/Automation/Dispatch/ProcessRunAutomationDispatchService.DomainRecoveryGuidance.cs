using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static string BuildDomainRecoveryFocusGuidance(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        return ProcessAutomationRecoveryGuidanceProviders.BuildFocusGuidance(
            candidate,
            detail,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures);
    }

    private static void AppendDomainRecoveryChecklists(
        StringBuilder builder,
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string missingConcreteImplementationProofSummary)
    {
        ProcessAutomationRecoveryGuidanceProviders.AppendChecklists(
            builder,
            candidate,
            detail,
            missingConcreteImplementationProofSummary);
    }

    private interface IProcessAutomationRecoveryGuidanceProvider
    {
        bool AppliesTo(DispatchCandidate candidate, ExecutionRunDetail detail);

        string BuildFocusGuidance(
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string? responseText,
            string missingConcreteImplementationProofSummary,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures);

        void AppendChecklist(
            StringBuilder builder,
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string missingConcreteImplementationProofSummary);
    }

    private static class ProcessAutomationRecoveryGuidanceProviders
    {
        private static readonly IReadOnlyList<IProcessAutomationRecoveryGuidanceProvider> Providers =
        [
            new CalculatorProcessAutomationRecoveryGuidanceProvider()
        ];

        public static string BuildFocusGuidance(
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string? responseText,
            string missingConcreteImplementationProofSummary,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
        {
            var guidance = Providers
                .Where(provider => provider.AppliesTo(candidate, detail))
                .Select(provider => provider.BuildFocusGuidance(
                    candidate,
                    detail,
                    responseText,
                    missingConcreteImplementationProofSummary,
                    missingRequiredTools,
                    unresolvedCriticalToolFailures))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();

            return guidance.Count == 0
                ? string.Empty
                : string.Join(Environment.NewLine, guidance);
        }

        public static void AppendChecklists(
            StringBuilder builder,
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string missingConcreteImplementationProofSummary)
        {
            foreach (var provider in Providers.Where(provider => provider.AppliesTo(candidate, detail)))
            {
                provider.AppendChecklist(builder, candidate, detail, missingConcreteImplementationProofSummary);
            }
        }
    }

    private sealed class CalculatorProcessAutomationRecoveryGuidanceProvider : IProcessAutomationRecoveryGuidanceProvider
    {
        public bool AppliesTo(DispatchCandidate candidate, ExecutionRunDetail detail)
        {
            return ContainsCalculatorContext(candidate) ||
                   RequiresCalculatorLikeImplementationProof(candidate, detail);
        }

        public string BuildFocusGuidance(
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string? responseText,
            string missingConcreteImplementationProofSummary,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
        {
            return BuildCalculatorRecoveryFocusGuidance(
                candidate,
                responseText,
                missingConcreteImplementationProofSummary,
                missingRequiredTools,
                unresolvedCriticalToolFailures);
        }

        public void AppendChecklist(
            StringBuilder builder,
            DispatchCandidate candidate,
            ExecutionRunDetail detail,
            string missingConcreteImplementationProofSummary)
        {
            if (RequiresCalculatorLikeImplementationProof(candidate, detail))
            {
                AppendCalculatorRecoveryChecklist(builder, missingConcreteImplementationProofSummary);
            }
        }
    }
}
