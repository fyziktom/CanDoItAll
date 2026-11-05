using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.AspNetCore.DataProtection;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class ProviderMutationWitness(IDataProtectionProvider protection, ICanonicalRuntimeDatabase database) {
    private const string Purpose = "CanDoItAll.ProviderMutationWitness.v1";
    private const int MaximumWitnessCharacters = 16 * 1024;
    private readonly IDataProtector protector = protection.CreateProtector(Purpose);

    public ProviderMutationAttempt Sign(ProviderMutationAttempt attempt) => attempt with {
        Witness = protector.Protect(JsonSerializer.Serialize(Claim(attempt)))
    };

    public bool IsTrusted(ProviderMutationAttempt attempt) {
        if (string.IsNullOrWhiteSpace(attempt.Witness) || attempt.Witness.Length > MaximumWitnessCharacters) {
            return false;
        }
        try {
            return JsonSerializer.Deserialize<NativeClaim>(protector.Unprotect(attempt.Witness)) == Claim(attempt);
        } catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException) {
            return false;
        }
    }

    private NativeClaim Claim(ProviderMutationAttempt attempt) => new(database.Profile.Profile.Id,
        database.Generation, database.Profile.Profile.Runtime.Fingerprint, attempt.AttemptId, attempt.ProviderId,
        attempt.Kind, attempt.ExpectedConcurrencyToken, attempt.IntendedConcurrencyToken, attempt.SubmissionFingerprint);

    private sealed record NativeClaim(Guid DatabaseProfileId, long Generation, string? DatabaseFingerprint,
        Guid AttemptId, Guid ProviderId, ProviderMutationKind Kind, Guid? Expected, Guid? Intended, string? SubmissionFingerprint);
}
