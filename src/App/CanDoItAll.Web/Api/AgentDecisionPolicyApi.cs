using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;
using JsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Web.Api;

internal static class AgentDecisionPolicyApi {
    internal const int MaximumRequestBytes = 8192;
    internal const string InvalidRequestCode = "agents.policy-request-invalid";
    internal const string ReadUnavailableCode = "agents.policy-read-unavailable";

    internal static void MapAgentDecisionPolicyApi(this RouteGroupBuilder agents) {
        agents.MapPost("/{agentId:guid}/decision-data-policy/lookup", LookupAsync)
            .WithName("LookupAgentDecisionDataPolicyOperation").WithApiPermission(ApiAccessScopeNames.ReadAgents)
            .Accepts<AgentDecisionPolicyApiSubmission>("application/json")
            .Produces<AgentDecisionPolicyApiLookupResult>().Produces<AgentDecisionPolicyApiLookupResult>(404)
            .Produces<AgentDecisionPolicyApiLookupResult>(409).Produces<AgentDecisionPolicyApiLookupResult>(503)
            .ProducesApiErrors(400, 401, 403, 413, 415)
            .DescribeApi("Look up the original Agent policy save", "Read-only historical receipt lookup under current Agent read permission. Requires the complete original submission. A missing receipt remains unresolved and never authorizes another PUT. Receipts are retained for 30 days; policy writes refuse when 1024 unexpired receipts occupy the log.",
                "The exact historical snapshot and acceptance time, or an explicit unresolved/context/conflict/unavailable result.");
        agents.MapGet("/{agentId:guid}/decision-data-policy", ReadAsync)
            .WithName("GetAgentDecisionDataPolicy")
            .Produces<AgentDecisionPolicyApiSnapshot>().ProducesApiErrors(400, 401, 403, 404, 409, 503)
            .DescribeApi("Read the Agent decision data policy", "Returns the original workspace, Agent and catalog expectation with lossless decimal-string revisions. Null policy means no Agent override. Invalid stored metadata is an error, never inheritance. A current read cannot resolve a prior uncertain write.", "The configured override and original expectation.");
        agents.MapPut("/{agentId:guid}/decision-data-policy", ReplaceAsync)
            .WithName("ReplaceAgentDecisionDataPolicy").WithApiPermission(ApiAccessScopeNames.WriteAgents)
            .Accepts<AgentDecisionPolicyApiSubmission>("application/json")
            .Produces<AgentDecisionPolicyApiResult>().Produces<AgentDecisionPolicyApiResult>(409)
            .Produces<AgentDecisionPolicyApiResult>(503).ProducesApiErrors(400, 401, 403, 413, 415)
            .DescribeApi("Conditionally replace the Agent decision data policy", "Requires the original expectation and a complete version-one override envelope, with null fields for inheritance. Updates future admissions only. Conflicts preserve the original intent; an uncertain outcome must not be automatically retried. Use read-only receipt lookup to investigate an unconfirmed save; PUT does not replay it.", "Exact acceptance, definite rejection or unconfirmed outcome. FollowupRequired preserves known acceptance when postcommit work failed.", "A nonempty operation ID, original expectation and nonnull override; maximum 8192 UTF-8 bytes.");
    }

    private static async Task<IResult> ReadAsync(Guid agentId, IAgentFrameworkWorkspaceService workspace,
        HttpContext context, ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        if (agentId == Guid.Empty) {
            return Invalid(StatusCodes.Status400BadRequest);
        }
        try {
            return Results.Ok(AgentDecisionPolicyApiSnapshot.From(
                await workspace.ReadAgentDecisionDataPolicyAsync(agentId, cancellationToken)));
        } catch (KeyNotFoundException) {
            return ApiAccessEndpoints.Error(StatusCodes.Status404NotFound, "The Agent was not found.", ReadUnavailableCode);
        } catch (AgentDecisionDataPolicyContextChangedException) {
            return ApiAccessEndpoints.Error(StatusCodes.Status409Conflict, "The original workspace is no longer active.", ReadUnavailableCode);
        } catch (UnauthorizedAccessException) {
            return Results.Forbid();
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            Log(logs, agentId, null, failure);
            return ApiAccessEndpoints.Error(StatusCodes.Status503ServiceUnavailable,
                "The Agent policy could not be read. No inherited or replacement policy was assumed.", ReadUnavailableCode);
        }
    }

    private static async Task<IResult> ReplaceAsync(Guid agentId, HttpContext context,
        IOptions<JsonOptions> options, ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        var parsed = await ReadSubmissionAsync(agentId, context, options, cancellationToken);
        if (parsed.Refusal is { } refused) {
            return refused;
        }
        var command = parsed.Command!;
        try {
            var workspace = context.RequestServices.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var outcome = await workspace.ReplaceAgentDecisionDataPolicyAsync(command, cancellationToken);
            if (outcome.Failure is { } failure) {
                logs.CreateLogger(typeof(AgentDecisionPolicyApi).FullName!).LogWarning(
                    "Agent policy operation {OperationId} for Agent {AgentId} returned {OutcomeType} with {FailureStage} diagnostic {FailureTypes}.",
                    command.OperationId, agentId, outcome.GetType().Name, failure.Stage, string.Join(", ", failure.ExceptionTypes));
            }
            return Outcome(command, outcome);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            Log(logs, agentId, command.OperationId, failure);
            return Outcome(command, new AgentDecisionDataPolicySaveOutcome.Unconfirmed(command));
        }
    }

