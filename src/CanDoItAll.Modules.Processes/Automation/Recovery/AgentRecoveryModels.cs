using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

public enum AgentRecoveryMode
{
    None,
    FormatRepair,
    FreshStepRetry,
    ReworkContinuation,
    ProviderFallbackRetry,
    ApprovalContinuation,
    HumanEscalation
}

public enum AgentFailureCategory
{
    Unknown,
    StructuredOutputInvalid,
    FinalizerMissing,
    FinalizerInvalid,
    MissingRequiredTool,
    CriticalToolFailure,
    ProviderFailure,
    BuildFailure,
    TestFailure,
    BrowserProofFailure,
    QaRejected,
    ArtifactMissing,
    PermissionDenied,
    RepeatedToolLoop,
    Timeout,
    HumanRequestedRerun,
    UpstreamArtifactInspectionMissing,
    OutOfScopeReference
}

public enum AgentRecoverySessionStrategy
{
    None,
    FreshSession,
    FreshSessionWithDurableContext,
    ReworkSessionWithPacket,
    SameCompatibleSession,
    HumanEscalation
}

public enum AgentProofStatus
{
    Unknown,
    Succeeded,
    Failed,
    Skipped
}

public sealed record AgentRecoveryDecision(
    AgentRecoveryMode Mode,
    AgentFailureCategory FailureCategory,
    string Reason,
    int AttemptNumber,
    string? SourceExecutionRunId,
    Guid? ReworkPacketId = null,
    DateTimeOffset? NextAttemptAtUtc = null);

public sealed record AgentReworkPacket
{
    public required Guid Id { get; init; }
    public required Guid ProcessRunId { get; init; }
    public required Guid StepRunId { get; init; }
    public string? SourceExecutionRunId { get; init; }
    public Guid? SourceQaStepRunId { get; init; }
    public required AgentRecoveryMode RecoveryMode { get; init; }
    public required AgentFailureCategory FailureCategory { get; init; }
    public required string Objective { get; init; }
    public required IReadOnlyList<AgentReworkFinding> Findings { get; init; }
    public required IReadOnlyList<AgentReworkArtifactRef> ArtifactsToInspect { get; init; }
    public required IReadOnlyList<AgentToolReceiptRef> FailedToolReceipts { get; init; }
    public required IReadOnlyList<AgentProofRequirement> ProofsToRerun { get; init; }
    public required IReadOnlyList<AgentReusableProofRef> ReusableProofs { get; init; }
    public required IReadOnlyList<string> MinimalNextActions { get; init; }
    public required IReadOnlyList<string> ProhibitedActions { get; init; }
    public string? HumanDirective { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
}

public sealed record AgentReworkFinding(
    string Title,
    string Details,
    string Severity,
    string Source);

public sealed record AgentReworkArtifactRef(
    string Title,
    string Path,
    string Reason);

public sealed record AgentToolReceiptRef(
    Guid ReceiptId,
    string ToolName,
    string Status,
    string Summary);

public sealed record AgentProofRequirement(
    string ToolName,
    string Command,
    string WorkingDirectory,
    string Reason);

public sealed record AgentReusableProofRef(
    Guid ReceiptId,
    string ToolName,
    string FingerprintHash,
    string ReuseReason);

public sealed record AgentProofFingerprint(
    string ToolName,
    string Command,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string> SourceFileHashes,
    IReadOnlyDictionary<string, string> ArtifactHashes,
    string EnvironmentSummary,
    string ToolVersion,
    string Hash);

public sealed record AgentProofReceipt(
    Guid Id,
    AgentProofFingerprint Fingerprint,
    AgentProofStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    string Summary);

public sealed record AgentProofReuseDecision(
    bool CanReuse,
    string Reason,
    AgentReusableProofRef? ReusableProof);

public sealed record AgentRecoveryLedgerEntry(
    Guid Id,
    Guid ProcessRunId,
    Guid StepRunId,
    AgentRecoveryMode RecoveryMode,
    AgentFailureCategory FailureCategory,
    string FailureSignatureHash,
    string ProviderName,
    string Model,
    string? SourceExecutionRunId,
    Guid? ReworkPacketId,
    int AttemptNumber,
    int ProviderFallbackCount,
    DateTimeOffset RecordedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    string? TerminalEscalationReason);

public sealed record AgentRecoveryLoopDecision(
    bool ShouldEscalate,
    string Reason,
    DateTimeOffset? NextAttemptAtUtc);

public sealed record AgentRecoveryContext(
    AgentRecoveryDecision Decision,
    AgentRecoverySessionStrategy SessionStrategy,
    Guid ProcessRunId,
    Guid StepRunId,
    Guid? ReworkPacketId,
    IReadOnlyList<AgentReworkArtifactRef> TargetArtifacts,
    IReadOnlyList<AgentToolReceiptRef> RelevantToolReceipts,
    IReadOnlyList<AgentProofRequirement> ProofsToRerun,
    string CompactPreviousOutputSummary,
    IReadOnlyList<string> ProhibitedActions);

public static class AgentRecoveryDecisionFactory
{
    public static AgentRecoveryDecision Create(
        AgentFailureCategory failureCategory,
        string reason,
        int attemptNumber,
        string? sourceExecutionRunId,
        Guid? reworkPacketId = null,
        DateTimeOffset? nextAttemptAtUtc = null)
    {
        return new AgentRecoveryDecision(
            ResolveMode(failureCategory, reworkPacketId),
            failureCategory,
            NormalizeReason(reason, failureCategory),
            attemptNumber,
            sourceExecutionRunId,
            reworkPacketId,
            nextAttemptAtUtc);
    }

