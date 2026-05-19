using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static partial class CognitiveMemoryApi
{
    private const string DefaultActorId = "api:cognitive-memory";
    private const string DefaultPolicyProfileId = "developer-api";

    public static RouteGroupBuilder MapCognitiveMemoryApi(this RouteGroupBuilder group)
    {
        var memory = group.MapGroup("/cognitive-memory")
            .WithTags("Cognitive Memory")
            .DisableAntiforgery();

        MapCognitiveMemoryApiEndpoints(memory, CognitiveMemoryApiSurface.Legacy);

        var memoryV1 = group.MapGroup("/cognitive-memory/v1")
            .WithTags("Cognitive Memory v1")
            .DisableAntiforgery();

        MapCognitiveMemoryApiEndpoints(memoryV1, CognitiveMemoryApiSurface.V1);

        return group;
    }

    private static void MapCognitiveMemoryApiEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        MapContractEndpoints(memory, surface);
        MapDatabaseEndpoints(memory, surface);
        MapSettingsEndpoints(memory, surface);
        MapOperationsEndpoints(memory, surface);
        MapIngestionEndpoints(memory, surface);
        MapRecallReviewEndpoints(memory, surface);
        MapAdvancedEndpoints(memory, surface);
        MapDistributedEndpoints(memory, surface);
    }

    private static async ValueTask<CognitiveMemoryPostgreSqlDatabaseProfileApiResponse> CreatePostgreSqlDatabaseProfileAsync(
        CognitiveMemoryPostgreSqlDatabaseProfileApiRequest request,
        IDatabaseProfileService profileService,
        IDatabaseProfileRuntimeAccessor profileAccessor,
        IDatabaseDriverRegistry driverRegistry,
        IAppDatabaseBootstrapper bootstrapper,
        IDatabaseSwitchCoordinator switchCoordinator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var databaseName = EnsureText(request.DatabaseName, nameof(request.DatabaseName));
        var username = EnsureText(request.Username, nameof(request.Username));
        var saveResult = await profileService.SaveAsync(new DatabaseProfileEditorModel
        {
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? $"PostgreSQL {databaseName}"
                : request.DisplayName.Trim(),
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            WorkspaceRoot = NormalizeOptionalText(request.WorkspaceRoot),
            PostgresHost = string.IsNullOrWhiteSpace(request.Host) ? "127.0.0.1" : request.Host.Trim(),
            PostgresPort = request.Port is > 0 ? request.Port.Value : 5432,
            PostgresDatabaseName = databaseName,
            PostgresUsername = username,
            PostgresPassword = request.Password ?? string.Empty,
            PostgresAdminDatabaseName = string.IsNullOrWhiteSpace(request.AdminDatabaseName)
                ? "postgres"
                : request.AdminDatabaseName.Trim(),
            PostgresTrustServerCertificate = request.TrustServerCertificate ?? false
        }, cancellationToken);
        if (saveResult.IsFailure)
        {
            throw new InvalidOperationException(BuildErrorMessage(saveResult.Errors));
        }

        var profile = profileAccessor.ResolveProfile(saveResult.Value);
        await driverRegistry.Resolve(profile.Profile.ProviderKind).CreateEmptyAsync(profile, cancellationToken);
        await bootstrapper.EnsureProfileReadyAsync(profile, cancellationToken);

        CognitiveMemoryDatabaseSwitchSummaryApiResponse? switchResponse = null;
        if (request.Activate != false)
        {
            var switchResult = await switchCoordinator.SwitchAsync(profile.Profile.Id, cancellationToken);
            if (switchResult.IsFailure)
            {
                throw new InvalidOperationException(BuildErrorMessage(switchResult.Errors));
            }

            switchResponse = CognitiveMemoryDatabaseSwitchSummaryApiResponse.From(switchResult.Value!);
            profile = profileAccessor.ResolveCurrentProfile();
        }

        return new CognitiveMemoryPostgreSqlDatabaseProfileApiResponse(
            CognitiveMemoryDatabaseProfileApiResponse.From(profile),
            switchResponse);
    }

    private static CognitiveMemoryAutomationSettingsUpdate BuildAutomationSettingsUpdate(
        CognitiveMemoryAutomationSettingsApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CognitiveMemoryAutomationSettingsUpdate(
            ParseEnum(
                request.ScheduleMode,
                CognitiveMemoryAutomationScheduleMode.ManualOnly,
                nameof(request.ScheduleMode)),
            EnsureText(request.NightlyLocalTime, nameof(request.NightlyLocalTime)),
            NormalizePositive(request.IdleMinutes, nameof(request.IdleMinutes)),
            request.ScheduledLocalTimes ?? [],
            request.AutoIngestProjectStructure,
            request.AutoIngestProcessRuntime,
            request.AutoConsolidateAfterIngestion,
            ParseEnum(
                request.ModelAccessMode,
                CognitiveMemoryModelAccessMode.AnyEnabledProvider,
                nameof(request.ModelAccessMode)),
            request.DefaultProviderProfileId,
            request.DefaultAgentId,
            request.AllowedProviderProfileIds ?? [],
            NormalizeActorId(request.ActorId))
        {
            ModelExecutionProfiles = request.ModelExecutionProfiles ?? CognitiveMemoryModelExecutionProfileDefaults.OpenAiProfiles
        };
    }

    private static CognitiveMemoryRetentionCleanupRequest BuildRetentionCleanupRequest(
        CognitiveMemoryRetentionCleanupApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopes = request.Scopes is null || request.Scopes.Count == 0
            ? CognitiveMemoryRetentionCleanupRequest.DefaultScopes
            : request.Scopes
                .Select(scope => ParseEnum(
                    scope,
                    CognitiveMemoryRetentionCleanupScope.RecallTraces,
                    nameof(request.Scopes)))
                .Distinct()
                .OrderBy(scope => scope)
                .ToArray();

        return new CognitiveMemoryRetentionCleanupRequest(
            request.ProjectId,
            request.DeleteBeforeUtc.ToUniversalTime(),
            request.DryRun ?? true,
            scopes,
            NormalizeActorId(request.ActorId));
    }

    private static CognitiveMemorySourceIngestionRequest BuildManualSourceIngestionRequest(
        CognitiveMemoryManualSourceIngestApiRequest request,
        MemorySourceKind sourceKind,
        bool requireScope,
        string operationName)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeId = request.ScopeId ?? request.ProjectId ?? Guid.Empty;
        if (requireScope && scopeId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty scopeId or projectId is required.", nameof(request.ScopeId));
        }

        return new CognitiveMemorySourceIngestionRequest(
            sourceKind,
            scopeId,
            BuildIdempotencyKey(request.IdempotencyKey, operationName),
            BuildCursor(request.Cursor),
            request.Take,
            request.ProjectId);
    }

    private static Uri BuildHttpUri(string? value)
    {
        var text = EnsureText(value, nameof(CognitiveMemoryExternalWebLinkApiRequest.Url));
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("URL must be an absolute HTTP or HTTPS URL.", nameof(CognitiveMemoryExternalWebLinkApiRequest.Url));
        }

        return uri;
    }

    private static async Task<IResult> ExecuteAsync<T>(Func<ValueTask<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "cognitive-memory.request-invalid");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "cognitive-memory.operation-unavailable");
        }
    }

    private static CognitiveMemorySelfRegulationAssessmentRequest BuildSelfRegulationAssessmentRequest(
        CognitiveMemorySelfRegulationAssessmentApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectId = request.ProjectId == Guid.Empty ? (Guid?)null : request.ProjectId;
        return new CognitiveMemorySelfRegulationAssessmentRequest(
            projectId,
            NormalizeActorId(request.ActorId),
            new CognitiveMemoryModelProfileId(EnsureText(request.ModelProfileId, nameof(request.ModelProfileId))),
            new CognitiveMemoryRoleKey(string.IsNullOrWhiteSpace(request.RoleKey) ? "developer" : request.RoleKey.Trim()),
            EnsureText(request.DomainKey, nameof(request.DomainKey)),
            EnsureText(request.TaskTypeKey, nameof(request.TaskTypeKey)),
            ParseEnum(request.RiskLevel, CognitiveMemoryRiskLevel.Low, nameof(request.RiskLevel)),
            BuildPolicyContext(projectId, request.Policy),
            request.SourceSufficiency,
            request.EvidenceCoverage,
            request.ContextFit,
            request.ContradictionPressure,
            request.RedactionPressure,
            request.CognitiveLoad,
            request.HighImpact,
            request.RecentCorrection,
            request.RecallTraceId,
            request.WorkspaceFrameId,
            request.AttentionDecisionId);
    }

    private static CognitiveMemoryAnswerGateRequest BuildAnswerGateRequest(
        CognitiveMemoryAnswerGateApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectId = EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        return new CognitiveMemoryAnswerGateRequest(
            projectId,
            NormalizeActorId(request.ActorId),
            BuildPolicyContext(projectId, request.Policy),
            request.RecallTraceId,
            request.SelfRegulationAssessmentId,
            request.AnswerPostureDecisionId,
            request.ProfessorReviewId,
            request.SourceSufficiency,
            request.ContextFit,
            request.EvidenceSupport,
            request.ContradictionPressure,
            request.StalenessPressure,
            request.RedactionPressure,
            request.CalibrationRisk,
            ParseEnum(request.RiskLevel, CognitiveMemoryRiskLevel.Low, nameof(request.RiskLevel)),
            request.ProcedureUnvalidated,
            request.ProfessorReviewRequired,
            request.DraftAnswerSummary?.Trim() ?? string.Empty);
    }

    private static CognitiveMemoryProfessorReviewRequest BuildProfessorReviewRequest(
        CognitiveMemoryProfessorReviewApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectId = request.ProjectId == Guid.Empty ? (Guid?)null : request.ProjectId;
        return new CognitiveMemoryProfessorReviewRequest(
            projectId,
            ParseEnum(request.ReviewMode, CognitiveMemoryProfessorReviewMode.SocraticChallenge, nameof(request.ReviewMode)),
            NormalizeActorId(request.ActorId),
            new CognitiveMemoryModelProfileId(EnsureText(request.ModelProfileId, nameof(request.ModelProfileId))),
            string.IsNullOrWhiteSpace(request.PromptProfileVersion) ? "professor-review-v1" : request.PromptProfileVersion.Trim(),
            BuildPolicyContext(projectId, request.Policy),
            request.SelfRegulationAssessmentId,
            request.AnswerPostureDecisionId,
            EnsureText(request.InputSummary, nameof(request.InputSummary)),
            request.ContextSummary?.Trim() ?? string.Empty,
            request.SuggestionKinds
                .Select(item => ParseEnum(item, CognitiveMemoryProfessorSuggestionKind.NoAction, nameof(request.SuggestionKinds)))
                .ToArray());
    }

    private static CognitiveMemorySourceIngestionRequest BuildSourceIngestionRequest(
        CognitiveMemorySourceIngestApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceKind = ParseEnum(
            request.SourceKind,
            MemorySourceKind.WorkbenchProjectStructure,
            nameof(request.SourceKind));
        var scopeId = EnsureNonEmpty(request.ScopeId, nameof(request.ScopeId));

        return new CognitiveMemorySourceIngestionRequest(
            sourceKind,
            scopeId,
            BuildIdempotencyKey(request.IdempotencyKey, "source-ingest"),
            BuildCursor(request.Cursor),
            request.Take,
            request.ProjectId);
    }

    private static CognitiveMemoryConsolidationRunRequest BuildConsolidationRunRequest(
        CognitiveMemoryConsolidationRunApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var policy = BuildPolicyContext(request.ProjectId, request.Policy);
        var budget = request.Budget is null
            ? null
            : new CognitiveMemoryConsolidationBudget(
                NormalizePositive(request.Budget.SourceItemLimit, "sourceItemLimit"),
                NormalizePositive(request.Budget.CandidateLimit, "candidateLimit"),
                NormalizeNonNegative(request.Budget.ReviewItemLimit, "reviewItemLimit"),
                NormalizePositive(request.Budget.MaxSourceCharacters, "maxSourceCharacters"),
                TimeSpan.FromMinutes(NormalizePositive(request.Budget.LeaseMinutes, "leaseMinutes")));

        return new CognitiveMemoryConsolidationRunRequest(
            request.ProjectId,
            ParseEnum(
                request.Mode,
                CognitiveMemoryConsolidationMode.IncrementalRecent,
                nameof(request.Mode)),
            ParseEnum(
                request.TriggerKind,
                CognitiveMemoryConsolidationTriggerKind.Manual,
                nameof(request.TriggerKind)),
            BuildConsolidationProfile(request.Profile),
            policy,
            BuildIdempotencyKey(request.IdempotencyKey, "consolidation"),
            budget,
            NormalizeOptionalText(request.Cursor),
            request.Options);
    }

    private static CognitiveMemoryRecallRequest BuildRecallRequest(CognitiveMemoryRecallApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var projectId = EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        var query = EnsureText(request.Query, nameof(request.Query));

        return new CognitiveMemoryRecallRequest(
            projectId,
            query,
            ParseEnum(
                request.Intent,
                CognitiveMemoryRecallIntentKind.SourceLookup,
                nameof(request.Intent)),
            ParseEnum(
                request.Mode,
                CognitiveMemoryRecallMode.FocusedTaskContext,
                nameof(request.Mode)),
            BuildPolicyContext(projectId, request.Policy),
            BuildRecallBudget(request.Budget),
            PreferredRecordKinds: ParseEnumList<CognitiveMemoryRecordKind>(request.PreferredRecordKinds, nameof(request.PreferredRecordKinds)),
            Metadata: request.Metadata);
    }

    private static CognitiveMemoryReviewDecisionRequest BuildReviewDecisionRequest(
        Guid reviewItemId,
        CognitiveMemoryReviewDecisionApiRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new CognitiveMemoryReviewDecisionRequest(
            new CognitiveMemoryReviewItemId(reviewItemId),
            ParseEnum(
                request.DecisionKind,
                CognitiveMemoryReviewDecisionKind.Defer,
                nameof(request.DecisionKind)),
            NormalizeActorId(request.ActorId),
            request.Notes?.Trim() ?? string.Empty,
            EnsureNonEmpty(request.ExpectedConcurrencyToken, nameof(request.ExpectedConcurrencyToken)));
    }

    private static CognitiveMemoryConsolidationProfile BuildConsolidationProfile(
        CognitiveMemoryConsolidationProfileApiRequest? request)
    {
        var defaults = CognitiveMemoryConsolidationProfile.IncrementalRecent;
        if (request is null)
        {
            return defaults;
        }

        return new CognitiveMemoryConsolidationProfile(
            string.IsNullOrWhiteSpace(request.Name) ? defaults.Name : request.Name.Trim(),
            request.ProcessSourceItems ?? defaults.ProcessSourceItems,
            request.DetectContradictions ?? defaults.DetectContradictions,
            request.ExtractProcedures ?? defaults.ExtractProcedures,
            request.RebuildProjections ?? defaults.RebuildProjections,
            request.CreateHumanReviewItems ?? defaults.CreateHumanReviewItems,
            NormalizePositive(request.MaxItems ?? defaults.MaxItems, "maxItems"));
    }

    private static CognitiveMemoryRecallBudget BuildRecallBudget(CognitiveMemoryRecallBudgetApiRequest? request)
    {
        return new CognitiveMemoryRecallBudget(
            request?.CoarseCandidateLimit ?? 24,
            request?.GraphExpansionDepth ?? 1,
            request?.VectorResultLimit ?? 12,
            request?.FocusLimit ?? 8,
            request?.DetailItemLimit ?? 8,
            request?.ContextCharacterBudget ?? 12_000,
            request?.MaxSourceBytes ?? 24_000);
    }

    private static CognitiveMemoryPolicyContext BuildPolicyContext(
        Guid? projectId,
        CognitiveMemoryPolicyApiRequest? request)
    {
        return new CognitiveMemoryPolicyContext(
            projectId,
            NormalizeActorId(request?.ActorId),
            ParseEnum(
                request?.AccessLevel,
                CognitiveMemoryAccessLevel.Project,
                nameof(CognitiveMemoryPolicyApiRequest.AccessLevel)),
            new CognitiveMemoryPolicyProfileId(
                string.IsNullOrWhiteSpace(request?.PolicyProfileId)
                    ? DefaultPolicyProfileId
                    : request.PolicyProfileId.Trim()),
            ParseEnum(
                request?.RiskLevel,
                CognitiveMemoryRiskLevel.Low,
                nameof(CognitiveMemoryPolicyApiRequest.RiskLevel)),
            request?.AllowRestrictedContent ?? false);
    }

    private static CognitiveMemoryIdempotencyKey BuildIdempotencyKey(string? value, string operationName)
    {
        var normalized = NormalizeOptionalText(value);
        return new CognitiveMemoryIdempotencyKey(
            normalized ?? $"api:{operationName}:{Guid.NewGuid():N}");
    }

    private static MemorySourceSnapshotCursor? BuildCursor(string? value)
    {
        var normalized = NormalizeOptionalText(value);
        return normalized is null ? null : new MemorySourceSnapshotCursor(normalized);
    }

    private static IReadOnlyList<TEnum>? ParseEnumList<TEnum>(
        IReadOnlyList<string>? values,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (values is null)
        {
            return null;
        }

        return values
            .Select(value => ParseEnum<TEnum>(value, default, parameterName))
            .ToList();
    }

    private static TEnum ParseEnum<TEnum>(
        string? value,
        TEnum fallback,
        string parameterName)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return Enum.TryParse<TEnum>(value.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException(
                $"Unsupported {parameterName} '{value}'. Supported values: {string.Join(", ", Enum.GetNames<TEnum>())}.",
                parameterName);
    }

    private static int NormalizeTake(int? take, int fallback, int maximum)
    {
        return Math.Clamp(take.GetValueOrDefault(fallback), 1, maximum);
    }

    private static int NormalizePositive(int? value, string parameterName)
    {
        if (value.GetValueOrDefault() <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be positive.");
        }

        return value!.Value;
    }

    private static int NormalizeNonNegative(int? value, string parameterName)
    {
        if (value.GetValueOrDefault() < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must not be negative.");
        }

        return value!.Value;
    }

    private static Guid EnsureNonEmpty(Guid value, string parameterName)
    {
        return value == Guid.Empty
            ? throw new ArgumentException("Identifier values must not be empty.", parameterName)
            : value;
    }

    private static string EnsureText(string? value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value must not be empty.", parameterName)
            : value.Trim();
    }

    private static string NormalizeActorId(string? actorId)
    {
        return string.IsNullOrWhiteSpace(actorId)
            ? DefaultActorId
            : actorId.Trim();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildErrorMessage(IReadOnlyList<Error> errors)
    {
        return string.Join(" | ", errors.Select(error => error.Message));
    }
}
