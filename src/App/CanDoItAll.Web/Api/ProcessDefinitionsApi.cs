using CanDoItAll.Processes.Application;
using System.ComponentModel;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Web.Api;

[Description("Complete process definition catalog result for the bounded search and scope filter.")]
internal sealed record ProcessDefinitionCatalogApiResponse(
    [property: Description("Definition catalog entries matching the requested search and scope filter.")] IReadOnlyList<ProcessDefinitionCatalogItemProjection> Items);

internal static class ProcessDefinitionsApi {
    public static void MapProcessDefinitionsApi(this RouteGroupBuilder processes) {
        var definitions = processes.MapGroup("/definitions").WithTags("Process definitions");
        definitions.MapGet("", ListAsync).WithName("ListProcessDefinitions").Produces<ProcessDefinitionCatalogApiResponse>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("List process definitions", "Read the global catalog, or the current project catalog including overlays when projectId is supplied. Requires process read capability; catalog availability is not launch eligibility.", "Matching process definition catalog entries.");
        definitions.MapGet("/{definitionKey}", ([Description("Opaque catalog key returned by the definition list; URL-encode it and preserve its value.")] string definitionKey, Guid? projectId, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, projectId, catalog, (scope, key) => editor.GetEditorAsync(scope, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinition").Produces<ProcessDefinitionEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition", "Read the definition overview in the optional project scope, or globally when projectId is omitted. Requires process read capability; exposes no definition write operation.", "Current definition overview projection.");
        definitions.MapGet("/{definitionKey}/roles", ([Description("Opaque definition catalog key returned by the definition list.")] string definitionKey, Guid? projectId, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionRoleEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, projectId, catalog, (scope, key) => editor.GetEditorAsync(scope, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinitionRoles").Produces<ProcessDefinitionRoleEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition roles", "Read role staffing, step bindings and validation in the optional project scope. Omitted projectId means global. Requires process read capability; authoring command availability is informational.", "Current definition role projection.");
        definitions.MapGet("/{definitionKey}/steps", ([Description("Opaque definition catalog key returned by the definition list.")] string definitionKey, Guid? projectId, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionStepEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, projectId, catalog, (scope, key) => editor.GetEditorAsync(scope, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinitionSteps").Produces<ProcessDefinitionStepEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition steps", "Read step contracts, routes, artifacts and subprocess bindings in the optional project scope. Omitted projectId means global. Requires process read capability; performs no execution.", "Current definition step projection.");
    }

    private static async Task<IResult> ListAsync([Description("Optional catalog search text, at most 256 characters.")] string? searchText,
        [Description("Catalog scope filter; 0 All, 1 Global, 2 Project. Omitted means All.")] ProcessDefinitionCatalogScopeKind? scopeFilter,
        [Description("Optional current project ID. Omit for the global catalog; an empty ID is invalid.")] Guid? projectId,
        ProcessDefinitionCatalogProjectionService catalog, CancellationToken cancellationToken) {
        if (searchText?.Length > 256 || scopeFilter is { } filter && !Enum.IsDefined(filter)) {
            return ApiEndpointResults.BadRequest("Invalid process catalog filter.", "processes.definition.filter-invalid");
        }
        return await InScopeAsync(projectId, async scope => Results.Ok(new ProcessDefinitionCatalogApiResponse(
            await catalog.GetCompleteCatalogItemsAsync(scope, searchText, scopeFilter ?? ProcessDefinitionCatalogScopeKind.All, cancellationToken))));
    }

    private static Task<IResult> ReadAsync<T>(string definitionKey, Guid? projectId, ProcessDefinitionCatalogProjectionService catalog,
        Func<ProcessWorkspaceShellScope, ProcessDefinitionCatalogItemKey, Task<T>> read, CancellationToken cancellationToken) =>
        InScopeAsync(projectId, async scope => {
        if (string.IsNullOrWhiteSpace(definitionKey) || definitionKey.Length > 256) {
            return ApiEndpointResults.BadRequest("Invalid process definition key.", "processes.definition.key-invalid");
        }
        var items = await catalog.GetCompleteCatalogItemsAsync(scope, cancellationToken: cancellationToken);
        var item = items.FirstOrDefault(item => string.Equals(item.Key.Value, definitionKey, StringComparison.OrdinalIgnoreCase));
        return item is null
            ? ApiEndpointResults.NotFound("The process definition was not found.", "processes.definition.not-found")
            : Results.Ok(await read(scope, item.Key));
    });

    private static async Task<IResult> InScopeAsync(Guid? projectId, Func<ProcessWorkspaceShellScope, Task<IResult>> read) {
        if (projectId == Guid.Empty) {
            return ApiEndpointResults.BadRequest("A nonempty project ID is required.", "processes.definition.project-invalid");
        }
        try {
            return await read(projectId is { } id ? ProcessWorkspaceShellScope.ForProject(id) : ProcessWorkspaceShellScope.Global);
        } catch (ProcessAuthoringProjectUnavailableException) {
            return ApiEndpointResults.NotFound("The authoring project no longer exists.", "processes.definition.project-unavailable");
        }
    }
}