    public static AgentRecoveryDecision FormatRepair(
        string reason,
        string? sourceExecutionRunId)
    {
        return new AgentRecoveryDecision(
            AgentRecoveryMode.FormatRepair,
            AgentFailureCategory.StructuredOutputInvalid,
            NormalizeReason(reason, AgentFailureCategory.StructuredOutputInvalid),
            AttemptNumber: 0,
            sourceExecutionRunId);
    }

    public static AgentRecoveryDecision ApprovalContinuation(
        string reason,
        int attemptNumber,
        string? sourceExecutionRunId)
    {
        return new AgentRecoveryDecision(
            AgentRecoveryMode.ApprovalContinuation,
            AgentFailureCategory.Unknown,
            NormalizeReason(reason, AgentFailureCategory.Unknown),
            attemptNumber,
            sourceExecutionRunId);
    }

    public static AgentRecoveryMode ResolveMode(
        AgentFailureCategory failureCategory,
        Guid? reworkPacketId = null)
    {
        if (reworkPacketId.HasValue)
        {
            return AgentRecoveryMode.ReworkContinuation;
        }

        return failureCategory switch
        {
            AgentFailureCategory.StructuredOutputInvalid => AgentRecoveryMode.FormatRepair,
            AgentFailureCategory.ProviderFailure => AgentRecoveryMode.ProviderFallbackRetry,
            AgentFailureCategory.BuildFailure or
            AgentFailureCategory.TestFailure or
            AgentFailureCategory.BrowserProofFailure or
            AgentFailureCategory.QaRejected or
            AgentFailureCategory.ArtifactMissing or
            AgentFailureCategory.UpstreamArtifactInspectionMissing or
            AgentFailureCategory.OutOfScopeReference or
            AgentFailureCategory.HumanRequestedRerun => AgentRecoveryMode.ReworkContinuation,
            AgentFailureCategory.PermissionDenied => AgentRecoveryMode.HumanEscalation,
            AgentFailureCategory.FinalizerMissing or
            AgentFailureCategory.FinalizerInvalid or
            AgentFailureCategory.MissingRequiredTool or
            AgentFailureCategory.CriticalToolFailure or
            AgentFailureCategory.RepeatedToolLoop or
            AgentFailureCategory.Timeout => AgentRecoveryMode.FreshStepRetry,
            _ => AgentRecoveryMode.FreshStepRetry
        };
    }

