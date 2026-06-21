using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ArtifactEvidence;

public sealed class ArtifactEvidenceAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(ArtifactEvidenceVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? request.SuppliedContent.EvidenceReference;
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = ArtifactEvidenceVerificationRequestPolicy.Validate(
            request,
            evidenceReferences,
            diagnostics,
            primaryEvidence);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(ArtifactEvidenceDiagnosticRules.Evaluate(request, primaryEvidence));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(ArtifactEvidenceDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Artifact evidence verification found supplied projection and validation descriptors with metadata.",
                primaryEvidence));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message)));
        var auditFacts = ArtifactEvidenceAuditFactMapper.CreateAuditFacts(
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
