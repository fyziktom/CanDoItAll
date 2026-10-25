using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

public enum ProcessAcceptedExecutionInputVersion { NotCaptured, OriginalAssignment, OriginalStepRequirements }

internal sealed record ProcessCapturedExecutionInput(ProcessRuntimeStepAssignment Assignment, ProcessAcceptedStepRequirements? Requirements);

internal static class ProcessAcceptedExecutionInputCodec {
    internal const int MaximumPayloadUtf8Bytes = 4_194_304;
    internal const int MaximumCollectionCount = 4096;
    private const string AssignmentDomain = "process-accepted-execution-input/v1\n";
    private const string RequirementsDomain = "process-accepted-execution-input/v2\n";

    internal static ProcessAcceptedSourceEnvelope Capture(ProcessRuntimeStepAssignment assignment, ProcessAcceptedStepRequirements requirements,
        string headerFingerprint, ProcessAcceptedSourceEnvelope originalMetadata) {
        RequireAssignment(assignment, headerFingerprint, originalMetadata);
        ArgumentNullException.ThrowIfNull(requirements);
        requirements.Validate();
        return ProcessAcceptedSourceCodec.Encode(new ProcessCapturedExecutionInput(assignment, requirements),
            InputDomain(RequirementsDomain, headerFingerprint, originalMetadata), MaximumPayloadUtf8Bytes);
    }

    internal static ProcessCapturedExecutionInput Read(ProcessAcceptedExecutionInputVersion version,
        ProcessAcceptedSourceEnvelope input, string headerFingerprint, ProcessAcceptedSourceEnvelope originalMetadata) {
        var decoded = version switch {
            ProcessAcceptedExecutionInputVersion.OriginalAssignment => new(
                ProcessAcceptedSourceCodec.Decode<ProcessRuntimeStepAssignment>(input,
                    InputDomain(AssignmentDomain, headerFingerprint, originalMetadata), MaximumPayloadUtf8Bytes), null),
            ProcessAcceptedExecutionInputVersion.OriginalStepRequirements => ProcessAcceptedSourceCodec.Decode<ProcessCapturedExecutionInput>(
                input, InputDomain(RequirementsDomain, headerFingerprint, originalMetadata), MaximumPayloadUtf8Bytes),
            _ => throw new NotSupportedException("The original Process execution input version is unsupported.")
        };
        RequireAssignment(decoded.Assignment, headerFingerprint, originalMetadata);
        if (version == ProcessAcceptedExecutionInputVersion.OriginalStepRequirements) {
            if (decoded.Requirements is null) {
                throw new InvalidDataException("The original Process execution input is missing its declared step requirements.");
            }
            decoded.Requirements.Validate();
        }
        return decoded;
    }

    private static void RequireAssignment(ProcessRuntimeStepAssignment assignment, string headerFingerprint,
        ProcessAcceptedSourceEnvelope originalMetadata) {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(originalMetadata);
        if (assignment.DecisionDataPolicy?.ExecutionContract is null || assignment.Prompt is null ||
            assignment.RoleKey is null || assignment.RoleResourceKey is null || assignment.RoleDisplayName is null ||
            assignment.ExecutorDisplayName is null || assignment.AssignmentReason is null ||
            assignment.CreatedAtUtc == default || assignment.CreatedAtUtc.Offset != TimeSpan.Zero ||
            assignment.ProducedArtifactSlotIds is null || assignment.RequiredArtifactSlotIds is null ||
            assignment.AllowedOperations is null || assignment.LaunchVariables is null ||
            assignment.ProducedArtifactSlotIds.Count > MaximumCollectionCount ||
            assignment.RequiredArtifactSlotIds.Count > MaximumCollectionCount ||
            assignment.AllowedOperations.Count > MaximumCollectionCount || assignment.LaunchVariables.Count > MaximumCollectionCount ||
            assignment.ProducedArtifactSlotIds.Any(id => id.Value == Guid.Empty) ||
            assignment.RequiredArtifactSlotIds.Any(id => id.Value == Guid.Empty) ||
            assignment.BranchGate is { } gate && (string.IsNullOrWhiteSpace(gate.SourceStepKey) || string.IsNullOrWhiteSpace(gate.RequiredOutcomeKey))) {
            throw new InvalidDataException("The original Process execution input has missing or unbounded assignment data.");
        }
        if (ProcessAcceptedSourceCodec.EncodeAssignment(ProcessAssignmentSource.From(assignment), headerFingerprint) != originalMetadata) {
            throw new InvalidDataException("The original Process execution input differs from its accepted assignment metadata.");
        }
        assignment.DecisionDataPolicy.RequireBinding(assignment.DecisionDataPolicy.DefinitionVersionId, assignment.StepInstanceId);
    }

    private static string InputDomain(string domain, string headerFingerprint, ProcessAcceptedSourceEnvelope metadata)
        => domain + headerFingerprint + "\n" + metadata.Fingerprint + "\n";
}
