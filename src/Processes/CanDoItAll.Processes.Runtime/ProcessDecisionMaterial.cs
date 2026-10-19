using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public readonly record struct ProcessDecisionMaterialScopeId(Guid Value);

public sealed record ProcessDecisionMaterialScope(ProcessDecisionMaterialScopeId Id, Guid DatabaseProfileId,
    Guid WorkspaceNamespaceId, ProcessRunId RootRunId, ProcessRunId OwnerRunId, ProcessStepInstanceId? StepInstanceId) {
    public ReadBasisMode ReadBasisMode => StepInstanceId is null ? ReadBasisMode.RunSnapshot : ReadBasisMode.StepSnapshot;

    public void Validate() {
        if (Id.Value == Guid.Empty || DatabaseProfileId == Guid.Empty || WorkspaceNamespaceId == Guid.Empty ||
            RootRunId.Value == Guid.Empty || OwnerRunId.Value == Guid.Empty || StepInstanceId is { Value: var step } && step == Guid.Empty) {
            throw new InvalidDataException("A Process material scope requires its original profile, namespace and runtime owner.");
        }
    }
}

public sealed record ProcessDecisionMaterialReference(ProcessDecisionMaterialScopeId ScopeId, int Generation, ProcessDecisionMaterialId MaterialId) {
    public void Validate() {
        if (ScopeId.Value == Guid.Empty || MaterialId.Value == Guid.Empty || Generation is < 1 or > ProcessDecisionGeneration.MaximumGenerations) {
            throw new InvalidDataException("A Process material reference requires its original scope, bounded generation and material identity.");
        }
    }
}

public sealed class ProcessDecisionMaterial {
    public const int MaximumPayloadUtf8Bytes = 1_048_576;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static ReadOnlySpan<byte> FingerprintDomain => "process-decision-material/v1"u8;

    public ProcessDecisionMaterial(ProcessDecisionMaterialScope scope, ProcessDecisionMaterialReference reference,
        ProcessDecisionExecutionContract executionContract, DateTimeOffset capturedAtUtc, string payloadJson) {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(executionContract);
        ArgumentNullException.ThrowIfNull(payloadJson);
        scope.Validate();
        reference.Validate();
        executionContract.Validate();
        if (scope.Id != reference.ScopeId || capturedAtUtc == default || capturedAtUtc.Offset != TimeSpan.Zero ||
            payloadJson.Length > MaximumPayloadUtf8Bytes) {
            throw new InvalidDataException("Process material requires its original scope, UTC capture and bounded payload.");
        }
        byte[] payload;
        try {
            payload = Utf8.GetBytes(payloadJson);
        } catch (EncoderFallbackException failure) {
            throw new InvalidDataException("Process material contains invalid Unicode.", failure);
        }
        if (payload.Length > MaximumPayloadUtf8Bytes) {
            throw new InvalidDataException("Process material exceeds its encoded payload bound.");
        }
        using var document = JsonDocument.Parse(payload);
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw new InvalidDataException("Process material requires an owner-encoded JSON object.");
        }
        Scope = scope;
        Reference = reference;
        ExecutionContract = executionContract;
        CapturedAtUtc = capturedAtUtc;
        PayloadJson = payloadJson;
        PayloadUtf8Bytes = payload.Length;
        Fingerprint = ComputeFingerprint(payload);
    }

    public ProcessDecisionMaterialScope Scope { get; }
    public ProcessDecisionMaterialReference Reference { get; }
    public ProcessDecisionExecutionContract ExecutionContract { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public string PayloadJson { get; }
    public int PayloadUtf8Bytes { get; }
    public string Fingerprint { get; }

    public void RequireOriginal(ProcessDecisionMaterial observed) {
        ArgumentNullException.ThrowIfNull(observed);
        if (Scope != observed.Scope || Reference != observed.Reference || ExecutionContract != observed.ExecutionContract ||
            CapturedAtUtc != observed.CapturedAtUtc || PayloadJson != observed.PayloadJson) {
            throw new InvalidDataException("The observed Process material differs from the original inherited snapshot.");
        }
    }

    public void RequireFresh(DateTimeOffset observedAtUtc, TimeSpan maximumAge) {
        if (observedAtUtc.Offset != TimeSpan.Zero || maximumAge <= TimeSpan.Zero || observedAtUtc < CapturedAtUtc) {
            throw new InvalidDataException("Process material freshness requires a current UTC observation after its original capture and a positive age bound.");
        }
        if (observedAtUtc - CapturedAtUtc > maximumAge) {
            throw new ProcessDecisionMaterialExpiredException(Reference);
        }
    }

    private string ComputeFingerprint(ReadOnlySpan<byte> payload) {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(FingerprintDomain);
        AppendGuid(Scope.Id.Value);
        AppendGuid(Scope.DatabaseProfileId);
        AppendGuid(Scope.WorkspaceNamespaceId);
        AppendGuid(Scope.RootRunId.Value);
        AppendGuid(Scope.OwnerRunId.Value);
        AppendGuid(Scope.StepInstanceId?.Value ?? Guid.Empty);
        AppendInteger(Reference.Generation);
        AppendGuid(Reference.MaterialId.Value);
        AppendGuid(ExecutionContract.ContractId);
        AppendInteger(ExecutionContract.Version);
        AppendInteger((int)ExecutionContract.Executor);
        AppendInteger(CapturedAtUtc.UtcTicks);
        AppendInteger(payload.Length);
        hash.AppendData(payload);
        return Convert.ToHexString(hash.GetHashAndReset());

        void AppendGuid(Guid value) {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes, bigEndian: true, out _);
            hash.AppendData(bytes);
        }

        void AppendInteger(long value) {
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bytes, value);
            hash.AppendData(bytes);
        }
    }
}

public sealed class ProcessDecisionMaterialExpiredException(ProcessDecisionMaterialReference reference)
    : InvalidOperationException("The inherited Process snapshot exceeded its original maximum age.") {
    public ProcessDecisionMaterialReference Reference { get; } = reference;
}
