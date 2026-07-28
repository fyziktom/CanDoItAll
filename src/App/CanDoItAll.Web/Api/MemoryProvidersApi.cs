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

        providers.MapGet(string.Empty, async (
                MemoryProviderApiService service,
                CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken).ConfigureAwait(false)))
            .WithName("ListMemoryProviders")
            .Produces<MemoryProviderProfileApiResponse[]>(StatusCodes.Status200OK)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        providers.MapGet("/{providerId}", async (
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
            }))
            .WithName("GetMemoryProvider")
            .Produces<MemoryProviderProfileApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        providers.MapPut("/{providerId}", async (
                string providerId,
                MemoryProviderProfileApiRequest request,
                MemoryProviderApiService service,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                Results.Ok(await service
                    .SaveProfileAsync(providerId, request, cancellationToken)
                    .ConfigureAwait(false))))
            .WithName("UpsertMemoryProvider")
            .Produces<MemoryProviderProfileApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.WriteMemoryProviders);

        providers.MapPost("/{providerId}/queries", async (
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
            }))
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

        providers.MapGet("/operations/{operationId:guid}", async (
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
            }))
            .WithName("GetMemoryProviderOperation")
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status200OK)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status404NotFound)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status409Conflict)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status502BadGateway)
            .Produces<MemoryProviderOperationStatusApiResponse>(StatusCodes.Status504GatewayTimeout)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ApplyApiAuthorization(group, ApiAuthorizationPolicies.ReadMemoryProviders);

        return group;
    }

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
