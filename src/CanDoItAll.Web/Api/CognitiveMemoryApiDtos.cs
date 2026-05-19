using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal sealed record CognitiveMemoryStatusApiResponse(
    Guid ActiveProfileId,
    string DisplayName,
    DatabaseProviderKind ProviderKind,
    string ProviderKindName,
    DatabaseProfileSourceKind SourceKind,
    string SourceKindName,
    string Fingerprint,
    string WorkspaceRoot,
    bool IsPostgreSql,
    string Descriptor,
    string ContractVersion,
    string ContractPath,
    IReadOnlyList<string> Routes)
{
    public static CognitiveMemoryStatusApiResponse From(
        ResolvedDatabaseProfile resolvedProfile,
        CognitiveMemoryApiContractResponse contract)
    {
        var profile = resolvedProfile.Profile;
        return new CognitiveMemoryStatusApiResponse(
            profile.Id,
            profile.DisplayName,
            profile.ProviderKind,
            profile.ProviderKind.ToString(),
            profile.SourceKind,
            profile.SourceKind.ToString(),
            profile.Runtime.Fingerprint,
            profile.Storage.WorkspaceRoot,
            profile.ProviderKind == DatabaseProviderKind.PostgreSql,
            BuildDescriptor(profile),
            contract.Version,
            $"{contract.BasePath}/contract",
            contract.Routes.Select(route => $"{route.Method} {route.Path}").ToArray());
    }

    public static string BuildDescriptor(DatabaseProfileRecord profile)
    {
        return profile.ProviderKind switch
        {
            DatabaseProviderKind.PostgreSql when profile.PostgreSql is not null =>
                $"{profile.PostgreSql.Host}:{profile.PostgreSql.Port}/{profile.PostgreSql.DatabaseName}",
            DatabaseProviderKind.Sqlite when profile.Sqlite is not null =>
                profile.Sqlite.DatabasePath,
            DatabaseProviderKind.InMemory when profile.InMemory is not null =>
                profile.InMemory.DatabaseName,
            _ => profile.ProviderKind.ToString()
        };
    }
}

internal sealed record CognitiveMemoryDatabaseProfileApiResponse(
    Guid Id,
    string DisplayName,
    DatabaseProviderKind ProviderKind,
    string ProviderKindName,
    DatabaseProfileSourceKind SourceKind,
    string SourceKindName,
    string Fingerprint,
    string WorkspaceRoot,
    string Descriptor,
    string ConnectionString,
    bool IsPostgreSql)
{
    public static CognitiveMemoryDatabaseProfileApiResponse From(ResolvedDatabaseProfile resolvedProfile)
    {
        var profile = resolvedProfile.Profile;
        return new CognitiveMemoryDatabaseProfileApiResponse(
            profile.Id,
            profile.DisplayName,
            profile.ProviderKind,
            profile.ProviderKind.ToString(),
            profile.SourceKind,
            profile.SourceKind.ToString(),
            profile.Runtime.Fingerprint,
            profile.Storage.WorkspaceRoot,
            CognitiveMemoryStatusApiResponse.BuildDescriptor(profile),
            resolvedProfile.ConnectionString,
            profile.ProviderKind == DatabaseProviderKind.PostgreSql);
    }
}

internal sealed record CognitiveMemoryPostgreSqlDatabaseProfileApiResponse(
    CognitiveMemoryDatabaseProfileApiResponse Profile,
    CognitiveMemoryDatabaseSwitchSummaryApiResponse? Switch);

internal sealed record CognitiveMemoryDatabaseSwitchSummaryApiResponse(
    Guid PreviousProfileId,
    Guid CurrentProfileId,
    long Generation,
    int ProcessId)
{
    public static CognitiveMemoryDatabaseSwitchSummaryApiResponse From(DatabaseSwitchResult result)
    {
        return new CognitiveMemoryDatabaseSwitchSummaryApiResponse(
            result.PreviousProfileId,
            result.CurrentProfileId,
            result.Generation,
            result.ProcessId);
    }
}

internal sealed record CognitiveMemoryDatabaseSwitchApiResponse(
    Guid PreviousProfileId,
    Guid CurrentProfileId,
    long Generation,
    int ProcessId,
    CognitiveMemoryDatabaseProfileApiResponse Profile);

