using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.OfficeEvidence;

public sealed class OfficeEvidenceAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(OfficeEvidenceVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? request.SuppliedContent.EvidenceReference;
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = OfficeEvidenceVerificationRequestPolicy.Validate(
            request,
            evidenceReferences,
            diagnostics,
            primaryEvidence);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(OfficeEvidenceDiagnosticRules.Evaluate(request.Items, primaryEvidence));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(OfficeEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Office evidence verification found supplied email and document metadata with text content.",
                primaryEvidence));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message)));
        var auditFacts = OfficeEvidenceAuditFactMapper.CreateAuditFacts(
            request,
            diagnostics,
            redaction.Descriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            evidenceReferences.Count == 0 ? [primaryEvidence] : evidenceReferences,
            redaction.Descriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }
}
