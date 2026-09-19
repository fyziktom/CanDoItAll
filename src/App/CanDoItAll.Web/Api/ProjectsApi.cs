using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Web.Api;

internal static class ProjectsApi
{
    public static RouteGroupBuilder MapProjectsApi(this RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects")
            .WithTags("Projects");

        projects.MapGet("/", ListProjectsAsync)
            .WithName("ListProjects")
            .Produces<IReadOnlyList<ProjectSummary>>();

        projects.MapGet("/access-list", ListProjectAccessItemsAsync)
            .WithName("ListProjectAccessItems")
            .Produces<IReadOnlyList<ProjectAccessListItem>>();

        projects.MapGet("/hierarchy-links", ListHierarchyLinksAsync)
            .WithName("ListProjectHierarchyLinks")
            .Produces<IReadOnlyList<ProjectHierarchyLinkSummary>>();

        projects.MapGet("/{projectId:guid}", GetProjectEditorAsync)
            .WithName("GetProjectEditor")
            .Produces<ProjectEditorModel>();

        projects.MapPost("/", SaveProjectAsync)
            .WithName("SaveProject")
            .Produces<Guid>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        projects.MapDelete("/{projectId:guid}", DeleteProjectAsync)
            .WithName("DeleteProject")
            .Produces<ProjectDeletionResult>(StatusCodes.Status200OK)
            .Produces<ProjectDeletionCleanupPendingApiResponse>(StatusCodes.Status409Conflict);

        projects.MapGet("/deletion-cleanups", ListPendingDeletionCleanupsAsync)
            .WithName("ListPendingProjectDeletionCleanups")
            .Produces<IReadOnlyList<ProjectDeletionPendingCleanup>>(StatusCodes.Status200OK);

        projects.MapGet("/deletion-completion-notices", ListDeletionCompletionNoticesAsync)
            .WithName("ListProjectDeletionCompletionNotices")
            .Produces<IReadOnlyList<ProjectDeletionCompletionNotice>>(StatusCodes.Status200OK);

        projects.MapPost(
                "/{projectId:guid}/deletion-cleanups/{participantId}/{recoveryId:guid}/retry",
                RetryDeletionCleanupAsync)
            .WithName("RetryPendingProjectDeletionCleanup")
            .Produces<ProjectDeletionResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .ProducesApiErrors(StatusCodes.Status404NotFound)
            .Produces<ProjectDeletionCleanupPendingApiResponse>(StatusCodes.Status409Conflict);

        projects.MapGet("/{projectId:guid}/hierarchy", GetProjectHierarchyAsync)
            .WithName("GetProjectHierarchy")
            .Produces<ProjectHierarchySnapshot>();

        projects.MapPost("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", AttachSubprojectAsync)
            .WithName("AttachProjectSubproject")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        projects.MapDelete("/{parentProjectId:guid}/subprojects/{childProjectId:guid}", DetachSubprojectAsync)
            .WithName("DetachProjectSubproject")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        projects.MapPost("/{childProjectId:guid}/reconnect-subproject", ReconnectSubprojectAsync)
            .WithName("ReconnectProjectSubproject")
            .Produces<ApiAck>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        return group;
    }

    /// <summary>
    /// List all projects with their status, hierarchy counts and main participants.
    /// </summary>
    /// <remarks>
    /// Returns every project, most recently updated first; the list is not paged or filtered. A project is the
    /// Projects-owned business record; its task and node hierarchy is read through <c>/api/project-structure</c>. Each
    /// summary carries the status, current phase, number of phases, number of parent and child projects, and the
    /// parties that take part in the project as a whole (not only in one of its tasks) together with the resulting
    /// primary customer, delivery unit and owner names.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <response code="200">All projects; an empty array when there are none.</response>
    internal static async Task<IResult> ListProjectsAsync(
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.ListAsync(cancellationToken));

    /// <summary>
    /// List the identifier and name of every project, ordered by name.
    /// </summary>
    /// <remarks>
    /// A lightweight alternative to <c>GET /api/projects</c> for pickers: one item per project with only its identifier
    /// and name, ordered by name. It lists every project and is not filtered by the caller.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <response code="200">One item per project; an empty array when there are none.</response>
    internal static async Task<IResult> ListProjectAccessItemsAsync(
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.ListAccessListAsync(cancellationToken));

    /// <summary>
    /// List every parent-child link between projects.
    /// </summary>
    /// <remarks>
    /// Returns all links of the project hierarchy, ordered by parent and then child project identifier. A project can
    /// have several parents and several children; the attach and reconnect operations reject links that would create a
    /// cycle. Use <c>GET /api/projects/{projectId}/hierarchy</c> for the direct parents and children of one project
    /// with their summaries.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <response code="200">All hierarchy links; an empty array when no project has a parent.</response>
    internal static async Task<IResult> ListHierarchyLinksAsync(
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.ListHierarchyLinksAsync(cancellationToken));

