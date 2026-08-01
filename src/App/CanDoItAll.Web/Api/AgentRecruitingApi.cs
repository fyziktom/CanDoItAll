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
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
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

    private static async Task<IResult> CreateInterviewAsync(
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

    private static async Task<IResult> AppendAttemptAsync(
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

    private static async Task<IResult> AppendReviewAsync(
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

    private static async Task<IResult> GetInterviewAsync(
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

    private static async Task<IResult> ListCandidateInterviewsAsync(
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

    private static async Task<IResult> GetReadinessAsync(
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
