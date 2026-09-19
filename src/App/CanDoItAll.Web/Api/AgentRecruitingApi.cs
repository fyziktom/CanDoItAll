using System.Security.Claims;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal static class AgentRecruitingApi
{
    public static RouteGroupBuilder MapAgentRecruitingApi(this RouteGroupBuilder group)
    {
        var recruiting = group.MapGroup("/agent-recruiting")
            .WithTags("Agent Recruiting");

        recruiting.MapPost("/interviews", CreateInterviewAsync)
            .WithName("CreateAgentRecruitingInterview")
            .Accepts<CreateAgentRecruitingInterviewCommand>("application/json")
            .Produces<AgentRecruitingInterview>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        recruiting.MapPost("/interviews/{interviewId:guid}/attempts", AppendAttemptAsync)
            .WithName("AppendAgentRecruitingAttempt")
            .Accepts<AppendAgentRecruitingAttemptCommand>("application/json")
            .Produces<AgentRecruitingInterview>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        recruiting.MapPost("/interviews/{interviewId:guid}/reviews", AppendReviewAsync)
            .WithName("AppendAgentRecruitingHumanReview")
            .Accepts<AppendAgentRecruitingReviewCommand>("application/json")
            .Produces<AgentRecruitingInterview>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        recruiting.MapGet("/interviews/{interviewId:guid}", GetInterviewAsync)
            .WithName("GetAgentRecruitingInterview")
            .Produces<AgentRecruitingInterview>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        recruiting.MapGet(
                "/candidates/{candidateAgentId:guid}/interviews",
                ListCandidateInterviewsAsync)
            .WithName("ListAgentRecruitingCandidateInterviews")
            .Produces<AgentRecruitingInterview[]>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        recruiting.MapGet("/candidates/{agentId:guid}/readiness", GetReadinessAsync)
            .WithName("GetAgentRecruitingCandidateReadiness")
            .Produces<AgentRecruitingCandidateReadiness>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return group;
    }

    /// <summary>
    /// Open an agent recruiting interview that assesses a candidate agent's current configuration.
    /// </summary>
    /// <remarks>
    /// Creates an interview, the container of assessment evidence for one candidate agent. The interview is fenced to
    /// the candidate's configuration at creation: readiness counts only interviews whose recorded configuration
    /// version equals the agent's current one, so evidence gathered before the agent's configuration changes stops
    /// counting afterwards. This is not a CRM/HR recruitment interview of a person
    /// (<c>POST /api/crm-hr/recruiting/interviews</c>).
    ///
    /// Typical sequence:
    ///
    /// 1. Read <c>GET /api/agent-recruiting/candidates/{agentId}/readiness</c> and send its
    /// <c>currentConfigurationVersion</c> as <c>candidateConfigurationVersion</c>.
    /// 2. Run the assessment as an agent execution run, workflow run or process run in which the candidate takes
    /// part, and wait until the run has finished.
    /// 3. Record the result with <c>POST /api/agent-recruiting/interviews/{interviewId}/attempts</c>.
    /// 4. Have a human reviewer approve or reject the attempt with
    /// <c>POST /api/agent-recruiting/interviews/{interviewId}/reviews</c>.
    /// 5. Read the readiness again.
    ///
    /// An interview changes only by appending attempts and reviews; there is no update or delete operation. Each
    /// request creates another interview, so after a lost response list the candidate's interviews before retrying.
    /// Recruiting evidence never activates or changes the agent. Text limits count UTF-16 code units after trimming.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="request">The candidate, its current configuration version, the purpose and optional links.</param>
    /// <response code="201">
    /// The interview was created. The body is the new interview, with empty <c>attempts</c> and <c>reviews</c>; the
    /// <c>Location</c> header is <c>/api/agent-recruiting/interviews/{interviewId}</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was created: the empty GUID as candidate, recruitment application or project identifier
    /// (<c>agent-recruiting.identifier-invalid</c>), or a missing or blank configuration version or purpose, a version
    /// longer than 128 or a purpose longer than 500 characters (<c>agent-recruiting.text-invalid</c>). A body the
    /// framework cannot bind is rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    /// <response code="404">
    /// The candidate agent does not exist in the current workspace (<c>agent-recruiting.candidate-not-found</c>).
    /// </response>
    /// <response code="409">
    /// <c>candidateConfigurationVersion</c> is not the candidate's current configuration version
    /// (<c>agent-recruiting.candidate-version-conflict</c>). Read the readiness again and use its
    /// <c>currentConfigurationVersion</c>.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> CreateInterviewAsync(
        CreateAgentRecruitingInterviewCommand request,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var interview = await service.CreateInterviewAsync(request, cancellationToken);
            return Results.Created(
                $"/api/agent-recruiting/interviews/{interview.Id:D}",
                interview);
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    /// <summary>
    /// Append an assessment attempt, the recorded result of a finished run, to an agent recruiting interview.
    /// </summary>
    /// <remarks>
    /// Records one attempt of the candidate: the run whose result is assessed (<c>target</c>), the challenge and
    /// rubric used, integrity hashes of input and output, the automated evaluation and an optional structured
    /// analysis. The server checks that the target run exists in the current workspace and that the interview's
    /// candidate took part in it: as the run's agent (<c>agent-execution-run</c>), as the agent of a workflow node
    /// that executed in the run (<c>workflow-run</c>) or as a recorded participant of the run
    /// (<c>process-run</c>). It stores the supplied hashes and evaluation without recomputing them.
    ///
    /// Missing evidence does not reject the attempt: it is stored with <c>completeness</c> <c>Incomplete</c> and
    /// <c>missingEvidence</c> naming what is missing, and it stays incomplete. Only complete attempts count for
    /// readiness. Append the attempt after the target run has finished (an agent execution run completed or failed;
    /// a workflow or process run completed, failed or cancelled); an attempt recorded while the run is still active
    /// is incomplete (<c>terminal-execution-target</c>). To replace an incomplete attempt, append a new one.
    ///
    /// Attempts are numbered 1, 2 and so on per interview in append order and cannot be changed or deleted. Each
    /// request appends another attempt, so after a lost response read the interview before retrying. Text limits
    /// count UTF-16 code units after trimming.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="interviewId">
    /// Identifier of the interview, as returned by <c>POST /api/agent-recruiting/interviews</c> or the candidate's
    /// interview list.
    /// </param>
    /// <param name="request">The target run, challenge, rubric, hashes, evaluation and optional analysis.</param>
    /// <response code="201">
    /// The attempt was appended. The body is the whole interview with the new attempt last in <c>attempts</c>; the
    /// <c>Location</c> header is <c>/api/agent-recruiting/interviews/{interviewId}#attempt-{attemptId}</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was appended: an empty GUID (<c>agent-recruiting.identifier-invalid</c>); a missing target, an empty
    /// target identifier or an unsupported kind value (<c>agent-recruiting.target-invalid</c>); a text outside its
    /// limits (<c>agent-recruiting.text-invalid</c>); a hash that is not SHA-256
    /// (<c>agent-recruiting.hash-invalid</c>); a score outside 0 to 100 (<c>agent-recruiting.score-invalid</c>); or an
    /// invalid analysis
    /// (<c>agent-recruiting.analysis-classification-invalid</c>, <c>agent-recruiting.analysis-next-step-invalid</c>,
    /// <c>agent-recruiting.analysis-confidence-invalid</c>, <c>agent-recruiting.analysis-items-invalid</c>). A body the
    /// framework cannot bind, for example an unknown enum text, is rejected with HTTP 400 before the operation runs
    /// and has no error envelope.
    /// </response>
    /// <response code="404">
    /// Nothing was appended: the interview (<c>agent-recruiting.interview-not-found</c>), the target run
    /// (<c>agent-recruiting.target-not-found</c>), the evaluator agent
    /// (<c>agent-recruiting.evaluator-agent-not-found</c>) or the evaluator provider profile
    /// (<c>agent-recruiting.evaluator-provider-not-found</c>) does not exist in the current workspace.
    /// </response>
    /// <response code="409">
    /// Nothing was appended: the target run shows no participation of the candidate
    /// (<c>agent-recruiting.target-candidate-conflict</c>), the evaluation's rubric version differs from the attempt's
    /// (<c>agent-recruiting.rubric-version-conflict</c>), or another attempt was appended at the same time
    /// (<c>agent-recruiting.concurrent-append-conflict</c>; read the interview before retrying).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> AppendAttemptAsync(
        Guid interviewId,
        AppendAgentRecruitingAttemptCommand request,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var interview = await service.AppendAttemptAsync(
                interviewId,
                request,
                cancellationToken);
            var attempt = interview.Attempts[^1];
            return Results.Created(
                $"/api/agent-recruiting/interviews/{interview.Id:D}#attempt-{attempt.Id:D}",
                interview);
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    /// <summary>
    /// Append a human reviewer's approval or rejection of an assessment attempt.
    /// </summary>
    /// <remarks>
    /// Records a human decision on one attempt of the interview. An approval qualifies the attempt for readiness only
    /// when it carries both <c>authorizationReference</c> and <c>authorizationEvidenceHash</c>; otherwise it is stored
    /// with <c>qualifiesForReadiness</c> false and <c>missingEvidence</c> naming what is missing. Readiness uses the
    /// latest review of the latest attempt for the candidate's current configuration, so a new review of an attempt
    /// supersedes its earlier reviews, which stay in the history. Reviews cannot be changed or deleted, and each
    /// request appends another review; after a lost response read the interview before retrying.
    ///
    /// Authority: this operation always needs an authenticated reviewer, also when API authorization is disabled (the
    /// development default), where it therefore always fails with HTTP 401. Send a bearer token issued by this host
    /// whose scopes include <c>agent-recruiting.review</c>; the <c>api</c> scope alone is not enough. The stored
    /// reviewer identity comes from the token: <c>reviewerActorId</c> is the token subject and
    /// <c>reviewerDisplayName</c> its name claim, or the supplied name when the token has none.
    ///
    /// Text limits count UTF-16 code units after trimming.
    /// </remarks>
    /// <param name="interviewId">
    /// Identifier of the interview that contains the reviewed attempt, as returned by
    /// <c>POST /api/agent-recruiting/interviews</c> or the candidate's interview list.
    /// </param>
    /// <param name="request">The reviewed attempt, the decision, the authorization evidence and notes.</param>
    /// <response code="201">
    /// The review was appended. The body is the whole interview with the new review last in <c>reviews</c>; the
    /// <c>Location</c> header is <c>/api/agent-recruiting/interviews/{interviewId}#review-{reviewId}</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was appended: an empty GUID (<c>agent-recruiting.identifier-invalid</c>), a text outside its limits
    /// (<c>agent-recruiting.text-invalid</c>), an evidence hash that is not SHA-256
    /// (<c>agent-recruiting.hash-invalid</c>), a token without a subject
    /// (<c>agent-recruiting.reviewer-identity-missing</c>) or a <c>reviewerActorId</c> that differs from the token
    /// subject (<c>agent-recruiting.reviewer-identity-conflict</c>). A body the framework cannot bind is rejected with
    /// HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    /// <response code="404">
    /// Nothing was appended: the interview (<c>agent-recruiting.interview-not-found</c>) or the attempt within it
    /// (<c>agent-recruiting.attempt-not-found</c>) does not exist.
    /// </response>
    /// <response code="401">
    /// No authenticated reviewer: <c>agent-recruiting.reviewer-authorization-required</c> (always when API
    /// authorization is disabled), or <c>api.authorization-required</c> when it is enabled and no valid bearer token
    /// was sent.
    /// </response>
    /// <response code="403">
    /// The bearer token does not include the <c>agent-recruiting.review</c> scope
    /// (<c>agent-recruiting.reviewer-scope-required</c>).
    /// </response>
    internal static async Task<IResult> AppendReviewAsync(
        Guid interviewId,
        AppendAgentRecruitingReviewCommand request,
        HttpContext httpContext,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            return ApiEndpointResults.Unauthorized(
                "An authenticated reviewer is required to append a human recruiting decision.",
                "agent-recruiting.reviewer-authorization-required");
        }

        if (!HasScope(
                httpContext.User,
                AgentRecruitingAuthorizationScopes.HumanReview))
        {
            return ApiEndpointResults.Forbidden(
                $"The reviewer token must include the '{AgentRecruitingAuthorizationScopes.HumanReview}' scope.",
                "agent-recruiting.reviewer-scope-required");
        }

        try
        {
            request = BindAuthenticatedReviewer(request, httpContext.User);
            var interview = await service.AppendReviewAsync(
                interviewId,
                request,
                cancellationToken);
            var review = interview.Reviews[^1];
            return Results.Created(
                $"/api/agent-recruiting/interviews/{interview.Id:D}#review-{review.Id:D}",
                interview);
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    /// <summary>
    /// Read an agent recruiting interview with all of its attempts and reviews.
    /// </summary>
    /// <remarks>
    /// Returns the interview as stored, with attempts and reviews in append order. Use it to confirm whether an append
    /// took effect after a lost response, and <c>GET /api/agent-recruiting/candidates/{agentId}/readiness</c> for the
    /// conclusion drawn from all interviews of the candidate.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="interviewId">
    /// Identifier of the interview, as returned by <c>POST /api/agent-recruiting/interviews</c> or the candidate's
    /// interview list.
    /// </param>
    /// <response code="200">The interview with its attempts and reviews.</response>
    /// <response code="400">
    /// The interview identifier is the empty GUID (<c>agent-recruiting.identifier-invalid</c>).
    /// </response>
    /// <response code="404">
    /// No interview with this identifier exists in the current workspace (<c>agent-recruiting.interview-not-found</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> GetInterviewAsync(
        Guid interviewId,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await service.GetInterviewAsync(interviewId, cancellationToken));
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    /// <summary>
    /// List the agent recruiting interviews of a candidate agent, newest first.
    /// </summary>
    /// <remarks>
    /// Returns every interview of the candidate in the current workspace, with attempts and reviews, ordered by
    /// creation time from newest to oldest (ties by descending identifier). The list is not paged and includes
    /// interviews for earlier configurations of the agent: compare their <c>candidateConfigurationVersion</c> with
    /// the <c>currentConfigurationVersion</c> of the readiness. The candidate is not looked up, so an unknown agent
    /// gives an empty array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="candidateAgentId">Identifier of the candidate agent (not the empty GUID).</param>
    /// <param name="recruitmentApplicationId">
    /// Optional identifier of a CRM/HR recruitment application; when set, only interviews created with this
    /// <c>recruitmentApplicationId</c> are returned. Not the empty GUID.
    /// </param>
    /// <response code="200">The interviews, newest first; an empty array when there are none.</response>
    /// <response code="400">
    /// An identifier is the empty GUID (<c>agent-recruiting.identifier-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> ListCandidateInterviewsAsync(
        Guid candidateAgentId,
        Guid? recruitmentApplicationId,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var interviews = await service.ListCandidateInterviewsAsync(
                candidateAgentId,
                recruitmentApplicationId,
                cancellationToken);
            return Results.Ok(interviews.ToArray());
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    /// <summary>
    /// Evaluate whether a candidate agent's current configuration is ready, from its recruiting evidence.
    /// </summary>
    /// <remarks>
    /// Derives readiness on every call from the candidate's interviews, optionally only those of one recruitment
    /// application, and from the agent's current configuration version:
    ///
    /// 1. <c>NoInterviews</c> when the candidate has no interview in scope.
    /// 2. <c>IncompleteEvidence</c> when no complete attempt exists for the current configuration.
    /// 3. Otherwise the latest attempt for the current configuration decides: <c>Rejected</c> when its latest review
    /// is a rejection, <c>IncompleteEvidence</c> when it is incomplete, <c>Ready</c> when its automated evaluation
    /// <c>Passed</c> and its latest review is a qualifying approval, and <c>AwaitingHumanApproval</c> in every other
    /// case.
    ///
    /// <c>Ready</c> names the qualifying interview, attempt and review and the approval's authorization evidence. The
    /// result is evidence for a separate activation decision: reading it never activates or changes the agent. It
    /// also returns <c>currentConfigurationVersion</c>, which <c>POST /api/agent-recruiting/interviews</c> requires.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="agentId">
    /// Identifier of the candidate agent (not the empty GUID), as listed by <c>GET /api/agents</c>.
    /// </param>
    /// <param name="recruitmentApplicationId">
    /// Optional identifier of a CRM/HR recruitment application; when set, only interviews created with this
    /// <c>recruitmentApplicationId</c> are considered. Not the empty GUID.
    /// </param>
    /// <response code="200">The readiness of the candidate's current configuration and the attempt history.</response>
    /// <response code="400">
    /// An identifier is the empty GUID (<c>agent-recruiting.identifier-invalid</c>).
    /// </response>
    /// <response code="404">
    /// The candidate agent does not exist in the current workspace (<c>agent-recruiting.candidate-not-found</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> GetReadinessAsync(
        Guid agentId,
        Guid? recruitmentApplicationId,
        IAgentRecruitingEvidenceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(
                await service.GetCandidateReadinessAsync(
                    agentId,
                    recruitmentApplicationId,
                    cancellationToken));
        }
        catch (AgentRecruitingEvidenceException exception)
        {
            return Error(exception);
        }
    }

    private static IResult Error(AgentRecruitingEvidenceException exception)
    {
        var statusCode = exception.Kind switch
        {
            AgentRecruitingEvidenceFailureKind.NotFound => StatusCodes.Status404NotFound,
            AgentRecruitingEvidenceFailureKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Json(
            new ApiErrorResponse(
                [new ApiErrorItem(exception.Code, exception.Message, ErrorSeverity.Error)]),
            statusCode: statusCode);
    }

    private static AppendAgentRecruitingReviewCommand BindAuthenticatedReviewer(
        AppendAgentRecruitingReviewCommand request,
        ClaimsPrincipal principal)
    {
        var actorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new AgentRecruitingEvidenceException(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.reviewer-identity-missing",
                "The authenticated reviewer token does not contain a subject.");
        if (!string.IsNullOrWhiteSpace(request.ReviewerActorId) &&
            !string.Equals(request.ReviewerActorId.Trim(), actorId, StringComparison.Ordinal))
        {
            throw new AgentRecruitingEvidenceException(
                AgentRecruitingEvidenceFailureKind.InvalidRequest,
                "agent-recruiting.reviewer-identity-conflict",
                "The reviewer actor must match the authenticated API subject.");
        }

        var displayName = principal.Identity?.Name
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("name")
            ?? request.ReviewerDisplayName;
        return request with
        {
            ReviewerActorId = actorId,
            ReviewerDisplayName = displayName
        };
    }

    private static bool HasScope(ClaimsPrincipal principal, string requiredScope)
    {
        return principal.Claims
            .Where(claim => claim.Type is "scope" or "scopes")
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
    }
}
