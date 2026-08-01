using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class AgentExternalProvisioningApi
{
    public static RouteGroupBuilder MapAgentExternalProvisioningApi(this RouteGroupBuilder agents)
    {
        agents.MapGet("/by-external-key/{externalNamespace}/{key}", GetAsync)
            .WithName("GetAgentByExternalKey")
            .Produces<AgentExternalProvisioningResource>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapPut("/by-external-key/{externalNamespace}/{key}", UpsertAsync)
            .WithName("ProvisionAgentByExternalKey")
            .Accepts<AgentEditorModel>("application/json")
            .Produces<AgentExternalProvisioningReceipt>(StatusCodes.Status200OK)
            .Produces<AgentExternalProvisioningReceipt>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status412PreconditionFailed)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapDelete("/by-external-key/{externalNamespace}/{key}", ArchiveAsync)
            .WithName("ArchiveAgentByExternalKey")
            .Produces<AgentExternalProvisioningReceipt>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status412PreconditionFailed)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return agents;
    }

    private static async Task<IResult> GetAsync(
        string externalNamespace,
        string key,
        HttpResponse response,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var resource = await workspaceService.GetAgentByExternalKeyAsync(
                externalNamespace,
                key,
                cancellationToken);
            response.Headers.ETag = QuoteEtag(resource.ConfigurationVersion);
            return Results.Ok(resource);
        }
        catch (AgentExternalProvisioningException exception)
        {
            return Error(exception);
        }
    }

    private static async Task<IResult> UpsertAsync(
        string externalNamespace,
        string key,
        AgentEditorModel request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await workspaceService.ProvisionAgentByExternalKeyAsync(
                new AgentExternalProvisioningCommand(
                    externalNamespace,
                    key,
                    idempotencyKey ?? string.Empty,
                    ifMatch,
                    request),
                cancellationToken);
            response.Headers.ETag = QuoteEtag(receipt.ConfigurationVersion);
            if (!receipt.Created)
            {
                return Results.Ok(receipt);
            }

            var location = $"/api/agents/by-external-key/{Uri.EscapeDataString(receipt.Namespace)}/{Uri.EscapeDataString(receipt.Key)}";
            return Results.Created(location, receipt);
        }
        catch (AgentExternalProvisioningException exception)
        {
            return Error(exception);
        }
        catch (InvalidOperationException exception)
        {
            return Error(
                StatusCodes.Status409Conflict,
                "agents.external-key-configuration-conflict",
                exception.Message);
        }
    }

    private static async Task<IResult> ArchiveAsync(
        string externalNamespace,
        string key,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        HttpResponse response,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        try
        {
            var receipt = await workspaceService.ArchiveAgentByExternalKeyAsync(
                new AgentExternalArchiveCommand(
                    externalNamespace,
                    key,
                    idempotencyKey ?? string.Empty,
                    ifMatch),
                cancellationToken);
            response.Headers.ETag = QuoteEtag(receipt.ConfigurationVersion);
            return Results.Ok(receipt);
        }
        catch (AgentExternalProvisioningException exception)
        {
            return Error(exception);
        }
    }

    private static string QuoteEtag(string configurationVersion)
        => $"\"{configurationVersion}\"";

    private static IResult Error(AgentExternalProvisioningException exception)
    {
        var statusCode = exception.Kind switch
        {
            AgentExternalProvisioningFailureKind.NotFound => StatusCodes.Status404NotFound,
            AgentExternalProvisioningFailureKind.Conflict => StatusCodes.Status409Conflict,
            AgentExternalProvisioningFailureKind.PreconditionFailed =>
                StatusCodes.Status412PreconditionFailed,
            _ => StatusCodes.Status400BadRequest
        };
        return Error(statusCode, exception.Code, exception.Message);
    }

    private static IResult Error(int statusCode, string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse([new ApiErrorItem(code, message, ErrorSeverity.Error)]),
            statusCode: statusCode);
    }
}
