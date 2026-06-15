using CanDoItAll.AgentFramework.Core;
using System.Security.Cryptography;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static class ProcessNoProgressRetrySignalBuilder
    {
        public static bool ShouldCompress(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<string> retryReasons,
            int attemptNumber)
        {
            if (attemptNumber <= 1)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
                TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
                ProcessNoProgressEvidenceDeltaRules.HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
            {
                return false;
            }

            var fingerprint = TryCreateFingerprint(candidate, detail, responseText, missingRequiredTools, retryReasons, attemptNumber);
            if (!string.IsNullOrWhiteSpace(fingerprint))
            {
                return true;
            }

            return missingRequiredTools.Count > 0 ||
                   retryReasons.Any(ProcessExecutionRetryReasonAggregator.IsNoProgressRetryReason);
        }

        public static string? TryCreateFingerprint(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<string> retryReasons,
            int attemptNumber)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(missingRequiredTools);
            ArgumentNullException.ThrowIfNull(retryReasons);

            if (attemptNumber <= 1 ||
                !string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
                TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
                HasSuccessfulConcreteProductMutation(candidate, detail) ||
                ProcessNoProgressEvidenceDeltaRules.HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
            {
                return null;
            }

            if (missingRequiredTools.Count == 0 &&
                !retryReasons.Any(ProcessExecutionRetryReasonAggregator.IsNoProgressRetryReason))
            {
                return null;
            }

            return TryCreateSignal(candidate, detail, responseText, missingRequiredTools, retryReasons)?.Fingerprint;
        }

        public static NoProgressRetrySignal? TryCreateSignal(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? responseText,
            IReadOnlyList<string> missingRequiredTools,
            IReadOnlyList<string> retryReasons)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(missingRequiredTools);
            ArgumentNullException.ThrowIfNull(retryReasons);

            if (!string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective) ||
                TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
                HasSuccessfulConcreteProductMutation(candidate, detail) ||
                ProcessNoProgressEvidenceDeltaRules.HasNewSatisfiedCurrentAttemptEvidence(candidate, detail))
            {
                return null;
            }

            if (missingRequiredTools.Count == 0 &&
                !retryReasons.Any(ProcessExecutionRetryReasonAggregator.IsNoProgressRetryReason))
            {
                return null;
            }

            var failedToolNames = detail.ToolReceipts
                .Where(IsFailedToolReceipt)
                .Select(receipt => NormalizeToolToken(receipt.ToolName))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var artifactSignals = detail.Artifacts
                .Select(artifact => string.Join(
                    ":",
                    NormalizeManagedRelativePathForComparison(artifact.RelativePath),
                    CollapsePromptWhitespace(artifact.DisplayName).ToLowerInvariant(),
                    CollapsePromptWhitespace(artifact.ContentType).ToLowerInvariant(),
                    CreateBoundedTextHash(artifact.Summary)))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var receiptSignals = detail.ToolReceipts
                .Select(receipt => string.Join(
                    ":",
                    NormalizeToolToken(receipt.ToolName),
                    NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                    CreateBoundedTextHash(receipt.RequestSummary),
                    CreateBoundedTextHash(receipt.ExitSummary),
                    IsFailedToolReceipt(receipt) ? "failed" : "succeeded"))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var unsatisfiedExpectationIds = candidate.ExpectedArtifacts
                .Where(expectation => expectation.IsRequired && !candidate.RecordedArtifactExpectationIds.Contains(expectation.Id))
                .Select(expectation => expectation.Id.ToString("D"))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var toolSignature = CreateBoundedTextHash(string.Join(
                "|",
                string.Join(",", missingRequiredTools.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                string.Join(",", failedToolNames),
                string.Join(",", receiptSignals)));
            var artifactValidationFingerprint = CreateBoundedTextHash(string.Join(
                "|",
                string.Join(",", unsatisfiedExpectationIds),
                string.Join(",", artifactSignals)));
            var mutationDelta = ResolveMutationDelta(candidate, detail);
            var proofDelta = ResolveProofDelta(detail);
            var normalized = string.Join(
                "|",
                "no-progress-retry",
                candidate.Run.Id.ToString("D"),
                candidate.StepRun.Id.ToString("D"),
                toolSignature,
                artifactValidationFingerprint,
                mutationDelta,
                proofDelta,
                string.Join(",", retryReasons.Select(reason => CollapsePromptWhitespace(reason).ToLowerInvariant()).OrderBy(item => item, StringComparer.Ordinal)));
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
            return new NoProgressRetrySignal(
                fingerprint,
                detail.Run.Id,
                toolSignature,
                artifactValidationFingerprint,
                mutationDelta,
                proofDelta);
        }

        public static string ResolveMutationDelta(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail)
        {
            var mutationSignals = detail.ToolReceipts
                .Where(receipt => !IsFailedToolReceipt(receipt))
                .Where(receipt => IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
                .Select(receipt => string.Join(
                    ":",
                    NormalizeToolToken(receipt.ToolName),
                    NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                    IsConcreteProductMutationReceipt(candidate, detail, receipt)
                        ? "concrete"
                        : "non-concrete",
                    CreateBoundedTextHash(receipt.RequestSummary),
                    CreateBoundedTextHash(receipt.ExitSummary)))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            return mutationSignals.Length == 0
                ? "mutation-delta:none"
                : $"mutation-delta:{CreateBoundedTextHash(string.Join("|", mutationSignals))}";
        }

        public static string ResolveProofDelta(ProcessAutomationExecutionRunDetail detail)
        {
            var proofSignals = detail.ToolReceipts
                .Where(receipt => !IsFailedToolReceipt(receipt))
                .Where(receipt => ImplementationProofToolNames.Contains(NormalizeToolToken(receipt.ToolName), StringComparer.Ordinal))
                .Select(receipt => string.Join(
                    ":",
                    NormalizeToolToken(receipt.ToolName),
                    NormalizeManagedRelativePathForComparison(receipt.WorkingDirectory),
                    CreateBoundedTextHash(receipt.RequestSummary),
                    CreateBoundedTextHash(receipt.ExitSummary)))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

            return proofSignals.Length == 0
                ? "proof-delta:none"
                : $"proof-delta:{CreateBoundedTextHash(string.Join("|", proofSignals))}";
        }

        private static string CreateBoundedTextHash(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CollapsePromptWhitespace(value)))).ToLowerInvariant();
        }
    }
}
