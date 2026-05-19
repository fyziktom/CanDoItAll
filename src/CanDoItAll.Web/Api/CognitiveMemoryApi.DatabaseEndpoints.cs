using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;


internal static partial class CognitiveMemoryApi
{
    private static void MapDatabaseEndpoints(
        RouteGroupBuilder memory,
        CognitiveMemoryApiSurface surface)
    {
        memory.MapGet("/status", (
                IDatabaseProfileRuntimeAccessor profileAccessor,
                IOptions<CognitiveMemoryProjectionOptions> projectionOptions,
                IWebHostEnvironment environment) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                return Results.Ok(CognitiveMemoryStatusApiResponse.From(
                    profile,
                    BuildApiContract(surface),
                    projectionOptions.Value,
                    environment));
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

        memory.MapGet("/database/transfer/sources/{targetProfileId:guid}", async (
                Guid targetProfileId,
                IDatabaseTransferService transferService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                await transferService.ListSourcesAsync(
                    EnsureNonEmpty(targetProfileId, nameof(targetProfileId)),
                    cancellationToken)))
            .WithName(EndpointName("ListCognitiveMemoryDatabaseTransferSources", surface));

        memory.MapGet("/database/transfer/preview", async (
                [FromQuery] Guid sourceProfileId,
                [FromQuery] Guid targetProfileId,
                IDatabaseTransferService transferService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                await transferService.PreviewAsync(
                    EnsureNonEmpty(sourceProfileId, nameof(sourceProfileId)),
                    EnsureNonEmpty(targetProfileId, nameof(targetProfileId)),
                    cancellationToken)))
            .WithName(EndpointName("PreviewCognitiveMemoryDatabaseTransfer", surface));

        memory.MapPost("/database/transfer", async (
                DatabaseTransferRequest request,
                IDatabaseTransferService transferService,
                CancellationToken cancellationToken) =>
            await ExecuteAsync(async () =>
                await transferService.TransferAsync(request, cancellationToken)))
            .WithName(EndpointName("RunCognitiveMemoryDatabaseTransfer", surface));
    }
}
