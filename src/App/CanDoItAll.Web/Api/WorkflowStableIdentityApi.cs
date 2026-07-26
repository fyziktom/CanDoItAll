using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
namespace CanDoItAll.Web.Api;

internal static class WorkflowStableIdentityApi
{
    public static RouteGroupBuilder MapWorkflowStableIdentityApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/definitions", async (
                string? externalNamespace,
                string? externalKey,
                IWorkflowCatalogService catalogService,
                IWorkflowStableIdentityLookupService stableIdentityLookup,
                CancellationToken cancellationToken) =>
            await ListWorkflowDefinitionsAsync(
                externalNamespace,
                externalKey,
                catalogService,
                stableIdentityLookup,
                cancellationToken))
            .WithName("ListWorkflowDefinitions")
            .Produces<WorkflowCatalogItem[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapGet("/definitions/by-template-key/{templateKey}", async (
                string templateKey,
                IWorkflowStableIdentityLookupService stableIdentityLookup,
                CancellationToken cancellationToken) =>
            await ResolveStableIdentityAsync(
                () => stableIdentityLookup.ResolveByTemplateKeyAsync(
                    templateKey,
                    cancellationToken)))
            .WithName("GetWorkflowDefinitionByTemplateKey")
            .Produces<WorkflowStableIdentityResolution>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapGet("/definitions/by-external-key/{externalNamespace}/{externalKey}", async (
                string externalNamespace,
                string externalKey,
                IWorkflowStableIdentityLookupService stableIdentityLookup,
                CancellationToken cancellationToken) =>
            await ResolveStableIdentityAsync(
                () => stableIdentityLookup.ResolveByExternalKeyAsync(
                    externalNamespace,
                    externalKey,
                    cancellationToken)))
            .WithName("GetWorkflowDefinitionByExternalKey")
            .Produces<WorkflowStableIdentityResolution>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return workflows;
    }

    private static async Task<IResult> ListWorkflowDefinitionsAsync(
        string? externalNamespace,
        string? externalKey,
        IWorkflowCatalogService catalogService,
        IWorkflowStableIdentityLookupService stableIdentityLookup,
        CancellationToken cancellationToken)
    {
        var hasExternalNamespace = !string.IsNullOrWhiteSpace(externalNamespace);
        var hasExternalKey = !string.IsNullOrWhiteSpace(externalKey);
        if (hasExternalNamespace != hasExternalKey)
        {
            return ApiEndpointResults.BadRequest(
                "externalNamespace and externalKey must be supplied together.",
                "workflows.external-identity-incomplete");
        }

        if (!hasExternalNamespace)
        {
            return Results.Ok(await catalogService.ListDefinitionsAsync(cancellationToken));
        }

        return await ResolveStableIdentityAsync(
            () => stableIdentityLookup.ResolveByExternalKeyAsync(
                externalNamespace!,
                externalKey!,
                cancellationToken),
            materializationsOnly: true);
    }

    private static async Task<IResult> ResolveStableIdentityAsync(
        Func<Task<WorkflowStableIdentityResolution>> resolve,
        bool materializationsOnly = false)
    {
        try
        {
            var resolution = await resolve();
            return materializationsOnly
                ? Results.Ok(resolution.Materializations)
                : Results.Ok(resolution);
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(
                exception.Message,
                "workflows.stable-identity-invalid");
        }
    }
}