    public static bool RequiresNewAgentExecution(AgentRecoveryMode mode)
    {
        return mode is AgentRecoveryMode.FreshStepRetry
            or AgentRecoveryMode.ReworkContinuation
            or AgentRecoveryMode.ProviderFallbackRetry;
    }

    private static string NormalizeReason(string reason, AgentFailureCategory category)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? category.ToString()
            : reason.Trim();
    }
}

public static class AgentReworkPacketFactory
{
    public static AgentReworkPacket Create(
        Guid processRunId,
        Guid stepRunId,
        AgentRecoveryDecision decision,
        string objective,
        IReadOnlyList<AgentReworkFinding>? findings = null,
        IReadOnlyList<AgentReworkArtifactRef>? artifactsToInspect = null,
        IReadOnlyList<AgentToolReceiptRef>? failedToolReceipts = null,
        IReadOnlyList<AgentProofRequirement>? proofsToRerun = null,
        IReadOnlyList<AgentReusableProofRef>? reusableProofs = null,
        IReadOnlyList<string>? minimalNextActions = null,
        IReadOnlyList<string>? prohibitedActions = null,
        string? humanDirective = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var packetId = decision.ReworkPacketId ?? Guid.NewGuid();
        return new AgentReworkPacket
        {
            Id = packetId,
            ProcessRunId = processRunId,
            StepRunId = stepRunId,
            SourceExecutionRunId = decision.SourceExecutionRunId,
            RecoveryMode = AgentRecoveryMode.ReworkContinuation,
            FailureCategory = decision.FailureCategory,
            Objective = NormalizeObjective(objective, decision),
            Findings = findings ?? [CreateFinding(decision)],
            ArtifactsToInspect = artifactsToInspect ?? [],
            FailedToolReceipts = failedToolReceipts ?? [],
            ProofsToRerun = proofsToRerun ?? [],
            ReusableProofs = reusableProofs ?? [],
            MinimalNextActions = minimalNextActions ?? ["Repair only the delta described by this packet.", "Rerun invalidated proof tools."],
            ProhibitedActions = prohibitedActions ?? ["Do not regenerate unrelated artifacts.", "Do not replay the failed chat transcript as process truth."],
            HumanDirective = string.IsNullOrWhiteSpace(humanDirective) ? null : humanDirective.Trim(),
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow
        };
    }

    public static AgentReworkPacket CreateQaRejectionPacket(
        Guid processRunId,
        Guid implementationStepRunId,
        Guid qaStepRunId,
        string finding,
        string artifactPath,
        string? sourceExecutionRunId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        var packetId = Guid.NewGuid();
        var decision = new AgentRecoveryDecision(
            AgentRecoveryMode.ReworkContinuation,
            AgentFailureCategory.QaRejected,
            "QA rejected the previous implementation and requested targeted repair.",
            AttemptNumber: 1,
            sourceExecutionRunId,
            packetId);
        var packet = Create(
            processRunId,
            implementationStepRunId,
            decision,
            "Resolve QA findings with the smallest safe change.",
            findings:
            [
                new AgentReworkFinding(
                    "QA finding",
                    string.IsNullOrWhiteSpace(finding) ? "QA requested rework." : finding.Trim(),
                    "High",
                    "QA")
            ],
            artifactsToInspect: string.IsNullOrWhiteSpace(artifactPath)
                ? []
                :
                [
                    new AgentReworkArtifactRef(
                        "QA target artifact",
                        artifactPath.Trim(),
                        "Inspect before repair.")
                ],
            createdAtUtc: createdAtUtc);

        return packet with
        {
            SourceQaStepRunId = qaStepRunId
        };
    }

