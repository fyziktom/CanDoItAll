using System.Security.Cryptography;
using System.Text.Json;
using EditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

/// <summary>
/// Kind of provider-profile write recorded in an unconfirmed-write receipt, as a JSON integer: 0 Create (a new
/// profile was being saved), 1 Update (an existing profile was being saved), 2 Delete (the profile was being deleted),
/// 3 HealthPersistence (the health result of a provider test was being stored on the profile),
/// 4 ModelMaintenancePersistence (the profile was being updated after an Ollama model maintenance request).
/// </summary>
public enum ProviderMutationKind { Create, Update, Delete, HealthPersistence, ModelMaintenancePersistence }

/// <summary>
/// Result of checking an unconfirmed provider-profile write against the stored profile, written as the text name of
/// the member. Committed: the stored profile shows that the write took effect (a created profile exists, a deleted one
/// is gone, or an update's intended concurrency token is stored). DefinitelyNotCommitted: the stored profile shows that
/// it did not (a created profile does not exist, or the token the write expected is still stored). StillUnconfirmed:
/// the stored state proves neither, or the profile could not be read; check again later and do not repeat the write
/// blindly.
/// </summary>
public enum ProviderVerificationDisposition { Committed, DefinitelyNotCommitted, StillUnconfirmed }

/// <summary>
/// Receipt of a provider-profile write whose outcome is unknown. The server returns it as the <c>attempt</c> member of
/// the HTTP 409 unconfirmed-write response (code <c>agents.provider-write-unconfirmed</c>); send it back unchanged to
/// <c>POST /api/agents/providers/mutations/verify</c> to find out whether the write took effect. Clients never build
/// or edit a receipt.
/// </summary>
/// <param name="AttemptId">
/// Server-generated identifier of the write attempt. Must not be the all-zero GUID.
/// </param>
/// <param name="ProviderId">
/// Identifier of the provider profile the write targeted, including the identifier assigned to a profile being
/// created. Must not be the all-zero GUID.
/// </param>
/// <param name="Kind">
/// Kind of write, as a JSON integer: 0 Create, 1 Update, 2 Delete, 3 HealthPersistence (storing a provider test's
/// health result), 4 ModelMaintenancePersistence (storing the profile after an Ollama model maintenance request).
/// Another value is rejected with HTTP 400.
/// </param>
/// <param name="ExpectedConcurrencyToken">
/// Concurrency token the write expected the stored profile to have, as sent in the editor's
/// <c>expectedConcurrencyToken</c>; null when the write expected none.
/// </param>
/// <param name="IntendedConcurrencyToken">
/// Concurrency token the write would have stored, when the server recorded it; null otherwise.
/// </param>
/// <param name="SubmissionFingerprint">
/// Server-computed fingerprint (hexadecimal SHA-256) of the submitted editor body, for correlating the attempt with
/// the request; null when not recorded. Verification does not use it.
/// </param>
public sealed record ProviderMutationAttempt(
    Guid AttemptId,
    Guid ProviderId,
    ProviderMutationKind Kind,
    Guid? ExpectedConcurrencyToken,
    Guid? IntendedConcurrencyToken = null,
    string? SubmissionFingerprint = null) {
    /// <summary>
    /// True when <c>kind</c> is 0 Create. Computed by the server; ignored when the receipt is sent back.
    /// </summary>
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
