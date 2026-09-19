using System.ComponentModel;
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

    /// <summary>
    /// Read which agent an external identity (namespace and key) is bound to.
    /// </summary>
    /// <remarks>
    /// Resolves a caller-defined external identity to the agent bound to it by
    /// <c>PUT /api/agents/by-external-key/{externalNamespace}/{key}</c> or by a package import
    /// (<c>POST /api/agents/import-package</c>), and returns the binding's configuration version, also as the quoted
    /// <c>ETag</c> response header. Send that value as <c>If-Match</c> with the next <c>PUT</c> or <c>DELETE</c>
    /// through this identity. Read the agent itself with <c>GET /api/agents/{agentId}</c>.
    ///
    /// The namespace and key are trimmed and lowered before the lookup; each must then be 1 to 100 characters of
    /// lowercase letters, digits, <c>.</c>, <c>_</c> or <c>-</c>, starting and ending with a letter or digit. Deleting
    /// the agent with <c>DELETE /api/agents/{agentId}</c> also removes its bindings.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="externalNamespace">
    /// Namespace of the external identity, chosen by the caller when provisioning (<c>package-import</c> for package
    /// imports without a namespace), for example <c>erp-sync</c>.
    /// </param>
    /// <param name="key">
    /// Key of the external identity within the namespace, for example <c>invoice-assistant</c>.
    /// </param>
    /// <response code="200">
    /// The binding: the bound agent, the recorded configuration version (also in the <c>ETag</c> header), whether the
    /// agent is archived and when the binding last changed.
    /// </response>
    /// <response code="400">The namespace or key has an invalid format (<c>agents.external-key-invalid</c>).</response>
    /// <response code="404">
    /// No agent is bound to the identity in this workspace (<c>agents.external-key-not-found</c>), or the binding
    /// points to an agent that no longer exists (<c>agents.external-key-agent-not-found</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> GetAsync(
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

    /// <summary>
    /// Create or update the agent bound to an external identity, idempotently and with optimistic concurrency.
    /// </summary>
    /// <remarks>
    /// Provisions an agent under a caller-defined external identity (namespace and key), so that an external system
    /// can manage it by its own key. When no agent is bound to the identity, a new agent is created and bound (HTTP
    /// 201). Otherwise the bound agent's configuration is replaced by the body (HTTP 200): the body is the complete
    /// desired configuration, not a patch, and omitted members take their default values. Use <c>POST /api/agents</c>
    /// to save an agent without an external identity.
    ///
    /// The body has the shape that <c>POST /api/agents</c> accepts, with these rules: <c>id</c> and
    /// <c>expectedUpdatedAtUtc</c> must be omitted or null (the server owns the identity and revision);
    /// <c>name</c> must not be blank; <c>allowedSecretReferences</c> and <c>permissions.allowedSecrets</c> must be
    /// empty; <c>selectedCapabilityIds</c> and <c>tags</c> must not be null; <c>configurationJson</c> must be empty or
    /// valid JSON without raw secret values (a non-empty string <c>apiKey</c>, <c>accessToken</c>,
    /// <c>bearerToken</c>, <c>clientSecret</c>, <c>password</c>, <c>privateKey</c> or <c>refreshToken</c> member,
    /// also inside string members whose names end in <c>Json</c>). Capability identifiers that do not exist in the
    /// workspace are ignored.
    ///
    /// Concurrency: omit <c>If-Match</c> when creating. When updating, send the configuration version from the last
    /// <c>GET</c> or receipt as <c>If-Match</c>; a missing or stale value is rejected with HTTP 412 and nothing is
    /// written. If the new configuration equals the recorded one, nothing is changed and the receipt's
    /// <c>warnings</c> say so. The response's <c>ETag</c> header carries the resulting configuration version in
    /// quotes.
    ///
    /// Idempotency: <c>Idempotency-Key</c> is required. Repeating the same request (identity, <c>If-Match</c> and
    /// body) with the same key returns the stored receipt with <c>replayed</c> true and the original status, and
    /// writes nothing again; the same key with a different request is rejected with HTTP 409. Keys are shared with
    /// <c>DELETE</c> on this route, and the last 2048 writes of the workspace are remembered. An HTTP 500 can follow a
    /// stored change when the CRM/HR directory refresh after the save fails: resend the identical request with the same
    /// key to receive the stored receipt (the replay does not repeat the refresh).
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="externalNamespace">
    /// Namespace of the external identity, chosen by the caller, for example <c>erp-sync</c>. It is trimmed and
    /// lowered; it must then be 1 to 100 characters of lowercase letters, digits, <c>.</c>, <c>_</c> or <c>-</c>,
    /// starting and ending with a letter or digit.
    /// </param>
    /// <param name="key">
    /// Key of the external identity within the namespace, for example <c>invoice-assistant</c>, in the same format as
    /// the namespace.
    /// </param>
    /// <param name="request">
    /// The complete agent definition to store under this identity, following the rules above.
    /// </param>
    /// <response code="200">
    /// The bound agent was updated, or already had this configuration (see <c>warnings</c>), or the request repeated
    /// an earlier update (<c>replayed</c> true). The body is the receipt; the <c>ETag</c> header carries its
    /// configuration version.
    /// </response>
    /// <response code="201">
    /// A new agent was created and bound to the identity, or the request repeated that creation (<c>replayed</c>
    /// true). The <c>Location</c> header is this route with the normalized namespace and key; the <c>ETag</c> header
    /// carries the configuration version.
    /// </response>
    /// <response code="400">
    /// Nothing was written. The identity or headers are invalid (<c>agents.external-key-invalid</c>,
    /// <c>agents.external-key-idempotency-key-invalid</c> for a missing, blank or longer than 200 characters key,
    /// <c>agents.external-key-version-invalid</c> for an <c>If-Match</c> that is not 64 hexadecimal digits), or the
    /// body breaks a rule above (<c>agents.external-key-agent-invalid</c>,
    /// <c>agents.external-key-server-identity-only</c>, <c>agents.external-key-secret-reference-forbidden</c>,
    /// <c>agents.external-key-configuration-json-invalid</c>, <c>agents.external-key-secret-material-forbidden</c>).
    /// A body the framework cannot bind is rejected with HTTP 400 before the operation runs and has no error
    /// envelope.
    /// </response>
    /// <response code="409">
    /// Nothing was written: the <c>Idempotency-Key</c> was already used for a different request
    /// (<c>agents.external-key-idempotency-conflict</c>), the binding points to an agent that no longer exists
    /// (<c>agents.external-key-agent-missing</c>), or the definition breaks an agent rule, for example a template key
    /// that another agent uses, an unsupported thinking-effort override or a model without a price row on its
    /// provider profile (<c>agents.external-key-configuration-conflict</c>, explained in the message).
    /// </response>
    /// <response code="412">
    /// Nothing was written: <c>If-Match</c> was sent for an identity that is not bound yet
    /// (<c>agents.external-key-create-version-conflict</c>), is missing for a bound identity
    /// (<c>agents.external-key-version-required</c>) or is stale (<c>agents.external-key-version-conflict</c>). Read
    /// the binding again, then decide whether to reapply the change.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> UpsertAsync(
        string externalNamespace,
        string key,
        AgentEditorModel request,
        [FromHeader(Name = "Idempotency-Key")]
        [Description(
            "Required. Client-chosen key of this write, 1 to 200 characters after trimming, compared exactly. " +
            "Repeating the same request with the same key returns the stored receipt; reusing the key for a " +
            "different request, including a DELETE on this route, is rejected with " +
            "agents.external-key-idempotency-conflict.")]
        string? idempotencyKey,
        [FromHeader(Name = "If-Match")]
        [Description(
            "Configuration version the update is based on, from the ETag or configurationVersion of the last read " +
            "or receipt: 64 hexadecimal digits, surrounding quotes allowed. Omit it when the identity is not bound " +
            "yet; required when updating.")]
        string? ifMatch,
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

    /// <summary>
    /// Archive the agent bound to an external identity.
    /// </summary>
    /// <remarks>
    /// Changes the status of the agent bound to the external identity to archived. The agent, its history and the
    /// binding are kept: <c>GET</c> on this route still resolves it, with <c>isArchived</c> true, and a later
    /// <c>PUT</c> can change its status again. To remove an agent permanently use
    /// <c>DELETE /api/agents/{agentId}</c>, which also removes its bindings.
    ///
    /// <c>If-Match</c> is required and must equal the binding's current configuration version; a missing or stale
    /// value is rejected with HTTP 412 and nothing is written. The response's <c>ETag</c> header carries the
    /// configuration version after archiving. Archiving an agent that is already archived succeeds again and keeps
    /// the same configuration version.
    ///
    /// Idempotency: <c>Idempotency-Key</c> is required. Repeating the same request (identity and <c>If-Match</c>) with
    /// the same key returns the stored receipt with <c>replayed</c> true and writes nothing again; the same key with a
    /// different request is rejected with HTTP 409. Keys are shared with <c>PUT</c> on this route. An HTTP 500 can
    /// follow a stored change when the CRM/HR directory refresh after the save fails: resend the identical request with
    /// the same key to receive the stored receipt (the replay does not repeat the refresh).
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="externalNamespace">
    /// Namespace of the external identity, for example <c>erp-sync</c>; trimmed and lowered before the lookup.
    /// </param>
    /// <param name="key">
    /// Key of the external identity within the namespace, for example <c>invoice-assistant</c>; trimmed and lowered
    /// before the lookup.
    /// </param>
    /// <response code="200">
    /// The agent is archived. The receipt has <c>archived</c> true and the configuration version, also in the
    /// <c>ETag</c> header; <c>replayed</c> is true for a repeated request.
    /// </response>
    /// <response code="400">
    /// Nothing was written: the namespace or key has an invalid format (<c>agents.external-key-invalid</c>), the
    /// <c>Idempotency-Key</c> is missing, blank or longer than 200 characters
    /// (<c>agents.external-key-idempotency-key-invalid</c>), or <c>If-Match</c> is not 64 hexadecimal digits
    /// (<c>agents.external-key-version-invalid</c>).
    /// </response>
    /// <response code="404">
    /// No agent is bound to the identity in this workspace (<c>agents.external-key-not-found</c>).
    /// </response>
    /// <response code="409">
    /// Nothing was written: the <c>Idempotency-Key</c> was already used for a different request
    /// (<c>agents.external-key-idempotency-conflict</c>), or the binding points to an agent that no longer exists
    /// (<c>agents.external-key-agent-missing</c>).
    /// </response>
    /// <response code="412">
    /// Nothing was written: <c>If-Match</c> is missing (<c>agents.external-key-version-required</c>) or stale
    /// (<c>agents.external-key-version-conflict</c>). Read the binding again before retrying.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// An authorization policy rejected the authenticated caller (<c>api.authorization-forbidden</c>). This operation
    /// itself requires only a valid bearer token.
    /// </response>
    internal static async Task<IResult> ArchiveAsync(
        string externalNamespace,
        string key,
        [FromHeader(Name = "Idempotency-Key")]
        [Description(
            "Required. Client-chosen key of this archive request, 1 to 200 characters after trimming, compared " +
            "exactly. Repeating the same request with the same key returns the stored receipt; reusing the key for " +
            "a different request, including a PUT on this route, is rejected with " +
            "agents.external-key-idempotency-conflict.")]
        string? idempotencyKey,
        [FromHeader(Name = "If-Match")]
        [Description(
            "Required. Current configuration version of the binding, from the ETag or configurationVersion of the " +
            "last read or receipt: 64 hexadecimal digits, surrounding quotes allowed.")]
        string? ifMatch,
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