    private static async Task<IResult> LookupAsync(Guid agentId, HttpContext context,
        IOptions<JsonOptions> options, ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        var parsed = await ReadSubmissionAsync(agentId, context, options, cancellationToken);
        if (parsed.Refusal is { } refused) {
            return refused;
        }
        var command = parsed.Command!;
        try {
            var workspace = context.RequestServices.GetRequiredService<IAgentFrameworkWorkspaceService>();
            var lookup = await workspace.LookupAgentDecisionDataPolicyAsync(command, cancellationToken);
            if (lookup is AgentDecisionDataPolicyLookup.Accepted accepted) {
                if (accepted.Receipt.Command != command) {
                    throw new InvalidOperationException("The historical receipt belongs to another policy submission.");
                }
                return Results.Ok(new AgentDecisionPolicyApiLookupResult(AgentDecisionPolicyApiLookupOutcome.HistoricalAccepted,
                    command.OperationId, AgentDecisionPolicyApiSnapshot.From(accepted.Receipt.Snapshot), accepted.Receipt.AcceptedAtUtc));
            }
            if (lookup is AgentDecisionDataPolicyLookup.Unavailable { Failure: { } failure }) {
                logs.CreateLogger(typeof(AgentDecisionPolicyApi).FullName!).LogWarning(
                    "Agent policy lookup {OperationId} for Agent {AgentId} failed at {FailureStage} with {FailureTypes}.",
                    command.OperationId, agentId, failure.Stage, string.Join(", ", failure.ExceptionTypes));
            }
            var result = lookup switch {
                AgentDecisionDataPolicyLookup.NotObserved => (AgentDecisionPolicyApiLookupOutcome.NotObserved, StatusCodes.Status404NotFound),
                AgentDecisionDataPolicyLookup.Conflict => (AgentDecisionPolicyApiLookupOutcome.Conflict, StatusCodes.Status409Conflict),
                AgentDecisionDataPolicyLookup.ContextChanged => (AgentDecisionPolicyApiLookupOutcome.ContextChanged, StatusCodes.Status409Conflict),
                AgentDecisionDataPolicyLookup.Unavailable => (AgentDecisionPolicyApiLookupOutcome.Unavailable, StatusCodes.Status503ServiceUnavailable),
                _ => throw new InvalidOperationException("Unknown Agent policy lookup outcome.")
            };
            return Results.Json(new AgentDecisionPolicyApiLookupResult(result.Item1, command.OperationId), statusCode: result.Item2);
        } catch (Exception failure) when (failure is not OutOfMemoryException) {
            Log(logs, agentId, command.OperationId, failure);
            return Results.Json(new AgentDecisionPolicyApiLookupResult(AgentDecisionPolicyApiLookupOutcome.Unavailable, command.OperationId),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<(ReplaceAgentDecisionDataPolicyCommand? Command, IResult? Refusal)> ReadSubmissionAsync(
        Guid agentId, HttpContext context, IOptions<JsonOptions> options, CancellationToken cancellationToken) {
        try {
            if (!context.Request.HasJsonContentType()) {
                return (null, Invalid(StatusCodes.Status415UnsupportedMediaType));
            }
            if (context.Request.ContentLength > MaximumRequestBytes) {
                return (null, Invalid(StatusCodes.Status413PayloadTooLarge));
            }
            var bytes = new byte[MaximumRequestBytes + 1];
            var length = await context.Request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
            if (length > MaximumRequestBytes) {
                return (null, Invalid(StatusCodes.Status413PayloadTooLarge));
            }
            using var document = JsonDocument.Parse(bytes.AsMemory(0, length), new() { MaxDepth = 8 });
            AgentApiRequestValidation.RequireUniqueMembers(document.RootElement);
            var json = new JsonSerializerOptions(options.Value.SerializerOptions) {
                PropertyNameCaseInsensitive = false, MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true
            };
            var submission = document.Deserialize<AgentDecisionPolicyApiSubmission>(json)
                ?? throw new JsonException("A policy submission is required.");
            return (submission.ToDomain(agentId), null);
        } catch (Exception failure) when (failure is JsonException or ArgumentException) {
            return (null, Invalid(StatusCodes.Status400BadRequest));
        }
    }

    private static IResult Outcome(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySaveOutcome outcome)
        => outcome switch {
            AgentDecisionDataPolicySaveOutcome.Accepted accepted when accepted.Command == command => Results.Ok(new AgentDecisionPolicyApiResult(
                AgentDecisionPolicyApiOutcome.Accepted, command.OperationId, AgentDecisionPolicyApiSnapshot.From(accepted.Snapshot),
                FollowupRequired: accepted.FollowupRequired)),
            AgentDecisionDataPolicySaveOutcome.Rejected rejected => Results.Json(new AgentDecisionPolicyApiResult(
                AgentDecisionPolicyApiOutcome.Rejected, command.OperationId, Rejection: rejected.Reason),
                statusCode: rejected.Reason == AgentDecisionDataPolicyRejection.Unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status409Conflict),
            AgentDecisionDataPolicySaveOutcome.Unconfirmed => Results.Json(new AgentDecisionPolicyApiResult(
                AgentDecisionPolicyApiOutcome.Unconfirmed, command.OperationId), statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("Unknown Agent policy save outcome.")
        };

    private static IResult Invalid(int status) => ApiAccessEndpoints.Error(status,
        "The Agent policy request is invalid; no write was dispatched.", InvalidRequestCode);

    private static void Log(ILoggerFactory logs, Guid agentId, Guid? operationId, Exception failure)
        => logs.CreateLogger(typeof(AgentDecisionPolicyApi).FullName!).LogWarning(
            "Agent policy access for Agent {AgentId}, operation {OperationId}, failed with {FailureType}.",
            agentId, operationId, failure.GetType().FullName);
}