    public static AgentReworkPacket CreateManualRerunPacket(
        Guid processRunId,
        Guid stepRunId,
        string stepTitle,
        string humanDirective,
        IReadOnlyList<AgentReworkArtifactRef> artifactsToInspect,
        DateTimeOffset? createdAtUtc = null)
    {
        var packetId = Guid.NewGuid();
        var decision = new AgentRecoveryDecision(
            AgentRecoveryMode.ReworkContinuation,
            AgentFailureCategory.HumanRequestedRerun,
            "Human operator requested a targeted rerun.",
            AttemptNumber: 1,
            SourceExecutionRunId: null,
            packetId);
        return Create(
            processRunId,
            stepRunId,
            decision,
            $"Repair or complete step '{stepTitle}' using the operator directive.",
            findings:
            [
                new AgentReworkFinding(
                    "Manual rerun directive",
                    string.IsNullOrWhiteSpace(humanDirective) ? "Operator requested rerun." : humanDirective.Trim(),
                    "Medium",
                    "Human")
            ],
            artifactsToInspect: artifactsToInspect,
            humanDirective: humanDirective,
            createdAtUtc: createdAtUtc);
    }

    public static AgentToolReceiptRef FromReceipt(ToolExecutionReceiptRecord receipt)
    {
        return new AgentToolReceiptRef(
            receipt.Id,
            receipt.ToolName,
            IsReceiptSuccessful(receipt) ? "Succeeded" : "Failed",
            string.IsNullOrWhiteSpace(receipt.ExitSummary) ? receipt.RequestSummary : receipt.ExitSummary);
    }

    private static AgentReworkFinding CreateFinding(AgentRecoveryDecision decision)
    {
        return new AgentReworkFinding(
            decision.FailureCategory.ToString(),
            decision.Reason,
            "High",
            "Automation");
    }

    private static string NormalizeObjective(string objective, AgentRecoveryDecision decision)
    {
        return string.IsNullOrWhiteSpace(objective)
            ? $"Recover from {decision.FailureCategory} with mode {decision.Mode}."
            : objective.Trim();
    }

    private static bool IsReceiptSuccessful(ToolExecutionReceiptRecord receipt)
    {
        return !receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
               !receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) &&
               !receipt.ExitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
    }
}

public static class AgentReworkPromptRenderer
{
    public static string RenderPacketSummary(AgentReworkPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var builder = new StringBuilder();
        builder.AppendLine($"Rework packet id: {packet.Id:D}");
        builder.AppendLine($"Recovery mode: {packet.RecoveryMode}; failure category: {packet.FailureCategory}.");
        builder.AppendLine($"Objective: {packet.Objective}");
        AppendList(builder, "Findings", packet.Findings.Select(item => $"{item.Title}: {item.Details}"));
        AppendList(builder, "Target artifacts/files", packet.ArtifactsToInspect.Select(item => $"{item.Path} - {item.Reason}"));
        AppendList(builder, "Proofs to rerun", packet.ProofsToRerun.Select(item => $"{item.ToolName} {item.Command}".Trim()));
        AppendList(builder, "Reusable proofs", packet.ReusableProofs.Select(item => $"{item.ToolName} {item.FingerprintHash}: {item.ReuseReason}"));
        AppendList(builder, "Minimal next actions", packet.MinimalNextActions);
        AppendList(builder, "Prohibited actions", packet.ProhibitedActions);
        if (!string.IsNullOrWhiteSpace(packet.HumanDirective))
        {
            builder.AppendLine($"Human directive: {packet.HumanDirective}");
        }

        builder.AppendLine("You are continuing an existing implementation. Do not regenerate the entire application or repeat completed work unless this rework packet explicitly requires it.");
        builder.AppendLine("Make the smallest change that resolves the findings, then rerun the invalidated proof tools.");
        builder.AppendLine($"Reference rework packet {packet.Id:D} in evidence refs or completion metadata.");
        return builder.ToString().Trim();
    }

