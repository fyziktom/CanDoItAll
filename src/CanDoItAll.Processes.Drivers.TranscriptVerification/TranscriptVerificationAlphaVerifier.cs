using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.TranscriptVerification;

public sealed class TranscriptVerificationAlphaVerifier
{
    private readonly TranscriptDiagnosticParserSet parserSet = new();

    public ProcessDriverVerificationResponse Verify(TranscriptVerificationAlphaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = request.VerificationRequest ?? throw new ArgumentException(
            "Verification request is required.",
            nameof(request));

        var evidenceReferences = ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            request.VerificationRequest.EvidenceReferences);
        var transcriptEvidence = ProcessDriverEvidencePolicy.CreateTranscriptEvidenceReference(
            request.TranscriptReference,
            request.TranscriptText);
        var primaryEvidence = evidenceReferences.FirstOrDefault() ?? transcriptEvidence;
        var redaction = ProcessDriverRedactionPolicy.Redact(request.TranscriptText);
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = TranscriptVerificationRequestPolicy.Validate(
            request,
            evidenceReferences,
            diagnostics,
            primaryEvidence);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(parserSet.Parse(
                request.TranscriptReference.Language,
                request.TranscriptText,
                primaryEvidence,
                redaction));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(TranscriptVerificationDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Transcript verification found no known .NET or Rust diagnostic markers.",
                primaryEvidence,
                redaction));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var auditFacts = TranscriptVerificationAuditFactBuilder.CreateAuditFacts(
            request,
            diagnostics,
            redaction.Descriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            evidenceReferences.Count == 0 ? [transcriptEvidence] : evidenceReferences,
            redaction.Descriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }
}
