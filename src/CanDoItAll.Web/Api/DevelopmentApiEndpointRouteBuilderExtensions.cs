using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

public static class DevelopmentApiEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCanDoItAllDevelopmentApi(this IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<DevelopmentApiAccessOptions>>().Value;
        if (!options.Enabled)
        {
            return endpoints;
        }

        var group = endpoints.MapGroup("/api/dev")
            .WithTags("Development API");
        if (options.Authorization.Enabled)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/access/status", (IDevelopmentApiTokenService tokenService) =>
                Results.Ok(tokenService.GetStatus()))
            .AllowAnonymous()
            .WithName("GetDevelopmentApiAccessStatus");

        group.MapPost("/access/tokens", (
                DevelopmentApiTokenIssueRequest request,
                IDevelopmentApiTokenService tokenService) =>
            {
                try
                {
                    return Results.Ok(tokenService.IssueToken(request));
                }
                catch (InvalidOperationException exception)
                {
                    return DevelopmentApiEndpointResults.BadRequest(exception.Message, "development-api.token-invalid");
                }
            })
            .WithName("IssueDevelopmentApiToken");

        group.MapDevelopmentProjectsApi();
        group.MapDevelopmentProcessesApi();
        group.MapDevelopmentAgentsApi();

        return endpoints;
    }

    public static IEndpointConventionBuilder ApplyDevelopmentApiAuthorization(
        this IEndpointConventionBuilder builder,
        IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider.GetService<IOptions<DevelopmentApiAccessOptions>>()?.Value;
        if (options?.Authorization.Enabled == true)
        {
            builder.RequireAuthorization();
        }

        return builder;
    }
}
