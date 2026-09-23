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
        definitions.MapGet("", ListAsync).WithName("ListProcessDefinitions").Produces<ProcessDefinitionCatalogApiResponse>().ProducesApiErrors(400, 401, 403, 503)
            .DescribeApi("List process definitions", "Read the current global process catalog. Requires process read capability when API authorization is enabled.", "Matching process definition catalog entries.");
        definitions.MapGet("/{definitionKey}", ([Description("Opaque catalog key returned by the definition list; URL-encode it and preserve its value.")] string definitionKey, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, catalog, key => editor.GetEditorAsync(ProcessWorkspaceShellScope.Global, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinition").Produces<ProcessDefinitionEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition", "Read the current global definition overview with optional authoring projections. Requires process read capability; exposes no definition write operation.", "Current definition overview projection.");
        definitions.MapGet("/{definitionKey}/roles", ([Description("Opaque definition catalog key returned by the definition list.")] string definitionKey, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionRoleEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, catalog, key => editor.GetEditorAsync(ProcessWorkspaceShellScope.Global, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinitionRoles").Produces<ProcessDefinitionRoleEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition roles", "Read role staffing, step bindings and validation from the current definition. Requires process read capability; authoring command availability is informational.", "Current definition role projection.");
        definitions.MapGet("/{definitionKey}/steps", ([Description("Opaque definition catalog key returned by the definition list.")] string definitionKey, ProcessDefinitionCatalogProjectionService catalog,
            ProcessDefinitionStepEditorProjectionService editor, CancellationToken cancellationToken) =>
            ReadAsync(definitionKey, catalog, key => editor.GetEditorAsync(ProcessWorkspaceShellScope.Global, key, cancellationToken), cancellationToken))
            .WithName("GetProcessDefinitionSteps").Produces<ProcessDefinitionStepEditorProjection>().ProducesApiErrors(400, 401, 403, 404, 503)
            .DescribeApi("Read process definition steps", "Read step contracts, routes, artifacts and subprocess bindings from the current definition. Requires process read capability; performs no execution.", "Current definition step projection.");
    }

    private static async Task<IResult> ListAsync([Description("Optional catalog search text, at most 256 characters.")] string? searchText,
        [Description("Catalog scope filter; 0 All, 1 Global, 2 Project. Omitted means All.")] ProcessDefinitionCatalogScopeKind? scopeFilter,
        ProcessDefinitionCatalogProjectionService catalog, CancellationToken cancellationToken) {
        if (searchText?.Length > 256 || scopeFilter is { } filter && !Enum.IsDefined(filter)) {
            return ApiEndpointResults.BadRequest("Invalid process catalog filter.", "processes.definition.filter-invalid");
        }
        return Results.Ok(new ProcessDefinitionCatalogApiResponse(await catalog.GetCompleteCatalogItemsAsync(
            ProcessWorkspaceShellScope.Global, searchText, scopeFilter ?? ProcessDefinitionCatalogScopeKind.All, cancellationToken)));
    }

    private static async Task<IResult> ReadAsync<T>(string definitionKey, ProcessDefinitionCatalogProjectionService catalog,
        Func<ProcessDefinitionCatalogItemKey, Task<T>> read, CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(definitionKey) || definitionKey.Length > 256) {
            return ApiEndpointResults.BadRequest("Invalid process definition key.", "processes.definition.key-invalid");
        }
        var items = await catalog.GetCompleteCatalogItemsAsync(ProcessWorkspaceShellScope.Global, cancellationToken: cancellationToken);
        var item = items.FirstOrDefault(item => string.Equals(item.Key.Value, definitionKey, StringComparison.OrdinalIgnoreCase));
        return item is null
            ? ApiEndpointResults.NotFound("The process definition was not found.", "processes.definition.not-found")
            : Results.Ok(await read(item.Key));
    }
}
