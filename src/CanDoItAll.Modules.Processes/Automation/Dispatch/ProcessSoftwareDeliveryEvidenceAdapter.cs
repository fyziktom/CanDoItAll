using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessSoftwareDeliveryEvidenceAdapter
    {
        internal static string ResolveMissingConcreteImplementationProofSummary(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail)
        {
            if (!RequiresConcreteImplementationProof(candidate))
            {
                return string.Empty;
            }

            if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
                .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
            {
                return string.Empty;
            }

            var successfulReceipts = detail.ToolReceipts
                .Where(receipt => !IsFailedToolReceipt(receipt))
                .ToList();
            var concreteReadReceipt = ResolveLatestImplementationProofReadReceipt(candidate, successfulReceipts);
            if (concreteReadReceipt is null)
            {
                return RequiresSourceOrProjectImplementationProof(candidate)
                    ? "the current attempt did not read any concrete product source or project file"
                    : "the current attempt did not read any concrete product deliverable, source, or project file";
            }

            var concreteMutationReceipts = successfulReceipts
                .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
                .Where(receipt => IsConcreteProductMutationReceipt(candidate, detail, receipt))
                .ToList();
            if (RequiresCurrentAttemptProductMutation(candidate) &&
                concreteMutationReceipts.Count == 0)
            {
                return "the current repair attempt did not mutate any concrete product file";
            }

            var latestMutationReceipt = concreteMutationReceipts
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestMutationReceipt is not null)
            {
                var latestValidationReceipt = ResolveLatestRequiredImplementationValidationReceipt(
                    candidate,
                    successfulReceipts);
                var hasValidationAfterLatestMutation = latestValidationReceipt is not null &&
                                                       !IsReceiptAfter(latestMutationReceipt, latestValidationReceipt);
                if (IsReceiptAfter(latestMutationReceipt, concreteReadReceipt) &&
                    !hasValidationAfterLatestMutation)
                {
                    return "workspace_read_file ran before the latest concrete product mutation";
                }

                var latestBootstrapReceipt = concreteMutationReceipts
                    .Where(receipt => IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
                    .OrderByDescending(receipt => receipt.CompletedAtUtc)
                    .ThenByDescending(receipt => receipt.StartedAtUtc)
                    .FirstOrDefault();
                if (latestBootstrapReceipt is not null &&
                    !successfulReceipts.Any(receipt =>
                        ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                        IsReceiptAfter(receipt, latestBootstrapReceipt) &&
                        HasConcreteProductImplementationPath(candidate, receipt)))
                {
                    return "the latest scaffold or bootstrap tool was not followed by a concrete product deliverable, source, or project file write";
                }

                if (latestValidationReceipt is not null &&
                    IsReceiptAfter(latestMutationReceipt, latestValidationReceipt))
                {
                    return $"{latestValidationReceipt.ToolName} ran before the latest concrete product mutation";
                }
            }

            return string.Empty;
        }

        internal static string ResolveMissingRunnableApplicationProofSummary(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail)
        {
            if (!RequiresConcreteImplementationProof(candidate))
            {
                return string.Empty;
            }

            if (IsDotNetSolutionSetupScaffoldMutationStep(candidate))
            {
                return string.Empty;
            }

            var implementationMentionsDotNet = ImplementationContractMentionsDotNet(candidate);
            if (!implementationMentionsDotNet &&
                (ImplementationContractMentionsJavaScript(candidate) || ImplementationContractNegatesDotNet(candidate)))
            {
                return string.Empty;
            }

            var successfulReceipts = detail.ToolReceipts
                .Where(receipt => !IsFailedToolReceipt(receipt))
                .ToList();
            if (!HasBuildValidationReceipt(successfulReceipts) &&
                !ContainsRunnableApplicationContractSignal(candidate))
            {
                return string.Empty;
            }

            var runnableDotNetProjectPaths = ResolveRunnableDotNetHostProjectPaths(detail, successfulReceipts);
            if (runnableDotNetProjectPaths.Count == 0)
            {
                return string.Empty;
            }

            var invalidHostSummary = ResolveInvalidRunnableDotNetHostSummary(runnableDotNetProjectPaths);
            if (!string.IsNullOrWhiteSpace(invalidHostSummary))
            {
                return invalidHostSummary;
            }

            var latestRunReceipt = ResolveLatestReceipt(
                successfulReceipts,
                IsRunValidationToolName,
                requireConcreteProductPath: true,
                requireConcreteDeliverableOrSourcePath: false);
            if (latestRunReceipt is null)
            {
                return $"the current attempt did not start the runnable .NET host with a run tool after implementation; detected host project: {runnableDotNetProjectPaths[0]}";
            }

            var latestMutationReceipt = successfulReceipts
                .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
                .Where(receipt => IsConcreteProductMutationReceipt(candidate, detail, receipt))
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestMutationReceipt is not null &&
                IsReceiptAfter(latestMutationReceipt, latestRunReceipt))
            {
                return "the run tool ran before the latest concrete product mutation";
            }

            return string.Empty;
        }
    }
}
