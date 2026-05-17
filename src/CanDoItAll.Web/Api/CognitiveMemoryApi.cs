using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class CognitiveMemoryApi
{
    private const string DefaultActorId = "api:cognitive-memory";
    private const string DefaultPolicyProfileId = "developer-api";

    public static RouteGroupBuilder MapCognitiveMemoryApi(this RouteGroupBuilder group)
    {
        var memory = group.MapGroup("/cognitive-memory")
            .WithTags("Cognitive Memory")
            .DisableAntiforgery();

        memory.MapGet("/status", (
                IDatabaseProfileRuntimeAccessor profileAccessor) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                return Results.Ok(CognitiveMemoryStatusApiResponse.From(profile));
            })
            .WithName("GetCognitiveMemoryStatus");

        memory.MapGet("/snapshot", async (
                [AsParameters] CognitiveMemorySnapshotApiQuery query,
                ICognitiveMemoryReviewUiService reviewUiService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => reviewUiService.GetSnapshotAsync(
                new CognitiveMemoryReviewUiQuery(
                    query.ProjectId,
                    NormalizeTake(query.Take, 12, 200)),
                cancellationToken)))
            .WithName("GetCognitiveMemorySnapshot");

        memory.MapPost("/sources/ingest", async (
                CognitiveMemorySourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildSourceIngestionRequest(request),
                cancellationToken)))
            .WithName("IngestCognitiveMemorySource");

        memory.MapPost("/consolidation/runs", async (
                CognitiveMemoryConsolidationRunApiRequest request,
                ICognitiveMemoryConsolidationEngine consolidationEngine,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => consolidationEngine.RunAsync(
                BuildConsolidationRunRequest(request),
                cancellationToken)))
            .WithName("RunCognitiveMemoryConsolidation");

        memory.MapPost("/recall", async (
                CognitiveMemoryRecallApiRequest request,
                IServiceProvider serviceProvider,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() =>
            {
                var recallOrchestrator = serviceProvider.GetRequiredService<ICognitiveMemoryRecallOrchestrator>();
                return recallOrchestrator.RecallAsync(
                    BuildRecallRequest(request),
                    cancellationToken);
            }))
            .WithName("RecallCognitiveMemoryContext");

        memory.MapPost("/review-items/{reviewItemId:guid}/decisions", async (
                Guid reviewItemId,
                CognitiveMemoryReviewDecisionApiRequest request,
                ICognitiveMemoryReviewUiService reviewUiService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => reviewUiService.DecideReviewItemAsync(
                BuildReviewDecisionRequest(reviewItemId, request),
                cancellationToken)))
            .WithName("DecideCognitiveMemoryReviewItem");

        return group;
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
}

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
    IReadOnlyList<string> Routes)
{
    public static CognitiveMemoryStatusApiResponse From(ResolvedDatabaseProfile resolvedProfile)
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
            [
                "GET /api/cognitive-memory/status",
                "GET /api/cognitive-memory/snapshot",
                "POST /api/cognitive-memory/sources/ingest",
                "POST /api/cognitive-memory/consolidation/runs",
                "POST /api/cognitive-memory/recall",
                "POST /api/cognitive-memory/review-items/{reviewItemId}/decisions"
            ]);
    }

    private static string BuildDescriptor(DatabaseProfileRecord profile)
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

internal sealed class CognitiveMemorySnapshotApiQuery
{
    public Guid? ProjectId { get; set; }

    public int? Take { get; set; }
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