    public static string RenderRecoveryDirective(
        AgentRecoveryDecision decision,
        AgentReworkPacket? packet,
        string legacyDirective)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var builder = new StringBuilder();
        builder.AppendLine($"Typed recovery decision: mode={decision.Mode}; category={decision.FailureCategory}; attempt={decision.AttemptNumber}; sourceExecutionRunId={decision.SourceExecutionRunId ?? "none"}.");
        builder.AppendLine($"Recovery reason: {decision.Reason}");
        if (packet is not null)
        {
            builder.AppendLine(RenderPacketSummary(packet));
        }

        if (!string.IsNullOrWhiteSpace(legacyDirective))
        {
            builder.AppendLine();
            builder.AppendLine("Recovery details:");
            builder.AppendLine(legacyDirective.Trim());
        }

        return builder.ToString().Trim();
    }

    public static string SerializePacket(AgentReworkPacket packet)
    {
        return JsonSerializer.Serialize(packet, AgentOutputJson.SerializerOptions);
    }

    private static void AppendList(
        StringBuilder builder,
        string title,
        IEnumerable<string> items)
    {
        var materializedItems = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Take(8)
            .ToList();
        if (materializedItems.Count == 0)
        {
            return;
        }

        builder.AppendLine($"{title}:");
        foreach (var item in materializedItems)
        {
            builder.AppendLine($"- {item}");
        }
    }
}

public static class AgentRecoveryContextBuilder
{
    public static AgentRecoveryContext Build(
        Guid processRunId,
        Guid stepRunId,
        AgentRecoveryDecision decision,
        AgentReworkPacket? packet,
        IReadOnlyList<AgentToolReceiptRef>? relevantToolReceipts = null,
        string? compactPreviousOutputSummary = null)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new AgentRecoveryContext(
            decision,
            ResolveSessionStrategy(decision.Mode),
            processRunId,
            stepRunId,
            packet?.Id ?? decision.ReworkPacketId,
            packet?.ArtifactsToInspect ?? [],
            relevantToolReceipts ?? packet?.FailedToolReceipts ?? [],
            packet?.ProofsToRerun ?? [],
            string.IsNullOrWhiteSpace(compactPreviousOutputSummary)
                ? string.Empty
                : compactPreviousOutputSummary.Trim(),
            packet?.ProhibitedActions ?? []);
    }

    public static AgentRecoverySessionStrategy ResolveSessionStrategy(AgentRecoveryMode mode)
    {
        return mode switch
        {
            AgentRecoveryMode.FormatRepair => AgentRecoverySessionStrategy.None,
            AgentRecoveryMode.FreshStepRetry => AgentRecoverySessionStrategy.FreshSessionWithDurableContext,
            AgentRecoveryMode.ReworkContinuation => AgentRecoverySessionStrategy.ReworkSessionWithPacket,
            AgentRecoveryMode.ProviderFallbackRetry => AgentRecoverySessionStrategy.FreshSession,
            AgentRecoveryMode.ApprovalContinuation => AgentRecoverySessionStrategy.SameCompatibleSession,
            AgentRecoveryMode.HumanEscalation => AgentRecoverySessionStrategy.HumanEscalation,
            _ => AgentRecoverySessionStrategy.FreshSession
        };
    }
}

public static class AgentProofFingerprintService
{
    public static AgentProofReceipt CreateReceipt(
        string toolName,
        string command,
        string workingDirectory,
        IReadOnlyDictionary<string, string> sourceFileHashes,
        IReadOnlyDictionary<string, string> artifactHashes,
        string environmentSummary,
        string toolVersion,
        AgentProofStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset finishedAtUtc,
        string summary)
    {
        var fingerprint = CreateFingerprint(
            toolName,
            command,
            workingDirectory,
            sourceFileHashes,
            artifactHashes,
            environmentSummary,
            toolVersion);
        return new AgentProofReceipt(
            Guid.NewGuid(),
            fingerprint,
            status,
            startedAtUtc,
            finishedAtUtc,
            summary);
    }

