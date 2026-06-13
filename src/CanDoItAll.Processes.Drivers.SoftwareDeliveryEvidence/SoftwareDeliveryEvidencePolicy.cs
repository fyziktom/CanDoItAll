namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public static class SoftwareDeliveryEvidencePolicy
{
    public static SoftwareDeliveryProofPolicyResult Evaluate(SoftwareDeliveryProofPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var successfulReceipts = request.ToolReceipts
            .Where(receipt => receipt.Succeeded)
            .ToList();
        var contractSignals = SoftwareDeliveryContractRules.ResolveSignals(request.Contract);
        var requiresCurrentAttemptProductMutation =
            SoftwareDeliveryContractRules.RequiresCurrentAttemptProductMutation(request.Contract);
        var hasConcreteImplementationProofEvidence =
            request.HasConcreteImplementationMockProof ||
            SoftwareDeliveryReceiptTimeline.ResolveLatestImplementationProofReadReceipt(
                contractSignals.RequiresSourceOrProjectImplementationProof,
                successfulReceipts) is not null;
        var hasRunnableApplicationProofEvidence =
            SoftwareDeliveryReceiptTimeline.ResolveLatestReceipt(
                successfulReceipts,
                SoftwareDeliveryReceiptTimeline.IsRunValidationToolName,
                requireConcreteProductPath: true,
                requireConcreteDeliverableOrSourcePath: false) is not null;
        var concreteMutationReceipts = ResolveConcreteMutationReceipts(
            request,
            successfulReceipts,
            contractSignals.RequiresSourceOrProjectImplementationProof,
            requiresCurrentAttemptProductMutation);
        var latestConcreteProductReadReceipt = SoftwareDeliveryReceiptTimeline.ResolveLatestImplementationProofReadReceipt(
            contractSignals.RequiresSourceOrProjectImplementationProof,
            successfulReceipts);
        var latestConcreteProductMutationReceipt = ResolveLatestReceipt(concreteMutationReceipts);
        var latestImplementationValidationReceipt = SoftwareDeliveryReceiptTimeline.ResolveLatestRequiredImplementationValidationReceipt(
            request.RequiredToolNames.ToHashSet(StringComparer.Ordinal),
            successfulReceipts);
        var hasValidationAfterLatestMutation = latestConcreteProductMutationReceipt is not null &&
                                               latestImplementationValidationReceipt is not null &&
                                               !SoftwareDeliveryReceiptTimeline.IsReceiptAfter(
                                                   latestConcreteProductMutationReceipt,
                                                   latestImplementationValidationReceipt);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(
            request,
            successfulReceipts,
            concreteMutationReceipts,
            latestConcreteProductReadReceipt,
            latestImplementationValidationReceipt,
            contractSignals.RequiresSourceOrProjectImplementationProof,
            requiresCurrentAttemptProductMutation);
        var missingRunnableApplicationProofSummary = ResolveMissingRunnableApplicationProofSummary(
            request,
            successfulReceipts,
            concreteMutationReceipts);

        return new SoftwareDeliveryProofPolicyResult(
            missingConcreteImplementationProofSummary,
            missingRunnableApplicationProofSummary,
            contractSignals.Stack,
            latestConcreteProductReadReceipt,
            latestConcreteProductMutationReceipt,
            latestImplementationValidationReceipt,
            hasValidationAfterLatestMutation,
            contractSignals.RequiresSourceOrProjectImplementationProof,
            requiresCurrentAttemptProductMutation,
            hasConcreteImplementationProofEvidence,
            hasRunnableApplicationProofEvidence,
            concreteMutationReceipts.Count > 0,
            CarriedProof: request.CarriedProof,
            Diagnostics: []);
    }

    public static SoftwareDeliveryCarriedProofSnapshot ResolveCarriedProof(
        SoftwareDeliveryProofPolicyRequest request,
        SoftwareDeliveryProofPolicyResult result,
        SoftwareDeliveryCarriedProofSnapshot previous)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(previous);

        if (!request.Contract.RequiresConcreteImplementationProof)
        {
            return previous;
        }

        var hasConcreteProductMutation =
            previous.HasCarriedConcreteProductMutation ||
            result.HasSuccessfulConcreteProductMutation;
        var hasConcreteImplementationProof = result.HasSuccessfulConcreteProductMutation
            ? false
            : previous.HasCarriedConcreteImplementationProof;
        var hasRunnableApplicationProof = result.HasSuccessfulConcreteProductMutation
            ? false
            : previous.HasCarriedRunnableApplicationProof;

        if (!result.HasSuccessfulConcreteProductMutation &&
            previous.HasCarriedConcreteProductMutation &&
            result.HasConcreteImplementationProofEvidence)
        {
            hasConcreteImplementationProof = true;
        }

        if (string.IsNullOrWhiteSpace(result.MissingConcreteImplementationProofSummary) &&
            result.HasConcreteImplementationProofEvidence)
        {
            hasConcreteImplementationProof = true;
        }

        if (string.IsNullOrWhiteSpace(result.MissingRunnableApplicationProofSummary) &&
            result.HasRunnableApplicationProofEvidence)
        {
            hasRunnableApplicationProof = true;
        }

        return previous with
        {
            HasCarriedConcreteImplementationProof = hasConcreteImplementationProof,
            HasCarriedRunnableApplicationProof = hasRunnableApplicationProof,
            HasCarriedConcreteProductMutation = hasConcreteProductMutation
        };
    }

    public static SoftwareDeliveryCarriedProofSnapshot ResolveHistoricalCarriedProof(
        bool requiresCurrentAttemptProductMutation,
        IEnumerable<SoftwareDeliveryHistoricalExecutionProofSnapshot> historicalProofs)
    {
        ArgumentNullException.ThrowIfNull(historicalProofs);

        return requiresCurrentAttemptProductMutation &&
               historicalProofs.Any(proof => proof.IsCarryForwardEligible && proof.HasSuccessfulConcreteProductMutation)
            ? new SoftwareDeliveryCarriedProofSnapshot(
                HasCarriedConcreteImplementationProof: false,
                HasCarriedRunnableApplicationProof: false,
                HasCarriedConcreteProductMutation: true,
                SourceRunId: string.Empty,
                Summary: "Historical execution supplied a concrete product mutation.")
            : EmptyCarriedProof;
    }

    public static string ResolveMissingConcreteImplementationProofSummaryWithCarryForward(
        string summary,
        SoftwareDeliveryProofPolicyResult result,
        SoftwareDeliveryCarriedProofSnapshot carriedProof)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(carriedProof);

        if (result.RequiresCurrentAttemptProductMutation &&
            carriedProof.HasCarriedConcreteProductMutation &&
            result.HasConcreteImplementationProofEvidence &&
            string.Equals(
                summary,
                "the current repair attempt did not mutate any concrete product file",
                StringComparison.Ordinal))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(summary) ||
            result.RequiresCurrentAttemptProductMutation ||
            !carriedProof.HasCarriedConcreteImplementationProof ||
            result.HasSuccessfulConcreteProductMutation)
        {
            return summary;
        }

        return string.Empty;
    }

    public static string ResolveMissingRunnableApplicationProofSummaryWithCarryForward(
        string summary,
        SoftwareDeliveryProofPolicyResult result,
        SoftwareDeliveryCarriedProofSnapshot carriedProof)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(carriedProof);

        if (string.IsNullOrWhiteSpace(summary) ||
            !carriedProof.HasCarriedRunnableApplicationProof ||
            result.HasSuccessfulConcreteProductMutation)
        {
            return summary;
        }

        return string.Empty;
    }

    public static SoftwareDeliveryCarriedProofSnapshot EmptyCarriedProof { get; } = new(
        HasCarriedConcreteImplementationProof: false,
        HasCarriedRunnableApplicationProof: false,
        HasCarriedConcreteProductMutation: false,
        SourceRunId: string.Empty,
        Summary: string.Empty);

    private static string ResolveMissingConcreteImplementationProofSummary(
        SoftwareDeliveryProofPolicyRequest request,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> concreteMutationReceipts,
        SoftwareDeliveryToolReceiptSnapshot? concreteReadReceipt,
        SoftwareDeliveryToolReceiptSnapshot? latestValidationReceipt,
        bool requiresSourceOrProjectImplementationProof,
        bool requiresCurrentAttemptProductMutation)
    {
        if (!request.Contract.RequiresConcreteImplementationProof)
        {
            return string.Empty;
        }

        if (request.HasConcreteImplementationMockProof)
        {
            return string.Empty;
        }

        if (concreteReadReceipt is null)
        {
            return requiresSourceOrProjectImplementationProof
                ? "the current attempt did not read any concrete product source or project file"
                : "the current attempt did not read any concrete product deliverable, source, or project file";
        }

        if (requiresCurrentAttemptProductMutation &&
            concreteMutationReceipts.Count == 0)
        {
            return "the current repair attempt did not mutate any concrete product file";
        }

        var latestMutationReceipt = ResolveLatestReceipt(concreteMutationReceipts);
        if (latestMutationReceipt is null)
        {
            return string.Empty;
        }

        var hasValidationAfterLatestMutation = latestValidationReceipt is not null &&
                                               !SoftwareDeliveryReceiptTimeline.IsReceiptAfter(
                                                   latestMutationReceipt,
                                                   latestValidationReceipt);
        if (SoftwareDeliveryReceiptTimeline.IsReceiptAfter(latestMutationReceipt, concreteReadReceipt) &&
            !hasValidationAfterLatestMutation)
        {
            return "workspace_read_file ran before the latest concrete product mutation";
        }

        var latestBootstrapReceipt = concreteMutationReceipts
            .Where(receipt => SoftwareDeliveryReceiptTimeline.IsImplementationBootstrapToolName(NormalizeToolToken(receipt.ToolName)))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestBootstrapReceipt is not null &&
            !successfulReceipts.Any(receipt =>
                SoftwareDeliveryReceiptTimeline.ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                SoftwareDeliveryReceiptTimeline.IsReceiptAfter(receipt, latestBootstrapReceipt) &&
                SoftwareDeliveryPathRules.HasConcreteProductImplementationPath(
                    requiresSourceOrProjectImplementationProof,
                    receipt)))
        {
            return "the latest scaffold or bootstrap tool was not followed by a concrete product deliverable, source, or project file write";
        }

        if (latestValidationReceipt is not null &&
            SoftwareDeliveryReceiptTimeline.IsReceiptAfter(latestMutationReceipt, latestValidationReceipt))
        {
            return $"{latestValidationReceipt.ToolName} ran before the latest concrete product mutation";
        }

        return string.Empty;
    }

    private static string ResolveMissingRunnableApplicationProofSummary(
        SoftwareDeliveryProofPolicyRequest request,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> concreteMutationReceipts)
    {
        if (!request.Contract.RequiresConcreteImplementationProof)
        {
            return string.Empty;
        }

        if (request.Contract.IsDotNetSolutionSetupScaffoldMutationStep)
        {
            return string.Empty;
        }

        var contractSignals = SoftwareDeliveryContractRules.ResolveSignals(request.Contract);
        if (!contractSignals.MentionsDotNet &&
            (contractSignals.MentionsJavaScript || contractSignals.NegatesDotNet))
        {
            return string.Empty;
        }

        if (!SoftwareDeliveryReceiptTimeline.HasBuildValidationReceipt(successfulReceipts) &&
            !contractSignals.ContainsRunnableApplicationSignal)
        {
            return string.Empty;
        }

        if (request.RunnableHost.RunnableProjectPaths.Count == 0)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(request.RunnableHost.InvalidHostSummary))
        {
            return request.RunnableHost.InvalidHostSummary;
        }

        var latestRunReceipt = SoftwareDeliveryReceiptTimeline.ResolveLatestReceipt(
            successfulReceipts,
            SoftwareDeliveryReceiptTimeline.IsRunValidationToolName,
            requireConcreteProductPath: true,
            requireConcreteDeliverableOrSourcePath: false);
        if (latestRunReceipt is null)
        {
            return $"the current attempt did not start the runnable .NET host with a run tool after implementation; detected host project: {request.RunnableHost.RunnableProjectPaths[0]}";
        }

        var latestMutationReceipt = ResolveLatestReceipt(concreteMutationReceipts);
        if (latestMutationReceipt is not null &&
            SoftwareDeliveryReceiptTimeline.IsReceiptAfter(latestMutationReceipt, latestRunReceipt))
        {
            return "the run tool ran before the latest concrete product mutation";
        }

        return string.Empty;
    }

    private static IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> ResolveConcreteMutationReceipts(
        SoftwareDeliveryProofPolicyRequest request,
        IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> successfulReceipts,
        bool requiresSourceOrProjectImplementationProof,
        bool requiresCurrentAttemptProductMutation)
    {
        return successfulReceipts
            .Where(receipt => SoftwareDeliveryReceiptTimeline.IsConcreteProductMutationToolName(NormalizeToolToken(receipt.ToolName)))
            .Where(receipt => SoftwareDeliveryPathRules.IsConcreteProductMutationReceipt(
                requiresCurrentAttemptProductMutation,
                requiresSourceOrProjectImplementationProof,
                request.ExternalTarget.AllowedAliases,
                receipt))
            .ToList();
    }

    private static SoftwareDeliveryToolReceiptSnapshot? ResolveLatestReceipt(
        IEnumerable<SoftwareDeliveryToolReceiptSnapshot> receipts)
    {
        return receipts
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    public static string NormalizeToolToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }
}

