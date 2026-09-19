using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

public static class ApiEndpointRouteBuilderExtensions
{
    public static WebApplication MapCanDoItAllApiDocumentation(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.Services.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (!options.OpenApiEnabled)
        {
            return app;
        }

        var openApiEndpoint = app.MapOpenApi();
        var swaggerJsonEndpoint = app.MapOpenApi("/swagger/{documentName}/swagger.json");
        if (options.Authorization.Enabled)
        {
            openApiEndpoint.RequireAuthorization();
            swaggerJsonEndpoint.RequireAuthorization();
        }

        if (options.SwaggerUiEnabled)
        {
            app.UseSwaggerUI(swagger =>
            {
                swagger.RoutePrefix = "swagger";
                swagger.DocumentTitle = "CanDoItAll API";
                swagger.SwaggerEndpoint("/swagger/v1/swagger.json", "CanDoItAll API v1");
            });
        }

        return app;
    }

    public static IEndpointRouteBuilder MapCanDoItAllApi(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<ApiAccessOptions>>().Value;
        if (!options.Enabled)
        {
            return endpoints;
        }

        endpoints.MapStoragePlacementRecoveryApi();
        var group = endpoints.MapGroup("/api")
            .WithTags("API")
            .DisableAntiforgery();
        if (options.Authorization.Enabled)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/access/status", GetAccessStatus)
            .AllowAnonymous()
            .WithName("GetApiAccessStatus")
            .Produces<ApiAccessStatus>();

        var issueTokenEndpoint = group.MapPost("/access/tokens", IssueToken)
            .WithName("IssueApiToken")
            .Produces<ApiTokenIssueResult>()
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);
        if (options.Authorization.Enabled)
        {
            issueTokenEndpoint.RequireAuthorization(ApiAuthorizationPolicies.IssueTokens);
        }

        group.MapProjectsApi();
        group.MapAgentsApi();
        group.MapAgentEventsApi();
        group.MapAgentProviderEventsApi();
        group.MapAgentAttachmentsApi();
        group.MapAgentRecruitingApi();
        group.MapPromptGalleryApi();
        group.MapWorkflowsApi();
        group.MapWorkflowRunEventsApi();
        group.MapProcessesApi();
        group.MapProcessRunEventsApi();
        group.MapMemoryProvidersApi();
        group.MapPluginsApi();
        group.MapCrmHrApi();
        group.MapLlmChatsApi();
        group.MapLlmChatOperationsApi();
        endpoints.MapSharedProviderCatalogApi();
        endpoints.MapSharedProviderInferenceApi();

        return endpoints;
    }

    public static IEndpointConventionBuilder ApplyApiAuthorization(
        this IEndpointConventionBuilder builder,
        IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetService<IOptions<ApiAccessOptions>>()?.Value;
        if (options?.Authorization.Enabled == true)
        {
            builder.RequireAuthorization();
        }

        return builder;
    }

    public static IEndpointConventionBuilder ApplyApiAuthorization(
        this IEndpointConventionBuilder builder,
        IEndpointRouteBuilder endpoints,
        string policyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);

        var options = endpoints.ServiceProvider.GetService<IOptions<ApiAccessOptions>>()?.Value;
        if (options?.Authorization.Enabled == true)
        {
            builder.RequireAuthorization(policyName);
        }

        return builder;
    }

    /// <summary>
    /// Read how the HTTP API of this host is configured: enabled surfaces, authorization and token lifetimes.
    /// </summary>
    /// <remarks>
    /// Use this read to discover whether a bearer token is needed before calling other operations, and which issuer,
    /// audience and lifetimes a token issued by <c>POST /api/access/tokens</c> gets. The response contains no signing
    /// key and no token.
    ///
    /// Authority: anonymous. The route never requires a bearer token, even when API authorization is enabled. It
    /// exists only while <c>Api:Enabled</c> is true, so <c>apiEnabled</c> is always true in a response.
    /// </remarks>
    /// <response code="200">The current API access configuration.</response>
    internal static IResult GetAccessStatus(IApiTokenService tokenService) =>
        Results.Ok(tokenService.GetStatus());

    /// <summary>
    /// Issue a signed bearer token for this host's HTTP API with the requested scopes and lifetime.
    /// </summary>
    /// <remarks>
    /// Creates a new managed API token: an HS256-signed JSON Web Token with this host's configured issuer and
    /// audience, the requested subject and scopes, and an expiry. The token is registered with the host, which checks
    /// the registration on every request, so a revoked, deleted or expired token is rejected even while its signature
    /// is still valid. Every call issues a new, separate token; nothing is replaced.
    ///
    /// Send the returned <c>token</c> as <c>Authorization: Bearer {token}</c>. The token value is returned only in
    /// this response and cannot be read again; store it as a secret and keep it out of URLs, logs and source control.
    /// Operations that require a specific scope check the scope names exactly and case-sensitively, and a scope name
    /// that no operation uses grants nothing.
    ///
    /// Tokens can be issued only while API authorization is enabled (<c>Api:Authorization:Enabled</c>); otherwise the
    /// request fails with HTTP 400. Read <c>GET /api/access/status</c> first to check this and the lifetime limits.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact <c>api.tokens.issue</c> scope; the
    /// general <c>api</c> scope is not enough. Grant that scope only to administrators, because a caller that holds it
    /// can issue tokens with any scope.
    /// </remarks>
    /// <param name="request">Subject, display name, lifetime and scopes of the new token.</param>
    /// <response code="200">
    /// The token was issued and registered. The body contains the token and the normalized subject, display name,
    /// scopes and expiry.
    /// </response>
    /// <response code="400">
    /// The token was not issued (<c>api.token-invalid</c>): API authorization is disabled or misconfigured, the subject
    /// is blank, the lifetime is zero, negative or above the configured maximum, or no non-blank scope was given.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and no valid bearer token was sent (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// The bearer token lacks the <c>api.tokens.issue</c> scope (<c>api.authorization-forbidden</c>).
    /// </response>
    internal static IResult IssueToken(
        ApiTokenIssueRequest request,
        IApiTokenService tokenService)
    {
        try
        {
            return Results.Ok(tokenService.IssueToken(request));
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "api.token-invalid");
        }
    }
}
