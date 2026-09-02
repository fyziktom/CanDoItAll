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

        var group = endpoints.MapGroup("/api")
            .WithTags("API")
            .DisableAntiforgery();
        if (options.Authorization.Enabled)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/access/status", (IApiTokenService tokenService) =>
                Results.Ok(tokenService.GetStatus()))
            .AllowAnonymous()
            .WithName("GetApiAccessStatus");

        var issueTokenEndpoint = group.MapPost("/access/tokens", (
                ApiTokenIssueRequest request,
                IApiTokenService tokenService) =>
            {
                try
                {
                    return Results.Ok(tokenService.IssueToken(request));
                }
                catch (InvalidOperationException exception)
                {
                    return ApiEndpointResults.BadRequest(exception.Message, "api.token-invalid");
                }
            })
            .WithName("IssueApiToken");
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
}
