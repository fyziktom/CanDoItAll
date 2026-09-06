using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Web.Api;

internal sealed record ProcessDefinitionCatalogApiResponse(
    IReadOnlyList<ProcessDefinitionCatalogItemProjection> Items);

internal static class ProcessDefinitionsApi
{
    public static void MapProcessDefinitionsApi(this RouteGroupBuilder processes)
    {
        var definitions = processes.MapGroup("/definitions")
            .WithTags("Process Definitions");

        definitions.MapGet(string.Empty, ListDefinitionsAsync)
            .WithName("ListProcessDefinitions")
            .WithDescription("Lists the process definition catalog, optionally filtered by search text and scope.")
            .Produces<ProcessDefinitionCatalogApiResponse>(StatusCodes.Status200OK);

        definitions.MapGet("/{definitionKey}", GetDefinitionAsync)
            .WithName("GetProcessDefinition")
            .WithDescription("Gets the overview/editor projection for a single process definition.")
            .Produces<ProcessDefinitionEditorProjection>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        definitions.MapGet("/{definitionKey}/roles", GetDefinitionRolesAsync)
            .WithName("GetProcessDefinitionRoles")
            .WithDescription("Gets the role editor projection for a single process definition.")
            .Produces<ProcessDefinitionRoleEditorProjection>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        definitions.MapGet("/{definitionKey}/steps", GetDefinitionStepsAsync)
            .WithName("GetProcessDefinitionSteps")
            .WithDescription("Gets the step editor projection for a single process definition.")
            .Produces<ProcessDefinitionStepEditorProjection>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> ListDefinitionsAsync(
        string? searchText,
        ProcessDefinitionCatalogScopeKind? scopeFilter,
        ProcessDefinitionCatalogProjectionService catalogService,
        CancellationToken cancellationToken)
    {
        var items = await catalogService.GetCompleteCatalogItemsAsync(
            ProcessWorkspaceShellScope.Global,
            searchText,
            scopeFilter ?? ProcessDefinitionCatalogScopeKind.All,
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(new ProcessDefinitionCatalogApiResponse(items));
    }

    private static async Task<IResult> GetDefinitionAsync(
        string definitionKey,
        ProcessDefinitionEditorProjectionService editorService,
        CancellationToken cancellationToken)
    {
        try
        {
            var editor = await editorService.GetEditorAsync(
                ProcessWorkspaceShellScope.Global,
                new ProcessDefinitionCatalogItemKey(definitionKey),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(editor);
        }
        catch (InvalidOperationException)
        {
            return ApiEndpointResults.NotFound(
                $"Process definition '{definitionKey}' was not found.",
                "processes.definition.not-found");
        }
    }

    private static async Task<IResult> GetDefinitionRolesAsync(
        string definitionKey,
        ProcessDefinitionRoleEditorProjectionService roleService,
        CancellationToken cancellationToken)
    {
        try
        {
            var roles = await roleService.GetEditorAsync(
                ProcessWorkspaceShellScope.Global,
                new ProcessDefinitionCatalogItemKey(definitionKey),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(roles);
        }
        catch (InvalidOperationException)
        {
            return ApiEndpointResults.NotFound(
                $"Process definition '{definitionKey}' was not found.",
                "processes.definition.not-found");
        }
    }

    private static async Task<IResult> GetDefinitionStepsAsync(
        string definitionKey,
        ProcessDefinitionStepEditorProjectionService stepService,
        CancellationToken cancellationToken)
    {
        try
        {
            var steps = await stepService.GetEditorAsync(
                ProcessWorkspaceShellScope.Global,
                new ProcessDefinitionCatalogItemKey(definitionKey),
                cancellationToken).ConfigureAwait(false);
            return Results.Ok(steps);
        }
        catch (InvalidOperationException)
        {
            return ApiEndpointResults.NotFound(
                $"Process definition '{definitionKey}' was not found.",
                "processes.definition.not-found");
        }
    }
}
