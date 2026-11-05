using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.CrmHr;

internal static class AiStewardEditValues {
    internal static string ScopeKey(Guid partyId) => $"crmhr:ai-steward:{partyId:N}";
    internal static AiStewardProfileObservation Observe(Guid partyId, PartyType type, AiAgentProfile? profile) => new(
        partyId, type, profile?.Id, profile?.OwnerPartyId, profile?.ValidationStatus ?? AiValidationStatus.Draft,
        profile?.LastReviewedAtUtc, profile?.Notes ?? string.Empty);
    internal static AiStewardProfileEditorModel Editor(Guid partyId, PartyType type, AiAgentProfile? profile) => new() {
        Id = profile?.Id, PartyId = partyId, OwnerPartyId = profile?.OwnerPartyId,
        ValidationStatus = profile?.ValidationStatus ?? AiValidationStatus.Draft,
        LastReviewedOn = profile?.LastReviewedAtUtc is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : null,
        Notes = profile?.Notes ?? string.Empty, Original = Observe(partyId, type, profile)
    };
    internal static DateTimeOffset? UtcDate(DateOnly? date) => date is { } value
        ? new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)) : null;
    internal static Error Conflict() => Error.Validation("The business profile changed. Read its current stewardship values before replacing it.", AiStewardEditingErrors.Conflict);
    internal static bool IsConflict(Exception exception) => exception is DbUpdateConcurrencyException ||
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation } ||
        exception is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.UniqueViolation } };
}
