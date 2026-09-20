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

        group.MapAccess(options);
        group.MapWorkspaceSettings();

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

}
