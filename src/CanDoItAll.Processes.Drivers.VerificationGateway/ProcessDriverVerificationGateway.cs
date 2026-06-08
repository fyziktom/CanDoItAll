using CanDoItAll.Processes.Drivers.Abstractions.Gateway;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;
using CanDoItAll.Processes.Drivers.RuntimeEvidence;
using CanDoItAll.Processes.Drivers.TranscriptVerification;

namespace CanDoItAll.Processes.Drivers.VerificationGateway;

public sealed class ProcessDriverVerificationGateway
{
    private static readonly IReadOnlyList<ProcessDriverVerificationGatewayLaneDescriptor> ImplementedLaneDescriptors =
    [
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.DotNetRustTranscriptVerification),
        ProcessDriverVerificationGatewayLaneRules.Describe(ProcessDriverVerificationGatewayLane.RuntimeEvidenceConsistency)
    ];

    private readonly TranscriptVerificationAlphaVerifier transcriptVerifier;
    private readonly RuntimeEvidenceConsistencyAlphaVerifier runtimeEvidenceVerifier;

    public ProcessDriverVerificationGateway(
        TranscriptVerificationAlphaVerifier transcriptVerifier,
        RuntimeEvidenceConsistencyAlphaVerifier runtimeEvidenceVerifier)
    {
        this.transcriptVerifier = transcriptVerifier ?? throw new ArgumentNullException(nameof(transcriptVerifier));
        this.runtimeEvidenceVerifier = runtimeEvidenceVerifier ?? throw new ArgumentNullException(nameof(runtimeEvidenceVerifier));
    }

    public IReadOnlyList<ProcessDriverVerificationGatewayLaneDescriptor> ImplementedLanes => ImplementedLaneDescriptors;

    public static ProcessDriverVerificationGateway CreateDefault()
    {
        return new ProcessDriverVerificationGateway(
            new TranscriptVerificationAlphaVerifier(),
            new RuntimeEvidenceConsistencyAlphaVerifier());
    }

    public ProcessDriverVerificationResponse VerifyTranscript(TranscriptVerificationAlphaRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return transcriptVerifier.Verify(request);
    }

    public ProcessDriverVerificationResponse VerifyRuntimeEvidence(RuntimeEvidenceConsistencyVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return runtimeEvidenceVerifier.Verify(request);
    }
}
