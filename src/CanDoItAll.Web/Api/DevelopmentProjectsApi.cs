using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal static class DevelopmentProjectsApi
{
    public static RouteGroupBuilder MapDevelopmentProjectsApi(this RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects")
            .WithTags("Development Projects");

        projects.MapGet("/", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListAsync(cancellationToken)))
            .WithName("ListDevelopmentProjects");

        projects.MapGet("/access-list", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListAccessListAsync(cancellationToken)))
            .WithName("ListDevelopmentProjectAccessItems");

        projects.MapGet("/hierarchy-links", async (
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.ListHierarchyLinksAsync(cancellationToken)))
            .WithName("ListDevelopmentProjectHierarchyLinks");

        projects.MapGet("/{projectId:guid}", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.GetAsync(projectId, cancellationToken)))
            .WithName("GetDevelopmentProjectEditor");

        projects.MapPost("/", async (
                ProjectEditorModel request,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await projectsService.SaveAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentProject");

        projects.MapDelete("/{projectId:guid}", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            {
                await projectsService.DeleteAsync(projectId, cancellationToken);
                return Results.Ok(new DevelopmentApiAck(true));
            })
            .WithName("DeleteDevelopmentProject");

        projects.MapGet("/{projectId:guid}/hierarchy", async (
                Guid projectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            Results.Ok(await projectsService.GetHierarchyAsync(projectId, cancellationToken)))
            .WithName("GetDevelopmentProjectHierarchy");

        projects.MapPost("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", async (
                Guid parentProjectId,
                Guid childProjectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await projectsService.AddSubprojectAsync(
                parentProjectId,
                childProjectId,
                cancellationToken)))
            .WithName("AttachDevelopmentProjectSubproject");

        projects.MapDelete("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", async (
                Guid parentProjectId,
                Guid childProjectId,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await projectsService.RemoveSubprojectAsync(
                parentProjectId,
                childProjectId,
                cancellationToken)))
            .WithName("DetachDevelopmentProjectSubproject");

        projects.MapPost("/{childProjectId:guid}/reconnect-subproject", async (
                Guid childProjectId,
                ProjectReconnectSubprojectApiRequest request,
                ProjectsService projectsService,
                CancellationToken cancellationToken) =>
            DevelopmentApiEndpointResults.FromResult(await projectsService.ReconnectSubprojectAsync(
                childProjectId,
                request.CurrentParentProjectId,
                request.NewParentProjectId,
                cancellationToken)))
            .WithName("ReconnectDevelopmentProjectSubproject");

        return group;
    }
}

internal sealed record ProjectReconnectSubprojectApiRequest(
    Guid CurrentParentProjectId,
    Guid NewParentProjectId);
