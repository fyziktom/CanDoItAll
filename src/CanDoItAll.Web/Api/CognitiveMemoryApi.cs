using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
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

        memory.MapGet("/database/selection", (
                IDatabaseProfileRuntimeAccessor profileAccessor) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                return Results.Ok(CognitiveMemoryDatabaseProfileApiResponse.From(profile));
            })
            .WithName("GetCognitiveMemoryDatabaseSelection");

        memory.MapGet("/database/profiles", async (
                IDatabaseProfileService profileService,
                CancellationToken cancellationToken) =>
            Results.Ok(await profileService.ListAsync(cancellationToken)))
            .WithName("ListCognitiveMemoryDatabaseProfiles");

        memory.MapPost("/database/profiles/postgresql", async (
                CognitiveMemoryPostgreSqlDatabaseProfileApiRequest request,
                IDatabaseProfileService profileService,
                IDatabaseProfileRuntimeAccessor profileAccessor,
                IDatabaseDriverRegistry driverRegistry,
                IAppDatabaseBootstrapper bootstrapper,
                IDatabaseSwitchCoordinator switchCoordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => CreatePostgreSqlDatabaseProfileAsync(
                request,
                profileService,
                profileAccessor,
                driverRegistry,
                bootstrapper,
                switchCoordinator,
                cancellationToken)))
            .WithName("CreateCognitiveMemoryPostgreSqlDatabaseProfile");

        memory.MapPost("/database/switch/{profileId:guid}", async (
                Guid profileId,
                IDatabaseSwitchCoordinator switchCoordinator,
                IDatabaseProfileRuntimeAccessor profileAccessor,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
            {
                var switchResult = await switchCoordinator.SwitchAsync(
                    EnsureNonEmpty(profileId, nameof(profileId)),
                    cancellationToken);
                if (switchResult.IsFailure)
                {
                    throw new InvalidOperationException(BuildErrorMessage(switchResult.Errors));
                }

                var profile = profileAccessor.ResolveCurrentProfile();
                return new CognitiveMemoryDatabaseSwitchApiResponse(
                    switchResult.Value!.PreviousProfileId,
                    switchResult.Value.CurrentProfileId,
                    switchResult.Value.Generation,
                    switchResult.Value.ProcessId,
                    CognitiveMemoryDatabaseProfileApiResponse.From(profile));
            }))
            .WithName("SwitchCognitiveMemoryDatabaseProfile");

        memory.MapGet("/settings", async (
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.GetAsync(cancellationToken)))
            .WithName("GetCognitiveMemorySettings");

        memory.MapPut("/settings", async (
                CognitiveMemoryAutomationSettingsApiRequest request,
                ICognitiveMemoryAutomationSettingsService settingsService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => settingsService.SaveAsync(
                BuildAutomationSettingsUpdate(request),
                cancellationToken)))
            .WithName("UpdateCognitiveMemorySettings");

        memory.MapPost("/ingestion/project-structure", async (
                CognitiveMemoryManualSourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildManualSourceIngestionRequest(
                    request,
                    MemorySourceKind.WorkbenchProjectStructure,
                    requireScope: true,
                    "project-structure"),
                cancellationToken)))
            .WithName("IngestCognitiveMemoryProjectStructure");

        memory.MapPost("/ingestion/processes", async (
                CognitiveMemoryManualSourceIngestApiRequest request,
                ICognitiveMemorySourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestAsync(
                BuildManualSourceIngestionRequest(
                    request,
                    MemorySourceKind.ProcessRuntime,
                    requireScope: false,
                    "process-runtime"),
                cancellationToken)))
            .WithName("IngestCognitiveMemoryProcesses");

        memory.MapPost("/external-sources/files", async (
                [FromForm] CognitiveMemoryExternalFileUploadApiRequest request,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
            {
                if (request.File is null)
                {
                    throw new ArgumentException("A file is required.", nameof(request.File));
                }

                if (request.File.Length > 10 * 1024 * 1024)
                {
                    throw new InvalidOperationException("File uploads for cognitive memory ingestion are limited to 10 MB.");
                }

                await using var stream = request.File.OpenReadStream();
                return await ingestionService.IngestFileAsync(
                    request.ProjectId,
                    request.File.FileName,
                    request.File.ContentType,
                    stream,
                    request.File.Length,
                    NormalizeActorId(request.ActorId),
                    request.IdempotencyKey,
                    cancellationToken);
            }))
            .WithName("IngestCognitiveMemoryExternalFile")
            .Accepts<CognitiveMemoryExternalFileUploadApiRequest>("multipart/form-data");

        memory.MapPost("/external-sources/web-links", async (
                CognitiveMemoryExternalWebLinkApiRequest request,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => ingestionService.IngestWebsiteAsync(
                request.ProjectId,
                BuildHttpUri(request.Url),
                NormalizeActorId(request.ActorId),
                request.IdempotencyKey,
                cancellationToken)))
            .WithName("IngestCognitiveMemoryExternalWebLink");

        memory.MapGet("/external-sources/ingestions/{operationId:guid}", async (
                Guid operationId,
                ICognitiveMemoryExternalSourceIngestionService ingestionService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                await ingestionService.GetAsync(
                    EnsureNonEmpty(operationId, nameof(operationId)),
                    cancellationToken)
                ?? throw new InvalidOperationException("External source ingestion operation was not found.")))
            .WithName("GetCognitiveMemoryExternalSourceIngestion");

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

        memory.MapPost("/probes/sessions", async (
                CognitiveMemoryProbeStartApiRequest request,
                ICognitiveMemoryProbeService probeService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => probeService.StartAsync(
                new CognitiveMemoryProbeStartRequest(
                    EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId)),
                    EnsureText(request.Title, nameof(request.Title)),
                    BuildPolicyContext(request.ProjectId, request.Policy),
                    ParseEnum(request.RecallMode, CognitiveMemoryRecallMode.FocusedTaskContext, nameof(request.RecallMode))),
                cancellationToken)))
            .WithName("StartCognitiveMemoryProbeSession");

        memory.MapPost("/probes/sessions/{sessionId:guid}/turns", async (
                Guid sessionId,
                CognitiveMemoryProbeAskApiRequest request,
                ICognitiveMemoryProbeService probeService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => probeService.AskAsync(
                new CognitiveMemoryProbeAskRequest(
                    EnsureNonEmpty(sessionId, nameof(sessionId)),
                    EnsureText(request.Question, nameof(request.Question)),
                    ParseEnum(request.Intent, CognitiveMemoryRecallIntentKind.Testing, nameof(request.Intent)),
                    BuildRecallBudget(request.Budget),
                    request.Metadata),
                cancellationToken)))
            .WithName("AskCognitiveMemoryProbeQuestion");

        memory.MapPost("/probes/turns/{turnId:guid}/feedback", async (
                Guid turnId,
                CognitiveMemoryProbeFeedbackApiRequest request,
                ICognitiveMemoryProbeService probeService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => probeService.RecordFeedbackAsync(
                new CognitiveMemoryProbeFeedbackRequest(
                    EnsureNonEmpty(turnId, nameof(turnId)),
                    ParseEnum(request.Action, CognitiveMemoryProbeFeedbackAction.MarkCorrect, nameof(request.Action)),
                    request.Notes?.Trim() ?? string.Empty,
                    request.CorrectionText?.Trim() ?? string.Empty,
                    ParseEnum(request.RiskLevel, CognitiveMemoryRiskLevel.Low, nameof(request.RiskLevel)),
                    request.CreateRegressionTest,
                    request.RequestHumanReview,
                    ParseEnum(request.CalibrationOutcome, CognitiveMemoryCalibrationOutcomeKind.Unknown, nameof(request.CalibrationOutcome))),
                cancellationToken)))
            .WithName("RecordCognitiveMemoryProbeFeedback");

        memory.MapPost("/self-regulation/assessments", async (
                CognitiveMemorySelfRegulationAssessmentApiRequest request,
                ICognitiveMemorySelfRegulationOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => orchestrator.AssessAsync(
                BuildSelfRegulationAssessmentRequest(request),
                cancellationToken)))
            .WithName("AssessCognitiveMemorySelfRegulation");

        memory.MapPost("/answer-gate/decisions", async (
                CognitiveMemoryAnswerGateApiRequest request,
                ICognitiveMemoryAnswerGateService answerGateService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => answerGateService.DecideAsync(
                BuildAnswerGateRequest(request),
                cancellationToken)))
            .WithName("DecideCognitiveMemoryAnswerGate");

        memory.MapPost("/professor-reviews", async (
                CognitiveMemoryProfessorReviewApiRequest request,
                ICognitiveMemoryProfessorReviewService professorReviewService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => professorReviewService.RequestReviewAsync(
                BuildProfessorReviewRequest(request),
                cancellationToken)))
            .WithName("RequestCognitiveMemoryProfessorReview");

        memory.MapPost("/professor-reviews/{reviewId:guid}/complete", async (
                Guid reviewId,
                CognitiveMemoryProfessorReviewCompleteApiRequest request,
                ICognitiveMemoryProfessorReviewService professorReviewService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => professorReviewService.CompleteReviewAsync(
                EnsureNonEmpty(reviewId, nameof(reviewId)),
                EnsureText(request.Critique, nameof(request.Critique)),
                request.MissingEvidence?.Trim() ?? string.Empty,
                ParseEnum(request.RecommendedPosture, CognitiveMemoryAnswerPostureKind.Caveated, nameof(request.RecommendedPosture)),
                request.SuggestionKinds
                    .Select(item => ParseEnum(item, CognitiveMemoryProfessorSuggestionKind.NoAction, nameof(request.SuggestionKinds)))
                    .ToArray(),
                cancellationToken)))
            .WithName("CompleteCognitiveMemoryProfessorReview");

        memory.MapPost("/epistemic-drive/scans", async (
                CognitiveMemoryEpistemicScanApiRequest request,
                ICognitiveMemoryEpistemicDriveService epistemicDriveService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => epistemicDriveService.ScanAsync(
                new CognitiveMemoryEpistemicScanRequest(
                    EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId)),
                    BuildPolicyContext(request.ProjectId, request.Policy),
                    NormalizeActorId(request.ActorId)),
                cancellationToken)))
            .WithName("ScanCognitiveMemoryEpistemicDrive");

        memory.MapPost("/epistemic-drive/proposals/{proposalId:guid}/decisions", async (
                Guid proposalId,
                CognitiveMemoryLearningProposalDecisionApiRequest request,
                ICognitiveMemoryEpistemicDriveService epistemicDriveService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => epistemicDriveService.DecideProposalAsync(
                EnsureNonEmpty(proposalId, nameof(proposalId)),
                ParseEnum(request.Decision, CognitiveMemoryLearningProposalStatus.Approved, nameof(request.Decision)),
                NormalizeActorId(request.ActorId),
                request.Notes?.Trim() ?? string.Empty,
                cancellationToken)))
            .WithName("DecideCognitiveMemoryLearningProposal");

        memory.MapPost("/cross-project/promotions", async (
                CognitiveMemoryCrossProjectPromotionApiRequest request,
                ICognitiveMemoryCrossProjectMemoryService crossProjectMemoryService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => crossProjectMemoryService.CreateCandidateAsync(
                new CognitiveMemoryCrossProjectPromotionRequest(
                    EnsureNonEmpty(request.SourceMemoryRecordId, nameof(request.SourceMemoryRecordId)),
                    EnsureNonEmpty(request.SourceProjectId, nameof(request.SourceProjectId)),
                    NormalizeActorId(request.ActorId),
                    BuildPolicyContext(request.SourceProjectId, request.Policy),
                    request.SemanticSimilarity,
                    request.EntityEquivalence,
                    request.ContextSeparation,
                    request.SourceReusePermission,
                    request.PolicyCompatibility,
                    EnsureText(request.Reason, nameof(request.Reason))),
                cancellationToken)))
            .WithName("CreateCognitiveMemoryCrossProjectPromotion");

        memory.MapPost("/distributed/workers", async (
                CognitiveMemoryDistributedWorkerApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.RegisterWorkerAsync(
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                EnsureText(request.MachineName, nameof(request.MachineName)),
                request.Capabilities
                    .Select(item => ParseEnum(item, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.Capabilities)))
                    .ToArray(),
                cancellationToken)))
            .WithName("RegisterCognitiveMemoryDistributedWorker");

        memory.MapPost("/distributed/jobs", async (
                CognitiveMemoryDistributedJobApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.EnqueueAsync(
                new CognitiveMemoryDistributedJobEnqueueRequest(
                    EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId)),
                    ParseEnum(request.JobKind, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.JobKind)),
                    EnsureText(request.SourceScopeKey, nameof(request.SourceScopeKey)),
                    EnsureText(request.InputPayloadJson, nameof(request.InputPayloadJson)),
                    EnsureText(request.ExpectedOutputSchema, nameof(request.ExpectedOutputSchema)),
                    EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
                    EnsureText(request.PolicyProfileId, nameof(request.PolicyProfileId))),
                cancellationToken)))
            .WithName("EnqueueCognitiveMemoryDistributedJob");

        memory.MapPost("/distributed/jobs/claim", async (
                CognitiveMemoryDistributedClaimApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.ClaimAsync(
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                request.Capabilities
                    .Select(item => ParseEnum(item, CognitiveMemoryDistributedJobKind.ProjectionRebuild, nameof(request.Capabilities)))
                    .ToArray(),
                TimeSpan.FromMinutes(NormalizePositive(request.LeaseMinutes, nameof(request.LeaseMinutes))),
                cancellationToken)))
            .WithName("ClaimCognitiveMemoryDistributedJob");

        memory.MapPost("/distributed/jobs/{jobId:guid}/results", async (
                Guid jobId,
                CognitiveMemoryDistributedResultApiRequest request,
                ICognitiveMemoryDistributedComputeCoordinator coordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => coordinator.SubmitResultAsync(
                EnsureNonEmpty(jobId, nameof(jobId)),
                EnsureText(request.WorkerId, nameof(request.WorkerId)),
                EnsureText(request.LeaseToken, nameof(request.LeaseToken)),
                EnsureText(request.InputHash, nameof(request.InputHash)),
                EnsureText(request.OutputPayloadJson, nameof(request.OutputPayloadJson)),
                EnsureText(request.AlgorithmVersion, nameof(request.AlgorithmVersion)),
                EnsureText(request.OutputSchema, nameof(request.OutputSchema)),
                cancellationToken)))
            .WithName("SubmitCognitiveMemoryDistributedResult");

        return group;
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
                "GET /api/cognitive-memory/database/selection",
                "GET /api/cognitive-memory/database/profiles",
                "POST /api/cognitive-memory/database/profiles/postgresql",
                "POST /api/cognitive-memory/database/switch/{profileId}",
                "GET /api/cognitive-memory/settings",
                "PUT /api/cognitive-memory/settings",
                "POST /api/cognitive-memory/ingestion/project-structure",
                "POST /api/cognitive-memory/ingestion/processes",
                "POST /api/cognitive-memory/external-sources/files",
                "POST /api/cognitive-memory/external-sources/web-links",
                "GET /api/cognitive-memory/external-sources/ingestions/{operationId}",
                "GET /api/cognitive-memory/snapshot",
                "POST /api/cognitive-memory/sources/ingest",
                "POST /api/cognitive-memory/consolidation/runs",
                "POST /api/cognitive-memory/recall",
                "POST /api/cognitive-memory/review-items/{reviewItemId}/decisions",
                "POST /api/cognitive-memory/probes/sessions",
                "POST /api/cognitive-memory/probes/sessions/{sessionId}/turns",
                "POST /api/cognitive-memory/probes/turns/{turnId}/feedback",
                "POST /api/cognitive-memory/self-regulation/assessments",
                "POST /api/cognitive-memory/answer-gate/decisions",
                "POST /api/cognitive-memory/professor-reviews",
                "POST /api/cognitive-memory/professor-reviews/{reviewId}/complete",
                "POST /api/cognitive-memory/epistemic-drive/scans",
                "POST /api/cognitive-memory/epistemic-drive/proposals/{proposalId}/decisions",
                "POST /api/cognitive-memory/cross-project/promotions",
                "POST /api/cognitive-memory/distributed/workers",
                "POST /api/cognitive-memory/distributed/jobs",
                "POST /api/cognitive-memory/distributed/jobs/claim",
                "POST /api/cognitive-memory/distributed/jobs/{jobId}/results"
            ]);
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
