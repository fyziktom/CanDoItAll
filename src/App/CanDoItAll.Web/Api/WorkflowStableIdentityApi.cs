using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
namespace CanDoItAll.Web.Api;

internal static class WorkflowStableIdentityApi
{
    public static RouteGroupBuilder MapWorkflowStableIdentityApi(this RouteGroupBuilder workflows)
    {
        workflows.MapGet("/definitions", ListDefinitionsAsync)
            .WithName("ListWorkflowDefinitions")
            .Produces<WorkflowCatalogItem[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapGet("/definitions/by-template-key/{templateKey}", GetDefinitionByTemplateKeyAsync)
            .WithName("GetWorkflowDefinitionByTemplateKey")
            .Produces<WorkflowStableIdentityResolution>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapGet(
                "/definitions/by-external-key/{externalNamespace}/{externalKey}",
                GetDefinitionByExternalKeyAsync)
            .WithName("GetWorkflowDefinitionByExternalKey")
            .Produces<WorkflowStableIdentityResolution>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        return workflows;
    }

    /// <summary>
    /// List the workflow definitions, or find the one bound to an external identity.
    /// </summary>
    /// <remarks>
    /// Without query parameters this returns one catalog item for every workflow: the metadata of its current
    /// version, whatever its status, most recently updated first. There is no paging.
    ///
    /// With <c>externalNamespace</c> and <c>externalKey</c> it returns only the workflows bound to that external
    /// identity: an empty array when none is, and normally at most one item because an external identity is unique.
    /// Supply both parameters or neither. To learn whether the identity can be started and which version would run, use
    /// <c>GET /api/workflows/definitions/by-external-key/{externalNamespace}/{externalKey}</c> instead, which also
    /// reports the resolution status.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="externalNamespace">
    /// Optional namespace of an external identity, for example <c>partner.system</c>. Required together with
    /// <c>externalKey</c>. It is trimmed and lower-cased; at most 100 characters of ASCII letters, digits, hyphen,
    /// underscore, period and colon.
    /// </param>
    /// <param name="externalKey">
    /// Optional key of an external identity within the namespace, for example <c>invoice:review</c>. Required
    /// together with <c>externalNamespace</c>. It is trimmed and lower-cased; at most 200 characters of the same
    /// characters.
    /// </param>
    /// <response code="200">The catalog items; an empty array when nothing matches.</response>
    /// <response code="400">
    /// Only one of the two parameters was supplied (<c>workflows.external-identity-incomplete</c>), or a value is
    /// too long or contains another character (<c>workflows.stable-identity-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    internal static async Task<IResult> ListDefinitionsAsync(
        string? externalNamespace,
        string? externalKey,
        IWorkflowCatalogService catalogService,
        IWorkflowStableIdentityLookupService stableIdentityLookup,
        CancellationToken cancellationToken)
        => await ListWorkflowDefinitionsAsync(
            externalNamespace,
            externalKey,
            catalogService,
            stableIdentityLookup,
            cancellationToken);

    /// <summary>
    /// Resolve the workflow installed from a template key to the version that would run.
    /// </summary>
    /// <remarks>
    /// Looks up the workflows whose template provenance carries this template key and reports the outcome in
    /// <c>status</c>; every outcome is returned with HTTP 200, so always check <c>status</c>:
    ///
    /// - Resolved: exactly one workflow matches and <c>runnableVersionId</c> names the version a latest-Active start
    /// would run.
    /// - NotFound: no workflow was installed from this template.
    /// - Ambiguous: several workflows carry the key; nothing is selected until the catalog is repaired.
    /// - Stale: one workflow matches but it has no runnable Active version.
    ///
    /// <c>materializations</c> lists every matching workflow in all cases. A template key identifies where a workflow
    /// came from; it is not unique and it is not the workflow identifier. Start the resolved workflow with
    /// <c>POST /api/workflows/runs/start</c> using <c>workflowId</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="templateKey">
    /// Template key to resolve, for example <c>billing.review</c>. It is trimmed and lower-cased before matching; at
    /// most 200 characters of ASCII letters, digits, hyphen, underscore, period and colon.
    /// </param>
    /// <response code="200">The resolution, whatever its <c>status</c>.</response>
    /// <response code="400">
    /// The key is blank, too long or contains another character (<c>workflows.stable-identity-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    internal static async Task<IResult> GetDefinitionByTemplateKeyAsync(
        string templateKey,
        IWorkflowStableIdentityLookupService stableIdentityLookup,
        CancellationToken cancellationToken)
        => await ResolveStableIdentityAsync(
            () => stableIdentityLookup.ResolveByTemplateKeyAsync(
                templateKey,
                cancellationToken));

    /// <summary>
    /// Resolve the workflow bound to an external identity to the version that would run.
    /// </summary>
    /// <remarks>
    /// Looks up the workflow whose external identity is the given namespace and key (set with
    /// <c>POST /api/workflows/definitions</c>) and reports the outcome in <c>status</c>; every outcome is returned
    /// with HTTP 200, so always check <c>status</c>:
    ///
    /// - Resolved: exactly one workflow matches and <c>runnableVersionId</c> names the version a latest-Active start
    /// would run.
    /// - NotFound: no workflow is bound to this identity.
    /// - Ambiguous: several workflows match; nothing is selected until the catalog is repaired.
    /// - Stale: the workflow exists but has no runnable Active version.
    ///
    /// Use this instead of storing workflow identifiers when an external system knows workflows by its own names.
    /// Start the resolved workflow with <c>POST /api/workflows/runs/start</c> using <c>workflowId</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="externalNamespace">
    /// Namespace of the external identity, for example <c>partner.system</c>. It is trimmed and lower-cased; at most
    /// 100 characters of ASCII letters, digits, hyphen, underscore, period and colon.
    /// </param>
    /// <param name="externalKey">
    /// Key within the namespace, for example <c>invoice:review</c>. It is trimmed and lower-cased; at most 200
    /// characters of the same characters. URL-encode it when it contains reserved characters.
    /// </param>
    /// <response code="200">The resolution, whatever its <c>status</c>.</response>
    /// <response code="400">
    /// A value is blank, too long or contains another character (<c>workflows.stable-identity-invalid</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    internal static async Task<IResult> GetDefinitionByExternalKeyAsync(
        string externalNamespace,
        string externalKey,
        IWorkflowStableIdentityLookupService stableIdentityLookup,
        CancellationToken cancellationToken)
        => await ResolveStableIdentityAsync(
            () => stableIdentityLookup.ResolveByExternalKeyAsync(
                externalNamespace,
                externalKey,
                cancellationToken));

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
