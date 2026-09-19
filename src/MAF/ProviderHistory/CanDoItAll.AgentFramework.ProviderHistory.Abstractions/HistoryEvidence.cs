namespace CanDoItAll.AgentFramework.ProviderHistory;

public enum HistorySourceKind { AgentConversation, SimpleChat, Workflow, Process, BatchItem, SharedRelay }
public enum HistoryOwnerRole { PrimaryEvidence, ContentOwner, Lineage }
public enum HistoryOwnerState { PendingCanonical, Linked, Unavailable, Deleted }
public enum HistoryGranularity { ProviderCallAttempt, LegacyAggregate }
public enum HistoryTimeBasis { AttemptStarted, CanonicalRecorded }
public enum HistoryMetadataAuthority { Standalone, CanonicalProjection }
public enum HistoryRetentionAuthority { HistoryPolicy, CanonicalOwner }
public enum HistoryOutcome { Started, Succeeded, Failed, Cancelled, TimedOut, Interrupted, Unknown }
public enum HistoryUsageState { Unavailable, Partial, Complete }
public enum HistoryPriceState { Unpriced, ProviderReported, CalculatedAtExecution, ExplicitFree, PartialEstimate, MissingTariff, MissingUsage, UnsupportedUnit, InvalidEvidence }
/// <summary>
/// How the caller of a recorded operation authenticated, as a JSON integer: 0 Unknown (not recorded, for example work
/// started by a background owner), 1 TrustedLocalOperator (the local operator of this host), 2 ManagedCredential (a
/// managed API credential issued by this host), 3 LegacyAuthenticated (a valid bearer token that is not a managed
/// credential), 4 AuthenticationDisabled (API authorization was disabled for the request).
/// </summary>
public enum HistoryAuthenticationKind { Unknown, TrustedLocalOperator, ManagedCredential, LegacyAuthenticated, AuthenticationDisabled }
public enum HistoryOperation { CompleteChat, AnalyzeImage, GenerateImage, EditImage, TranscribeSpeech, SynthesizeSpeech, ListModels, TestHealth, CreateOrUpdateModel }
public enum HistoryWorkload { Direct, Agent, SimpleChat, Workflow, Process, Batch, SharedRelay, Diagnostic }
public enum HistoryCaptureMode { Light, Detailed }
public enum HistoryDetailState { NotCaptured, PendingCanonical, Canonical, Captured, UnsupportedDetailShape, QuotaExceeded, Expired, Unavailable, Deleted, ProtectionUnavailable }
public enum HistoryCoverageState { Pending, Partial, Current, Failed }
public enum HistoryPermission { ReadMetadata, ReadContent, Manage }
public enum HistoryFailure { Denied, InvalidQuery, StaleContext, InvalidCursor, Unavailable, TimedOut, Conflict }

[Flags]
public enum HistoryDetailFlags { None = 0, Truncated = 1, Redacted = 2, PriorContextNotCaptured = 4 }

/// <summary>
/// Caller recorded for an operation: how it authenticated and, for a managed API credential, which one. It identifies
/// the caller for audit and grants nothing.
/// </summary>
/// <param name="Kind">
/// How the caller authenticated, as a JSON integer: 0 Unknown, 1 TrustedLocalOperator, 2 ManagedCredential, 3
/// LegacyAuthenticated, 4 AuthenticationDisabled.
/// </param>
/// <param name="CredentialId">Managed API credential that authenticated the caller; null for every other kind.</param>
/// <param name="Issuer">Issuer of the caller's credential or token; null when unknown.</param>
/// <param name="Subject">
/// Subject of the caller's token; <c>api-authorization-disabled</c> when API authorization was disabled for the
/// request; null when unknown.
/// </param>
/// <param name="DisplayName">
/// Display name of the managed credential, or <c>Local operator</c> for the local operator; null otherwise.
/// </param>
public sealed record HistoryCaller(
    HistoryAuthenticationKind Kind,
    ManagedCredentialId? CredentialId = null,
    string? Issuer = null,
    string? Subject = null,
    string? DisplayName = null);

public sealed record HistoryProvider(
    ProviderIdentity? Id, string Name, string Kind,
    ProviderModelIdentity? RequestedModel, ProviderModelIdentity? ResolvedModel);

public sealed record HistoryUsage(
    HistoryUsageState State, long? InputTokens = null, long? OutputTokens = null,
    long? CachedInputTokens = null, long? CacheWriteTokens = null,
    long? ReasoningTokens = null, int? ImageCount = null);

public sealed record HistoryPrice(
    HistoryPriceState State, decimal? Amount = null, string? Currency = null,
    string? ProfileHash = null, string? Version = null) {
    public string? SourceRevision { get; init; }
}

public sealed record HistoryCoverage(
    HistoryCoverageState State, DateTimeOffset? IndexedThroughUtc, string? FailureCode = null);

public sealed record HistoryExternalReference
{
    public const string LocalProjectType = "local-project";
    public const int MaximumValueLength = 256;
    public const int MaximumTypeLength = 64;

    public HistoryExternalReference(string value, string? type = null)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(
                $"An external reference must be an exact non-control value from 1 to {MaximumValueLength} characters.",
                nameof(value));
        }
        if (type is not null && !IsValidType(type))
        {
            throw new ArgumentException(
                $"An external reference type must be a canonical lowercase ASCII token from 1 to {MaximumTypeLength} characters.",
                nameof(type));
        }

        Value = value;
        Type = type;
    }

    public string Value { get; }

    public string? Type { get; }

    public static bool TryCreate(
        string? value,
        string? type,
        out HistoryExternalReference? reference)
    {
        if (!IsValidValue(value) || type is not null && !IsValidType(type))
        {
            reference = null;
            return false;
        }

        reference = new HistoryExternalReference(value!, type);
        return true;
    }

    private static bool IsValidValue(string? value)
        => value is { Length: > 0 and <= MaximumValueLength } &&
            value == value.Trim() &&
            !value.Any(char.IsControl);

    private static bool IsValidType(string? value)
        => value is { Length: > 0 and <= MaximumTypeLength } &&
            value.All(character =>
                character is >= 'a' and <= 'z' ||
                char.IsAsciiDigit(character) ||
                character is '.' or '_' or '-');
}

public sealed class ProviderHistoryException(HistoryFailure failure, string message, Exception? innerException = null) : Exception(message, innerException) {
    public HistoryFailure Failure { get; } = failure;
}
