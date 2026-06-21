using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.RuntimeEvidence;

public sealed class RuntimeEvidenceConsistencyAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(RuntimeEvidenceConsistencyVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var descriptorContext = RuntimeEvidenceDescriptorNormalizer.CreateContext(request);
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = RuntimeEvidenceVerificationRequestPolicy.Validate(
            request,
            descriptorContext.NormalizedEvidenceReferences,
            diagnostics,
            descriptorContext.PrimaryEvidenceReference);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(RuntimeEvidenceContradictionRules.Evaluate(
                request,
                descriptorContext.PrimaryEvidenceReference));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(RuntimeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Runtime evidence consistency verification found no contradictory Core descriptor facts.",
                descriptorContext.PrimaryEvidenceReference));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message)));
        var auditFacts = RuntimeEvidenceAuditFactMapper.CreateAuditFacts(
            request,
            diagnostics,
            redaction.Descriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            descriptorContext.CreateResponseEvidenceReferences(),
            redaction.Descriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }
}