    /// <summary>
    /// Read the editable fields of a project, or a blank template when no project has the identifier.
    /// </summary>
    /// <remarks>
    /// Returns the project's name, description, objective, status, current phase, target date, phases and technology
    /// options, and <c>expectedLifetimeId</c>, the identifier of the project's current lifetime. Change the values and
    /// send the whole object to <c>POST /api/projects</c> to update the project.
    ///
    /// When no project has the identifier the response is still 200: a blank template for a new project whose
    /// <c>id</c> and <c>expectedLifetimeId</c> are null. Check <c>id</c> before treating the response as an existing
    /// project.
    ///
    /// <c>options</c> always contains an entry for each of the categories Language, Database, Ui, ExternalApi, Storage,
    /// Deployment and Testing; an entry without a stored value has a null <c>id</c> and an empty name.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="projectId">Identifier of the project, as returned by <c>GET /api/projects</c>.</param>
    /// <response code="200">The project's editable fields, or a blank template with a null <c>id</c>.</response>
    internal static async Task<IResult> GetProjectEditorAsync(
        Guid projectId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.GetAsync(projectId, cancellationToken));

    /// <summary>
    /// Create a project, or overwrite the editable fields, phases and options of an existing project.
    /// </summary>
    /// <remarks>
    /// Without <c>id</c> a new project with a new identifier is created; every such call creates another project. With
    /// <c>id</c> the project with that identifier is updated: all editable fields are overwritten with the values sent,
    /// phases and options missing from the request are deleted, entries whose <c>id</c> matches a stored entry are
    /// updated and the other entries are added. Start from <c>GET /api/projects/{projectId}</c> and send the complete
    /// object. Phases are stored in array order; option entries with an empty name and empty notes are skipped.
    ///
    /// Send <c>expectedLifetimeId</c> from the read: when the project was deleted and recreated with the same
    /// identifier since then, the save is rejected with <c>projects.lifetime-changed</c>. Without it the save applies
    /// to the current project with that identifier. There is no other concurrency check; the last save wins. Subproject
    /// links are changed with the subproject operations, not with this request.
    ///
    /// The project is saved first; the search index and the activity feed are updated afterwards, and failures of those
    /// updates are logged without changing the response.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="request">
    /// The project's editable fields, phases and options; omit <c>id</c> to create a project.
    /// </param>
    /// <response code="200">
    /// The project was saved. The body is its identifier as a JSON string (a GUID); read it back with
    /// <c>GET /api/projects/{projectId}</c>.
    /// </response>
    /// <response code="400">
    /// The project was not saved: <c>validation</c> (blank name) or <c>projects.lifetime-changed</c>
    /// (<c>expectedLifetimeId</c> names an earlier lifetime of the project; read it again). A body the framework cannot
    /// bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    /// <response code="404">
    /// No project has the sent <c>id</c> (<c>projects.not-found</c>), for example because it was deleted. Omit
    /// <c>id</c> to create a project.
    /// </response>
    internal static async Task<IResult> SaveProjectAsync(
        ProjectEditorModel request,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(
            await projectsService.SaveAsync(request, cancellationToken),
            ProjectErrorCodes.NotFound);

    /// <summary>
    /// Delete a project and have the other modules clean up the data they keep for it.
    /// </summary>
    /// <remarks>
    /// Deletes the project record with its phases, options and hierarchy links, retires its lifetime and removes its
    /// search entries and storage routing. Then every registered deletion participant cleans up its own data for the
    /// project: currently <c>workbench</c> (Project Structure nodes and managed media) and
    /// <c>agent-project-structure-access</c> (agents' access to the project). Subprojects are kept; only their links to
    /// this project are removed.
    ///
    /// Outcomes:
    ///
    /// - 200: the project is deleted and every participant finished. <c>warnings</c> lists objects that were kept, such
    /// as managed media retained by a storage provider, with remediation text.
    /// - 409 with <c>projects.delete-cleanup-pending</c>: the project is deleted, but at least one participant did not
    /// finish. The deletion is not rolled back. Retry each entry of <c>recovery.failures</c> with
    /// <c>POST /api/projects/{projectId}/deletion-cleanups/{participantId}/{recoveryId}/retry</c>, using its exact
    /// <c>participantId.value</c> and <c>recoveryId</c>; do not delete the project again to finish the cleanup.
    ///
    /// Deleting an identifier that has no project still runs the participant cleanups for it and returns the same
    /// outcomes.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="projectId">Identifier of the project to delete, as returned by <c>GET /api/projects</c>.</param>
    /// <response code="200">
    /// The project is deleted (or did not exist) and all cleanup finished; <c>warnings</c> lists retained objects.
    /// </response>
    /// <response code="409">
    /// The project is deleted but cleanup is incomplete (<c>projects.delete-cleanup-pending</c>); <c>recovery</c> lists
    /// the participant cleanups to retry.
    /// </response>
    internal static async Task<IResult> DeleteProjectAsync(
        Guid projectId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// List the project-deletion cleanups that have not finished.
    /// </summary>
    /// <remarks>
    /// After a project is deleted, each deletion participant cleans up its own data for it (currently <c>workbench</c>
    /// for Project Structure nodes and managed media and <c>agent-project-structure-access</c> for agents' access to
    /// the project). This read returns one item for each participant cleanup that has not finished, across all
    /// projects, ordered by project, participant and recovery identifier.
    ///
    /// Retry an item with <c>POST /api/projects/{projectId}/deletion-cleanups/{participantId}/{recoveryId}/retry</c>,
    /// using its <c>projectId</c>, <c>participantId.value</c> and <c>recoveryId</c>. When <c>canRetryNow</c> is false,
    /// another attempt is running and holds the cleanup until <c>retryAvailableAtUtc</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <response code="200">The unfinished cleanups; an empty array when nothing is pending.</response>
    internal static async Task<IResult> ListPendingDeletionCleanupsAsync(
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.ListPendingDeletionCleanupsAsync(cancellationToken));

    /// <summary>
    /// List the finished project-deletion cleanups and the objects they kept.
    /// </summary>
    /// <remarks>
    /// Returns one notice for each finished participant cleanup of a project deletion, and for each finished cleanup of
    /// deleted Project Structure nodes that kept objects, across all projects, ordered by project, participant and
    /// recovery identifier. <c>warnings</c> lists managed media that was kept, for example by an immutable storage
    /// provider, with remediation text. Reading the notices does not remove them.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <response code="200">The completion notices; an empty array when there are none.</response>
    internal static async Task<IResult> ListDeletionCompletionNoticesAsync(
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.ListDeletionCompletionNoticesAsync(
            cancellationToken));

    /// <summary>
    /// Retry one unfinished participant cleanup of a deleted project.
    /// </summary>
    /// <remarks>
    /// Runs again the cleanup identified by the project, participant and recovery identifiers, taken from
    /// <c>recovery.failures</c> of a 409 deletion response or from <c>GET /api/projects/deletion-cleanups</c>. Use the
    /// exact identifiers; deleting the project again does not finish an earlier cleanup. When the cleanup has already
    /// finished, the response is 200 with the warnings recorded for it, so repeating a successful retry is safe.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="projectId">Identifier of the deleted project.</param>
    /// <param name="participantId">
    /// Text identifier of the deletion participant, the <c>participantId.value</c> of the cleanup, for example
    /// <c>workbench</c>. Surrounding whitespace is ignored; the rest must match exactly.
    /// </param>
    /// <param name="recoveryId">Recovery identifier of the cleanup, from the same source as the participant.</param>
    /// <response code="200">
    /// The cleanup has finished, now or earlier; <c>warnings</c> lists retained objects.
    /// </response>
    /// <response code="400">
    /// The participant identifier is blank (<c>projects.delete-cleanup-participant-invalid</c>).
    /// </response>
    /// <response code="404">
    /// No unfinished or finished cleanup matches the project, participant and recovery identifiers
    /// (<c>projects.delete-cleanup-not-found</c>).
    /// </response>
    /// <response code="409">
    /// The cleanup is still incomplete (<c>projects.delete-cleanup-pending</c>); the project stays deleted. Retry later
    /// with the identifiers in <c>recovery.failures</c>.
    /// </response>
    internal static async Task<IResult> RetryDeletionCleanupAsync(
        Guid projectId,
        string participantId,
        Guid recoveryId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Read the direct parent and child projects of a project.
    /// </summary>
    /// <remarks>
    /// Returns the summaries of the projects directly above and directly below the project in the project hierarchy,
    /// each list most recently updated first. A project can have several parents. When no project has the identifier,
    /// both lists are empty and the response is still 200.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="projectId">Identifier of the project, as returned by <c>GET /api/projects</c>.</param>
    /// <response code="200">The project's direct parents and children; empty lists when it has none.</response>
    internal static async Task<IResult> GetProjectHierarchyAsync(
        Guid projectId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => Results.Ok(await projectsService.GetHierarchyAsync(projectId, cancellationToken));

    /// <summary>
    /// Make an existing project a subproject of another existing project.
    /// </summary>
    /// <remarks>
    /// Adds a parent-child link between two projects. A project can have several parents, so the child's other parent
    /// links are kept; use <c>POST /api/projects/{childProjectId}/reconnect-subproject</c> to move a subproject from
    /// one parent to another. Attaching a link that already exists succeeds without a change. A link that would make a
    /// project its own ancestor is rejected.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="parentProjectId">Identifier of the project that becomes the parent.</param>
    /// <param name="childProjectId">Identifier of the project that becomes the subproject.</param>
    /// <response code="200">The link exists.</response>
    /// <response code="400">
    /// Nothing was changed (<c>validation</c>): both identifiers are equal, the parent or the subproject was not found,
    /// or the link would create a cycle. The message says which.
    /// </response>
    internal static async Task<IResult> AttachSubprojectAsync(
        Guid parentProjectId,
        Guid childProjectId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(await projectsService.AddSubprojectAsync(
            parentProjectId,
            childProjectId,
            cancellationToken));

    /// <summary>
    /// Remove the link between a parent project and one of its subprojects.
    /// </summary>
    /// <remarks>
    /// Deletes only the parent-child link; both projects and the subproject's other parent links are kept. Detaching a
    /// link that does not exist fails.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="parentProjectId">Identifier of the parent project.</param>
    /// <param name="childProjectId">Identifier of the subproject to detach.</param>
    /// <response code="200">The link was removed.</response>
    /// <response code="400">
    /// Nothing was changed (<c>validation</c>): the link does not exist, or one of the projects no longer exists.
    /// </response>
    internal static async Task<IResult> DetachSubprojectAsync(
        Guid parentProjectId,
        Guid childProjectId,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(await projectsService.RemoveSubprojectAsync(
            parentProjectId,
            childProjectId,
            cancellationToken));

    /// <summary>
    /// Move a subproject from its current parent project to another parent project.
    /// </summary>
    /// <remarks>
    /// Removes the link from <c>currentParentProjectId</c> and adds a link from <c>newParentProjectId</c> in one save.
    /// When the subproject is already linked to the new parent, only the old link is removed. The subproject's other
    /// parent links are kept. A move that would make the subproject an ancestor of its new parent is rejected.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host.
    /// </remarks>
    /// <param name="childProjectId">Identifier of the subproject to move.</param>
    /// <param name="request">The current and the new parent project.</param>
    /// <response code="200">The subproject was moved.</response>
    /// <response code="400">
    /// Nothing was changed (<c>validation</c>): both parents are the same, the subproject is not linked to
    /// <c>currentParentProjectId</c>, one of the three projects was not found, or the move would create a cycle. A body
    /// the framework cannot bind is rejected with HTTP 400 without the <c>errors</c> envelope.
    /// </response>
    internal static async Task<IResult> ReconnectSubprojectAsync(
        Guid childProjectId,
        ProjectReconnectSubprojectApiRequest request,
        ProjectsService projectsService,
        CancellationToken cancellationToken)
        => ApiEndpointResults.FromResult(await projectsService.ReconnectSubprojectAsync(
            childProjectId,
            request.CurrentParentProjectId,
            request.NewParentProjectId,
            cancellationToken));
}

/// <summary>
/// Request of <c>POST /api/projects/{childProjectId}/reconnect-subproject</c>: the parent to leave and the parent to
/// join.
/// </summary>
/// <param name="CurrentParentProjectId">
/// Identifier of the parent project the subproject is linked to now; the link must exist.
/// </param>
/// <param name="NewParentProjectId">
/// Identifier of the existing project that becomes the new parent; must differ from <c>currentParentProjectId</c>.
/// </param>
internal sealed record ProjectReconnectSubprojectApiRequest(
    Guid CurrentParentProjectId,
    Guid NewParentProjectId);

/// <summary>
/// Error body of a project deletion or cleanup retry that deleted the project but could not finish every participant
/// cleanup (HTTP 409). Unlike the general <c>errors</c> envelope it carries the recovery details to retry.
/// </summary>
/// <param name="Code">Stable code of the outcome, always <c>projects.delete-cleanup-pending</c>.</param>
/// <param name="Message">Human-readable explanation for operators and logs. Its wording can change.</param>
/// <param name="Recovery">
/// The participant cleanups that did not finish, with the identifiers to use for
/// <c>POST /api/projects/{projectId}/deletion-cleanups/{participantId}/{recoveryId}/retry</c>.
/// </param>
internal sealed record ProjectDeletionCleanupPendingApiResponse(
    string Code,
    string Message,
    ProjectDeletionRecovery Recovery);
