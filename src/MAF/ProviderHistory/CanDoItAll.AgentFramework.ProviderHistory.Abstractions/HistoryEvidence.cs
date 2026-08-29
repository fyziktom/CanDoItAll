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

public sealed class ProviderHistoryException(HistoryFailure failure, string message, Exception? innerException = null) : Exception(message, innerException) {
    public HistoryFailure Failure { get; } = failure;
}
