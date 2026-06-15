using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.BusinessAnalysis;

public sealed class BusinessAnalysisAlphaVerifier
{
    public ProcessDriverVerificationResponse Verify(BusinessAnalysisVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? request.SuppliedContent.EvidenceReference;
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = BusinessAnalysisVerificationRequestPolicy.Validate(
            request,
            evidenceReferences,
            diagnostics,
            primaryEvidence);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(BusinessAnalysisDiagnosticRules.Evaluate(request.Items, primaryEvidence));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(BusinessAnalysisDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Business analysis verification found supplied deliverable and evidence text with metadata.",
                primaryEvidence));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var redaction = ProcessDriverRedactionPolicy.Redact(string.Join(
            " ",
            diagnostics.Select(diagnostic => diagnostic.Message)));
        var auditFacts = BusinessAnalysisAuditFactMapper.CreateAuditFacts(
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
