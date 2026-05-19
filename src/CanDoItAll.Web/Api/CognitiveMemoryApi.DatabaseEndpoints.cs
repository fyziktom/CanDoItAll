using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapDatabaseEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapGet("/status", (
                IDatabaseProfileRuntimeAccessor profileAccessor) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                return Results.Ok(CognitiveMemoryStatusApiResponse.From(profile, BuildApiContract(surface)));
            })
            .WithName(EndpointName("GetCognitiveMemoryStatus", surface));

        memory.MapGet("/database/selection", (
                IDatabaseProfileRuntimeAccessor profileAccessor) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                return Results.Ok(CognitiveMemoryDatabaseProfileApiResponse.From(profile));
            })
            .WithName(EndpointName("GetCognitiveMemoryDatabaseSelection", surface));

        memory.MapGet("/database/profiles", async (
                IDatabaseProfileService profileService,
                CancellationToken cancellationToken) =>
            Results.Ok(await profileService.ListAsync(cancellationToken)))
            .WithName(EndpointName("ListCognitiveMemoryDatabaseProfiles", surface));

        memory.MapPost("/database/profiles/postgresql", async (
                CognitiveMemoryPostgreSqlDatabaseProfileApiRequest request,
                IDatabaseProfileService profileService,
                IDatabaseProfileRuntimeAccessor profileAccessor,
                IDatabaseDriverRegistry driverRegistry,
                IAppDatabaseBootstrapper bootstrapper,
                IDatabaseSwitchCoordinator switchCoordinator,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(() => CreatePostgreSqlDatabaseProfileAsync(
                request,
                profileService,
                profileAccessor,
                driverRegistry,
                bootstrapper,
                switchCoordinator,
                cancellationToken)))
            .WithName(EndpointName("CreateCognitiveMemoryPostgreSqlDatabaseProfile", surface));

        memory.MapPost("/database/switch/{profileId:guid}", async (
                Guid profileId,
                IDatabaseSwitchCoordinator switchCoordinator,
                IDatabaseProfileRuntimeAccessor profileAccessor,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
            {
                var switchResult = await switchCoordinator.SwitchAsync(
                    EnsureNonEmpty(profileId, nameof(profileId)),
                    cancellationToken);
                if (switchResult.IsFailure)
                {
                    throw new InvalidOperationException(BuildErrorMessage(switchResult.Errors));
                }

                var profile = profileAccessor.ResolveCurrentProfile();
                return new CognitiveMemoryDatabaseSwitchApiResponse(
                    switchResult.Value!.PreviousProfileId,
                    switchResult.Value.CurrentProfileId,
                    switchResult.Value.Generation,
                    switchResult.Value.ProcessId,
                    CognitiveMemoryDatabaseProfileApiResponse.From(profile));
            }))
            .WithName(EndpointName("SwitchCognitiveMemoryDatabaseProfile", surface));
    }
}
