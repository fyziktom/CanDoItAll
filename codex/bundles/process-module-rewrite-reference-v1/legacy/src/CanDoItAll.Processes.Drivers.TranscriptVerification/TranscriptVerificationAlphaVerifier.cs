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

        var evidenceContext = TranscriptVerificationEvidencePolicy.CreateContext(request);
        var diagnostics = new List<ProcessDriverDiagnostic>();
        var denialReason = TranscriptVerificationRequestPolicy.Validate(
            request,
            evidenceContext.NormalizedEvidenceReferences,
            diagnostics,
            evidenceContext.PrimaryEvidenceReference);

        if (denialReason == ProcessDriverDenialReason.None)
        {
            diagnostics.AddRange(parserSet.Parse(
                request.TranscriptReference.Language,
                request.TranscriptText,
                evidenceContext.PrimaryEvidenceReference,
                evidenceContext.Redaction));
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(TranscriptVerificationDiagnosticFactory.Create(
                ProcessDriverDiagnosticSeverity.Info,
                ProcessDriverDiagnosticCategory.NoIssueDetected,
                "Transcript verification found no known .NET or Rust diagnostic markers.",
                evidenceContext.PrimaryEvidenceReference,
                evidenceContext.Redaction));
        }

        var accepted = denialReason == ProcessDriverDenialReason.None;
        var auditFacts = TranscriptVerificationAuditFactBuilder.CreateAuditFacts(
            request,
            diagnostics,
            evidenceContext.Redaction.Descriptor,
            accepted,
            denialReason);

        return new ProcessDriverVerificationResponse(
            accepted,
            denialReason,
            diagnostics,
            evidenceContext.CreateResponseEvidenceReferences(),
            evidenceContext.Redaction.Descriptor,
            NoMutationPerformed: true,
            auditFacts,
            ProcessDriverContractVersion.Current);
    }
}
