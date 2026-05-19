using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapAdvancedEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapPost("/probes/sessions", async (
                CognitiveMemoryProbeStartApiRequest request,
                ICognitiveMemoryProbeService probeService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => probeService.StartAsync(
                new CognitiveMemoryProbeStartRequest(
                    EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId)),
                    EnsureText(request.Title, nameof(request.Title)),
                    BuildPolicyContext(request.ProjectId, request.Policy),
                    ParseEnum(request.RecallMode, CognitiveMemoryRecallMode.FocusedTaskContext, nameof(request.RecallMode)),
                    ProjectionCollectionName: CreateProjectionCollectionName(request.ProjectionCollectionName),
                    ProjectionProfileId: CreateProjectionProfileId(request.ProjectionProfileId),
                    EmbeddingProfileId: CreateEmbeddingProfileId(request.EmbeddingProfileId)),
                cancellationToken)))
            .WithName(EndpointName("StartCognitiveMemoryProbeSession", surface));

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
                    request.Metadata,
                    CreateProjectionCollectionName(request.ProjectionCollectionName),
                    CreateProjectionProfileId(request.ProjectionProfileId),
                    CreateEmbeddingProfileId(request.EmbeddingProfileId)),
                cancellationToken)))
            .WithName(EndpointName("AskCognitiveMemoryProbeQuestion", surface));

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
            .WithName(EndpointName("RecordCognitiveMemoryProbeFeedback", surface));

        memory.MapPost("/self-regulation/assessments", async (
                CognitiveMemorySelfRegulationAssessmentApiRequest request,
                ICognitiveMemorySelfRegulationOrchestrator orchestrator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => orchestrator.AssessAsync(
                BuildSelfRegulationAssessmentRequest(request),
                cancellationToken)))
            .WithName(EndpointName("AssessCognitiveMemorySelfRegulation", surface));

        memory.MapPost("/answer-gate/decisions", async (
                CognitiveMemoryAnswerGateApiRequest request,
                ICognitiveMemoryAnswerGateService answerGateService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => answerGateService.DecideAsync(
                BuildAnswerGateRequest(request),
                cancellationToken)))
            .WithName(EndpointName("DecideCognitiveMemoryAnswerGate", surface));

        memory.MapPost("/professor-reviews", async (
                CognitiveMemoryProfessorReviewApiRequest request,
                ICognitiveMemoryProfessorReviewService professorReviewService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => professorReviewService.RequestReviewAsync(
                BuildProfessorReviewRequest(request),
                cancellationToken)))
            .WithName(EndpointName("RequestCognitiveMemoryProfessorReview", surface));

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
            .WithName(EndpointName("CompleteCognitiveMemoryProfessorReview", surface));

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
            .WithName(EndpointName("ScanCognitiveMemoryEpistemicDrive", surface));

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
            .WithName(EndpointName("DecideCognitiveMemoryLearningProposal", surface));

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
            .WithName(EndpointName("CreateCognitiveMemoryCrossProjectPromotion", surface));
    }
}
