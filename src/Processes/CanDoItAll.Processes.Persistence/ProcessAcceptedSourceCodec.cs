using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessAcceptedSourceCodec {
    internal const int MaximumPayloadUtf8Bytes = 1_048_576;
    internal const int MaximumGovernedProjectionUtf8Bytes = 16_777_216;
    private const string HeaderDomain = "process-accepted-source-header/v1\n";
    private const string AssignmentDomain = "process-accepted-source-assignment/v1\n";
    private static readonly JsonSerializerOptions Options = new(ProcessInstancePlanPersistenceMapper.CreateSerializerOptions()) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static ProcessAcceptedSourceProjection Capture(ProcessPreparedLaunchSnapshot saved) {
        ArgumentNullException.ThrowIfNull(saved);
        var header = OriginalHeader(saved);
        var assignments = saved.Preparation.InitialCommit.InitialAssignments
            ?? throw Invalid("The accepted source requires its original assignments.");
        var encodedHeader = Encode(header, HeaderDomain);
        var governed = assignments.Any(item => item.DecisionDataPolicy is not null);
        var requirements = assignments.Any(item => item.DecisionDataPolicy?.ExecutionContract is not null)
            ? new ProcessAcceptedStepRequirementsCapture(saved.Preparation.InitialCommit) : null;
        var totalBytes = 0;
        RequireCapacity(encodedHeader);
        Dictionary<ProcessStepInstanceId, ProcessAcceptedSourceEnvelope> encodedAssignments = [];
        Dictionary<ProcessStepInstanceId, ProcessAcceptedSourceEnvelope> inputs = [];
        foreach (var assignment in assignments) {
            var source = ProcessAssignmentSource.From(assignment);
            RequireAssignment(header, source);
            var metadata = EncodeAssignment(source, encodedHeader.Fingerprint);
            RequireCapacity(metadata);
            if (!encodedAssignments.TryAdd(source.StepInstanceId, metadata)) {
                throw Invalid("The accepted source repeats an original step assignment.");
            }
            if (assignment.DecisionDataPolicy?.ExecutionContract is not null) {
                var input = ProcessAcceptedExecutionInputCodec.Capture(assignment, requirements!.Capture(assignment), encodedHeader.Fingerprint, metadata);
                RequireCapacity(input);
                inputs.Add(source.StepInstanceId, input);
            }
        }
        return new(encodedHeader, encodedAssignments, inputs);

        void RequireCapacity(ProcessAcceptedSourceEnvelope envelope) {
            if (!governed) {
                return;
            }
            var bytes = Encoding.UTF8.GetByteCount(envelope.PayloadJson);
            if (bytes > MaximumGovernedProjectionUtf8Bytes - totalBytes) {
                throw Invalid("The accepted governed Process source exceeds its total UTF-8 byte limit.");
            }
            totalBytes += bytes;
        }
    }

    internal static ProcessAcceptedSource ReadLegacy(ProcessPreparedLaunchSnapshot saved, ProcessStepInstanceId stepId) {
        ArgumentNullException.ThrowIfNull(saved);
        var header = OriginalHeader(saved);
        var original = saved.Preparation.InitialCommit.InitialAssignments?.SingleOrDefault(item => item.StepInstanceId == stepId)
            ?? throw Invalid("The legacy accepted source does not contain the selected original assignment.");
        var assignment = ProcessAssignmentSource.From(original);
        RequireAssignment(header, assignment);
        return new(header, assignment);
    }

    internal static ProcessAcceptedSource Read(ProcessAcceptedSourceVersion version, ProcessLaunchAdmissionId admissionId,
        ProcessStepInstanceId stepId, string preparationFingerprint, ProcessAcceptedSourceEnvelope header,
        ProcessAcceptedSourceEnvelope assignment) {
        var decodedHeader = ReadHeader(version, admissionId, preparationFingerprint, header);
        var decodedAssignment = Decode<ProcessAssignmentSource>(assignment, AssignmentDomain + header.Fingerprint + "\n");
        RequireAssignment(decodedHeader, decodedAssignment);
        if (decodedAssignment.StepInstanceId != stepId) {
            throw Invalid("The accepted source assignment differs from the selected step.");
        }
        return new(decodedHeader, decodedAssignment);
    }

    internal static ProcessAcceptedSourceHeader ReadHeader(ProcessAcceptedSourceVersion version, ProcessLaunchAdmissionId admissionId,
        string preparationFingerprint, ProcessAcceptedSourceEnvelope header) {
        if (version != ProcessAcceptedSourceVersion.OriginalProjection) {
            throw new NotSupportedException("The accepted source projection version is unsupported.");
        }
        var decoded = Decode<ProcessAcceptedSourceHeader>(header, HeaderDomain);
        RequireHeader(decoded);
        if (decoded.AdmissionId != admissionId || decoded.PreparationFingerprint != preparationFingerprint) {
            throw Invalid("The accepted source header differs from its original admission.");
        }
        return decoded;
    }

    internal static ProcessAcceptedSourceEnvelope EncodeAssignment(ProcessAssignmentSource assignment, string headerFingerprint)
        => Encode(assignment, AssignmentDomain + headerFingerprint + "\n");

    internal static ProcessAcceptedSourceEnvelope Encode<T>(T value, string domain, int maximumBytes = MaximumPayloadUtf8Bytes) {
        var payload = JsonSerializer.Serialize(value, Options);
        RequirePayload(payload, maximumBytes);
        return new(payload, Hash(domain, payload));
    }

    internal static T Decode<T>(ProcessAcceptedSourceEnvelope envelope, string domain, int maximumBytes = MaximumPayloadUtf8Bytes) {
        ArgumentNullException.ThrowIfNull(envelope);
        RequirePayload(envelope.PayloadJson, maximumBytes);
        if (!IsDigest(envelope.Fingerprint) || Hash(domain, envelope.PayloadJson) != envelope.Fingerprint) {
            throw Invalid("The accepted source evidence has an inconsistent fingerprint.");
        }
        var value = JsonSerializer.Deserialize<T>(envelope.PayloadJson, Options)
            ?? throw Invalid("The accepted source evidence is empty.");
        if (JsonSerializer.Serialize(value, Options) != envelope.PayloadJson) {
            throw Invalid("The accepted source evidence is not canonical.");
        }
        return value;
    }

    private static void RequireHeader(ProcessAcceptedSourceHeader header) {
        if (header.AdmissionId.Value == Guid.Empty || header.RunId.Value == Guid.Empty || header.RootRunId.Value == Guid.Empty ||
            header.PlanId.Value == Guid.Empty || header.DefinitionVersionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(header.PlanHash) ||
            !IsDigest(header.PreparationFingerprint)) {
            throw Invalid("The accepted source header has an incomplete original identity.");
        }
        header.Authority?.Validate();
        if (header.Authority is not null && header.Authority.ProjectAdmission != header.ProjectAdmission) {
            throw Invalid("The accepted source authority differs from its original project lifetime.");
        }
    }

    private static ProcessAcceptedSourceHeader OriginalHeader(ProcessPreparedLaunchSnapshot saved) {
        var preparation = saved.Preparation;
        var initial = preparation.InitialCommit;
        var plan = initial.InitialPlan ?? throw Invalid("The accepted source requires its original plan.");
        var state = initial.Mutation.State;
        ProcessAcceptedSourceHeader header = new(preparation.AdmissionId, state.RunId, state.RootRunId, state.PlanId,
            state.PlanHash, plan.Definition.VersionId, state.ProjectAdmission, preparation.Authority, saved.PreparationFingerprint);
        RequireHeader(header);
        if (state.LaunchAdmissionId != preparation.AdmissionId || state.PlanId != plan.Header.PlanId || state.PlanHash != plan.PlanHash) {
            throw Invalid("The accepted source plan differs from its original admission.");
        }
        return header;
    }

    private static void RequireAssignment(ProcessAcceptedSourceHeader header, ProcessAssignmentSource assignment) {
        if (assignment.RunId != header.RunId || assignment.PlanId != header.PlanId || assignment.StepInstanceId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(assignment.StepKey) || assignment.ExecutorKind is null || assignment.ExecutorId is null ||
            assignment.ReadinessHash is null || assignment.AllowedOperations is null || assignment.OperationTargetScope is null ||
            assignment.LaunchVariables is null || assignment.CapabilityScope is null ||
            assignment.AllowedOperations.Any(string.IsNullOrWhiteSpace) ||
            assignment.LaunchVariables.Any(item => string.IsNullOrEmpty(item.Key) || item.Value is null)) {
            throw Invalid("The accepted source assignment has an inconsistent original identity or contract.");
        }
        assignment.DecisionDataPolicy?.RequireBinding(header.DefinitionVersionId, assignment.StepInstanceId);
    }

    private static void RequirePayload(string payload, int maximumBytes) {
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > maximumBytes || Encoding.UTF8.GetByteCount(payload) > maximumBytes) {
            throw Invalid($"The accepted Process payload is empty or exceeds its {maximumBytes}-byte UTF-8 limit.");
        }
    }

    private static bool IsDigest(string value)
        => value is { Length: 71 } && value.StartsWith("sha256:", StringComparison.Ordinal) &&
            value.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;

    private static string Hash(string domain, string payload)
        => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(domain + payload)));

    private static InvalidDataException Invalid(string message) => new(message);
}
