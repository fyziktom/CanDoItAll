using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal static class ProjectsApi
{
    public static RouteGroupBuilder MapProjectsApi(this RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects")
            .WithTags("Projects");

        projects.MapGet("/", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListAsync(cancellationToken)))
            .WithName("ListProjects");

        projects.MapGet("/access-list", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListAccessListAsync(cancellationToken)))
            .WithName("ListProjectAccessItems");

        projects.MapGet("/hierarchy-links", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListHierarchyLinksAsync(cancellationToken)))
            .WithName("ListProjectHierarchyLinks");

        projects.MapGet("/{projectId:guid}", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.GetAsync(projectId, cancellationToken)))
            .WithName("GetProjectEditor");

        projects.MapPost("/", async (
                ProjectEditorModel request,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(
                await projectsService.SaveAsync(request, cancellationToken),
                ProjectErrorCodes.NotFound))
            .WithName("SaveProject")
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        projects.MapDelete("/{projectId:guid}", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await projectsService.DeleteAsync(
                        projectId,
                        cancellationToken));
                }
                catch (ProjectDeletionPartialCommitException exception)
                {
                    return Results.Json(
                        new ProjectDeletionCleanupPendingApiResponse(
                            "projects.delete-cleanup-pending",
                            "The project was deleted, but one or more cleanup steps remain.",
                            exception.Recovery),
                        statusCode: StatusCodes.Status409Conflict);
                }
            })
            .WithName("DeleteProject")
            .Produces<ProjectDeletionResult>(StatusCodes.Status200OK)
            .Produces<ProjectDeletionCleanupPendingApiResponse>(StatusCodes.Status409Conflict);

        projects.MapGet("/deletion-cleanups", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListPendingDeletionCleanupsAsync(cancellationToken)))
            .WithName("ListPendingProjectDeletionCleanups")
            .Produces<IReadOnlyList<ProjectDeletionPendingCleanup>>(StatusCodes.Status200OK);

        projects.MapGet("/deletion-completion-notices", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListDeletionCompletionNoticesAsync(
                cancellationToken)))
            .WithName("ListProjectDeletionCompletionNotices")
            .Produces<IReadOnlyList<ProjectDeletionCompletionNotice>>(StatusCodes.Status200OK);

        projects.MapPost("/{projectId:guid}/deletion-cleanups/{participantId}/{recoveryId:guid}/retry", async (
                Guid projectId,
                string participantId,
                Guid recoveryId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            {
                ProjectDeletionParticipantId deletionParticipantId;
                try
                {
                    deletionParticipantId = new ProjectDeletionParticipantId(participantId);
                }
                catch (ArgumentException)
                {
                    return ApiEndpointResults.BadRequest(
                        "The project-cleanup participant identifier is invalid.",
                        "projects.delete-cleanup-participant-invalid");
                }

                try
                {
                    return Results.Ok(await projectsService.RetryDeletionCleanupAsync(
                        projectId,
                        deletionParticipantId,
                        recoveryId,
                        cancellationToken));
                }
                catch (ProjectDeletionRecoveryNotFoundException)
                {
                    return ApiEndpointResults.NotFound(
                        "The exact pending project-cleanup operation was not found.",
                        "projects.delete-cleanup-not-found");
                }
                catch (ProjectDeletionPartialCommitException exception)
                {
                    return Results.Json(
                        new ProjectDeletionCleanupPendingApiResponse(
                            "projects.delete-cleanup-pending",
                            "Project cleanup is still incomplete.",
                            exception.Recovery),
                        statusCode: StatusCodes.Status409Conflict);
                }
            })
            .WithName("RetryPendingProjectDeletionCleanup")
            .Produces<ProjectDeletionResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound)
            .Produces<ProjectDeletionCleanupPendingApiResponse>(StatusCodes.Status409Conflict);

        projects.MapGet("/{projectId:guid}/hierarchy", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.GetHierarchyAsync(projectId, cancellationToken)))
            .WithName("GetProjectHierarchy");

        projects.MapPost("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", async (
                Guid parentProjectId,
                Guid childProjectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await projectsService.AddSubprojectAsync(
                parentProjectId,
                childProjectId,
                cancellationToken)))
            .WithName("AttachProjectSubproject");

        projects.MapDelete("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", async (
                Guid parentProjectId,
                Guid childProjectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await projectsService.RemoveSubprojectAsync(
                parentProjectId,
                childProjectId,
                cancellationToken)))
            .WithName("DetachProjectSubproject");

        projects.MapPost("/{childProjectId:guid}/reconnect-subproject", async (
                Guid childProjectId,
                ProjectReconnectSubprojectApiRequest request,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            ApiEndpointResults.FromResult(await projectsService.ReconnectSubprojectAsync(
                childProjectId,
                request.CurrentParentProjectId,
                request.NewParentProjectId,
                cancellationToken)))
            .WithName("ReconnectProjectSubproject");

        return group;
    }
}

internal sealed record ProjectReconnectSubprojectApiRequest(
    Guid CurrentParentProjectId,
    Guid NewParentProjectId);

internal sealed record ProjectDeletionCleanupPendingApiResponse(
    string Code,
    string Message,
    ProjectDeletionRecovery Recovery);
