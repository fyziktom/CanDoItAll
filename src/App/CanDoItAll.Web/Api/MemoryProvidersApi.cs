using System.Security.Claims;
using CanDoItAll.Modules.Memory.Services;

namespace CanDoItAll.Web.Api;

internal static class MemoryProvidersApi
{
    private const string LocalRequesterId = "api.local";

    public static RouteGroupBuilder MapMemoryProvidersApi(this RouteGroupBuilder group)
    {
        var providers = group.MapGroup("/memory-providers")
            .WithTags("Memory Providers")
            .DisableAntiforgery();

        providers.MapGet(string.Empty, ListMemoryProvidersAsync)
            .WithName("ListMemoryProviders")
            .Produces<MemoryProviderProfileApiResponse[]>(StatusCodes.Status200OK)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        providers.MapGet("/{providerId}", GetMemoryProviderAsync)
            .WithName("GetMemoryProvider")
            .Produces<MemoryProviderProfileApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        providers.MapPut("/{providerId}", UpsertMemoryProviderAsync)
            .WithName("UpsertMemoryProvider")
            .Produces<MemoryProviderProfileApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.WriteMemoryProviders);

        providers.MapPost("/{providerId}/queries", QueryMemoryProviderAsync)
            .WithName("QueryMemoryProvider")
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status200OK)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status202Accepted)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status404NotFound)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status409Conflict)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status502BadGateway)
            .Produces<MemoryProviderQueryApiResponse>(StatusCodes.Status504GatewayTimeout)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.QueryMemoryProviders);

        providers.MapGet("/operations/{operationId:guid}", GetMemoryProviderOperationAsync)
            .WithName("GetMemoryProviderOperation")
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status200OK)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status404NotFound)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        return group;
    }

    /// <summary>
    /// List all configured memory provider profiles.
    /// </summary>
    /// <remarks>
    /// Experimental API: memory providers are an experimental integration and this contract can still change.
    ///
    /// Returns every memory provider profile the host knows, enabled or not, ordered by display name (ignoring
    /// letter case) and then by provider identifier. The list is not paged. An empty array means that no provider is
    /// configured, which is a valid deployment. Each item has the same shape as
    /// <c>GET /api/memory-providers/{providerId}</c>; transport settings name credential environment variables but
    /// never contain credential values. The read has no side effects.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.memory-providers.read</c> scope.
    /// </remarks>
    /// <response code="200">All memory provider profiles; an empty array when none is configured.</response>
    internal static async Task<IResult> ListMemoryProvidersAsync(
        MemoryProviderApiService service,
        CancellationToken cancellationToken) =>
        Results.Ok(await service.ListProfilesAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Read one memory provider profile by its provider identifier.
    /// </summary>
    /// <remarks>
    /// Experimental API: memory providers are an experimental integration and this contract can still change.
    ///
    /// Returns the configuration, advertised capabilities, limits and last recorded health of the profile. Transport
    /// settings name credential environment variables but never contain credential values. The read has no side
    /// effects and does not contact the provider.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.memory-providers.read</c> scope.
    /// </remarks>
    /// <param name="providerId">
    /// Identifier of the provider profile, as chosen when it was saved with
    /// <c>PUT /api/memory-providers/{providerId}</c> and returned as <c>providerId</c>. Surrounding whitespace is
    /// ignored; at most 256 characters without control characters.
    /// </param>
    /// <response code="200">The provider profile.</response>
    /// <response code="400">
    /// The identifier is blank, longer than 256 characters or contains control characters
    /// (<c>memory-provider.request-invalid</c>).
    /// </response>
    /// <response code="404">No profile has this identifier (<c>memory-provider.not-found</c>).</response>
    internal static async Task<IResult> GetMemoryProviderAsync(
        string providerId,
        MemoryProviderApiService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var profile = await service
                .GetProfileAsync(providerId, cancellationToken)
                .ConfigureAwait(false);
            return profile is null
                ? ApiEndpointResults.NotFound(
                    $"Memory provider '{providerId}' was not found.",
                    "memory-provider.not-found")
                : Results.Ok(profile);
        });

    /// <summary>
    /// Create a memory provider profile or replace the configuration of an existing one.
    /// </summary>
    /// <remarks>
    /// Experimental API: memory providers are an experimental integration and this contract can still change.
    ///
    /// Upsert by provider identifier: when no profile has the identifier, a new profile is created; otherwise its
    /// configuration is replaced by the request. There is no concurrency check, so the last write wins. Sending the
    /// same body again stores the same configuration. The operation stores configuration only; it does not contact
    /// the provider or check that it is reachable, so <c>healthState</c> keeps its last recorded value (<c>Unknown</c>
    /// for a new profile).
    ///
    /// What the request replaces: the display name, driver kind, enabled flag, fallback behavior, provider kind,
    /// selection tags, the three query and status capabilities, and the transport of the driver kind. Saving through
    /// this operation also turns off the ingestion, feedback and event capabilities of the profile, because this API
    /// cannot configure them, and removes the stored settings of the other transport when the driver kind changes.
    /// The user-interface capabilities, provider limits and protocol version of an existing profile are kept; the
    /// workspace scope is always all workspaces.
    ///
    /// Rules checked before anything is saved (HTTP 400, <c>memory-provider.request-invalid</c>):
    ///
    /// - <c>Http</c> and <c>NativeRemote</c> drivers need <c>http</c> and no <c>mcp</c>; <c>Mcp</c> needs
    /// <c>mcp</c> and no <c>http</c>; <c>Mock</c> takes neither.
    /// - Each driver runs only some capabilities: <c>Http</c>, <c>NativeRemote</c> and <c>Mock</c> support synchronous
    /// queries only; <c>Mcp</c> supports synchronous queries, asynchronous queries and operation status.
    /// - Asynchronous queries require operation-status support, because polling is the only completion path.
    /// - An MCP profile with a query capability needs <c>contextQueryTool</c>, and with asynchronous queries or
    /// operation status also <c>operationStatusTool</c>.
    /// - Endpoints are absolute HTTPS addresses, or HTTP to a loopback address, without user information, query or
    /// fragment; credentials are referenced by environment-variable name, never sent as values.
    ///
    /// A body the framework cannot bind (malformed JSON, an unknown member or a wrong JSON type) is rejected with
    /// HTTP 400 before the operation runs, without the <c>errors</c> envelope.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.memory-providers.write</c> scope. Read the result back with
    /// <c>GET /api/memory-providers/{providerId}</c> when needed.
    /// </remarks>
    /// <param name="providerId">
    /// Identifier of the profile to create or replace, chosen by the caller, for example
    /// <c>provider.team-memory</c>. Surrounding whitespace is removed; at most 256 characters without control
    /// characters. It is the profile's permanent identity: use it in every later memory-provider request.
    /// </param>
    /// <param name="request">The complete configuration of the profile.</param>
    /// <response code="200">The configuration was saved; the body is the stored profile.</response>
    /// <response code="400">
    /// Nothing was saved (<c>memory-provider.request-invalid</c>): the identifier or a value is invalid, or the
    /// driver, capabilities and transport do not fit together. The message names the rule.
    /// </response>
    internal static async Task<IResult> UpsertMemoryProviderAsync(
        string providerId,
        MemoryProviderProfileApiRequest request,
        MemoryProviderApiService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
            Results.Ok(await service
                .SaveProfileAsync(providerId, request, cancellationToken)
                .ConfigureAwait(false)));

    /// <summary>
    /// Query one memory provider for context, synchronously or as an asynchronous operation.
    /// </summary>
    /// <remarks>
    /// Experimental API: memory providers are an experimental integration and this contract can still change.
    ///
    /// Sends the query text to exactly the provider named in the route; no other provider is used as a fallback. The
    /// provider must exist, be enabled and support the capability of the requested mode: <c>context.query.sync</c>
    /// for <c>Synchronous</c> or <c>context.query.async</c> for <c>Asynchronous</c>. Before calling the provider the
    /// host records an operation in its memory operation ledger; that operation belongs to the caller: the bearer
    /// token's subject when API authorization is enabled, otherwise one local identity shared by all callers.
    ///
    /// The status code follows <c>status</c> in the body, which always has the same shape:
    ///
    /// - 200 (<c>Completed</c>): <c>contextPack</c> holds the retrieved context.
    /// - 202 (<c>Accepted</c>): the provider started an asynchronous operation. Poll
    /// <c>GET /api/memory-providers/operations/{operationId}</c> with <c>acceptedOperation.operationId</c>, waiting
    /// at least <c>pollAfterSeconds</c> between reads, until the operation ends or <c>expiresAtUtc</c> passes.
    /// - 403 and 404: the provider was not selected and nothing was dispatched (<c>driverDispatchAttempted</c> is
    /// false); <c>selection</c> explains why.
    /// - 409: the query cannot run. Usually the provider was not selected and nothing was dispatched (for example
    /// <c>ProviderDisabled</c>); <c>UnsupportedOperation</c> means the provider was called and reported that it does
    /// not support the query.
    /// - 502 and 504: dispatch failed or timed out; <c>driverDispatchAttempted</c> tells whether the provider was
    /// contacted, and <c>operation</c> records the failure.
    ///
    /// Every call creates a new operation; a retry is a new query and never resumes an earlier operation. Retry a 502
    /// or 504 only as a new query.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.memory-providers.query</c> scope.
    /// </remarks>
    /// <param name="providerId">
    /// Identifier of the provider profile to query, as returned by <c>GET /api/memory-providers</c>. Surrounding
    /// whitespace is ignored; at most 256 characters without control characters.
    /// </param>
    /// <param name="request">The query text and the query mode.</param>
    /// <response code="200">The query completed; <c>contextPack</c> holds the result.</response>
    /// <response code="202">
    /// The provider accepted an asynchronous operation; poll it through <c>acceptedOperation</c>.
    /// </response>
    /// <response code="400">
    /// The request was rejected before any operation was recorded (<c>memory-provider.request-invalid</c>): the query
    /// is blank or longer than 32,768 characters after trimming, the identifier is invalid, or the mode is unknown. A
    /// body the framework cannot bind (malformed JSON, an unknown member or a wrong JSON type) is also rejected with
    /// 400, without the <c>errors</c> envelope.
    /// </response>
    /// <response code="403">
    /// Selection denied the provider (<c>status</c> <c>ProviderDenied</c> or <c>CapabilityDenied</c>), for example
    /// because the profile is bound to a single workspace, which the host cannot verify. A 403 whose body is the
    /// general <c>errors</c> envelope instead (<c>memory-provider.identity-missing</c>) means the bearer token has no
    /// subject.
    /// </response>
    /// <response code="404">No profile has this identifier (<c>status</c> <c>ProviderNotFound</c>).</response>
    /// <response code="409">
    /// The query cannot run: usually the provider was not selected and nothing was dispatched, for example
    /// <c>ProviderDisabled</c> or <c>CapabilityUnavailable</c> (the profile does not advertise the capability of the
    /// mode); <c>UnsupportedOperation</c> means the provider was called and rejected the query as unsupported.
    /// </response>
    /// <response code="502">
    /// The query failed (<c>DriverUnavailable</c>, <c>DriverFailed</c> or <c>Failed</c>), for example because the
    /// provider could not be reached or returned an invalid result.
    /// </response>
    /// <response code="504">The provider did not answer in time (<c>TimedOut</c>).</response>
    internal static async Task<IResult> QueryMemoryProviderAsync(
        string providerId,
        MemoryProviderQueryApiRequest request,
        HttpContext httpContext,
        MemoryProviderApiService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var requesterId = ResolveRequesterId(httpContext.User);
            var result = await service
                .ExecuteQueryAsync(providerId, request, requesterId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Json(result.Response, statusCode: result.StatusCode);
        });

    /// <summary>
    /// Read the progress of a memory operation that the caller created with a provider query.
    /// </summary>
    /// <remarks>
    /// Experimental API: memory providers are an experimental integration and this contract can still change.
    ///
    /// Use it to poll an asynchronous query accepted by <c>POST /api/memory-providers/{providerId}/queries</c>. It
    /// returns the host's ledger record of the operation (<c>operation.status</c>, retry and transition counts and
    /// timestamps); it does not contact the provider and it returns no context pack. The record advances only when the
    /// host updates it, for example when its memory background workers are enabled and poll the provider, so a record
    /// can stay <c>Accepted</c> or <c>Running</c> when those workers are disabled. Stop polling when
    /// <c>operation.status</c> is <c>Completed</c>, <c>Failed</c>, <c>TimedOut</c>, <c>Cancelled</c>, <c>Expired</c>
    /// or <c>Forgotten</c>.
    ///
    /// Only the caller that created the operation can read it: the same bearer token subject when API authorization
    /// is enabled, otherwise the shared local identity. The provider that ran the operation must still exist, be
    /// enabled and advertise <c>operations.status</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.memory-providers.read</c> scope.
    /// </remarks>
    /// <param name="operationId">
    /// Identifier (GUID) of the operation, from <c>acceptedOperation.operationId</c> or <c>operation.operationId</c> of
    /// the query response. A value that is not a GUID does not match this route.
    /// </param>
    /// <response code="200">The operation record (<c>status</c> <c>Completed</c> means the read succeeded).</response>
    /// <response code="400">
    /// The identifier is the empty GUID (<c>memory-provider.request-invalid</c>).
    /// </response>
    /// <response code="403">
    /// The operation belongs to another caller (<c>status</c> <c>AccessDenied</c>) or its provider was denied by
    /// selection; <c>operation</c> is null. A 403 with the general <c>errors</c> envelope instead
    /// (<c>memory-provider.identity-missing</c>) means the bearer token has no subject.
    /// </response>
    /// <response code="404">
    /// The operation does not exist (<c>NotFound</c>) or its provider profile no longer exists
    /// (<c>ProviderNotFound</c>).
    /// </response>
    /// <response code="409">
    /// The operation's provider cannot report status now, for example <c>ProviderDisabled</c> or
    /// <c>CapabilityUnavailable</c> (it no longer advertises <c>operations.status</c>).
    /// </response>
    internal static async Task<IResult> GetMemoryProviderOperationAsync(
        Guid operationId,
        HttpContext httpContext,
        MemoryProviderApiService service,
        CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            var requesterId = ResolveRequesterId(httpContext.User);
            var result = await service
                .GetOperationStatusAsync(operationId, requesterId, cancellationToken)
                .ConfigureAwait(false);
            return Results.Json(result.Response, statusCode: result.StatusCode);
        });

    private static string ResolveRequesterId(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return LocalRequesterId;
        }

        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? throw new MemoryProviderApiIdentityException(
                "The authenticated API token does not contain a subject.");
    }

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (MemoryProviderApiRequestException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "memory-provider.request-invalid");
        }
        catch (MemoryProviderProfileConfigurationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "memory-provider.request-invalid");
        }
        catch (MemoryProviderApiIdentityException exception)
        {
            return ApiEndpointResults.Forbidden(exception.Message, "memory-provider.identity-missing");
        }
    }
}

internal sealed class MemoryProviderApiIdentityException(string message) : Exception(message);
