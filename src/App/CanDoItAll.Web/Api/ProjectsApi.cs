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
            ApiEndpointResults.FromResult(await projectsService.SaveAsync(request, cancellationToken)))
            .WithName("SaveProject");

        projects.MapDelete("/{projectId:guid}", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            {
                await projectsService.DeleteAsync(projectId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            })
            .WithName("DeleteProject");

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