    public static AgentProofFingerprint CreateFingerprint(
        string toolName,
        string command,
        string workingDirectory,
        IReadOnlyDictionary<string, string> sourceFileHashes,
        IReadOnlyDictionary<string, string> artifactHashes,
        string environmentSummary,
        string toolVersion)
    {
        var normalizedSourceHashes = NormalizeHashes(sourceFileHashes);
        var normalizedArtifactHashes = NormalizeHashes(artifactHashes);
        var hash = ComputeHash(
            toolName,
            command,
            workingDirectory,
            normalizedSourceHashes,
            normalizedArtifactHashes,
            environmentSummary,
            toolVersion);
        return new AgentProofFingerprint(
            Normalize(toolName),
            Normalize(command),
            Normalize(workingDirectory),
            normalizedSourceHashes,
            normalizedArtifactHashes,
            Normalize(environmentSummary),
            Normalize(toolVersion),
            hash);
    }

    public static AgentProofReuseDecision EvaluateReuse(
        AgentProofReceipt receipt,
        IReadOnlyDictionary<string, string> currentSourceFileHashes,
        IReadOnlyDictionary<string, string> currentArtifactHashes,
        DateTimeOffset now,
        TimeSpan maxAge)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (receipt.Status != AgentProofStatus.Succeeded)
        {
            return new AgentProofReuseDecision(false, "Only successful proof receipts can be reused.", null);
        }

        if (now - receipt.FinishedAtUtc > maxAge)
        {
            return new AgentProofReuseDecision(false, "Proof receipt is older than the configured reuse window.", null);
        }

        var normalizedSourceHashes = NormalizeHashes(currentSourceFileHashes);
        if (!HashesMatch(receipt.Fingerprint.SourceFileHashes, normalizedSourceHashes, out var sourceMismatch))
        {
            return new AgentProofReuseDecision(false, $"Source fingerprint changed: {sourceMismatch}.", null);
        }

        var normalizedArtifactHashes = NormalizeHashes(currentArtifactHashes);
        if (!HashesMatch(receipt.Fingerprint.ArtifactHashes, normalizedArtifactHashes, out var artifactMismatch))
        {
            return new AgentProofReuseDecision(false, $"Artifact fingerprint changed: {artifactMismatch}.", null);
        }