internal sealed class CognitiveMemorySnapshotApiQuery
{
    public Guid? ProjectId { get; set; }

    public int? Take { get; set; }

    public bool? IncludeResolvedReviewItems { get; set; }
}

internal sealed class CognitiveMemoryPostgreSqlDatabaseProfileApiRequest
{
    public string? DisplayName { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? DatabaseName { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? AdminDatabaseName { get; set; }

    public bool? TrustServerCertificate { get; set; }

    public string? WorkspaceRoot { get; set; }

    public bool? Activate { get; set; }
}

internal sealed class CognitiveMemoryAutomationSettingsApiRequest
{
    public string? ScheduleMode { get; set; }

    public string NightlyLocalTime { get; set; } = "02:00";

    public int? IdleMinutes { get; set; } = 30;

    public IReadOnlyList<string>? ScheduledLocalTimes { get; set; }

    public bool AutoIngestProjectStructure { get; set; } = true;

    public bool AutoIngestProcessRuntime { get; set; } = true;

    public bool AutoConsolidateAfterIngestion { get; set; } = true;

    public string? ModelAccessMode { get; set; }

    public Guid? DefaultProviderProfileId { get; set; }

    public Guid? DefaultAgentId { get; set; }

    public IReadOnlyList<Guid>? AllowedProviderProfileIds { get; set; }

    public IReadOnlyList<CognitiveMemoryModelExecutionProfile>? ModelExecutionProfiles { get; set; }

    public string? ActorId { get; set; }
}

internal sealed class CognitiveMemoryProjectionRebuildApiRequest
{
    public Guid? ProjectId { get; set; }

    public int? Take { get; set; }

    public string? ActorId { get; set; }

    public string? CollectionName { get; set; }

    public bool ProjectMissingRecords { get; set; }

    public string? ProjectionProfileId { get; set; }

    public string? EmbeddingProfileId { get; set; }

    public string? TargetProviderName { get; set; }

    public string? ProjectionStoreKind { get; set; }

    public int? VectorDimensions { get; set; }
}

internal sealed class CognitiveMemoryAutomationRunApiRequest
{
    public Guid? ProjectId { get; set; }

    public string? TriggerKind { get; set; }

    public string? ActorId { get; set; }

    public int? Take { get; set; }
}

internal sealed class CognitiveMemoryRetentionCleanupApiRequest
{
    public Guid? ProjectId { get; set; }

    public DateTimeOffset DeleteBeforeUtc { get; set; }

    public bool? DryRun { get; set; } = true;

    public IReadOnlyList<string>? Scopes { get; set; }

    public string? ActorId { get; set; }
}

internal sealed class CognitiveMemoryManualSourceIngestApiRequest
{
    public Guid? ScopeId { get; set; }

    public Guid? ProjectId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? Cursor { get; set; }

    public int? Take { get; set; }
}

internal sealed class CognitiveMemoryExternalFileUploadApiRequest
{
    public IFormFile? File { get; set; }

    public Guid? ProjectId { get; set; }

    public string? ActorId { get; set; }

    public string? IdempotencyKey { get; set; }
}

internal sealed class CognitiveMemoryExternalWebLinkApiRequest
{
    public string Url { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }

    public string? ActorId { get; set; }

    public string? IdempotencyKey { get; set; }
}

internal sealed class CognitiveMemorySourceIngestApiRequest
{
    public string? SourceKind { get; set; }

    public Guid ScopeId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? Cursor { get; set; }

    public int? Take { get; set; }

    public Guid? ProjectId { get; set; }
}

internal sealed class CognitiveMemoryConsolidationRunApiRequest
{
    public Guid? ProjectId { get; set; }

    public string? Mode { get; set; }

    public string? TriggerKind { get; set; }

    public CognitiveMemoryConsolidationProfileApiRequest? Profile { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public string? IdempotencyKey { get; set; }

    public CognitiveMemoryConsolidationBudgetApiRequest? Budget { get; set; }

    public string? Cursor { get; set; }

    public IReadOnlyDictionary<string, string>? Options { get; set; }
}

internal sealed class CognitiveMemoryConsolidationProfileApiRequest
{
    public string? Name { get; set; }

    public bool? ProcessSourceItems { get; set; }

    public bool? DetectContradictions { get; set; }

    public bool? ExtractProcedures { get; set; }

    public bool? RebuildProjections { get; set; }

    public bool? CreateHumanReviewItems { get; set; }

    public int? MaxItems { get; set; }
}

internal sealed class CognitiveMemoryConsolidationBudgetApiRequest
{
    public int? SourceItemLimit { get; set; } = CognitiveMemoryConsolidationBudget.Default.SourceItemLimit;

    public int? CandidateLimit { get; set; } = CognitiveMemoryConsolidationBudget.Default.CandidateLimit;

    public int? ReviewItemLimit { get; set; } = CognitiveMemoryConsolidationBudget.Default.ReviewItemLimit;

    public int? MaxSourceCharacters { get; set; } = CognitiveMemoryConsolidationBudget.Default.MaxSourceCharacters;

    public int? LeaseMinutes { get; set; } = (int)CognitiveMemoryConsolidationBudget.Default.LeaseDuration.TotalMinutes;
}

internal sealed class CognitiveMemoryRecallApiRequest
{
    public Guid ProjectId { get; set; }

    public string Query { get; set; } = string.Empty;

    public string? Intent { get; set; }

    public string? Mode { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public CognitiveMemoryRecallBudgetApiRequest? Budget { get; set; }

    public IReadOnlyList<string>? PreferredRecordKinds { get; set; }

    public string? ProjectionCollectionName { get; set; }

    public string? ProjectionProfileId { get; set; }

    public string? EmbeddingProfileId { get; set; }

    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}

internal sealed class CognitiveMemoryRecallBudgetApiRequest
{
    public int? CoarseCandidateLimit { get; set; }

    public int? GraphExpansionDepth { get; set; }

    public int? VectorResultLimit { get; set; }

    public int? FocusLimit { get; set; }

    public int? DetailItemLimit { get; set; }

    public int? ContextCharacterBudget { get; set; }

    public int? MaxSourceBytes { get; set; }
}

internal sealed class CognitiveMemoryPolicyApiRequest
{
    public string? ActorId { get; set; }

    public string? AccessLevel { get; set; }

    public string? PolicyProfileId { get; set; }

    public string? RiskLevel { get; set; }

    public bool? AllowRestrictedContent { get; set; }
}

internal sealed class CognitiveMemoryReviewDecisionApiRequest
{
    public string? DecisionKind { get; set; }

    public string? ActorId { get; set; }

    public string? Notes { get; set; }

    public Guid ExpectedConcurrencyToken { get; set; }
}

internal sealed class CognitiveMemoryProbeStartApiRequest
{
    public Guid ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? RecallMode { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }
}

internal sealed class CognitiveMemoryProbeAskApiRequest
{
    public string Question { get; set; } = string.Empty;

    public string? Intent { get; set; }

    public CognitiveMemoryRecallBudgetApiRequest? Budget { get; set; }

    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}

internal sealed class CognitiveMemoryProbeFeedbackApiRequest
{
    public string? Action { get; set; }

    public string? Notes { get; set; }

    public string? CorrectionText { get; set; }

    public string? RiskLevel { get; set; }

    public bool CreateRegressionTest { get; set; }

    public bool RequestHumanReview { get; set; }

    public string? CalibrationOutcome { get; set; }
}

internal sealed class CognitiveMemorySelfRegulationAssessmentApiRequest
{
    public Guid ProjectId { get; set; }

    public string? ActorId { get; set; }

    public string ModelProfileId { get; set; } = string.Empty;

    public string RoleKey { get; set; } = "developer";

    public string DomainKey { get; set; } = string.Empty;

    public string TaskTypeKey { get; set; } = string.Empty;

    public string? RiskLevel { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public double SourceSufficiency { get; set; } = 0.5;

    public double EvidenceCoverage { get; set; } = 0.5;

    public double ContextFit { get; set; } = 0.5;

    public double ContradictionPressure { get; set; }

    public double RedactionPressure { get; set; }

    public double CognitiveLoad { get; set; }

    public bool HighImpact { get; set; }

    public bool RecentCorrection { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? WorkspaceFrameId { get; set; }

    public Guid? AttentionDecisionId { get; set; }
}

internal sealed class CognitiveMemoryAnswerGateApiRequest
{
    public Guid ProjectId { get; set; }

    public string? ActorId { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public Guid? RecallTraceId { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public Guid? ProfessorReviewId { get; set; }

    public double SourceSufficiency { get; set; } = 0.5;

    public double ContextFit { get; set; } = 0.5;

    public double EvidenceSupport { get; set; } = 0.5;

    public double ContradictionPressure { get; set; }

    public double StalenessPressure { get; set; }

    public double RedactionPressure { get; set; }

    public double CalibrationRisk { get; set; }

    public string? RiskLevel { get; set; }

    public bool ProcedureUnvalidated { get; set; }

    public bool ProfessorReviewRequired { get; set; }

    public string? DraftAnswerSummary { get; set; }
}

internal sealed class CognitiveMemoryProfessorReviewApiRequest
{
    public Guid ProjectId { get; set; }

    public string? ReviewMode { get; set; }

    public string? ActorId { get; set; }

    public string ModelProfileId { get; set; } = string.Empty;

    public string? PromptProfileVersion { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public Guid? SelfRegulationAssessmentId { get; set; }

    public Guid? AnswerPostureDecisionId { get; set; }

    public string InputSummary { get; set; } = string.Empty;

    public string? ContextSummary { get; set; }

    public IReadOnlyList<string> SuggestionKinds { get; set; } = [];
}

internal sealed class CognitiveMemoryProfessorReviewCompleteApiRequest
{
    public string Critique { get; set; } = string.Empty;

    public string? MissingEvidence { get; set; }

    public string? RecommendedPosture { get; set; }

    public IReadOnlyList<string> SuggestionKinds { get; set; } = [];
}

internal sealed class CognitiveMemoryEpistemicScanApiRequest
{
    public Guid ProjectId { get; set; }

    public string? ActorId { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }
}

internal sealed class CognitiveMemoryLearningProposalDecisionApiRequest
{
    public string? Decision { get; set; }

    public string? ActorId { get; set; }

    public string? Notes { get; set; }
}

internal sealed class CognitiveMemoryCrossProjectPromotionApiRequest
{
    public Guid SourceMemoryRecordId { get; set; }

    public Guid SourceProjectId { get; set; }

    public string? ActorId { get; set; }

    public CognitiveMemoryPolicyApiRequest? Policy { get; set; }

    public double SemanticSimilarity { get; set; } = 0.5;

    public double EntityEquivalence { get; set; } = 0.5;

    public double ContextSeparation { get; set; } = 0.5;

    public double SourceReusePermission { get; set; } = 0.5;

    public double PolicyCompatibility { get; set; } = 0.5;

    public string Reason { get; set; } = string.Empty;
}

internal sealed class CognitiveMemoryDistributedWorkerApiRequest
{
    public string WorkerId { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; set; } = [];
}

internal sealed class CognitiveMemoryDistributedJobApiRequest
{
    public Guid ProjectId { get; set; }

    public string? JobKind { get; set; }

    public string SourceScopeKey { get; set; } = string.Empty;

    public string InputPayloadJson { get; set; } = "{}";

    public string ExpectedOutputSchema { get; set; } = string.Empty;

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string PolicyProfileId { get; set; } = string.Empty;
}

internal sealed class CognitiveMemoryDistributedClaimApiRequest
{
    public string WorkerId { get; set; } = string.Empty;

    public IReadOnlyList<string> Capabilities { get; set; } = [];

    public int? LeaseMinutes { get; set; } = 5;
}

internal sealed class CognitiveMemoryDistributedResultApiRequest
{
    public string WorkerId { get; set; } = string.Empty;

    public string LeaseToken { get; set; } = string.Empty;

    public string InputHash { get; set; } = string.Empty;

    public string OutputPayloadJson { get; set; } = "{}";

    public string AlgorithmVersion { get; set; } = string.Empty;

    public string OutputSchema { get; set; } = string.Empty;
}
