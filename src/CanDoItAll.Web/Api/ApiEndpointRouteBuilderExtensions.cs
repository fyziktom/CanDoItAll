using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

public static class ApiEndpointRouteBuilderExtensions
{
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

        group.MapPost("/access/tokens", (
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

        group.MapProjectsApi();
        group.MapProcessesApi();
        group.MapAgentsApi();
        group.MapWorkflowsApi();
        group.MapPluginsApi();

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
}