        return new AgentProofReuseDecision(
            true,
            "Relevant command, working directory, source hashes, artifact hashes, environment, and tool version still match.",
            new AgentReusableProofRef(
                receipt.Id,
                receipt.Fingerprint.ToolName,
                receipt.Fingerprint.Hash,
                "Fingerprint still matches current relevant inputs."));
    }

    public static bool InvalidatesBuildOrTestProof(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
    }

    public static bool InvalidatesBrowserProof(string path)
    {
        return InvalidatesBuildOrTestProof(path) ||
               path.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/wwwroot/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("\\wwwroot\\", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> NormalizeHashes(IReadOnlyDictionary<string, string> hashes)
    {
        return hashes
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => NormalizePath(item.Key),
                item => Normalize(item.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool HashesMatch(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> current,
        out string mismatch)
    {
        foreach (var expectedHash in expected)
        {
            if (!current.TryGetValue(expectedHash.Key, out var currentHash))
            {
                mismatch = $"{expectedHash.Key} is missing";
                return false;
            }

            if (!string.Equals(expectedHash.Value, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                mismatch = $"{expectedHash.Key} changed";
                return false;
            }
        }

        mismatch = string.Empty;
        return true;
    }

    private static string ComputeHash(
        string toolName,
        string command,
        string workingDirectory,
        IReadOnlyDictionary<string, string> sourceFileHashes,
        IReadOnlyDictionary<string, string> artifactHashes,
        string environmentSummary,
        string toolVersion)
    {
        var builder = new StringBuilder();
        builder.AppendLine(Normalize(toolName));
        builder.AppendLine(Normalize(command));
        builder.AppendLine(NormalizePath(workingDirectory));
        AppendHashes(builder, "source", sourceFileHashes);
        AppendHashes(builder, "artifact", artifactHashes);
        builder.AppendLine(Normalize(environmentSummary));
        builder.AppendLine(Normalize(toolVersion));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static void AppendHashes(
        StringBuilder builder,
        string prefix,
        IReadOnlyDictionary<string, string> hashes)
    {
        foreach (var item in hashes.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(prefix);
            builder.Append(':');
            builder.Append(NormalizePath(item.Key));
            builder.Append('=');
            builder.AppendLine(Normalize(item.Value));
        }
    }

    private static string NormalizePath(string value)
    {
        return Normalize(value).Replace('\\', '/');
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}

public static class AgentRecoveryLedger
{
    public static string ComputeFailureSignatureHash(
        AgentFailureCategory failureCategory,
        string reason)
    {
        var input = $"{failureCategory}|{Normalize(reason)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    public static AgentRecoveryLedgerEntry CreateEntry(
        Guid processRunId,
        Guid stepRunId,
        AgentRecoveryDecision decision,
        string providerName,
        string model,
        int providerFallbackCount,
        DateTimeOffset recordedAtUtc,
        string? terminalEscalationReason = null)
    {
        return new AgentRecoveryLedgerEntry(
            Guid.NewGuid(),
            processRunId,
            stepRunId,
            decision.Mode,
            decision.FailureCategory,
            ComputeFailureSignatureHash(decision.FailureCategory, decision.Reason),
            Normalize(providerName),
            Normalize(model),
            decision.SourceExecutionRunId,
            decision.ReworkPacketId,
            decision.AttemptNumber,
            providerFallbackCount,
            recordedAtUtc,
            decision.NextAttemptAtUtc,
            terminalEscalationReason);
    }

    public static AgentRecoveryLoopDecision EvaluateLoopControl(
        IReadOnlyList<AgentRecoveryLedgerEntry> ledger,
        AgentRecoveryDecision nextDecision,
        int maxIdenticalFailures,
        int maxProviderFallbacks,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(nextDecision);

        var signatureHash = ComputeFailureSignatureHash(nextDecision.FailureCategory, nextDecision.Reason);
        var identicalFailureCount = ledger.Count(item =>
            string.Equals(item.FailureSignatureHash, signatureHash, StringComparison.OrdinalIgnoreCase));
        if (identicalFailureCount >= maxIdenticalFailures)
        {
            return new AgentRecoveryLoopDecision(
                true,
                $"Failure signature repeated {identicalFailureCount} time(s); escalate instead of looping.",
                null);
        }

        if (nextDecision.Mode == AgentRecoveryMode.ProviderFallbackRetry)
        {
            var providerFallbackCount = ledger.Count(item => item.RecoveryMode == AgentRecoveryMode.ProviderFallbackRetry);
            if (providerFallbackCount >= maxProviderFallbacks)
            {
                return new AgentRecoveryLoopDecision(
                    true,
                    $"Provider fallback budget exhausted after {providerFallbackCount} fallback attempt(s).",
                    null);
            }
        }

        var pendingEntry = ledger
            .Where(item => item.NextAttemptAtUtc is DateTimeOffset nextAttemptAtUtc && nextAttemptAtUtc > now)
            .OrderBy(item => item.NextAttemptAtUtc)
            .FirstOrDefault();
        return pendingEntry is null
            ? new AgentRecoveryLoopDecision(false, "Recovery attempt is allowed now.", nextDecision.NextAttemptAtUtc)
            : new AgentRecoveryLoopDecision(false, "Recovery attempt is waiting for backoff.", pendingEntry.NextAttemptAtUtc);
    }

    public static bool CanAttemptNow(
        IReadOnlyList<AgentRecoveryLedgerEntry> ledger,
        DateTimeOffset now)
    {
        return !ledger.Any(item => item.NextAttemptAtUtc is DateTimeOffset nextAttemptAtUtc && nextAttemptAtUtc > now);
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