public sealed record SoftwareDeliveryProofPolicyRequest(
    SoftwareDeliveryImplementationContractSnapshot Contract,
    SoftwareDeliveryPathFacts PathFacts,
    SoftwareDeliveryExternalTargetSnapshot ExternalTarget,
    IReadOnlyList<SoftwareDeliveryToolReceiptSnapshot> ToolReceipts,
    IReadOnlyList<SoftwareDeliveryArtifactExpectationSnapshot> ExpectedArtifacts,
    IReadOnlyList<SoftwareDeliveryArtifactRecordSnapshot> ArtifactRecords,
    SoftwareDeliveryBrowserEvidenceSnapshot BrowserEvidence,
    SoftwareDeliveryRunnableHostSnapshot RunnableHost,
    SoftwareDeliveryCarriedProofSnapshot CarriedProof,
    IReadOnlyList<string> RequiredToolNames,
    bool HasConcreteImplementationMockProof,
    DateTimeOffset RequestedAtUtc);

public sealed record SoftwareDeliveryProofPolicyResult(
    string MissingConcreteImplementationProofSummary,
    string MissingRunnableApplicationProofSummary,
    SoftwareDeliveryImplementationStack Stack,
    SoftwareDeliveryToolReceiptSnapshot? LatestConcreteProductReadReceipt,
    SoftwareDeliveryToolReceiptSnapshot? LatestConcreteProductMutationReceipt,
    SoftwareDeliveryToolReceiptSnapshot? LatestImplementationValidationReceipt,
    bool HasValidationAfterLatestMutation,
    bool RequiresSourceOrProjectImplementationProof,
    bool RequiresCurrentAttemptProductMutation,
    bool HasConcreteImplementationProofEvidence,
    bool HasRunnableApplicationProofEvidence,
    bool HasSuccessfulConcreteProductMutation,
    SoftwareDeliveryCarriedProofSnapshot CarriedProof,
    IReadOnlyList<string> Diagnostics)
{
    public bool HasMissingProof =>
        !string.IsNullOrWhiteSpace(MissingConcreteImplementationProofSummary) ||
        !string.IsNullOrWhiteSpace(MissingRunnableApplicationProofSummary);
}
