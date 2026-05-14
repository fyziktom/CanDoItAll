using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static AgentRecoveryDecision CreateRecoveryDecisionForRetry(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        int attemptNumber,
        DateTimeOffset? nextAttemptAtUtc = null)
    {
        var category = ResolveRecoveryFailureCategory(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            unresolvedCriticalToolFailures);
        var reason = ResolveRecoveryFailureReason(
            candidate,
            detail,
            responseText,
            missingRequiredTools,
            unresolvedCriticalToolFailures,
            category);
        return AgentRecoveryDecisionFactory.Create(
            category,
            reason,
            attemptNumber,
            detail.Run.Id.ToString("D"),
            nextAttemptAtUtc: nextAttemptAtUtc);
    }

    private static AgentReworkPacket? CreateReworkPacketForDecision(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        AgentRecoveryDecision decision,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        DateTimeOffset createdAtUtc)
    {
        if (decision.Mode != AgentRecoveryMode.ReworkContinuation)
        {
            return null;
        }

        var decisionWithPacket = decision with { ReworkPacketId = decision.ReworkPacketId ?? Guid.NewGuid() };
        return AgentReworkPacketFactory.Create(
            candidate.Run.Id,
            candidate.StepRun.Id,
            decisionWithPacket,
            ResolveReworkObjective(candidate, decisionWithPacket),
            findings: [new AgentReworkFinding(decision.FailureCategory.ToString(), decision.Reason, "High", "Automation")],
            artifactsToInspect: ResolveReworkArtifactsToInspect(candidate),
            failedToolReceipts: unresolvedCriticalToolFailures.Select(AgentReworkPacketFactory.FromReceipt).ToList(),
            proofsToRerun: ResolveProofRequirementsToRerun(missingRequiredTools, unresolvedCriticalToolFailures),
            reusableProofs: ResolveReusableProofRefs(detail),
            minimalNextActions:
            [
                "Inspect the target artifacts and receipts named in this packet.",
                "Make only the smallest change needed to resolve the packet findings.",
                "Rerun every invalidated proof requirement listed in this packet."
            ],
            prohibitedActions:
            [
                "Do not regenerate unrelated files or artifacts.",
                "Do not treat the failed chat transcript as process truth.",
                "Do not mark the step complete until invalidated proof requirements pass."
            ],
            createdAtUtc: createdAtUtc);
    }

    private static string BuildTypedRecoveryDirective(
        AgentRecoveryDecision decision,
        AgentReworkPacket? packet,
        string legacyDirective)
    {
        return AgentReworkPromptRenderer.RenderRecoveryDirective(decision, packet, legacyDirective);
    }

    private async Task PersistRecoveryJournalAsync(
        DispatchCandidate candidate,
        AgentRecoveryDecision decision,
        AgentReworkPacket? packet,
        int providerFallbackCount,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var recordedAtUtc = clock.GetUtcNow();
        if (packet is not null)
        {
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    EventType = ProcessRuntimeEventTypes.AgentReworkPacketCreated,
                    Title = "Agent rework packet created",
                    Description = AgentReworkPromptRenderer.RenderPacketSummary(packet),
                    CorrelationId = packet.Id.ToString("N"),
                    OperatingMode = candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = AgentReworkPromptRenderer.SerializePacket(packet),
                    OccurredAtUtc = recordedAtUtc
                },
                cancellationToken);
        }

        var ledgerEntry = AgentRecoveryLedger.CreateEntry(
            candidate.Run.Id,
            candidate.StepRun.Id,
            decision,
            candidate.TechnicalAgentId.ToString("D"),
            "assigned-agent-model",
            providerFallbackCount,
            recordedAtUtc);
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.AgentRecoveryAttemptRecorded,
                Title = "Agent recovery attempt recorded",
                Description = $"{decision.Mode} recovery for {decision.FailureCategory}: {decision.Reason}",
                CorrelationId = ledgerEntry.Id.ToString("N"),
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(ledgerEntry, AgentOutputJson.SerializerOptions),
                OccurredAtUtc = recordedAtUtc
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AgentFailureCategory ResolveRecoveryFailureCategory(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        if (TryResolveRecoverableProviderFailure(detail, responseText, out _))
        {
            return AgentFailureCategory.ProviderFailure;
        }

        if (TryResolveRecoverableExecutionInterruption(detail, responseText, out _))
        {
            return AgentFailureCategory.Timeout;
        }

        if (TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out var finalizerFailureSummary))
        {
            return finalizerFailureSummary.Contains("not called", StringComparison.OrdinalIgnoreCase)
                ? AgentFailureCategory.FinalizerMissing
                : AgentFailureCategory.FinalizerInvalid;
        }

        if (MentionsQaRejection(responseText) || MentionsQaRejection(detail.Run.ResultSummary))
        {
            return AgentFailureCategory.QaRejected;
        }

        if (unresolvedCriticalToolFailures.Any(receipt =>
                NormalizeToolToken(receipt.ToolName).EndsWith("_test", StringComparison.Ordinal)))
        {
            return AgentFailureCategory.TestFailure;
        }

        if (unresolvedCriticalToolFailures.Any(receipt =>
                NormalizeToolToken(receipt.ToolName).EndsWith("_build", StringComparison.Ordinal)))
        {
            return AgentFailureCategory.BuildFailure;
        }

        if (RequiresConcreteBrowserProof(candidate) &&
            !string.IsNullOrWhiteSpace(ResolveInvalidBrowserProofSummary(candidate, detail)))
        {
            return AgentFailureCategory.BrowserProofFailure;
        }

        if (!string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, ResolveOutputInspectionText(responseText))))
        {
            return AgentFailureCategory.ArtifactMissing;
        }

        if (!string.IsNullOrWhiteSpace(ResolveOutOfScopeExternalTargetReferenceSummary(detail, ResolveOutputInspectionText(responseText))))
        {
            return AgentFailureCategory.OutOfScopeReference;
        }

        if (MentionsRepeatedToolInvocation(responseText) || MentionsRepeatedToolInvocation(detail.Run.ResultSummary))
        {
            return AgentFailureCategory.RepeatedToolLoop;
        }

        if (missingRequiredTools.Count > 0)
        {
            return AgentFailureCategory.MissingRequiredTool;
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun) &&
            !TryReadProcessStepOutcome(responseText, out _, out _))
        {
            return AgentFailureCategory.FinalizerInvalid;
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            return AgentFailureCategory.CriticalToolFailure;
        }

        return AgentFailureCategory.Unknown;
    }

    private static string ResolveRecoveryFailureReason(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        AgentFailureCategory category)
    {
        if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
        {
            return providerFailureSummary;
        }

        if (TryResolveRecoverableExecutionInterruption(detail, responseText, out var interruptionSummary))
        {
            return interruptionSummary;
        }

        if (TryResolveRecoverableFinalizerValidationFailure(candidate, detail, responseText, out var finalizerFailureSummary))
        {
            return finalizerFailureSummary;
        }

        if (missingRequiredTools.Count > 0)
        {
            return $"Missing required tool execution(s): {string.Join(", ", missingRequiredTools)}.";
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            return string.Join(
                "; ",
                unresolvedCriticalToolFailures.Take(2).Select(item => $"{item.ToolName}: {item.ExitSummary}"));
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        var invalidBrowserProof = ResolveInvalidBrowserProofSummary(candidate, detail);
        if (!string.IsNullOrWhiteSpace(invalidBrowserProof))
        {
            return invalidBrowserProof;
        }

        var missingArtifact = ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText);
        if (!string.IsNullOrWhiteSpace(missingArtifact))
        {
            return missingArtifact;
        }

        var outOfScopeReference = ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText);
        if (!string.IsNullOrWhiteSpace(outOfScopeReference))
        {
            return outOfScopeReference;
        }

        return category == AgentFailureCategory.QaRejected
            ? "QA rejected the implementation and requested repair."
            : category.ToString();
    }

    private static IReadOnlyList<AgentReworkArtifactRef> ResolveReworkArtifactsToInspect(DispatchCandidate candidate)
    {
        var refs = new List<AgentReworkArtifactRef>();
        refs.AddRange(candidate.ArtifactInputs
            .SelectMany(input => input.Artifacts)
            .Where(artifact => !string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
            .Select(artifact => new AgentReworkArtifactRef(
                artifact.Title,
                artifact.ManagedStoragePath,
                "Upstream durable artifact for targeted repair.")));
        refs.AddRange(candidate.ExpectedArtifacts
            .Where(expectation => expectation.IsRequired)
            .Select(expectation => new AgentReworkArtifactRef(
                expectation.Title,
                expectation.Title,
                "Required output artifact expectation.")));

        return refs
            .DistinctBy(item => $"{item.Title}|{item.Path}", StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<AgentProofRequirement> ResolveProofRequirementsToRerun(
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var requirements = new List<AgentProofRequirement>();
        requirements.AddRange(missingRequiredTools.Select(toolName => new AgentProofRequirement(
            toolName,
            string.Empty,
            string.Empty,
            "Required proof tool was not successfully executed.")));
        requirements.AddRange(unresolvedCriticalToolFailures.Select(receipt => new AgentProofRequirement(
            receipt.ToolName,
            receipt.RequestSummary,
            receipt.WorkingDirectory,
            string.IsNullOrWhiteSpace(receipt.ExitSummary)
                ? "Critical proof tool failed."
                : receipt.ExitSummary)));

        return requirements
            .DistinctBy(item => $"{item.ToolName}|{item.Command}|{item.WorkingDirectory}", StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<AgentReusableProofRef> ResolveReusableProofRefs(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Where(receipt => AgentToolInvocationPolicyMetadata.IsValidationTool(NormalizeToolToken(receipt.ToolName)))
            .Take(10)
            .Select(receipt =>
            {
                var fingerprint = AgentProofFingerprintService.CreateFingerprint(
                    receipt.ToolName,
                    receipt.RequestSummary,
                    receipt.WorkingDirectory,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    "execution-run-receipt",
                    "unknown");
                return new AgentReusableProofRef(
                    receipt.Id,
                    receipt.ToolName,
                    fingerprint.Hash,
                    "Prior validation receipt is available as a candidate; reuse still requires matching fingerprints.");
            })
            .ToList();
    }

    private static string ResolveReworkObjective(DispatchCandidate candidate, AgentRecoveryDecision decision)
    {
        return decision.FailureCategory switch
        {
            AgentFailureCategory.QaRejected => $"Repair QA findings for step '{candidate.StepRun.Title}' without regenerating unrelated work.",
            AgentFailureCategory.BuildFailure => $"Repair build failures for step '{candidate.StepRun.Title}' and rerun build proof.",
            AgentFailureCategory.TestFailure => $"Repair test failures for step '{candidate.StepRun.Title}' and rerun test proof.",
            AgentFailureCategory.BrowserProofFailure => $"Repair browser-proof failure for step '{candidate.StepRun.Title}' and capture fresh evidence.",
            AgentFailureCategory.ArtifactMissing => $"Produce or repair missing artifacts for step '{candidate.StepRun.Title}'.",
            AgentFailureCategory.OutOfScopeReference => $"Remove stale or ungrounded path references from step '{candidate.StepRun.Title}' evidence and use only current-run grounded paths.",
            AgentFailureCategory.HumanRequestedRerun => $"Apply human-directed repair for step '{candidate.StepRun.Title}'.",
            _ => $"Recover step '{candidate.StepRun.Title}' from {decision.FailureCategory}."
        };
    }

    private static bool MentionsQaRejection(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               (text.Contains("QA rejected", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("quality gate rejected", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("repairs required", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("rework required", StringComparison.OrdinalIgnoreCase));
    }
}
