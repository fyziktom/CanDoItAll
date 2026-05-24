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
        memory.MapGet("/status", async (
                IDatabaseProfileRuntimeAccessor profileAccessor,
                IDatabaseProfileService profileService,
                IOptions<CognitiveMemoryProjectionOptions> projectionOptions,
                IWebHostEnvironment environment,
                CancellationToken cancellationToken) =>
            {
                var profile = profileAccessor.ResolveCurrentProfile();
                var persistedSelection = await profileService.GetCurrentSelectionAsync(cancellationToken);
                var selection = BuildDatabaseSelection(profile, persistedSelection);
                return Results.Ok(CognitiveMemoryStatusApiResponse.From(
                    profile,
                    selection,
                    BuildApiContract(surface),
                    projectionOptions.Value,
                    environment));
            })
            .WithName(EndpointName("GetCognitiveMemoryStatus", surface));

        memory.MapGet("/database/selection", async (
                IDatabaseProfileRuntimeAccessor profileAccessor,
                IDatabaseProfileService profileService,
                CancellationToken cancellationToken) =>
            {
                var runtimeProfile = profileAccessor.ResolveCurrentProfile();
                var persistedSelection = await profileService.GetCurrentSelectionAsync(cancellationToken);
                var selection = BuildDatabaseSelection(runtimeProfile, persistedSelection);
                var pendingProfile = selection.PendingRestartProfileId.HasValue
                    ? profileAccessor.ResolveProfile(selection.PendingRestartProfileId.Value)
                    : null;
                return Results.Ok(CognitiveMemoryDatabaseSelectionApiResponse.From(
                    runtimeProfile,
                    selection,
                    pendingProfile));
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

                var runtimeProfile = profileAccessor.ResolveCurrentProfile();
                var activatedProfile = profileAccessor.ResolveProfile(switchResult.Value!.CurrentProfileId);
                return new CognitiveMemoryDatabaseSwitchApiResponse(
                    switchResult.Value.PreviousProfileId,
                    switchResult.Value.CurrentProfileId,
                    switchResult.Value.RuntimeProfileId,
                    switchResult.Value.PendingRestartProfileId,
                    switchResult.Value.Generation,
                    switchResult.Value.ProcessId,
                    switchResult.Value.RequiresRestart,
                    switchResult.Value.RuntimeChangedInProcess,
                    switchResult.Value.Message,
                    CognitiveMemoryDatabaseProfileApiResponse.From(activatedProfile),
                    CognitiveMemoryDatabaseProfileApiResponse.From(runtimeProfile));
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

    private static DatabaseSelectionStateModel BuildDatabaseSelection(
        ResolvedDatabaseProfile runtimeProfile,
        DatabaseSelectionStateModel persistedSelection)
    {
        var selection = new DatabaseSelectionStateModel
        {
            ActiveProfileId = runtimeProfile.Profile.Id,
            RuntimeProfileId = runtimeProfile.Profile.Id,
            DisplayName = runtimeProfile.Profile.DisplayName,
            ProviderKind = runtimeProfile.Profile.ProviderKind,
            SourceKind = runtimeProfile.Profile.SourceKind,
            ResolutionSource = runtimeProfile.ResolutionSource,
            IsRuntimeLocked = runtimeProfile.Profile.Runtime.LockedByRuntimeOverride,
            Fingerprint = runtimeProfile.Profile.Runtime.Fingerprint,
            WorkspaceRoot = runtimeProfile.Profile.Storage.WorkspaceRoot,
            Descriptor = CognitiveMemoryStatusApiResponse.BuildDescriptor(runtimeProfile.Profile)
        };

        if (!runtimeProfile.Profile.Runtime.LockedByRuntimeOverride &&
            persistedSelection.ActiveProfileId != runtimeProfile.Profile.Id)
        {
            selection.PendingRestartProfileId = persistedSelection.ActiveProfileId;
            selection.PendingRestartDisplayName = persistedSelection.DisplayName;
            selection.PendingRestartDescriptor = persistedSelection.Descriptor;
            selection.PendingRestartFingerprint = persistedSelection.Fingerprint;
        }

        return selection;
    }
}
