using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.SharedKernel;
using System.ComponentModel;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class WorkflowsApi
{
    // The XML comment transformer matches <param> tags by the C# parameter name, which differs from the header's wire
    // name, so the header description is supplied with [Description] instead.
    private const string IdempotencyKeyHeaderDescription =
        "Optional client-chosen key, 1 to 256 characters after trimming, that identifies this start request, for " +
        "example `invoice-4711-review`. Send the header at most once and without commas. A repeated request with the " +
        "same key and the same content returns the original run instead of starting another. A key belongs to the " +
        "caller that first sent it; another caller's request with the same key is rejected with HTTP 409.";

    public static RouteGroupBuilder MapWorkflowsApi(this RouteGroupBuilder group)
    {
        var workflows = group.MapGroup("/workflows").WithApiSection(ApiAccessScopeNames.ReadWorkflows, ApiAccessScopeNames.WriteWorkflows)
            .WithTags("Workflows")
            .DisableAntiforgery();

        workflows.MapWorkflowRunControlApi();
        workflows.MapWorkflowTemplates();
        workflows.MapWorkflowRunReadApi();
        workflows.MapWorkflowRunIdempotencyApi();
        workflows.MapWorkflowStableIdentityApi();
        workflows.MapWorkflowExternalResponseApi();

        workflows.MapGet("/contract", GetContract)
            .WithName("GetWorkflowsApiContract")
            .Produces<WorkflowApiContractResponse>();

        workflows.MapGet("/settings", GetSettingsAsync)
            .WithName("GetWorkflowSettings")
            .Produces<WorkflowSettings>();

        workflows.MapPost("/settings", SaveSettingsAsync)
            .WithName("SaveWorkflowSettings")
            .Produces<WorkflowSettings>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        workflows.MapGet("/runtime-backends", ListRuntimeBackends)
            .WithName("ListWorkflowRuntimeBackends")
            .Produces<WorkflowRuntimeBackendDescriptor[]>();

        workflows.MapGet("/executor-catalog", ListExecutorCatalogAsync)
            .WithName("ListWorkflowExecutorCatalog")
            .Produces<WorkflowExecutorDescriptor[]>();

        workflows.MapGet("/definitions/{workflowId:guid}", GetDefinitionAsync)
            .WithName("GetWorkflowDefinition")
            .Produces<WorkflowDefinitionDetail>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/definitions/{workflowId:guid}/versions/{versionId:guid}", GetDefinitionVersionAsync)
            .WithName("GetWorkflowDefinitionVersion")
            .Produces<WorkflowDefinitionDetail>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapGet("/definitions/{workflowId:guid}/export", ExportDefinitionAsync)
            .WithName("ExportWorkflowDefinition")
            .Produces<WorkflowDefinitionExportEnvelope>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/definitions", SaveDefinitionAsync)
            .WithName("SaveWorkflowDefinition")
            .Produces<WorkflowDefinition>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        workflows.MapPost("/definitions/import", ImportDefinitionAsync)
            .WithName("ImportWorkflowDefinition")
            .Produces<WorkflowDefinition>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        workflows.MapPost("/definitions/{workflowId:guid}/publish", PublishDefinitionAsync)
            .WithName("PublishWorkflowDefinition")
            .Produces<WorkflowDefinition>()
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound);

        workflows.MapPost("/definitions/{workflowId:guid}/suspend", SuspendDefinitionAsync)
            .WithName("SuspendWorkflowDefinition")
            .Produces<WorkflowDefinition>()
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound);

        workflows.MapPost("/definitions/{workflowId:guid}/archive", ArchiveDefinitionAsync)
            .WithName("ArchiveWorkflowDefinition")
            .Produces<WorkflowDefinition>()
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound);

        workflows.MapDelete("/definitions/{workflowId:guid}", DeleteDefinitionAsync)
            .WithName("DeleteWorkflowDefinition")
            .Produces<ApiAck>();

        workflows.MapPost("/definitions/{workflowId:guid}/validate", ValidateSavedDefinitionAsync)
            .WithName("ValidateSavedWorkflowDefinition").WithApiPermission(ApiAccessScopeNames.ReadWorkflows)
            .Produces<WorkflowValidationResult>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/validate", ValidateDraftDefinitionAsync)
            .WithName("ValidateDraftWorkflowDefinition").WithApiPermission(ApiAccessScopeNames.ReadWorkflows)
            .Produces<WorkflowValidationResult>();

        workflows.MapGet("/provider-options", ListProviderOptionsAsync)
            .WithName("ListWorkflowProviderOptions")
            .Produces<WorkflowProviderOption[]>();

        workflows.MapGet("/components", ListComponentsAsync)
            .WithName("ListWorkflowComponents")
            .Produces<LlmCallComponent[]>();

        workflows.MapGet("/components/{componentId:guid}", GetComponentAsync)
            .WithName("GetWorkflowComponent")
            .Produces<LlmCallComponent>()
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        workflows.MapPost("/components", SaveComponentAsync)
            .WithName("SaveWorkflowComponent")
            .Produces<LlmCallComponent>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        workflows.MapDelete("/components/{componentId:guid}", DeleteComponentAsync)
            .WithName("DeleteWorkflowComponent")
            .Produces<ApiAck>();

        workflows.MapPost("/test-runs", RunTestAsync)
            .WithName("RunWorkflowTest").WithApiPermission(ApiAccessScopeNames.ExecuteWorkflows)
            .Produces<WorkflowTestRunResult>()
            .Produces<WorkflowTestRunResult>(StatusCodes.Status400BadRequest);

        workflows.MapGet("/analytics", GetAnalyticsAsync)
            .Produces<WorkflowAnalyticsSnapshot>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .WithName("GetWorkflowAnalytics");

        return group;
    }

    internal static RouteGroupBuilder MapWorkflowRunControlApi(this RouteGroupBuilder workflows)
    {
        workflows.MapPost("/definitions/{workflowId:guid}/runs/start", StartDefinitionRunAsync)
            .WithName("StartWorkflowDefinitionRun").WithApiPermission(ApiAccessScopeNames.ExecuteWorkflows)
            .Produces<WorkflowRunStartApiResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapPost("/runs/start", StartRunAsync)
            .WithName("StartWorkflowRun").WithApiPermission(ApiAccessScopeNames.ExecuteWorkflows)
            .Produces<WorkflowRunStartApiResponse>()
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        workflows.MapPost("/runs/{runId:guid}/cancel", CancelRunAsync)
            .WithName("CancelWorkflowRun").WithApiPermission(ApiAccessScopeNames.ExecuteWorkflows)
            .Produces<WorkflowRunCancellationResult>()
            .Produces<WorkflowRunCancellationResult>(StatusCodes.Status404NotFound)
            .Produces<WorkflowRunCancellationResult>(StatusCodes.Status409Conflict)
            .Produces<WorkflowRunCancellationResult>(StatusCodes.Status422UnprocessableEntity);

        return workflows;
    }

    /// <summary>
    /// List the routes of the workflow API family together with a short boundary note.
    /// </summary>
    /// <remarks>
    /// Returns a fixed list of <c>METHOD /route</c> strings for this family and one sentence about where agent
    /// capability setup is validated. The list is informational: it does not reflect authorization, configuration or
    /// runtime state, and this OpenAPI document stays the exact contract for parameters, bodies and responses.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The route list and the boundary note.</response>
    internal static IResult GetContract()
        => Results.Ok(new WorkflowApiContractResponse(
            [
                "GET /api/workflows/contract",
                "GET /api/workflows/templates",
                "POST /api/workflows/templates/{templateKey}/drafts",
                "GET /api/workflows/settings",
                "POST /api/workflows/settings",
                "GET /api/workflows/runtime-backends",
                "GET /api/workflows/executor-catalog",
                "GET /api/workflows/definitions",
                "GET /api/workflows/definitions?externalNamespace={namespace}&externalKey={key}",
                "GET /api/workflows/definitions/by-template-key/{templateKey}",
                "GET /api/workflows/definitions/by-external-key/{externalNamespace}/{externalKey}",
                "POST /api/workflows/definitions",
                "GET /api/workflows/definitions/{workflowId}",
                "GET /api/workflows/definitions/{workflowId}/versions/{versionId}",
                "GET /api/workflows/definitions/{workflowId}/export",
                "POST /api/workflows/definitions/import",
                "POST /api/workflows/definitions/{workflowId}/publish",
                "POST /api/workflows/definitions/{workflowId}/suspend",
                "POST /api/workflows/definitions/{workflowId}/archive",
                "DELETE /api/workflows/definitions/{workflowId}",
                "POST /api/workflows/definitions/{workflowId}/validate",
                "POST /api/workflows/definitions/{workflowId}/runs/start",
                "POST /api/workflows/validate",
                "GET /api/workflows/provider-options",
                "GET /api/workflows/components",
                "POST /api/workflows/components",
                "GET /api/workflows/components/{componentId}",
                "DELETE /api/workflows/components/{componentId}",
                "POST /api/workflows/test-runs",
                "POST /api/workflows/runs/start",
                "GET /api/workflows/runs/by-idempotency-key/{key}",
                "GET /api/workflows/runs",
                "GET /api/workflows/runs/page",
                "GET /api/workflows/runs/{runId}",
                "GET /api/workflows/runs/{runId}/detail",
                "POST /api/workflows/runs/{runId}/cancel",
                "GET /api/workflows/runs/{runId}/events",
                "GET /api/workflows/runs/{runId}/events/page",
                "GET /api/workflows/events/stream",
                "GET /api/workflows/runs/{runId}/events/stream",
                "GET /api/workflows/runs/{runId}/artifacts",
                "GET /api/workflows/runs/{runId}/artifacts/{artifactId}/content",
                "GET /api/workflows/runs/{runId}/checkpoints",
                "GET /api/workflows/runs/{runId}/pending-requests",
                "POST /api/workflows/external-requests/{requestId}/response",
                "GET /api/workflows/external-response-operations/{operationId}",
                "GET /api/workflows/analytics"
            ],
            "Workflow control remains HTTP/API-driven. Executor catalogs expose tool side-effect contracts; agent skill, tool, and MCP capability setup is validated through /api/agents/capabilities."));

    /// <summary>
    /// Read the workflow settings document of this host.
    /// </summary>
    /// <remarks>
    /// Returns the single settings document shared by all workflows: the recorded default runtime policy, the payload
    /// and artifact capture policy, the human-in-the-loop policy and the agent voice settings. When no settings were
    /// saved yet, the built-in defaults are returned: the in-process backend, 64,000 inline payload characters with
    /// node-output capture, human input allowed with a 240-minute default timeout, and voice input and output
    /// disabled. Change the document with <c>POST /api/workflows/settings</c>, which replaces it as a whole.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The saved settings, or the built-in defaults when none were saved.</response>
    internal static async Task<IResult> GetSettingsAsync(
        IWorkflowSettingsService settingsService,
        CancellationToken cancellationToken)
        => Results.Ok(await settingsService.GetSettingsAsync(cancellationToken));

    /// <summary>
    /// Replace the workflow settings document.
    /// </summary>
    /// <remarks>
    /// Stores the body as the complete settings document and returns it; nothing is merged with the stored document.
    /// Read the current document with <c>GET /api/workflows/settings</c>, change the members you need and send all of
    /// them: an omitted <c>voiceSettings</c> is stored as null, which resets agent voice input and output to their
    /// disabled defaults.
    ///
    /// Checks before anything is stored:
    ///
    /// - <c>artifactPolicy.maxInlinePayloadCharacters</c> and <c>humanInLoopPolicy.defaultRequestTimeoutMinutes</c>
    /// must be greater than zero.
    /// - The preferred backend of <c>defaultRuntimePolicy</c> must be registered and runnable in this host (see
    /// <c>GET /api/workflows/runtime-backends</c>).
    /// - The two Azure Functions exposure flags may be true only when an Azure Functions backend is runnable; none is
    /// registered by default.
    ///
    /// The new settings apply to later work. Setting <c>humanInLoopPolicy.allowHumanInputNodes</c> to false makes
    /// every definition that contains a human-input node fail validation, so it can no longer be saved, published or
    /// launched until the setting is restored.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete settings document to store. Send every member.</param>
    /// <response code="200">The settings were stored; the body is the stored document.</response>
    /// <response code="400">
    /// A check failed (<c>workflows.request-invalid</c>, the message names the check) and nothing was stored. A body
    /// the framework cannot bind is rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    internal static async Task<IResult> SaveSettingsAsync(
        WorkflowSettings request,
        IWorkflowSettingsService settingsService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(() => settingsService.SaveSettingsAsync(request, cancellationToken));

    /// <summary>
    /// List the workflow runtime backends this host knows and whether each one can run workflows.
    /// </summary>
    /// <remarks>
    /// Returns one entry for each backend kind (InProcess, DurableTask and AzureFunctions) with its capabilities and
    /// availability. By default only the in-process backend is registered and runnable; the others are listed as
    /// planned. Use the list to choose <c>requestedBackend</c> when starting a run or
    /// <c>runtimePolicy.preferredBackend</c> for a definition: a backend that is not registered and runnable is
    /// rejected when a run starts and makes a definition that prefers it fail validation.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">Every known backend, runnable or not.</response>
    internal static IResult ListRuntimeBackends(
        IWorkflowRuntimeBackendCatalog backendCatalog)
        => Results.Ok(backendCatalog.ListBackends());

    /// <summary>
    /// List the executors that executor nodes of a workflow definition can use, with their current availability.
    /// </summary>
    /// <remarks>
    /// Returns the built-in and plugin-provided executors known to this host. Each entry gives the executor
    /// identifier to put in <c>settings.executorId</c> of an Executor node, its input and result shapes, its settings
    /// schema and default settings, its default execution policy, its source and trust level, its side effects, the
    /// permissions and approval it requires, and whether it can run now. Availability is evaluated while the list is
    /// read, so an executor can be listed but not runnable, for example when its plugin is disabled or not
    /// configured.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The executor catalog.</response>
    internal static async Task<IResult> ListExecutorCatalogAsync(
        IWorkflowExecutorRuntimeAvailabilityCatalog executorCatalog,
        CancellationToken cancellationToken)
        => Results.Ok(await executorCatalog.ListExecutorsAsync(cancellationToken));

    /// <summary>
    /// Read the current version of a workflow definition with a fresh validation result.
    /// </summary>
    /// <remarks>
    /// Returns the workflow's current version, whatever its lifecycle status, and a validation result computed now
    /// against the current components, providers, Prompt Gallery bindings, runtime backends and workflow settings, so
    /// a definition that was valid when saved can become invalid later. A workflow keeps its <c>workflowId</c> across
    /// versions; every save, import and status change stores a new version with a new <c>versionId</c>. Read an
    /// earlier version with <c>GET /api/workflows/definitions/{workflowId}/versions/{versionId}</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">
    /// Identifier of the workflow, as returned in <c>id</c> by <c>GET /api/workflows/definitions</c> or by a save.
    /// </param>
    /// <response code="200">The current version and its validation result.</response>
    /// <response code="404">No workflow has this identifier (<c>workflows.definition-not-found</c>).</response>
    internal static async Task<IResult> GetDefinitionAsync(
        Guid workflowId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await GetDefinitionResultAsync(workflowId, versionId: null, catalogService, cancellationToken);

    /// <summary>
    /// Read one stored version of a workflow definition with a fresh validation result.
    /// </summary>
    /// <remarks>
    /// Returns the exact version, which may be older than the current one, and a validation result computed now.
    /// Version identifiers come from <c>versionId</c> of a definition, a catalog item or a run. A version identifier
    /// is only found under the workflow it belongs to.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow that owns the version.</param>
    /// <param name="versionId">Identifier of the stored version of that workflow.</param>
    /// <response code="200">The requested version and its validation result.</response>
    /// <response code="404">
    /// The workflow or the version does not exist (<c>workflows.definition-not-found</c>).
    /// </response>
    internal static async Task<IResult> GetDefinitionVersionAsync(
        Guid workflowId,
        Guid versionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await GetDefinitionResultAsync(workflowId, versionId, catalogService, cancellationToken);

    /// <summary>
    /// Export a workflow definition version as a portable envelope.
    /// </summary>
    /// <remarks>
    /// Returns the current version, or the version named by <c>versionId</c>, wrapped with the exchange format
    /// <c>CanDoItAll.WorkflowDefinition/v1</c>, its validation result at export time and the export time. Send the
    /// envelope unchanged in <c>envelope</c> of <c>POST /api/workflows/definitions/import</c> to recreate the workflow
    /// on this or another host. The envelope holds the whole definition, including node instructions and executor
    /// settings, so handle it like the definition itself. Exporting changes nothing.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow to export.</param>
    /// <param name="versionId">
    /// Optional identifier of the version to export. Omit it to export the current version.
    /// </param>
    /// <response code="200">The export envelope.</response>
    /// <response code="404">
    /// The workflow or the requested version does not exist (<c>workflows.definition-not-found</c>).
    /// </response>
    internal static async Task<IResult> ExportDefinitionAsync(
        Guid workflowId,
        Guid? versionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var envelope = await catalogService.ExportDefinitionAsync(
            new WorkflowId(workflowId),
            versionId.HasValue ? new WorkflowVersionId(versionId.Value) : null,
            cancellationToken);
        return envelope is null
            ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
            : Results.Ok(envelope);
    }

    /// <summary>
    /// Create a workflow definition or store a new version of an existing one.
    /// </summary>
    /// <remarks>
    /// Without <c>id</c> a new workflow is created with a server-generated identifier. With <c>id</c> the body becomes
    /// the new current version of that workflow; an unknown <c>id</c> creates a workflow with that identifier. Every
    /// successful save stores a new version with a new <c>versionId</c>; earlier versions stay readable. The body
    /// replaces the definition content: send the complete graph, runtime policy and input parameters, not a patch.
    ///
    /// To edit safely, read the definition, change it and send the <c>versionId</c> you read as
    /// <c>expectedVersionId</c>; the save is rejected if another save happened in between. Without
    /// <c>expectedVersionId</c> the last save wins.
    ///
    /// The new version must pass validation whatever its <c>status</c>, so an invalid draft cannot be stored; check a
    /// draft first with <c>POST /api/workflows/validate</c>. The requested <c>status</c> is stored as sent, so saving
    /// with Active publishes the new version directly. For LLM call nodes that reference a component, an empty
    /// provider, model or instructions value is filled from the component when the version is stored. The name and
    /// description are trimmed.
    ///
    /// Stable identity: <c>externalNamespace</c> and <c>externalKey</c> must be sent together; they are stored
    /// lower-cased and must be unique across workflows. Leaving both empty keeps the identity of the current version,
    /// so this operation cannot remove an identity. Template provenance cannot be set here.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">
    /// The complete definition content to store and the optional concurrency precondition.
    /// </param>
    /// <response code="200">
    /// The version was stored. The body is the stored definition with its new <c>versionId</c>.
    /// </response>
    /// <response code="400">
    /// Nothing was stored (<c>workflows.request-invalid</c>; the message gives the reason): a blank name, a missing
    /// graph or runtime policy, a stale <c>expectedVersionId</c>, an incomplete, malformed or already used external
    /// identity, a validation failure (the message lists the issues), or a component or Prompt Gallery binding that
    /// cannot be resolved. Read the current version before retrying a stale save. A body the framework cannot bind
    /// is rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    internal static async Task<IResult> SaveDefinitionAsync(
        WorkflowDefinitionSaveRequest request,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(() => catalogService.SaveDefinitionAsync(request, cancellationToken));

    /// <summary>
    /// Import a workflow definition from an export envelope.
    /// </summary>
    /// <remarks>
    /// Stores the definition inside an envelope produced by <c>GET /api/workflows/definitions/{workflowId}/export</c>.
    /// Only the definition of the envelope is used; its validation result and export time are ignored. The imported
    /// version is validated like a save and stored with the requested status, Draft when <c>status</c> is omitted.
    ///
    /// With <c>preserveWorkflowId</c> false the import creates a new workflow with a new identifier and without the
    /// source's external identity or template provenance. With true it keeps the source's workflow identifier,
    /// external identity and template provenance; if that workflow already exists here, the import becomes its new
    /// current version without any concurrency check.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The export envelope and the import options.</param>
    /// <response code="200">The definition was stored. The body is the stored version.</response>
    /// <response code="400">
    /// Nothing was stored (<c>workflows.request-invalid</c>): the envelope or its definition is missing, the envelope
    /// format is not <c>CanDoItAll.WorkflowDefinition/v1</c>, the definition fails validation, or its external
    /// identity is already used by another workflow. A body the framework cannot bind is rejected with HTTP 400
    /// before the operation runs and has no error envelope.
    /// </response>
    internal static async Task<IResult> ImportDefinitionAsync(
        WorkflowDefinitionImportRequest request,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(() => catalogService.ImportDefinitionAsync(request, cancellationToken));

    /// <summary>
    /// Publish the current version of a workflow definition by storing it as a new Active version.
    /// </summary>
    /// <remarks>
    /// Validates the workflow's current version and stores a copy of it with status Active as the new current version
    /// (a new <c>versionId</c>). Production runs started with <c>POST /api/workflows/runs/start</c> without a
    /// <c>versionId</c> use the latest Active version. Publishing does not affect runs that already exist.
    ///
    /// Send the <c>versionId</c> you last read as <c>expectedVersionId</c> so that the change is rejected when the
    /// workflow changed in between; without it the change applies to whatever version is current.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow to publish.</param>
    /// <param name="expectedVersionId">
    /// Optional <c>versionId</c> of the current version as last read. When it no longer is the current version, the
    /// change is rejected with HTTP 400.
    /// </param>
    /// <response code="200">The new Active version.</response>
    /// <response code="400">
    /// Nothing was stored (<c>workflows.request-invalid</c>): the current version fails validation (the message lists
    /// the issues) or <c>expectedVersionId</c> is stale. Read the definition again before retrying.
    /// </response>
    /// <response code="404">No workflow has this identifier (<c>workflows.resource-not-found</c>).</response>
    internal static async Task<IResult> PublishDefinitionAsync(
        Guid workflowId,
        Guid? expectedVersionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ChangeDefinitionStatusAsync(
            workflowId,
            expectedVersionId,
            WorkflowLifecycleStatus.Active,
            catalogService,
            cancellationToken);

    /// <summary>
    /// Suspend a workflow definition by storing its current version as a new Suspended version.
    /// </summary>
    /// <remarks>
    /// Stores a copy of the current version with status Suspended as the new current version (a new
    /// <c>versionId</c>). While the current version is Suspended, no version of the workflow can be started by the
    /// start operations: latest-Active starts find no runnable version and exact-version starts are rejected. Runs
    /// that already exist continue. Publish again to make the workflow runnable.
    ///
    /// The stored copy is validated like any save, so a definition that no longer validates cannot be suspended until
    /// it is fixed. Send the <c>versionId</c> you last read as <c>expectedVersionId</c> to detect concurrent changes.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow to suspend.</param>
    /// <param name="expectedVersionId">
    /// Optional <c>versionId</c> of the current version as last read. When it no longer is the current version, the
    /// change is rejected with HTTP 400.
    /// </param>
    /// <response code="200">The new Suspended version.</response>
    /// <response code="400">
    /// Nothing was stored (<c>workflows.request-invalid</c>): the definition fails validation or
    /// <c>expectedVersionId</c> is stale.
    /// </response>
    /// <response code="404">No workflow has this identifier (<c>workflows.resource-not-found</c>).</response>
    internal static async Task<IResult> SuspendDefinitionAsync(
        Guid workflowId,
        Guid? expectedVersionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ChangeDefinitionStatusAsync(
            workflowId,
            expectedVersionId,
            WorkflowLifecycleStatus.Suspended,
            catalogService,
            cancellationToken);

    /// <summary>
    /// Archive a workflow definition by storing its current version as a new Archived version.
    /// </summary>
    /// <remarks>
    /// Stores a copy of the current version with status Archived as the new current version (a new <c>versionId</c>).
    /// Archiving keeps the workflow and its versions but, like suspending, stops the start operations from starting
    /// it; runs that already exist continue. It does not delete anything: use
    /// <c>DELETE /api/workflows/definitions/{workflowId}</c> for that.
    ///
    /// The stored copy is validated like any save, so a definition that no longer validates cannot be archived until
    /// it is fixed. Send the <c>versionId</c> you last read as <c>expectedVersionId</c> to detect concurrent changes.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow to archive.</param>
    /// <param name="expectedVersionId">
    /// Optional <c>versionId</c> of the current version as last read. When it no longer is the current version, the
    /// change is rejected with HTTP 400.
    /// </param>
    /// <response code="200">The new Archived version.</response>
    /// <response code="400">
    /// Nothing was stored (<c>workflows.request-invalid</c>): the definition fails validation or
    /// <c>expectedVersionId</c> is stale.
    /// </response>
    /// <response code="404">No workflow has this identifier (<c>workflows.resource-not-found</c>).</response>
    internal static async Task<IResult> ArchiveDefinitionAsync(
        Guid workflowId,
        Guid? expectedVersionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => await ChangeDefinitionStatusAsync(
            workflowId,
            expectedVersionId,
            WorkflowLifecycleStatus.Archived,
            catalogService,
            cancellationToken);

    /// <summary>
    /// Permanently delete a workflow definition and all its versions.
    /// </summary>
    /// <remarks>
    /// Removes the workflow and every stored version; this cannot be undone except by importing an earlier export.
    /// Runs of the workflow, with their events, artifacts and checkpoints, are not deleted and stay readable through
    /// the run operations. LLM call components are not deleted. To stop new runs but keep the definition, archive or
    /// suspend it instead.
    ///
    /// The operation is idempotent: deleting an unknown or already deleted workflow also returns
    /// <c>{ "ok": true }</c>. There is no concurrency precondition.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow to delete.</param>
    /// <response code="200">The workflow no longer exists; <c>{ "ok": true }</c>.</response>
    internal static async Task<IResult> DeleteDefinitionAsync(
        Guid workflowId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        await catalogService.DeleteDefinitionAsync(new WorkflowId(workflowId), cancellationToken);
        return Results.Ok(new ApiAck(true));
    }

    /// <summary>
    /// Validate the current version of a stored workflow definition.
    /// </summary>
    /// <remarks>
    /// Runs the same validation as a save or a start against the workflow's current version and the current
    /// components, providers, backends and settings, and returns the issues. Nothing is changed. An empty
    /// <c>issues</c> list means the version is valid now.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="workflowId">Identifier of the workflow whose current version is validated.</param>
    /// <response code="200">The validation result; issues are returned with HTTP 200 too.</response>
    /// <response code="404">No workflow has this identifier (<c>workflows.definition-not-found</c>).</response>
    internal static async Task<IResult> ValidateSavedDefinitionAsync(
        Guid workflowId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var detail = await catalogService.GetDefinitionAsync(
            new WorkflowId(workflowId),
            versionId: null,
            cancellationToken);
        return detail is null
            ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
            : Results.Ok(detail.Validation);
    }

    /// <summary>
    /// Validate an unsaved workflow definition.
    /// </summary>
    /// <remarks>
    /// Checks a complete definition, for example an edited draft, with the same rules a save applies, without storing
    /// it: graph structure, node settings, component, executor and provider references, runtime policy and the
    /// workflow settings. Nothing is stored and the identifiers in the body are not looked up, so any non-empty
    /// identifiers can be sent. An empty <c>issues</c> list means that a save of this content would pass validation
    /// at this moment.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete definition to validate.</param>
    /// <response code="200">The validation result; issues are returned with HTTP 200 too.</response>
    internal static async Task<IResult> ValidateDraftDefinitionAsync(
        WorkflowDefinition request,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
        => Results.Ok(await catalogService.ValidateDefinitionAsync(request, cancellationToken));

    /// <summary>
    /// List the chat model providers that LLM call components and nodes can use.
    /// </summary>
    /// <remarks>
    /// Returns the chat-purpose provider profiles configured in this host, enabled ones first and then by name, with
    /// their default and suggested models and capability flags. Use <c>providerProfileId</c> and one of
    /// <c>modelOptions</c> in a component or an LLM call node. The list contains no credentials. An empty list means
    /// no chat provider is configured.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The provider options.</response>
    internal static async Task<IResult> ListProviderOptionsAsync(
        IWorkflowComponentLibraryService componentLibrary,
        CancellationToken cancellationToken)
        => Results.Ok(await componentLibrary.ListProviderOptionsAsync(cancellationToken));

    /// <summary>
    /// List the reusable LLM call components.
    /// </summary>
    /// <remarks>
    /// Returns every LLM call component ordered by name. A component is a reusable model call (provider, model,
    /// instructions, input and result shapes, permissions) that LLM call nodes reference by <c>componentId</c>. Its
    /// instructions are kept as a Prompt Gallery prompt version, identified by <c>promptArtifactId</c> and
    /// <c>promptVersionId</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The components; an empty array when none exist.</response>
    internal static async Task<IResult> ListComponentsAsync(
        IWorkflowComponentLibraryService componentLibrary,
        CancellationToken cancellationToken)
        => Results.Ok(await componentLibrary.ListComponentsAsync(cancellationToken));

    /// <summary>
    /// Read one LLM call component.
    /// </summary>
    /// <remarks>
    /// Returns the component with its instructions resolved from its Prompt Gallery prompt version.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="componentId">
    /// Identifier of the component, as returned in <c>id</c> by <c>GET /api/workflows/components</c> or a save.
    /// </param>
    /// <response code="200">The component.</response>
    /// <response code="404">No component has this identifier (<c>workflows.component-not-found</c>).</response>
    internal static async Task<IResult> GetComponentAsync(
        Guid componentId,
        IWorkflowComponentLibraryService componentLibrary,
        CancellationToken cancellationToken)
    {
        var component = await componentLibrary.GetComponentAsync(
            new WorkflowComponentId(componentId),
            cancellationToken);
        return component is null
            ? ApiEndpointResults.NotFound("Workflow component was not found.", "workflows.component-not-found")
            : Results.Ok(component);
    }

    /// <summary>
    /// Create or replace an LLM call component and record its instructions in the Prompt Gallery.
    /// </summary>
    /// <remarks>
    /// Without <c>id</c> a new component is created; with <c>id</c> the stored component with that identifier is
    /// replaced (an unknown <c>id</c> creates it). The name, model and instructions are trimmed and must not be blank,
    /// and the component must pass the component validation and the provider checks: the provider must exist, be
    /// enabled and be a chat provider, support vision for Vision or Multimodal modality and structured output when JSON
    /// output is required, and have a price row for the model.
    ///
    /// The instructions are stored as a Prompt Gallery prompt version, which is a side effect of this call:
    ///
    /// - With <c>promptVersionId</c>, that version's content becomes the instructions and the sent
    /// <c>instructions</c> are not stored.
    /// - With <c>promptArtifactId</c> (or an existing binding) and changed instructions, a new version of that prompt
    /// is created.
    /// - Without any binding, a new Prompt Gallery prompt is created for the component.
    ///
    /// Nodes that reference the component copy its provider, model and instructions when their definition is saved,
    /// so saved definitions keep the values they copied.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete component to store.</param>
    /// <response code="200">
    /// The component was stored. The body shows the stored instructions and the Prompt Gallery binding.
    /// </response>
    /// <response code="400">
    /// The component was rejected (<c>workflows.request-invalid</c>; the message lists the reasons): a blank name,
    /// model or instructions, a validation or provider check failure, or a Prompt Gallery item or version that is
    /// missing, mismatched or incompatible. A Prompt Gallery prompt created before a later check failed can remain.
    /// A body the framework cannot bind is rejected with HTTP 400 before the operation runs and has no error envelope.
    /// </response>
    internal static async Task<IResult> SaveComponentAsync(
        LlmCallComponentSaveRequest request,
        IWorkflowComponentLibraryService componentLibrary,
        CancellationToken cancellationToken)
        => await ToApiResultAsync(() => componentLibrary.SaveComponentAsync(request, cancellationToken));

    /// <summary>
    /// Delete an LLM call component.
    /// </summary>
    /// <remarks>
    /// Removes the component. Stored definition versions keep the provider, model and instructions they copied from
    /// it, but a definition whose LLM call node still references the deleted component fails validation when it is
    /// validated, saved, published or started. Its Prompt Gallery prompt is not deleted.
    ///
    /// The operation is idempotent: deleting an unknown component also returns <c>{ "ok": true }</c>.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="componentId">Identifier of the component to delete.</param>
    /// <response code="200">The component no longer exists; <c>{ "ok": true }</c>.</response>
    internal static async Task<IResult> DeleteComponentAsync(
        Guid componentId,
        IWorkflowComponentLibraryService componentLibrary,
        CancellationToken cancellationToken)
    {
        await componentLibrary.DeleteComponentAsync(new WorkflowComponentId(componentId), cancellationToken);
        return Results.Ok(new ApiAck(true));
    }

    /// <summary>
    /// Validate a workflow, or run it once in preview mode, and wait for the outcome.
    /// </summary>
    /// <remarks>
    /// Tests either an unsaved draft (<c>draftDefinition</c>, which must have status Draft) or an exact stored version
    /// (<c>workflowId</c> and <c>versionId</c>). With <c>validateOnly</c> the definition is only validated and no run
    /// is created.
    ///
    /// Otherwise a real preview run is created and executed before the response is sent: it is stored like any run,
    /// appears in the run lists and executes its nodes, including executors with external effects, except the nodes
    /// replaced by <c>previewSimulationPlan</c>. Preview runs do not require an Active version but must be allowed by
    /// the definition's runtime policy. There is no idempotency key: repeating the request creates another run.
    ///
    /// The run succeeds when it ends Completed, WaitingForInput or Idle. Unlike the start operations, this response
    /// returns the stored run records without the public safe projection, including event payloads, artifact storage
    /// paths, external request and response JSON, checkpoint references and the run's launch origin. Use it only for
    /// trusted authoring clients.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open. The authenticated subject, or the local operator when
    /// authorization is disabled, becomes the preview run's actor.
    /// </remarks>
    /// <param name="request">What to test, the run input and the preview options.</param>
    /// <response code="200">
    /// Validation passed (<c>validateOnly</c>) or the preview run ended Completed, WaitingForInput or Idle.
    /// </response>
    /// <response code="400">
    /// The test did not succeed; the body has the same shape and explains why in <c>validation</c> and
    /// <c>errorMessage</c>: no definition was identified, the definition failed validation, the launch was rejected,
    /// or the preview run failed or was cancelled (then <c>run</c> is present). A body the framework cannot bind is
    /// rejected with HTTP 400 before the operation runs and has no envelope.
    /// </response>
    internal static async Task<IResult> RunTestAsync(
        WorkflowTestRunRequest request,
        HttpContext httpContext,
        IWorkflowTestRunner testRunner,
        CancellationToken cancellationToken)
    {
        var result = await testRunner.RunAsync(request with {
            StructureAuthority = await ResolveStructureAuthorityAsync(httpContext, cancellationToken)
        }, cancellationToken);
        return result.Succeeded
            ? Results.Ok(result)
            : Results.BadRequest(result);
    }

    /// <summary>
    /// Aggregate workflow definition, run, duration and model-usage figures.
    /// </summary>
    /// <remarks>
    /// Computes a snapshot at request time. Definition counts cover the current version of each workflow (all of them,
    /// or the one named by <c>WorkflowId</c>). Run figures cover the runs that match every supplied filter:
    /// <c>WorkflowId</c>, <c>State</c>, <c>Backend</c> and <c>Search</c> (a case-insensitive substring of the run
    /// summary, backend run identifier or run identifier). <c>Take</c> limits only <c>recentRuns</c>; <c>runs</c>
    /// lists every matching run, so filter large catalogs.
    ///
    /// Usage figures add up the model usage recorded for the matching runs; a cost is included only when a price was
    /// known, and the counts of known and unknown observations say how complete the totals are. Durations of runs
    /// still in progress are measured up to the snapshot time.
    ///
    /// Like the cancellation result, the run entries are the stored run records without the public safe projection:
    /// they include the backend run identifier, but their launch origin is withheld (always null) because the runs can
    /// belong to other callers.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="query">Optional filters and the size of the recent-run list.</param>
    /// <response code="200">
    /// The analytics snapshot. Filters that match nothing give zero counts and empty lists.
    /// </response>
    /// <response code="400">
    /// <c>Take</c> is outside 1 through 500, or a stored run cannot be measured (<c>workflows.request-invalid</c>).
    /// </response>
    internal static async Task<IResult> GetAnalyticsAsync(
        [AsParameters] WorkflowAnalyticsApiQuery query,
        IWorkflowAnalyticsQueryService analyticsQueryService,
        CancellationToken cancellationToken)
        => await GetWorkflowAnalyticsResultAsync(query, analyticsQueryService, cancellationToken);

    /// <summary>
    /// Start a production run of a workflow addressed in the route and wait until it stops.
    /// </summary>
    /// <remarks>
    /// Same operation as <c>POST /api/workflows/runs/start</c>, with the workflow identifier taken from the route. A
    /// <c>workflowId</c> in the body is optional but, when present, must equal the route value.
    ///
    /// Without <c>versionId</c> the latest Active version runs; this requires the current version to be Draft or
    /// Active. With <c>versionId</c> that exact version runs; it must itself be Active and the workflow's current
    /// version must be Draft or Active. The definition must pass validation, the chosen backend must be registered and
    /// runnable, and a definition that requires durable production runs cannot run on a non-durable backend.
    ///
    /// The request waits while the run executes and returns when it stops: completed, failed, cancelled, waiting for
    /// an external response (see <c>pendingExternalRequests</c>) or idle. Closing the connection before the response
    /// arrives cancels the run in this host. The response is the safe run projection with its events, artifacts,
    /// pending external requests and checkpoints; <c>detailsComplete</c> false means the run was admitted but its
    /// detail could not be read, so read it with <c>GET /api/workflows/runs/{runId}/detail</c>.
    ///
    /// Idempotency: send an <c>Idempotency-Key</c> to make retries safe. The first request with a key creates the run
    /// and records the key; a later request with the same key and the same request returns that original run
    /// (<c>replayed</c> true, <c>idempotencyDisposition</c> ReplayedExistingRun) instead of starting another one, and
    /// a concurrent duplicate waits for the first to finish. In a replay, <c>run</c> shows the state recorded when the
    /// original start returned; read <c>GET /api/workflows/runs/{runId}</c> for the current state. A key is compared
    /// after trimming and belongs to the caller that first sent it (the bearer token subject, or the local operator
    /// when API authorization is disabled): the same key from another caller, or reused for another workflow, version
    /// choice or request content, is rejected with HTTP 409; a request never replays another caller's run. When the
    /// original start failed without admitting a run, the key is released and can be used again. After a lost
    /// response, read <c>GET /api/workflows/runs/by-idempotency-key/{key}</c> or resend the same request with the same
    /// key. Without a key every request starts a new run, so a request whose response was lost cannot be retried
    /// safely; look for the run in <c>GET /api/workflows/runs</c> first.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; its subject becomes
    /// the run's actor. With authorization disabled (the development default) the route is open and the run is
    /// attributed to the local operator.
    /// </remarks>
    /// <param name="workflowId">
    /// Identifier of the workflow to run, as returned in <c>id</c> by <c>GET /api/workflows/definitions</c>.
    /// </param>
    /// <param name="request">
    /// Optional version choice, run input and backend. The body is required; send <c>{}</c> to run the latest Active
    /// version with an empty input.
    /// </param>
    /// <response code="200">
    /// The run was started, or replayed for a known idempotency key, and has stopped. Check <c>run.state</c>: a
    /// failed or cancelled run is still returned with HTTP 200.
    /// </response>
    /// <response code="400">
    /// The run was not started. Codes: <c>workflows.workflow-id-mismatch</c> (route and body identifiers differ),
    /// <c>workflows.validation.{issue}</c> for each validation issue of the definition (for example
    /// <c>workflows.validation.MissingStartNode</c>) or <c>workflows.validation-failed</c>, and
    /// <c>workflows.request-invalid</c> (input that is not a JSON object, an invalid <c>Idempotency-Key</c>, an exact
    /// version that is not Active or whose workflow's current version is Suspended or Archived, a backend that is not
    /// runnable or not durable enough, or an authenticated token without a subject or expiry). A body the framework
    /// cannot bind, including an unknown member, is rejected with HTTP 400 before the operation runs and has no
    /// error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    /// <response code="404">
    /// The workflow or the requested version does not exist, or no Active version can run because none was published
    /// or the current version is Suspended or Archived (<c>workflows.resource-not-found</c>). Nothing was started.
    /// </response>
    /// <response code="409">
    /// The <c>Idempotency-Key</c> was already used for a different start request or by another caller
    /// (<c>workflows.idempotency-key-conflict</c>). Nothing was started; use a new key or send the original request.
    /// </response>
    internal static async Task<IResult> StartDefinitionRunAsync(
        Guid workflowId,
        WorkflowRunStartApiRequest request,
        [Description(IdempotencyKeyHeaderDescription)] [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpContext httpContext,
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => await StartWorkflowRunAsync(
            workflowId,
            request,
            idempotencyKey,
            httpContext,
            launchService,
            runtimeManager,
            runStore,
            cancellationToken);

    /// <summary>
    /// Start a production run of a workflow and wait until it stops.
    /// </summary>
    /// <remarks>
    /// Starts a production run of the workflow named by <c>workflowId</c> in the body. To test a draft or a version
    /// that is not Active, use <c>POST /api/workflows/test-runs</c> instead.
    ///
    /// Without <c>versionId</c> the latest Active version runs; this requires the current version to be Draft or
    /// Active. With <c>versionId</c> that exact version runs; it must itself be Active and the workflow's current
    /// version must be Draft or Active. The definition must pass validation, the chosen backend must be registered and
    /// runnable, and a definition that requires durable production runs cannot run on a non-durable backend.
    ///
    /// The request waits while the run executes and returns when it stops: completed, failed, cancelled, waiting for
    /// an external response (see <c>pendingExternalRequests</c>) or idle. Closing the connection before the response
    /// arrives cancels the run in this host. The response is the safe run projection with its events, artifacts,
    /// pending external requests and checkpoints; <c>detailsComplete</c> false means the run was admitted but its
    /// detail could not be read, so read it with <c>GET /api/workflows/runs/{runId}/detail</c>.
    ///
    /// Idempotency: send an <c>Idempotency-Key</c> to make retries safe. The first request with a key creates the run
    /// and records the key; a later request with the same key and the same request returns that original run
    /// (<c>replayed</c> true, <c>idempotencyDisposition</c> ReplayedExistingRun) instead of starting another one, and
    /// a concurrent duplicate waits for the first to finish. In a replay, <c>run</c> shows the state recorded when the
    /// original start returned; read <c>GET /api/workflows/runs/{runId}</c> for the current state. A key is compared
    /// after trimming and belongs to the caller that first sent it (the bearer token subject, or the local operator
    /// when API authorization is disabled): the same key from another caller, or reused for another workflow, version
    /// choice or request content, is rejected with HTTP 409; a request never replays another caller's run. When the
    /// original start failed without admitting a run, the key is released and can be used again. After a lost
    /// response, read <c>GET /api/workflows/runs/by-idempotency-key/{key}</c> or resend the same request with the same
    /// key. Without a key every request starts a new run, so a request whose response was lost cannot be retried
    /// safely; look for the run in <c>GET /api/workflows/runs</c> first.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; its subject becomes
    /// the run's actor. With authorization disabled (the development default) the route is open and the run is
    /// attributed to the local operator.
    /// </remarks>
    /// <param name="request">
    /// The workflow to run (<c>workflowId</c> is required here), the optional version, the run input and the backend.
    /// </param>
    /// <response code="200">
    /// The run was started, or replayed for a known idempotency key, and has stopped. Check <c>run.state</c>: a
    /// failed or cancelled run is still returned with HTTP 200.
    /// </response>
    /// <response code="400">
    /// The run was not started. Codes: <c>workflows.workflow-id-required</c> (no <c>workflowId</c> in the body),
    /// <c>workflows.validation.{issue}</c> for each validation issue of the definition (for example
    /// <c>workflows.validation.MissingStartNode</c>) or <c>workflows.validation-failed</c>, and
    /// <c>workflows.request-invalid</c> (input that is not a JSON object, an invalid <c>Idempotency-Key</c>, an exact
    /// version that is not Active or whose workflow's current version is Suspended or Archived, a backend that is not
    /// runnable or not durable enough, or an authenticated token without a subject or expiry). A body the framework
    /// cannot bind, including an unknown member, is rejected with HTTP 400 before the operation runs and has no
    /// error envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Reserved for a bearer token that does not authorize the operation (<c>api.authorization-forbidden</c>). The
    /// <c>/api</c> group currently accepts any valid token, so this route does not return it today.
    /// </response>
    /// <response code="404">
    /// The workflow or the requested version does not exist, or no Active version can run because none was published
    /// or the current version is Suspended or Archived (<c>workflows.resource-not-found</c>). Nothing was started.
    /// </response>
    /// <response code="409">
    /// The <c>Idempotency-Key</c> was already used for a different start request or by another caller
    /// (<c>workflows.idempotency-key-conflict</c>). Nothing was started; use a new key or send the original request.
    /// </response>
    internal static async Task<IResult> StartRunAsync(
        WorkflowRunStartApiRequest request,
        [Description(IdempotencyKeyHeaderDescription)] [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        HttpContext httpContext,
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
        => await StartWorkflowRunAsync(
            routeWorkflowId: null,
            request,
            idempotencyKey,
            httpContext,
            launchService,
            runtimeManager,
            runStore,
            cancellationToken);

    /// <summary>
    /// Request cancellation of a workflow run.
    /// </summary>
    /// <remarks>
    /// Asks the runtime to stop a run that has not finished. What happens depends on the run:
    ///
    /// - A run executing in this host whose backend supports cancellation is signalled; it becomes Cancelled shortly
    /// after the response, which may still show its earlier state.
    /// - A run waiting for an external response is cancelled at once: its pending request is cancelled and the
    /// response shows the Cancelled run.
    /// - A run that is already Completed, Failed or Cancelled, that is not executing in this host (for example after a
    /// restart or on another host), or whose pending request is being answered is not changed.
    ///
    /// Every outcome returns the same body; branch on <c>outcome</c> and read the run again with
    /// <c>GET /api/workflows/runs/{runId}</c> to confirm its final state. Repeating the request is safe: once the run
    /// is terminal, it returns HTTP 409 with <c>AlreadyTerminal</c>.
    ///
    /// The <c>run</c> in the body is the stored run record without the public safe projection: it includes the backend
    /// run identifier, but its launch origin is withheld (always null) because the run can belong to another caller.
    ///
    /// Authority: when API authorization is enabled, a valid bearer token satisfying this operation's capability policy; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="runId">
    /// Identifier of the run, as returned in <c>run.runId</c> by a start operation or in <c>runId</c> by the run
    /// lists.
    /// </param>
    /// <response code="200">
    /// <c>CancellationRequested</c>: the run was cancelled or signalled to stop.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no run has this identifier; <c>run</c> is null.</response>
    /// <response code="409">
    /// <c>AlreadyTerminal</c> (the run already finished), <c>NotActive</c> (the run is not executing in this host, or
    /// its pending request is being answered) or <c>TransitionRejected</c> (the waiting run could not be cancelled
    /// consistently). Nothing was changed.
    /// </response>
    /// <response code="422">
    /// <c>BackendNotCancellable</c>: the run executes on a backend that does not support cancellation.
    /// </response>
    internal static async Task<IResult> CancelRunAsync(
        Guid runId,
        IWorkflowRuntimeManager runtimeManager,
        CancellationToken cancellationToken)
        => MapCancellationResult(WorkflowApiSafeProjection.WithoutLaunchOrigin(
            await runtimeManager.RequestCancellationAsync(
                new WorkflowRunId(runId),
                cancellationToken)));

    private static async Task<IResult> ChangeDefinitionStatusAsync(
        Guid workflowId,
        Guid? expectedVersionId,
        WorkflowLifecycleStatus status,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        return await ToApiResultAsync(() => catalogService.ChangeDefinitionStatusAsync(
            new WorkflowDefinitionStatusChangeRequest(
                new WorkflowId(workflowId),
                expectedVersionId.HasValue ? new WorkflowVersionId(expectedVersionId.Value) : null,
                status),
            cancellationToken));
    }

    private static async Task<IResult> StartWorkflowRunAsync(
        Guid? routeWorkflowId,
        WorkflowRunStartApiRequest request,
        string? idempotencyKey,
        HttpContext httpContext,
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager,
        IWorkflowRunStore runStore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (routeWorkflowId.HasValue &&
            request.WorkflowId.HasValue &&
            routeWorkflowId.Value != request.WorkflowId.Value)
        {
            return ApiEndpointResults.BadRequest(
                "Route workflow id does not match the request workflow id.",
                "workflows.workflow-id-mismatch");
        }

        var requestedWorkflowId = routeWorkflowId ?? request.WorkflowId;
        if (!requestedWorkflowId.HasValue)
        {
            return ApiEndpointResults.BadRequest(
                "Workflow id is required to start a workflow run.",
                "workflows.workflow-id-required");
        }

        try
        {
            var workflowId = new WorkflowId(requestedWorkflowId.Value);
            WorkflowDefinitionSelection selection = request.VersionId.HasValue
                ? new WorkflowDefinitionSelection.ExactSavedVersion(
                    workflowId,
                    new WorkflowVersionId(request.VersionId.Value))
                : new WorkflowDefinitionSelection.LatestActive(workflowId);
            var launchResult = await launchService.LaunchAsync(
                new WorkflowLaunchIntent(
                    selection,
                    WorkflowLaunchMode.Production,
                    await ResolveApiLaunchOriginAsync(httpContext, cancellationToken),
                    request.InputJson ?? "{}",
                    WorkflowLaunchCompletionPolicy.WaitForStopped,
                    ResolveLaunchIdempotency(httpContext, idempotencyKey))
                {
                    RequestedBackend = request.RequestedBackend
                },
                cancellationToken);
            try {
                var detail = await WorkflowRunReadEndpoints.BuildRunDetailAsync(
                    launchResult.Run, runtimeManager, runStore, cancellationToken);
                return Results.Ok(WorkflowRunStartApiResponse.From(detail, launchResult.IdempotencyDisposition) with {
                    Observation = launchResult.Observation
                });
            } catch (Exception exception) {
                httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("WorkflowLaunch")
                    .LogWarning(exception, "Workflow {RunId} was admitted; API detail observation failed.", launchResult.Run.RunId);
                return Results.Ok(new WorkflowRunStartApiResponse(WorkflowApiSafeProjection.Map(launchResult.Run), [], [], [], [],
                    launchResult.IdempotencyDisposition,
                    launchResult.IdempotencyDisposition != WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun,
                    launchResult.IdempotencyDisposition == WorkflowLaunchIdempotencyDisposition.ReplayedExistingRun) {
                    Observation = WorkflowLaunchObservation.RecoveredAfterObserverFailure,
                    DetailsComplete = false
                });
            }
        }
        catch (WorkflowLaunchValidationException exception)
        {
            var errors = exception.Validation.Issues.Count == 0
                ?
                [
                    new ApiErrorItem(
                        "workflows.validation-failed",
                        exception.Message,
                        ErrorSeverity.Error)
                ]
                : exception.Validation.Issues
                    .Select(issue => new ApiErrorItem(
                        $"workflows.validation.{issue.Code}",
                        issue.Message,
                        ErrorSeverity.Error))
                    .ToArray();
            return Results.BadRequest(new ApiErrorResponse(errors));
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (WorkflowLaunchIdempotencyConflictException)
        {
            return ApiEndpointResults.Conflict(
                "The Idempotency-Key was already used for a different workflow launch request.",
                "workflows.idempotency-key-conflict");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (KeyNotFoundException exception)
        {
            return ApiEndpointResults.NotFound(exception.Message, "workflows.resource-not-found");
        }
    }

    private static WorkflowLaunchIdempotency ResolveLaunchIdempotency(
        HttpContext httpContext,
        string? idempotencyKey)
    {
        const string headerName = "Idempotency-Key";
        if (!httpContext.Request.Headers.TryGetValue(headerName, out var values))
        {
            return new WorkflowLaunchIdempotency.NotRequested();
        }

        if (values.Count != 1 ||
            string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"{headerName} must contain exactly one non-empty value.",
                headerName);
        }

        return new WorkflowLaunchIdempotency.CallerSupplied(
            new WorkflowLaunchIdempotencyKey(idempotencyKey));
    }

    private static WorkflowLaunchActor ResolveApiActor(ClaimsPrincipal principal)
    {
        var subjectId = principal.FindFirst("sub")?.Value ??
                        principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                        principal.Identity?.Name;
        return string.IsNullOrWhiteSpace(subjectId)
            ? new WorkflowLaunchActor(WorkflowLaunchActorKind.Service, "candoitall-api")
            : new WorkflowLaunchActor(WorkflowLaunchActorKind.User, subjectId);
    }

    // The launch origin of the HTTP start operations. The idempotency lookup identifies its caller the same way, so a
    // key is found only by the caller that recorded it.
    internal static async Task<WorkflowLaunchOrigin.Api> ResolveApiLaunchOriginAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var structureAuthority = await ResolveStructureAuthorityAsync(httpContext, cancellationToken);
        return new WorkflowLaunchOrigin.Api(
            structureAuthority.Principal,
            new WorkflowLaunchCorrelationId(httpContext.TraceIdentifier)) {
            HistoryCaller = ProviderHistoryRequestContext.Caller(httpContext),
            StructureAuthority = structureAuthority
        };
    }

    private static Task<WorkflowStructureAuthority> ResolveStructureAuthorityAsync(HttpContext context, CancellationToken cancellationToken) {
        var factory = context.RequestServices.GetRequiredService<IWorkflowStructureAuthorityFactory>();
        if (context.User.Identity?.IsAuthenticated != true) {
            return factory.CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.Api, cancellationToken);
        }

        var actor = ResolveApiActor(context.User);
        if (actor.Kind != WorkflowLaunchActorKind.User || !long.TryParse(context.User.FindFirst("exp")?.Value,
                System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var expiresAt)) {
            throw new InvalidOperationException("The authenticated workflow request must retain its subject and validated token expiry.");
        }

        return factory.CaptureAuthenticatedOperatorAsync(actor.SubjectId, DateTimeOffset.FromUnixTimeSeconds(expiresAt), cancellationToken);
    }

    private static IResult MapCancellationResult(WorkflowRunCancellationResult result)
        => result.Outcome switch
        {
            WorkflowRunCancellationOutcome.CancellationRequested => Results.Ok(result),
            WorkflowRunCancellationOutcome.NotFound => Results.Json(result, statusCode: StatusCodes.Status404NotFound),
            WorkflowRunCancellationOutcome.AlreadyTerminal or
                WorkflowRunCancellationOutcome.NotActive or
                WorkflowRunCancellationOutcome.TransitionRejected =>
                Results.Json(result, statusCode: StatusCodes.Status409Conflict),
            WorkflowRunCancellationOutcome.BackendNotCancellable =>
                Results.Json(result, statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown workflow cancellation outcome.")
        };

    private static async Task<IResult> GetDefinitionResultAsync(
        Guid workflowId,
        Guid? versionId,
        IWorkflowCatalogService catalogService,
        CancellationToken cancellationToken)
    {
        var detail = await catalogService.GetDefinitionAsync(
            new WorkflowId(workflowId),
            versionId.HasValue ? new WorkflowVersionId(versionId.Value) : null,
            cancellationToken);
        return detail is null
            ? ApiEndpointResults.NotFound("Workflow definition was not found.", "workflows.definition-not-found")
            : Results.Ok(detail);
    }

    private static int NormalizeAnalyticsRecentTake(int? take)
    {
        if (take is null)
        {
            return 8;
        }

        if (take is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                "Workflow analytics recent take must be between 1 and 500.");
        }

        return take.Value;
    }

    internal static Task<IResult> GetWorkflowAnalyticsResultAsync(
        WorkflowAnalyticsApiQuery query,
        IWorkflowAnalyticsQueryService analyticsQueryService,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(analyticsQueryService);
        return ToApiResultAsync(async () => WorkflowApiSafeProjection.WithoutLaunchOrigins(
            await analyticsQueryService.QueryAsync(
                new WorkflowAnalyticsQuery(
                    query.WorkflowId.HasValue ? new WorkflowId(query.WorkflowId.Value) : null,
                    query.State,
                    query.Backend,
                    query.Search ?? string.Empty,
                    NormalizeAnalyticsRecentTake(query.Take)),
                cancellationToken)));
    }

    private static async Task<IResult> ToApiResultAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Results.Ok(await action());
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "workflows.request-invalid");
        }
        catch (KeyNotFoundException exception)
        {
            return ApiEndpointResults.NotFound(exception.Message, "workflows.resource-not-found");
        }
    }
}

/// <summary>
/// Informational list of the workflow API routes returned by <c>GET /api/workflows/contract</c>.
/// </summary>
/// <param name="Endpoints">
/// Route strings made of an HTTP method and a path with placeholders in braces, for example
/// <c>GET /api/workflows/runs/{runId}</c>. The list is fixed by the host version and is not filtered by authority.
/// </param>
/// <param name="BoundarySummary">
/// One sentence about the boundary of the family, for example that agent capability setup is validated through
/// <c>/api/agents/capabilities</c>.
/// </param>
internal sealed record WorkflowApiContractResponse(
    IReadOnlyList<string> Endpoints,
    string BoundarySummary);

/// <summary>
/// Query-string filters of <c>GET /api/workflows/analytics</c>. Every filter is optional and supplied filters are
/// combined.
/// </summary>
internal sealed class WorkflowAnalyticsApiQuery
{
    /// <summary>
    /// Identifier of one workflow to analyze. Omitted means all workflows; an unknown identifier gives zero counts.
    /// </summary>
    public Guid? WorkflowId { get; set; }

    /// <summary>
    /// Count only runs in this state, as an integer: 0 NotStarted, 1 Running, 2 WaitingForInput, 3 Idle,
    /// 4 Completed, 5 Failed, 6 Cancelled. It filters the run figures only, not the definition counts.
    /// </summary>
    public WorkflowRunState? State { get; set; }

    /// <summary>
    /// Count only runs executed on this runtime backend, as an integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions.
    /// It filters the run figures only.
    /// </summary>
    public WorkflowRuntimeBackendKind? Backend { get; set; }

    /// <summary>
    /// Text matched case-insensitively as a substring of each run's summary, backend run identifier or run
    /// identifier. Surrounding whitespace is ignored; omitted or blank applies no text filter.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Number of most recently updated runs to return in <c>recentRuns</c>, from 1 through 500. Omitted means 8; a
    /// value outside the range is rejected with HTTP 400. It does not limit the counts or the <c>runs</c> list.
    /// </summary>
    public int? Take { get; set; }
}

/// <summary>
/// Body of the production start operations <c>POST /api/workflows/runs/start</c> and
/// <c>POST /api/workflows/definitions/{workflowId}/runs/start</c>. Every member is optional in JSON, but the body
/// itself is required. Members other than the four listed are rejected before the operation runs.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class WorkflowRunStartApiRequest
{
    /// <summary>
    /// Identifier of the workflow to run, as returned in <c>id</c> by <c>GET /api/workflows/definitions</c>.
    /// Required by <c>POST /api/workflows/runs/start</c>; optional on the route that names the workflow, where it
    /// must equal the route value when present.
    /// </summary>
    public Guid? WorkflowId { get; set; }

    /// <summary>
    /// Exact version to run, as returned in <c>versionId</c>. It must be an Active version. Omit it, or send null, to
    /// run the latest Active version of the workflow.
    /// </summary>
    public Guid? VersionId { get; set; }

    /// <summary>
    /// Run input as a JSON string that contains a JSON object (not a nested JSON object), for example
    /// <c>"{\"customerEmail\":\"client@example.com\"}"</c>. Omitted, null or blank means <c>{}</c>. Anything that is
    /// not a JSON object is rejected with HTTP 400. Its members are the input the definition's
    /// <c>inputParameters</c> describe; member order does not matter for idempotency.
    /// </summary>
    public string? InputJson { get; set; }

    /// <summary>
    /// Runtime backend to run on, as a JSON integer: 0 InProcess, 1 DurableTask, 2 AzureFunctions. Omitted or null
    /// uses the definition's <c>runtimePolicy.preferredBackend</c>. The backend must be registered and runnable in
    /// this host; by default only InProcess is (see <c>GET /api/workflows/runtime-backends</c>).
    /// </summary>
    public WorkflowRuntimeBackendKind? RequestedBackend { get; set; }
}
