using System.Security.Cryptography;
using System.Text.Json;
using EditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum ProviderMutationKind { Create, Update, Delete, HealthPersistence, ModelMaintenancePersistence }
public enum ProviderVerificationDisposition { Committed, DefinitelyNotCommitted, StillUnconfirmed }

public sealed record ProviderMutationAttempt(
    Guid AttemptId,
    Guid ProviderId,
    ProviderMutationKind Kind,
    Guid? ExpectedConcurrencyToken,
    Guid? IntendedConcurrencyToken = null,
    string? SubmissionFingerprint = null) {
    public bool IsCreate => Kind == ProviderMutationKind.Create;

    public static ProviderMutationAttempt Capture(EditorModel request, Guid providerId, ProviderMutationKind kind) =>
        new(Guid.NewGuid(), providerId, kind, request.ExpectedConcurrencyToken,
            SubmissionFingerprint: Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request))));
}

public sealed record ProviderMutationVerification(
    ProviderVerificationDisposition Disposition,
    Guid ProviderId,
    Guid? ConcurrencyToken = null);

public interface IProviderMutationVerification {
    Task<ProviderMutationVerification> VerifyAsync(ProviderMutationAttempt attempt, CancellationToken cancellationToken = default);
}
