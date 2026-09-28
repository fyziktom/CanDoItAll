using CanDoItAll.Modules.Projects;
using CanDoItAll.AgentFramework.Models;
using System.Diagnostics;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace CanDoItAll.Web;

public static class ProjectStructureAgentApi
{
    private const string ProjectLifetimeRefreshRequiredErrorCode = "ProjectLifetimeRefreshRequired";
    private const string ReadSourceUnavailableErrorCode =
        "ProjectStructureReadSourceUnavailable";
    private const string ReadSourceInvalidErrorCode =
        "ProjectStructureReadSourceInvalid";

    public static IEndpointRouteBuilder MapProjectStructureAgentApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/project-structure")
            .WithTags("Project Structure")
            .DisableAntiforgery();
        group.WithMetadata(ProjectStructureHttpResponseContract.Instance);
        group.ApplyApiAuthorization(endpoints, ApiAuthorizationPolicies.WriteProjectStructure);

        group.MapGet("/node-catalog", GetNodeCatalogAsync)
            .Produces<ProjectStructureNodeCatalogResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects", ListProjectsAsync)
            .Produces<IReadOnlyList<ProjectSummary>>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects", CreateProjectAsync)
            .Produces<ProjectSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPut("/projects/{projectId:guid}", UpdateProjectAsync)
            .Produces<ProjectSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/hierarchy", GetProjectHierarchyAsync)
            .Produces<ProjectHierarchySnapshot>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{parentProjectId:guid}/subprojects", ChangeSubprojectAsync)
            .Produces<ProjectStructureOkResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/structure/read", ReadStructureAsync)
            .Accepts<ProjectStructureReadRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.StructureRead)
            .Produces<ProjectStructureReadResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/plan/summary", GetPlanSummaryAsync)
            .Produces<ProjectPlanSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/tasks", CreateTaskAsync)
            .Produces<ProjectStructureTaskCreateResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPut("/projects/{projectId:guid}/tasks/{taskId}", UpdateTaskAsync)
            .Produces<ProjectStructureGanttMutationResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/tasks/{taskId}/resource", AttachTaskResourceAsync)
            .Produces<ProjectStructureTaskResourceAttachResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes", CreateNodeAsync)
            .Accepts<ProjectStructureNodeCreateOpenApiRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeCreate)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError);

        group.MapPut("/projects/{projectId:guid}/nodes/{nodeId}", UpdateNodeAsync)
            .Accepts<ProjectStructureNodeEditOpenApiRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeEdit)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/type", ChangeNodeTypeAsync)
            .Accepts<ProjectStructureNodeTypeInput>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeType)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/metadata", UpdateNodeMetadataAsync)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/statuses", UpdateNodeStatusesAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/status", UpdateNodeStatusAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/progress", UpdateNodesProgressAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/progress", UpdateNodeProgressAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/markers", UpdateNodeMarkersAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/markers", ChangeNodeMarkerAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/priorities", UpdateNodePrioritiesAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/priority", UpdateNodePriorityAsync)
            .Produces<int>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/move", MoveNodeAsync)
            .Produces<ProjectStructureOkResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/recompose", RecomposeNodeAsync)
            .Produces<ProjectStructureSubtreeRecompositionResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/reparent", ReparentNodeAsync)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/reparent", ReparentBodyNodeAsync)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/copy", CopyNodesAsync)
            .Produces<ProjectStructureNodesCopyResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/move-to-new-subproject", MoveNodesToNewSubprojectAsync)
            .Produces<ProjectStructureNodesToSubprojectResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost(
                "/projects/{projectId:guid}/nodes/{nodeId}/move-descendants-to-project",
                MoveDescendantsToProjectAsync)
            .Produces<ProjectStructureSubprojectTransferResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/command", ExecuteNodeCommandAsync)
            .Produces<ArtifactReference>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/process-definition", LinkProcessDefinitionAsync)
            .Produces<ProjectStructureLinkChangeResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/process/start", StartProcessNodeAsync)
            .Produces<ProjectStructureProcessNodeStartResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow-add-options", GetWorkflowAddOptionsAsync)
            .Produces<ProjectStructureWorkflowAddOptionsResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow-definition", CreateWorkflowNodeAsync)
            .Produces<ProjectStructureWorkflowNodeCreateResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow/start", StartWorkflowNodeAsync)
            .Produces<ProjectStructureWorkflowNodeStartResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/nodes/{nodeId}/workflow/status", GetWorkflowNodeStatusAsync)
            .Produces<ProjectStructureWorkflowRunStatus>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/delete", DeleteNodeAsync)
            .Produces<ProjectStructureDeletionResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/deletion-completion-notices", ListDeletionCompletionNoticesAsync)
            .Produces<IReadOnlyList<ProjectStructureDeletionCompletionNotice>>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/deletion-cleanups", ListDeletionCleanupsAsync)
            .Produces<IReadOnlyList<ProjectStructureDeletionRecovery>>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/nodes/delete", DeleteNodesAsync)
            .Produces<ProjectStructureDeletionResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/approvals/request", CreateApprovalRequestAsync)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/checklists/query", QueryChecklistAsync)
            .Accepts<ProjectStructureChecklistRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.ChecklistQuery)
            .Produces<ProjectStructureChecklistResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/dependencies/query", QueryDependenciesAsync)
            .Produces<ProjectStructureDependencyResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/links", LinkNodesAsync)
            .Produces<ProjectStructureLinkChangeResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/links/unlink", UnlinkNodesAsync)
            .Produces<ProjectStructureLinkChangeResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/dependencies/link", LinkDependencyAsync)
            .Produces<ProjectStructureLinkChangeResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/projects/{projectId:guid}/dependencies/unlink", UnlinkDependencyAsync)
            .Produces<ProjectStructureLinkChangeResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/assets/{nodeId}", GetAssetAsync)
            .Produces<ProjectStructureAssetDescriptor>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/projects/{projectId:guid}/assets/{nodeId}/content", GetAssetContentAsync)
            .Produces<ProjectStructureAssetContentDescriptor>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/projects/{projectId:guid}/assets", CreateAssetAsync)
            .Accepts<ProjectStructureAssetCreateInput>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.AssetCreate)
            .Produces<ProjectStructureNodeSummary>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status415UnsupportedMediaType,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status502BadGateway,
                StatusCodes.Status504GatewayTimeout);

        group.MapPost("/projects/{projectId:guid}/assets/{nodeId}/revisions", CreateAssetRevisionAsync)
            .Produces<ProjectStructureAssetDescriptor>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status413PayloadTooLarge,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/imports", ImportAsync)
            .Produces<ProjectStructureImportResult>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/knowledge/query", QueryKnowledgeAsync)
            .Produces<ProjectManagementGuidanceResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/leases/acquire", AcquireLeaseAsync)
            .Produces<ProjectStructureLeaseSnapshot>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/leases/renew", RenewLeaseAsync)
            .Produces<ProjectStructureLeaseSnapshot>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/leases/release", ReleaseLeaseAsync)
            .Produces<ProjectStructureLeaseSnapshot>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapGet("/leases/current", GetCurrentLeaseAsync)
            .Produces<ProjectStructureLeaseSnapshot>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        group.MapPost("/analytics/query", QueryAnalyticsAsync)
            .Produces<ProjectStructureAnalyticsResponse>()
            .ProducesProjectStructureErrors(
                StatusCodes.Status409Conflict,
                StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    /// <summary>
    /// Update a canonical project task using the task state previously read by the caller.
    /// </summary>
    /// <remarks>
    /// Changes the title, progress, estimate, planned schedule, execution state or direct assignee of a canonical task
    /// (one created with <c>POST /api/project-structure/projects/{projectId}/tasks</c>). Generic node operations
    /// reject canonical tasks; workflow and process resources are attached with
    /// <c>POST /api/project-structure/projects/{projectId}/tasks/{taskId}/resource</c> instead.
    ///
    /// Read, modify, write:
    ///
    /// 1. Call <c>POST /api/project-structure/projects/{projectId}/structure/read</c> with
    /// <c>{ "includeMetadata": true }</c> and keep its <c>expectedProjectAdmission</c>.
    /// 2. Take the task from <c>nodes</c> by its <c>id</c> and copy <c>title</c>, <c>progressPercent</c>,
    /// <c>startUtc</c> and <c>endUtc</c>.
    /// 3. Parse the task's <c>metadataJson</c> string. Under <c>workItem</c>: <c>expectedEffortHours</c>,
    /// <c>expectedEffortUnit</c>, <c>expectedCostAmount</c> and <c>expectedCostCurrencyCode</c> form the current
    /// estimate; <c>executionState</c>, <c>actualStartedAtUtc</c> and <c>actualEndedAtUtc</c> the current execution;
    /// <c>expectedCostBasis</c> the current cost basis; <c>directAssignmentRevision</c> the current direct-assignment
    /// revision. A member missing there is null, and a missing revision is 0.
    /// 4. Send every current value unchanged and change only the proposed values you intend to change.
    /// 5. Read the structure again to see the stored result.
    ///
    /// <c>metadataJson</c> writes enums as camel-case text (for example <c>"manDays"</c> or <c>"notStarted"</c>), but
    /// this request expects the JSON integers listed on each enum schema. Always include <c>currentCostBasis</c>, using
    /// null when the read had none.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The body must also carry the project write admission from the read;
    /// it carries no lease token.
    ///
    /// Failures handled by the operation use the Project Structure error envelope. HTTP 409 means the task, its
    /// assignments or the project changed after your read, or the admission is missing or stale: read again and
    /// reapply the intended change; never resend an obsolete body with only its preconditions refreshed. A body the
    /// framework cannot bind (malformed JSON, a wrong JSON type such as text for an integer enum, or a missing
    /// <c>currentCostBasis</c> member) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project that contains the task. The body's <c>expectedProjectAdmission</c> must name this
    /// project.
    /// </param>
    /// <param name="taskId">
    /// String node identifier of the task as returned in <c>nodes[].id</c> by the structure read, for example
    /// <c>custom:3f2504e04f8911d39a0c0305e82c3301</c>; not a GUID in general. It must equal <c>taskId</c> in the body.
    /// </param>
    /// <param name="request">Current task state as last read, the proposed values and the project write admission.</param>
    /// <response code="200">
    /// The update was committed. The body lists the tasks whose stored values changed; read the structure again for the
    /// stored values, which the owner may have normalized or repriced.
    /// </response>
    /// <response code="400">
    /// The request was rejected before any change: <c>TaskRouteMismatch</c> (route and body task identifiers differ),
    /// <c>TaskUpdateRequestInvalid</c> (blank identifier, unknown gesture or empty interval), <c>InvalidRequest</c>
    /// (blank title, out-of-range progress, invalid estimate, cost basis or execution transition, or a direct assignee
    /// that is not a person or agent), <c>InvalidTask</c> or <c>InvalidTitle</c> (the node is not an editable task or
    /// the title is not accepted), <c>InvalidSchedule</c> or <c>ProjectionOnlySchedule</c> (the schedule change does not
    /// fit the stored schedule). A body the framework cannot bind is rejected with HTTP 400 without an envelope.
    /// </response>
    /// <response code="404">
    /// The task was not found in the project (<c>WorkItemNotFound</c> or <c>TaskNotFound</c>), or the project was not
    /// found (<c>ProjectNotFound</c>).
    /// </response>
    /// <response code="409">
    /// The read state is no longer current or the write is not admitted: <c>ProjectLifetimeRefreshRequired</c> (the
    /// admission is missing or names another project), <c>ConcurrencyConflict</c> (the current estimate, execution
    /// state, cost basis or direct-assignment revision differs from the stored task, or the task changed while its
    /// pricing was prepared), <c>StaleTask</c> (the current title, progress or a previous interval differs from the
    /// stored task, or another current value changed just before the save), <c>AssignmentConflict</c> (for example
    /// several direct assignees) or <c>ProjectStructureConcurrentMutation</c>. Read the task again before deciding
    /// whether to retry.
    /// </response>
    /// <response code="500">
    /// The update failed unexpectedly (<c>UnhandledError</c>), or an assignee change was rolled back but its previous
    /// assignee or pricing could not be restored (<c>AssignmentCompensationFailed</c>). Read the project before making
    /// another change.
    /// </response>
    internal static Task<IResult> UpdateTaskAsync(
        Guid projectId,
        string taskId,
        HttpContext httpContext,
        ProjectStructureTaskUpdateAgentInput request,
        ProjectStructureTaskDetailsService taskDetailsService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "tasks.update",
            projectId,
            taskId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            async (agent, cancellationToken) =>
            {
                if (!string.Equals(taskId, request.TaskId, StringComparison.Ordinal))
                {
                    throw new ProjectStructureAgentException(
                        StatusCodes.Status400BadRequest,
                        "TaskRouteMismatch",
                        "The task id in the route must match request.taskId.");
                }

                var update = request.ToRequest();
                var expected = RequireProjectAdmission(projectId, update.ExpectedProjectAdmission);
                var owner = agent with { ExpectedProjectAdmission = expected };
                try
                {
                    return await taskDetailsService.UpdateAsync(
                        projectId,
                        update with { MutationOwner = owner },
                        cancellationToken);
                }
                catch (ProjectStructureTaskDetailsException exception)
                {
                    throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                }
                catch (ProjectStructureGanttMutationException exception)
                {
                    throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                }
            },
            cancellationToken);

    /// <summary>
    /// List the node kinds, object types and link kinds that project structures support.
    /// </summary>
    /// <remarks>
    /// Returns the catalog the structure editor uses: node kinds with their object type, subtype, labels and input
    /// fields; every object type with the subtypes callers can create; every link kind with guidance; and general
    /// advice. It does not depend on a project and changes only with the application version. Use it to choose
    /// <c>objectType</c> and <c>objectSubtype</c> for node creation and <c>kind</c> for links.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <response code="200">The node catalog.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetNodeCatalogAsync(
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-catalog",
            null,
            null,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.GetNodeCatalogAsync(cancellationToken),
            cancellationToken);

    /// <summary>
    /// List every project with its summary, most recently updated first.
    /// </summary>
    /// <remarks>
    /// Returns all projects in the active database, not only the caller's, each with its status, hierarchy counts and
    /// related parties. Use <c>id</c> as <c>projectId</c> in the other Project Structure operations. The list is not
    /// paged.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <response code="200">Every project, most recently updated first; empty when there is none.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> ListProjectsAsync(
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "projects.list",
            null,
            null,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.ListProjectsAsync(cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a root project.
    /// </summary>
    /// <remarks>
    /// Creates a new top-level project with the sent values and returns its summary. Its <c>id</c> identifies the
    /// project in the other operations, and its structure starts with the project root node <c>project:{id}</c>. To
    /// place a project under another one, attach it with
    /// <c>POST /api/project-structure/projects/{parentProjectId}/subprojects</c>, or extract existing nodes into a new
    /// subproject with <c>POST /api/project-structure/projects/{projectId}/nodes/move-to-new-subproject</c>. Creation
    /// is not idempotent: repeating the request creates another project. No lease is involved.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope.
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a status name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">Name and optional values of the new project.</param>
    /// <response code="200">The created project.</response>
    /// <response code="400">
    /// Nothing was created: <c>ProjectNameRequired</c> (blank name) or <c>ProjectCreationRejected</c> (the project
    /// owner rejected the values; <c>details</c> lists its errors).
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>: the project was saved but could not be read back; list the projects before retrying.
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; retry.
    /// </response>
    /// <response code="500">
    /// <c>ProjectIdMissing</c> or <c>UnhandledError</c>. The project may have been created before the failure; list the
    /// projects before retrying.
    /// </response>
    internal static Task<IResult> CreateProjectAsync(
        HttpContext httpContext,
        ProjectStructureProjectSaveRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "projects.create",
            null,
            null,
            null,
            null,
            request,
            (agent, cancellationToken) => agentService.SaveProjectAsync(null, request, agent, cancellationToken),
            cancellationToken,
            response => response.Id);

    /// <summary>
    /// Replace the values of an existing project.
    /// </summary>
    /// <remarks>
    /// Replaces the project's name, description, objective, current phase, status and target date with the sent values
    /// and returns the updated summary. Omitted members are not kept: send the current value of everything you keep.
    /// The update also removes the project's phases and option selections, which this request cannot carry. The
    /// project structure and the project hierarchy are not changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a status name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">The complete new values of the project and an optional lease token.</param>
    /// <response code="200">The updated project.</response>
    /// <response code="400">
    /// Nothing was changed: <c>ProjectNameRequired</c> (blank name) or <c>ProjectCreationRejected</c> (the project
    /// owner rejected the values; <c>details</c> lists its errors; the code has this name for updates too).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>ProjectIdMissing</c> or <c>UnhandledError</c>. The update may have been committed before the failure; read
    /// the project before retrying.
    /// </response>
    internal static Task<IResult> UpdateProjectAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureProjectSaveRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "projects.update",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.SaveProjectAsync(projectId, request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read a project's direct parent projects and direct subprojects.
    /// </summary>
    /// <remarks>
    /// Returns the summaries of the projects directly above and directly below the project in the project hierarchy,
    /// each list most recently updated first. A project can have several parents. The project's own summary is not
    /// included, and an unknown project id returns empty lists rather than an error. Change the hierarchy with
    /// <c>POST /api/project-structure/projects/{parentProjectId}/subprojects</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">
    /// The project's direct parents and direct subprojects; both lists are empty for an unknown project.
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetProjectHierarchyAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "projects.hierarchy",
            projectId,
            null,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.GetHierarchyAsync(projectId, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Make a project a subproject of another project, as an additional parent or in place of a current parent.
    /// </summary>
    /// <remarks>
    /// Without <c>currentParentProjectId</c>, adds the route's project as a parent of <c>childProjectId</c> and keeps
    /// the child's other parents; adding a parent the child already has succeeds without a change. With
    /// <c>currentParentProjectId</c>, replaces that parent of the child with the route's project. A change that would
    /// make a project its own parent or create a cycle is rejected. Only the project hierarchy changes; the structures
    /// of the projects are not modified. Read the result with
    /// <c>GET /api/project-structure/projects/{projectId}/hierarchy</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The lease is taken on the child project, not on the parent: send
    /// <c>leaseToken</c> to work under your own lease on the child; without it the operation uses the lease your
    /// identity already holds on the child, or else takes a five-minute lease on it for its own duration; if another
    /// identity holds that lease, it waits when the lease ends within 30 seconds and otherwise fails with HTTP 409
    /// <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="parentProjectId">
    /// Identifier of the project that becomes the parent, as returned in <c>id</c> by
    /// <c>GET /api/project-structure/projects</c>.
    /// </param>
    /// <param name="request">The child project, the parent to replace (optional) and an optional lease token.</param>
    /// <response code="200">The hierarchy change is committed; the body is always <c>{ "ok": true }</c>.</response>
    /// <response code="400">
    /// Nothing was changed: <c>ChildProjectRequired</c> (no child id) or <c>ProjectStructureValidation</c>, with the
    /// reason in <c>message</c>: the parent project does not exist, the child would become its own parent or part of a
    /// cycle, or <c>currentParentProjectId</c> is not a parent of the child or equals the new parent.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: the child project does not exist.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the hierarchy before
    /// retrying.
    /// </response>
    internal static Task<IResult> ChangeSubprojectAsync(
        Guid parentProjectId,
        HttpContext httpContext,
        ProjectStructureSubprojectChangeRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "projects.subproject-change",
            request.ChildProjectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            request.ChildProjectId.ToString(),
            request,
            async (agent, cancellationToken) =>
            {
                await agentService.ChangeSubprojectAsync(parentProjectId, request, agent, cancellationToken);
                return new { Ok = true };
            },
            cancellationToken);

    /// <summary>
    /// Read the nodes and links of a project structure, with optional filters and sections.
    /// </summary>
    /// <remarks>
    /// Returns the current stored structure of the project: the selected nodes, the links between them when requested,
    /// and <c>expectedProjectAdmission</c>, the project write admission that task writes and managed-file deletions
    /// send back. Read it before changing anything, to obtain node identifiers (<c>nodes[].id</c>) and the current
    /// values that edits are checked against. The node catalog lists the kinds of node that can be created.
    ///
    /// Send <c>{}</c> to get every node with the basic members only. Set <c>includeMetadata</c> to get
    /// <c>metadataJson</c> (task edits need it), <c>includeLinks</c> for links, and <c>includeNotes</c>,
    /// <c>includeLayout</c> or <c>includeAssets</c> for the other optional members. <c>nodeIds</c> and
    /// <c>subtreeRootIds</c> select nodes (their union); the other filters narrow the selection. Nodes are ordered by
    /// title, and <c>take</c> cuts the list with a warning. Only the current structure can be read over HTTP:
    /// <c>source</c> 1 (InvocationSnapshot) belongs to in-process agent invocations and is rejected.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">
    /// The selected part of the structure as currently stored. Keep <c>expectedProjectAdmission</c> for
    /// admission-checked writes; it becomes stale when the project is replaced or deleted.
    /// </response>
    /// <response code="400">
    /// <c>ProjectStructureReadSourceUnavailable</c> (<c>source</c> 1) or <c>ProjectStructureReadSourceInvalid</c> (an
    /// undefined <c>source</c>), both with <c>details.requestedSource</c> and <c>details.supportedSource</c>;
    /// <c>ProjectStructureRequestInvalid</c> (malformed JSON or a member of the wrong JSON type, such as a source name
    /// instead of its integer); <c>ProjectStructureObjectTypeInvalid</c> (an unknown value in <c>objectTypes</c>).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists in the active database.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c>: the body exceeds 262,144 bytes; <c>details.maximumBytes</c> gives
    /// the limit.
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> ReadStructureAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureReadRequest, ProjectStructureReadResponse>(
            httpContext,
            analyticsService,
            "structure.read",
            projectId,
            null,
            null,
            null,
            ProjectStructureHttpBodyContracts.StructureRead,
            (request, _, cancellationToken) => agentService.GetStructureAsync(
                projectId,
                ResolveHttpReadRequest(request),
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Compute the plan summary of a project from its canonical tasks.
    /// </summary>
    /// <remarks>
    /// Computes, as of <c>asOfUtc</c> (now by default), how the project's canonical tasks are distributed over plan
    /// states, their schedule span, total effort, task- and effort-weighted progress, expected cost per currency,
    /// remaining cost per resource group and date, resource coverage, previews of running, blocked and waiting tasks,
    /// and counts of tasks with missing or unusable data. Nothing is stored or changed. Costs are totalled per currency
    /// and never converted. It uses POST because the query travels in the body.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Reference instant, preview size and hours per man-day.</param>
    /// <response code="200">The computed plan summary.</response>
    /// <response code="400">
    /// <c>PlanSummaryQueryInvalid</c>: <c>taskPreviewLimit</c> is outside 0 through 100 or <c>hoursPerManDay</c>
    /// outside 1 through 24.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="413">
    /// <c>PlanSummaryPayloadLimitExceeded</c>: the plan has more nodes or links than the summary's safety limits
    /// (50,000 nodes and 100,000 links by default); <c>details</c> gives the counts and limits.
    /// </response>
    /// <response code="500">The computation failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetPlanSummaryAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectPlanSummaryQuery request,
        ProjectPlanAnalyticsQueryService planAnalyticsService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "plan.summary",
            projectId,
            null,
            null,
            null,
            request,
            (_, cancellationToken) => planAnalyticsService.GetSummaryAsync(projectId, request, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a canonical project task with its planned interval and optional estimate and resource.
    /// </summary>
    /// <remarks>
    /// Creates a canonical task (a WorkItem with subtype <c>task</c>) in the project's main backlog block, which is
    /// created when missing, with execution state NotStarted. With a resource the owner attaches it (a person or agent
    /// becomes the direct assignee) and prices the estimate from it; the task is then placed in the Gantt row order
    /// after <c>afterTaskNodeId</c> or at the end. Generic node creation rejects tasks; change the task later with
    /// <c>PUT /api/project-structure/projects/{projectId}/tasks/{taskId}</c>.
    ///
    /// Requires the <c>expectedProjectAdmission</c> from the latest structure read. When resource attachment or row
    /// ordering fails after the task was written, the owner deletes the task again: HTTP 409
    /// <c>ResourceAttachmentFailed</c> or <c>RowOrderingFailed</c> means nothing remains, while HTTP 500
    /// <c>ResourceAttachmentCompensationFailed</c> or <c>RowOrderingCompensationFailed</c> means the task could not be
    /// removed; <c>details.taskNodeId</c> names it in both cases. Creation is not idempotent: repeating a request
    /// creates another task, so read the structure before retrying after an unclear failure.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The body carries no lease token: the operation uses the project lease
    /// your identity already holds, or else takes a five-minute project lease for its own duration; if another identity
    /// holds the lease, it waits when that lease ends within 30 seconds and otherwise fails with HTTP 409
    /// <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type, such as an enum name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Title, planned interval, optional estimate, resource and row position, and the
    /// admission.</param>
    /// <response code="200">
    /// The committed task: its node identifier, its backlog block, the attached resource and how it was priced.
    /// </response>
    /// <response code="400">
    /// Nothing was created: <c>TaskTitleRequired</c>, <c>TaskTitleTooLong</c> (over 200 characters),
    /// <c>TaskDatesRequired</c>, <c>TaskDateRangeInvalid</c> (end not after start), <c>TaskDurationTooLong</c>,
    /// <c>TaskEstimateInvalid</c>, or an invalid resource (<c>TaskResourceKindInvalid</c>, <c>TaskResourceRequired</c>,
    /// <c>TaskResourceVersionInvalid</c>, <c>TaskResourceVersionNotSupported</c>, <c>TaskWorkflowVersionRequired</c>).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>ProjectLifetimeRefreshRequired</c> (admission missing or for another project; nothing created),
    /// <c>ResourceAttachmentFailed</c> or <c>RowOrderingFailed</c> (the created task was removed again),
    /// <c>LeaseConflict</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>ResourceAttachmentCompensationFailed</c> or <c>RowOrderingCompensationFailed</c> (the task remains and needs
    /// attention), or <c>UnhandledError</c>. Read the structure before retrying.
    /// </response>
    internal static Task<IResult> CreateTaskAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureTaskCreateRequest request,
        ProjectStructureTaskCreationService taskCreationService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "tasks.create",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            async (agent, cancellationToken) =>
            {
                var expected = RequireProjectAdmission(projectId, request.ExpectedProjectAdmission);
                agent = agent with { ExpectedProjectAdmission = expected };
                try
                {
                    return await taskCreationService.CreateAsync(
                        projectId,
                        request,
                        agent,
                        cancellationToken);
                }
                catch (ProjectStructureTaskCreationException exception)
                {
                    throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                }
                catch (ProjectStructureGanttMutationException exception)
                {
                    throw ProjectStructureTaskAgentExceptionMapper.Map(exception);
                }
            },
            cancellationToken);

    /// <summary>
    /// Attach a workflow or process definition to a canonical task and reprice the task for it.
    /// </summary>
    /// <remarks>
    /// Attaches the workflow (at its exact version, with optional run input settings) or the process definition to the
    /// task and recalculates the task's expected cost from that resource's price. The result reports the pricing and
    /// the node created for the resource, if any. Persons and agents are not attached here: set them as the direct
    /// assignee with <c>PUT /api/project-structure/projects/{projectId}/tasks/{taskId}</c>.
    ///
    /// Read, modify, write: read the structure with <c>includeMetadata</c>, keep <c>expectedProjectAdmission</c>, and
    /// send the task's execution state from its <c>metadataJson</c> (<c>workItem.executionState</c> and the actual
    /// timestamps) as <c>currentExecution</c>. When pricing fails after the resource was attached, the attachment is
    /// rolled back and the response is HTTP 409 <c>TaskResourceAttachmentPricingConflict</c>; read the task again and
    /// retry. HTTP 500 <c>TaskResourceAttachmentCompensationFailed</c> means the rollback failed: read the task and
    /// resolve its resource before editing it again.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The body carries no lease token; where the owner needs a project
    /// lease, it uses the one your identity already holds, or else takes a five-minute lease for its own duration; if
    /// another identity holds the lease, it waits when that lease ends within 30 seconds and otherwise fails with HTTP
    /// 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON, a member of the wrong JSON type, or a missing
    /// <c>resource</c> or <c>currentExecution</c>) is rejected with HTTP 400 before the operation runs and has no
    /// envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="taskId">
    /// String node identifier of the canonical task, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">Resource, the task's execution state as last read, workflow input settings and
    /// admission.</param>
    /// <response code="200">The resource is attached and the task repriced; the body reports both.</response>
    /// <response code="400">
    /// Nothing was changed: <c>TaskAttachedResourceKindInvalid</c> (a person or agent),
    /// <c>TaskResourceRequired</c>, <c>TaskResourceVersionInvalid</c>, <c>TaskWorkflowVersionRequired</c>,
    /// <c>TaskWorkflowInputSettingsResourceKindInvalid</c>, <c>TaskExecutionSnapshotRequired</c>,
    /// <c>CanonicalTaskRequired</c> (the node is not a canonical task) or invalid workflow input settings.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>TaskNotFound</c>.</response>
    /// <response code="409">
    /// <c>ProjectLifetimeRefreshRequired</c> (admission missing or for another project),
    /// <c>TaskResourceAttachmentPricingConflict</c> (rolled back), <c>LeaseConflict</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>TaskResourceAttachmentCompensationFailed</c> (attached but not priced and not rolled back) or
    /// <c>UnhandledError</c>. Read the task before retrying.
    /// </response>
    internal static Task<IResult> AttachTaskResourceAsync(
        Guid projectId,
        string taskId,
        HttpContext httpContext,
        ProjectStructureTaskResourceAttachRequest request,
        ProjectStructureTaskResourceAttachmentService attachmentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "tasks.resource-attach",
            projectId,
            taskId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => attachmentService.AttachAsync(
                projectId,
                taskId,
                request,
                agent with {
                    ExpectedProjectAdmission = RequireProjectAdmission(projectId, request.ExpectedProjectAdmission)
                },
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a generic node, such as a block, note, decision, link or runtime node, in a project structure.
    /// </summary>
    /// <remarks>
    /// Creates one node under <c>parentNodeKey</c> (the project root when omitted) and returns it with every optional
    /// member filled. Choose <c>objectType</c> and <c>objectSubtype</c> from <c>GET
    /// /api/project-structure/node-catalog</c>. Use <c>POST /api/project-structure/projects/{projectId}/tasks</c> for
    /// canonical tasks (WorkItem with subtype <c>task</c>) and <c>POST
    /// /api/project-structure/projects/{projectId}/assets</c> for File nodes and <c>mermaid</c> subtypes; this
    /// operation rejects them. The node is placed on the canvas automatically near its parent.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>. Metadata folders of runtime nodes and delivery blocks must
    /// lie inside the managed workspace.
    ///
    /// The whole body stays under the host's request limit of 30,000,000 bytes by default: a larger body is rejected
    /// with HTTP 400 <c>ProjectStructureRequestInvalid</c> (or HTTP 413 when its declared length exceeds the reader's
    /// limit). The creation is not idempotent: repeating a request creates another node, so read the structure before
    /// retrying after an unclear failure.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">The committed node, with notes, metadata, layout and file members filled.</response>
    /// <response code="400">
    /// Nothing was created: <c>ProjectStructureRequestInvalid</c> (malformed or oversized body, or a member of the
    /// wrong JSON type), <c>ProjectStructureObjectTypeInvalid</c> (unknown object type or alias),
    /// <c>ManagedAssetCreationRequired</c> (File or mermaid node), <c>InvalidRuntimeMetadata</c> (invalid Script,
    /// Environment or Infrastructure metadata), or an invalid <c>media</c> payload (<c>FileNameRequired</c>,
    /// <c>MediaPayloadRequired</c>, <c>InvalidBase64Payload</c>, <c>InvalidSvgRoot</c>, <c>InvalidSvgXml</c>).
    /// </response>
    /// <response code="403">
    /// <c>RuntimePathNotAuthorized</c> or <c>ProjectBlockRootOutsideExecutionScope</c>: the metadata or the parent
    /// block names a folder outside the managed workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>ParentNodeNotFound</c>.</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c> (a WorkItem with subtype <c>task</c>), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c> (declared body length over 36,001,112 bytes) or
    /// <c>MediaPayloadTooLarge</c> (file content over 25 MiB).
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>, also returned for <c>metadataJson</c> that is not valid JSON. The node may have been
    /// created before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> CreateNodeAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureNodeCreateInput, ProjectStructureNodeSummary>(
            httpContext,
            analyticsService,
            "structure.node-create",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            ProjectStructureHttpBodyContracts.NodeCreate,
            async (request, agent, cancellationToken) =>
            {
                ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericCreateAllowed(
                    request.ObjectType,
                    request.ObjectSubtype);
                return await agentService.CreateNodeAsync(
                    projectId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Replace the display fields, schedule and metadata of a generic node, optionally changing its kind.
    /// </summary>
    /// <remarks>
    /// Writes the sent values over the node's subtitle, notes and schedule, so read the node first and send the current
    /// values of everything you keep: an omitted <c>subtitle</c> or <c>notes</c> is cleared, and omitted schedule
    /// members clear the planned interval unless the node is also reclassified. A blank <c>title</c> keeps the title,
    /// and blank metadata keeps the stored metadata. A new <c>objectType</c> or <c>objectSubtype</c> reclassifies the
    /// node when that change is supported. Status, progress, markers, priority, position and parent have their own
    /// operations.
    ///
    /// Canonical tasks are rejected; change them with
    /// <c>PUT /api/project-structure/projects/{projectId}/tasks/{taskId}</c>. Nodes kept in sync with other records
    /// (badge Synced) cannot be updated and are reported as not found.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <response code="200">The committed node, with notes, metadata, layout and file members filled.</response>
    /// <response code="400">
    /// Nothing was changed: <c>ProjectStructureRequestInvalid</c>, <c>ProjectStructureObjectTypeInvalid</c>,
    /// <c>NodeReclassificationUnavailable</c> (the kind change is not supported) or <c>InvalidRuntimeMetadata</c>.
    /// </response>
    /// <response code="403">
    /// <c>RuntimePathNotAuthorized</c> or <c>ProjectBlockRootOutsideExecutionScope</c>: the metadata names a folder
    /// outside the managed workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c> (also for synced nodes).</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c> (the node is or would become a canonical task),
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c>: the body exceeds 1,048,576 bytes; <c>details.maximumBytes</c> gives
    /// the limit.
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>, also returned for <c>metadataJson</c> that is not valid JSON. The change may have been
    /// committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureNodeEditInput, ProjectStructureNodeSummary>(
            httpContext,
            analyticsService,
            "structure.node-update",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            ProjectStructureHttpBodyContracts.NodeEdit,
            async (request, agent, cancellationToken) =>
            {
                await EnsureGenericNodeUpdateAllowedAsync(
                    agentService,
                    projectId,
                    nodeId,
                    request.ObjectType,
                    request.ObjectSubtype,
                    cancellationToken);
                return await agentService.UpdateNodeAsync(
                    projectId,
                    nodeId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Change the object type or subtype of a generic node, keeping its other values.
    /// </summary>
    /// <remarks>
    /// Reclassifies the node while keeping its title, subtitle, notes, metadata and schedule. Supported changes are a
    /// subtype change within the same family of node kinds, promoting a Note to a kind that accepts promotion, and
    /// switching between runnable runtime kinds (script, runtime environment, Docker); other changes are rejected.
    /// Send only a real change: repeating the node's current type and subtype clears its planned interval.
    ///
    /// Canonical tasks cannot be changed here, and no node can be turned into one. Nodes kept in sync with other
    /// records (badge Synced) are reported as not found.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <response code="200">The committed node, with notes, metadata, layout and file members filled.</response>
    /// <response code="400">
    /// Nothing was changed: <c>ProjectStructureRequestInvalid</c>, <c>ProjectStructureObjectTypeInvalid</c>,
    /// <c>NodeReclassificationUnavailable</c> (unsupported change) or <c>InvalidRuntimeMetadata</c> (the kept metadata
    /// is not valid for the new runtime kind).
    /// </response>
    /// <response code="403">
    /// <c>RuntimePathNotAuthorized</c> or <c>ProjectBlockRootOutsideExecutionScope</c>: the metadata names a folder
    /// outside the managed workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c> (also for synced nodes).</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c>, <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c>: the body exceeds 262,144 bytes; <c>details.maximumBytes</c> gives
    /// the limit.
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> ChangeNodeTypeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureNodeTypeInput, ProjectStructureNodeSummary>(
            httpContext,
            analyticsService,
            "structure.node-type",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            ProjectStructureHttpBodyContracts.NodeType,
            async (request, agent, cancellationToken) =>
            {
                await EnsureGenericNodeUpdateAllowedAsync(
                    agentService,
                    projectId,
                    nodeId,
                    request.ObjectType,
                    request.ObjectSubtype,
                    cancellationToken);
                return await agentService.UpdateNodeTypeAsync(
                    projectId,
                    nodeId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Replace the metadata of a generic node, optionally with its notes and status.
    /// </summary>
    /// <remarks>
    /// Replaces the node's metadata with <c>metadataJson</c> (blank or exactly <c>{}</c> keeps it), replaces the notes
    /// when <c>notes</c> is sent and sets the status, with its derived progress, when <c>status</c> is not blank. Other
    /// values are kept. Canonical tasks are rejected; their values change through <c>PUT
    /// /api/project-structure/projects/{projectId}/tasks/{taskId}</c>. Nodes kept in sync with other records (badge
    /// Synced) are reported as not found.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <param name="request">New metadata and optional notes, status and lease token.</param>
    /// <response code="200">The committed node, with notes, metadata, layout and file members filled.</response>
    /// <response code="400">
    /// <c>InvalidRuntimeMetadata</c>: invalid Script, Environment or Infrastructure metadata.
    /// </response>
    /// <response code="403">
    /// <c>RuntimePathNotAuthorized</c> or <c>ProjectBlockRootOutsideExecutionScope</c>: the metadata names a folder
    /// outside the managed workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c> (also for synced nodes).</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c>, <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>, also returned for <c>metadataJson</c> that is not valid JSON. The change may have been
    /// committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeMetadataAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureNodeMetadataInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-metadata",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            async (agent, cancellationToken) =>
            {
                var node = await GetNodeForGenericUpdateAsync(
                    agentService,
                    projectId,
                    nodeId,
                    cancellationToken);
                ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericMetadataUpdateAllowed(node);
                return await agentService.UpdateNodeMetadataAsync(
                    projectId,
                    nodeId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Set the status of several nodes of a project.
    /// </summary>
    /// <remarks>
    /// Gives each listed node the same free-text status and resets its progress mode and percentage from the status
    /// text (for example Done gives <c>complete</c> and 100, Active gives <c>progress</c> and 62). Unknown identifiers
    /// and nodes kept in sync with other records (badge Synced) are skipped without error, and a blank status changes
    /// nothing. Canonical tasks are not excluded. The response counts the nodes that were changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Identifiers of the nodes, the new status and an optional lease token.</param>
    /// <response code="200">
    /// Number of nodes whose status was set, as a JSON integer; 0 when no listed node matched or the status was blank.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeStatusesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureStatusBatchInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-statuses",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodeStatusesAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Set the status of one node.
    /// </summary>
    /// <remarks>
    /// Replaces the node's free-text status and resets its progress mode and percentage from the status text (for
    /// example Done gives <c>complete</c> and 100). An unknown node or a node kept in sync with another record (badge
    /// Synced) is skipped without error, and a blank status changes nothing. Canonical tasks are not excluded.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <param name="request">New status and an optional lease token.</param>
    /// <response code="200">
    /// Number of nodes changed, as a JSON integer: 1, or 0 when the node was not found, is synced or the status was
    /// blank.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeStatusAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureStatusInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-status",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodeStatusesAsync(
                projectId,
                new ProjectStructureStatusBatchInput([nodeId], request.Status, request.LeaseToken),
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Set explicit progress on several nodes of a project.
    /// </summary>
    /// <remarks>
    /// Gives each listed node the same progress mode and percentage, independent of its status; a later status change
    /// resets the progress from the status text again. Unknown identifiers are skipped without error; canonical tasks
    /// and synced nodes are not excluded. The response counts the nodes that were changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Identifiers of the nodes, the progress mode and percentage and an optional lease
    /// token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer; 0 when no listed node matched.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> UpdateNodesProgressAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureProgressBatchInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-progress",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodeProgressAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Set explicit progress on one node.
    /// </summary>
    /// <remarks>
    /// Replaces the node's progress mode and percentage, independent of its status; a later status change resets the
    /// progress from the status text again. An unknown node is skipped without error; canonical tasks and synced nodes
    /// are not excluded.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <param name="request">Progress mode and percentage and an optional lease token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer: 1, or 0 when the node was not found.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeProgressAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureProgressInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-progress-single",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodeProgressAsync(
                projectId,
                new ProjectStructureProgressBatchInput(
                    [nodeId],
                    request.ProgressMode,
                    request.ProgressPercent,
                    request.LeaseToken),
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Replace the markers of several nodes of a project with one marker.
    /// </summary>
    /// <remarks>
    /// Each listed node keeps only the given marker; a blank <c>markerIcon</c> removes all markers of the nodes. To
    /// add, toggle or remove one marker on a node without touching its others, use <c>POST
    /// /api/project-structure/projects/{projectId}/nodes/{nodeId}/markers</c>. Unknown identifiers are skipped without
    /// error; canonical tasks and synced nodes are not excluded. The response counts the nodes that were changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Identifiers of the nodes, the marker and an optional lease token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer; 0 when no listed node matched.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> UpdateNodeMarkersAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureMarkerBatchInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-markers",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodeMarkerAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Add, toggle, remove, replace or clear a marker on one node.
    /// </summary>
    /// <remarks>
    /// Changes the node's markers according to <c>mode</c>: Replace keeps only the given marker, Add adds it (or
    /// replaces the one with the same icon), Toggle removes it when present and adds it otherwise, Remove removes the
    /// marker with that icon and Clear removes all markers. Markers are identified by their icon, ignoring case; the
    /// primary marker returned in structure reads is the one added last. An unknown node is skipped without error.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <param name="request">Mode, marker and an optional lease token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer: 1, or 0 when the node was not found.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> ChangeNodeMarkerAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureMarkerInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-marker",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.ChangeNodeMarkerAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Set the priority of several nodes of a project.
    /// </summary>
    /// <remarks>
    /// Gives each listed node the same priority: 0 removes it, 1 is the most urgent and 6 the least, and values
    /// outside 0 through 6 are clamped. A parent's effective priority follows its most urgent unfinished descendant.
    /// Unknown identifiers are skipped without error; canonical tasks and synced nodes are not excluded. The response
    /// counts the nodes that were changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Identifiers of the nodes, the priority and an optional lease token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer; 0 when no listed node matched.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> UpdateNodePrioritiesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructurePriorityBatchInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-priorities",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodePriorityAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Set the priority of one node.
    /// </summary>
    /// <remarks>
    /// Replaces the node's priority: 0 removes it, 1 is the most urgent and 6 the least, and values outside 0 through
    /// 6 are clamped. An unknown node is skipped without error; canonical tasks and synced nodes are not excluded.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read; not a GUID in general.
    /// </param>
    /// <param name="request">Priority and an optional lease token.</param>
    /// <response code="200">Number of nodes changed, as a JSON integer: 1, or 0 when the node was not found.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The change may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> UpdateNodePriorityAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructurePriorityInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-priority",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UpdateNodePriorityAsync(
                projectId,
                new ProjectStructurePriorityBatchInput([nodeId], request.Priority, request.LeaseToken),
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Move a node to a new position on the structure canvas.
    /// </summary>
    /// <remarks>
    /// Changes only the node's canvas coordinates in the structure editor; its parent, links and schedule stay the
    /// same. Use the reparent operations to move a node in the hierarchy. An unknown node identifier is ignored and the
    /// response is still <c>{ "ok": true }</c>, so read the structure with <c>includeLayout</c> to confirm the new
    /// position.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Node identifier, new coordinates and an optional lease token.</param>
    /// <response code="200">The operation completed; the body is always <c>{ "ok": true }</c>.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The move may have been committed before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> MoveNodeAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodeMoveInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-move",
            projectId,
            request.NodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            async (agent, cancellationToken) =>
            {
                await agentService.MoveNodeAsync(projectId, request, agent, cancellationToken);
                return new { Ok = true };
            },
            cancellationToken);

    /// <summary>
    /// Automatically re-lay out the descendants of a node on the structure canvas.
    /// </summary>
    /// <remarks>
    /// Computes a new layout for every descendant of the root node and stores their canvas positions; the root node,
    /// the hierarchy, links and schedules stay the same. A node without descendants returns zero counts.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Root node identifier and an optional lease token.</param>
    /// <response code="200">
    /// The committed re-layout, with the number of descendants and of repositioned nodes.
    /// </response>
    /// <response code="400"><c>RecompositionUnavailable</c>: the root node does not exist in the project.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The layout may have been stored before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> RecomposeNodeAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodeRecomposeInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-recompose",
            projectId,
            request.RootNodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.RecomposeNodeAsync(projectId, request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Move the node named in the route under another parent in the project hierarchy.
    /// </summary>
    /// <remarks>
    /// Makes <c>parentNodeKey</c> the node's parent (the project root when null or blank); its descendants move with it
    /// and its links other than parent links are kept. Sending the current parent changes nothing. The new parent
    /// cannot be the node itself or one of its descendants. Nodes kept in sync with other records (badge Synced) cannot
    /// be moved and are reported as not found. <c>POST /api/project-structure/projects/{projectId}/nodes/reparent</c>
    /// does the same with the node identifier in the body.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node to move, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">New parent node identifier and an optional lease token.</param>
    /// <response code="200">
    /// The committed node with its new <c>parentId</c>, with every optional member filled.
    /// </response>
    /// <response code="400">
    /// <c>InvalidStructureRelationship</c>: the requested parent relationship is not valid.
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: the new parent is a block whose folder lies outside the managed
    /// workspace.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>NodeNotFound</c> (also for synced nodes) or <c>ParentNodeNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>HierarchyCycle</c> (the parent is inside the node's subtree), <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The move may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> ReparentNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureNodeParentInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-reparent-single",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.ReparentNodeAsync(
                projectId,
                new ProjectStructureNodeReparentInput(nodeId, request.ParentNodeKey, request.LeaseToken),
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Move the node named in the body under another parent in the project hierarchy.
    /// </summary>
    /// <remarks>
    /// Same as <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/reparent</c>, with the node
    /// identifier in <c>nodeId</c> of the body: <c>parentNodeKey</c> becomes the node's parent (the project root when
    /// null or blank), its descendants move with it and its links other than parent links are kept. The new parent
    /// cannot be the node itself or one of its descendants, and synced nodes are reported as not found.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Node identifier, new parent node identifier and an optional lease token.</param>
    /// <response code="200">
    /// The committed node with its new <c>parentId</c>, with every optional member filled.
    /// </response>
    /// <response code="400">
    /// <c>InvalidStructureRelationship</c>: the requested parent relationship is not valid.
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: the new parent is a block whose folder lies outside the managed
    /// workspace.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>NodeNotFound</c> (also for synced nodes) or <c>ParentNodeNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>HierarchyCycle</c> (the parent is inside the node's subtree), <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The move may have been committed before the failure; read the node before retrying.
    /// </response>
    internal static Task<IResult> ReparentBodyNodeAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodeReparentInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-reparent",
            projectId,
            request.NodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.ReparentNodeAsync(projectId, request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Copy one or more subtrees of a project structure under another parent in the same project.
    /// </summary>
    /// <remarks>
    /// Copies each source node with all its descendants under <c>destinationParentNodeId</c>, together with the
    /// caller-created links between copied nodes; the response maps every source node to its copy and lists the
    /// caller-created links that crossed the copied set and were left out. The copy fails as a whole, before anything
    /// is saved, when a subtree contains a canonical task, a source is not editable or not finished with deferred work,
    /// or the destination is missing or a synced project node. Copying is not idempotent: repeating a request creates
    /// another set of copies.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Source subtree roots, destination parent and an optional lease token.</param>
    /// <response code="200">The committed copy with the new node identifiers.</response>
    /// <response code="400">
    /// <c>NodeCopySourceRequired</c> (no source), <c>NodeCopySourceInvalid</c> (a blank source) or
    /// <c>NodeCopyDestinationRequired</c> (no destination).
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: the destination is a block whose folder lies outside the managed
    /// workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>ParentNodeNotFound</c> (destination not found).</response>
    /// <response code="409">
    /// The copy was rejected before anything was saved, with <c>details.reason</c>, <c>details.subjectNodeId</c> and
    /// <c>details.deferredState</c>: <c>NodeCopyTaskAuthorityRequired</c> (a subtree contains a canonical task),
    /// <c>NodeCopySourceNotEditable</c>, <c>NodeCopyDeferredCompletionIncomplete</c>,
    /// <c>NodeCopyDestinationNotFound</c> or <c>NodeCopyDestinationProjected</c>. Also <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The copies may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> CopyNodesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodesCopyInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.nodes-copy",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.CopyNodesAsync(
                projectId,
                request,
                agent,
                ProjectStructureClipboardCopyTaskPolicy.NonTaskStructureOnly,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a subproject of the project and move selected nodes into its structure.
    /// </summary>
    /// <remarks>
    /// Creates a new project under the route's project (status Active and current phase Execution unless sent) and
    /// moves the selected nodes into it, by default with all their descendants. Moved nodes keep their identifiers;
    /// those whose parent stays behind become children of the new project's root. With <c>includeDescendants</c>
    /// false, children that stay are attached to the moved node's former parent. Links between moved nodes move with
    /// them; links between a moved node and one that stays are deleted and listed in <c>removedBoundaryLinks</c>.
    /// Identifiers that are not editable nodes of the project (unknown ids, the project root, synced nodes) are ignored
    /// with a warning. The operation is not idempotent: repeating it creates another subproject.
    ///
    /// The subproject is created first and the nodes move afterwards. When the move fails, the empty subproject is
    /// removed again (HTTP 400 <c>SelectedNodesTransferUnavailable</c> or HTTP 500 <c>SubprojectTransferFailed</c>).
    /// When the nodes moved but reconciling their task assignments failed, the change stays committed and HTTP 409
    /// <c>ProjectStructureTransferPartialCommit</c> is returned: do not repeat the move.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own lease on the route's
    /// project. Without it the operation uses the lease your identity already holds on that project, or else takes a
    /// five-minute lease on it for its own duration; if another identity holds the lease, it waits when that lease ends
    /// within 30 seconds and otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a status name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project the nodes come from, as returned in <c>id</c> by
    /// <c>GET /api/project-structure/projects</c>.
    /// </param>
    /// <param name="request">Name and optional values of the new subproject, the nodes to move and a lease
    /// token.</param>
    /// <response code="200">
    /// The committed extraction: the new subproject's identifier and name, the moved nodes and the removed links.
    /// </response>
    /// <response code="400">
    /// Nothing was changed: <c>SubprojectNameRequired</c>, <c>SelectedNodesRequired</c>,
    /// <c>ProjectCreationRejected</c> (the project owner rejected the new subproject; <c>details</c> lists its errors)
    /// or <c>SelectedNodesTransferUnavailable</c> (no node could be moved; the new subproject was removed again).
    /// </response>
    /// <response code="404">
    /// Nothing was changed: <c>ProjectNotFound</c>, or <c>SelectedNodesNotFound</c> (no selected identifier is an
    /// editable node of the project; <c>details.requestedNodeIds</c> repeats them).
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureTransferPartialCommit</c>: the subproject exists and the nodes moved, but reconciling their
    /// task assignments failed; <c>details</c> holds <c>targetProjectId</c>, <c>durableMutationId</c> and
    /// <c>retryGuidance</c>. Also <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>SubprojectTransferFailed</c> (the move failed after the subproject was created, and the subproject was
    /// removed again; <c>details.removedProjectId</c> names it), <c>SubprojectTransferTargetMismatch</c> or
    /// <c>UnhandledError</c>. After an <c>UnhandledError</c> the new subproject may remain; read the hierarchy and the
    /// structure before retrying.
    /// </response>
    internal static Task<IResult> MoveNodesToNewSubprojectAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodesToSubprojectInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.nodes-move-to-new-subproject",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.MoveNodesToNewSubprojectAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken,
            response => response.TargetProjectId);

    /// <summary>
    /// Move all descendants of a node into another existing project.
    /// </summary>
    /// <remarks>
    /// Moves the descendants of the route's node, except synced nodes and anything below them, into
    /// <c>targetProjectId</c>, which can be any other existing project. The node itself stays; its former children
    /// become children of the target project's root and deeper descendants keep their parents. Moved nodes keep their
    /// identifiers. Links between moved nodes move with them; links between a moved node and one that stays are deleted
    /// and listed in <c>removedBoundaryLinks</c>. A node without descendants, or an identifier that is not in the
    /// project, returns a result with empty lists and changes nothing.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Both projects are leased: send <c>leaseToken</c> for your lease on the
    /// route's project and <c>targetLeaseToken</c> for your lease on the target project. For each token omitted, the
    /// operation uses the lease your identity already holds on that project, or else takes a five-minute lease on it;
    /// if another identity holds the lease, it waits when that lease ends within 30 seconds and otherwise fails with
    /// HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project the descendants come from, as returned in <c>id</c> by
    /// <c>GET /api/project-structure/projects</c>.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node whose descendants move, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">The target project and optional lease tokens for both projects.</param>
    /// <response code="200">The committed transfer; all lists are empty when nothing was moved.</response>
    /// <response code="400">
    /// Nothing was changed: <c>TargetProjectRequired</c> or <c>TargetProjectMustDiffer</c>.
    /// </response>
    /// <response code="404">
    /// Nothing was changed: <c>ProjectNotFound</c> (the route's project or the target project), or
    /// <c>NodeNotFound</c> (the route's project has no stored nodes).
    /// </response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The nodes may have moved before the failure, also when only the reconciliation of their
    /// task assignments failed; read both structures before retrying.
    /// </response>
    internal static Task<IResult> MoveDescendantsToProjectAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureSubtreeTransferInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-transfer-descendants",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.MoveDescendantsToProjectAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Resolve the page or artifact a node opens, creating a prompt node's Prompt Gallery prompt when needed.
    /// </summary>
    /// <remarks>
    /// Returns a reference to what the node opens in the web application: the project overview for the project root,
    /// the managed file preview for file and media nodes, the Prompt Gallery prompt for a prompt node bound to one,
    /// and the project structure page for ordinary nodes. Command 3 (Test) returns the project's Test Lab for any
    /// existing node. Commands 0 (Open) and 1 (Wizard) on a PromptFlow, PromptSession or PromptStep node without a
    /// prompt first create a draft Prompt Gallery prompt and bind the node to it; that is the only case that writes.
    /// The other commands only resolve the reference.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Although most commands only read, the operation needs the project
    /// lease: send <c>leaseToken</c> to work under your own lease. Without it the operation uses the project lease your
    /// identity already holds, or else takes a five-minute project lease for its own duration; if another identity
    /// holds the lease, it waits when that lease ends within 30 seconds and otherwise fails with HTTP 409
    /// <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a command name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node, as returned in <c>nodes[].id</c> by the structure read, for example the project
    /// root <c>project:{projectId}</c>.
    /// </param>
    /// <param name="request">The command and an optional lease token; <c>{}</c> sends command 0 (Open).</param>
    /// <response code="200">The reference to open: see <c>route</c> and <c>tabKind</c>.</response>
    /// <response code="400">
    /// <c>NodeCommandUnavailable</c>: the node does not exist in the project or has no page to open. Nothing was
    /// changed.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>, for example when a prompt node is bound to a prompt that is missing, belongs to another
    /// project or is not a full prompt. A prompt created by Open or Wizard may have been committed before the failure;
    /// run the command again to read it.
    /// </response>
    internal static Task<IResult> ExecuteNodeCommandAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureNodeCommandInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-command",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.ExecuteNodeCommandAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Link a node to a process definition, so that process starts for the node launch that definition.
    /// </summary>
    /// <remarks>
    /// Creates a Uses link from the node to the definition's node <c>process-definition:{processDefinitionId}</c>.
    /// <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start</c> then launches this
    /// definition when it is called without <c>processDefinitionId</c>. The definition's existence is not checked
    /// here. Canonical tasks are rejected; attach a process to a task with
    /// <c>POST /api/project-structure/projects/{projectId}/tasks/{taskId}/resource</c>. The result echoes the link and
    /// <c>changed</c> is true also when it already existed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node to link, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">Process definition identifier and an optional lease token.</param>
    /// <response code="200">The link exists; the body echoes it with kind 2 (Uses).</response>
    /// <response code="400">
    /// Nothing was changed: <c>ProcessDefinitionRequired</c> (empty identifier) or
    /// <c>InvalidStructureRelationship</c>.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c>.</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c> (the node is a canonical task), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The link may have been committed before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> LinkProcessDefinitionAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureProcessDefinitionLinkInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-link-process-definition",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.LinkProcessDefinitionAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Launch a process run for a project structure node.
    /// </summary>
    /// <remarks>
    /// Launches <c>processDefinitionId</c>, or else the definition the node represents or is linked to, for the node,
    /// and links the created run back to the node. The call returns when the launch is prepared, not when the process
    /// finishes. <c>stage</c> Running with a <c>runId</c> means a run exists; with <c>execute</c> true it is also
    /// queued for execution by the process runtime. <c>stage</c> Blocked means readiness findings stopped the launch
    /// and no run was created. Follow a run with <c>GET /api/processes/runs/{runId}</c>.
    ///
    /// Every call launches again; there is no idempotency key, so after an unclear failure look for the run before
    /// retrying. This operation does not take a project lease and ignores <c>leaseToken</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node to launch for, as returned in <c>nodes[].id</c> by the structure read, or a
    /// <c>process-definition:{processDefinitionId}</c> node.
    /// </param>
    /// <param name="request">Optional process definition, readiness and execution choices and a launch-plan
    /// flag.</param>
    /// <response code="200">
    /// The launch was prepared: its stage, launch plan identifier and, when a run was created, its <c>runId</c>.
    /// </response>
    /// <response code="400">
    /// <c>ProcessDefinitionRequired</c> (no definition given or linked) or <c>ProcessStartTargetRequired</c> (a
    /// definition node that no source node links to).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>ProjectStructureNodeNotFound</c>.</response>
    /// <response code="409"><c>ProjectStructureConcurrentMutation</c>: the project changed concurrently.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>, also for launch failures such as an unknown process definition. A run may have been
    /// created; check before retrying.
    /// </response>
    internal static Task<IResult> StartProcessNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureProcessNodeStartInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-start-process",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.StartProcessNodeAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Preview the workflow definitions and run input for a workflow node under a parent node.
    /// </summary>
    /// <remarks>
    /// Lists every workflow definition (active ones first) with whether it can be selected, resolves the preselected
    /// workflow and version, normalizes the input settings and previews the input that a workflow node under
    /// <c>nodeId</c> would pass to its runs. Nothing is created or changed; create the node with
    /// <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow-definition</c>. It uses POST because
    /// the settings travel in the body.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the parent node the workflow node would be created under, as returned in
    /// <c>nodes[].id</c>.
    /// </param>
    /// <param name="request">Optional preselection, input settings and selected nodes.</param>
    /// <response code="200">The workflow choices and the input preview.</response>
    /// <response code="400">
    /// Invalid input settings: <c>WorkflowInputProjectRequired</c>, <c>WorkflowInputParentRequired</c>,
    /// <c>WorkflowManualInputInvalid</c>, <c>WorkflowInputSourceInvalid</c> or <c>WorkflowInputSourceDuplicate</c>.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>ParentNodeNotFound</c> or <c>WorkflowSelectedNodeNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetWorkflowAddOptionsAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureWorkflowAddOptionsInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-workflow-add-options",
            projectId,
            nodeId,
            null,
            null,
            request,
            (_, cancellationToken) => agentService.GetWorkflowAddOptionsAsync(
                projectId,
                nodeId,
                request,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a workflow node that runs a workflow definition, under a parent node.
    /// </summary>
    /// <remarks>
    /// Creates a WorkflowDefinition node under <c>nodeId</c>, bound to the workflow definition and version, with the
    /// run input settings in its metadata. The definition must exist and be active. Canonical task parents are
    /// rejected; attach a workflow to a task with
    /// <c>POST /api/project-structure/projects/{projectId}/tasks/{taskId}/resource</c>. Preview the choices first with
    /// <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow-add-options</c> and start runs
    /// later with <c>POST /api/project-structure/projects/{projectId}/nodes/{workflowNodeId}/workflow/start</c>.
    /// Repeating a request creates another node.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the parent node, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">Workflow definition, optional version, titles, input settings and lease token.</param>
    /// <response code="200">The committed workflow node with the bound workflow and version.</response>
    /// <response code="400">
    /// Nothing was created: <c>WorkflowDefinitionRequired</c>, <c>WorkflowVersionInvalid</c>,
    /// <c>WorkflowDefinitionInactive</c>, or invalid input settings (<c>WorkflowInputProjectRequired</c>,
    /// <c>WorkflowInputParentRequired</c>, <c>WorkflowManualInputInvalid</c>, <c>WorkflowInputSourceInvalid</c>,
    /// <c>WorkflowInputSourceDuplicate</c>).
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>ParentNodeNotFound</c> or <c>WorkflowDefinitionNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c> (the parent is a canonical task), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The node may have been created before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> CreateWorkflowNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureWorkflowNodeCreateInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-create-workflow-definition",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.CreateWorkflowNodeAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Launch a run of a workflow node's workflow, once per launch intent.
    /// </summary>
    /// <remarks>
    /// Admits a launch of the workflow bound to the node: reserves a run for <c>intentId</c>, builds the run input from
    /// the node's settings and starts the run on the requested backend. HTTP 200 means the launch was admitted, not
    /// that the run finished: poll <c>GET
    /// /api/project-structure/projects/{projectId}/nodes/{nodeId}/workflow/status</c> or <c>GET
    /// /api/workflows/runs/{runId}</c>.
    ///
    /// Send a new <c>intentId</c> for each launch you intend and repeat the same one to retry: a retry returns the
    /// launch it created instead of starting another run, and an intent that belongs to another node, source or
    /// project lifetime is rejected with HTTP 409 <c>WorkflowAdmissionConflict</c>. Without <c>intentId</c> every call
    /// starts another run.
    ///
    /// When the launch was admitted but a later step, such as execution or delivery to the node, failed, the response
    /// is still HTTP 200 with the retained run, <c>status.delivery</c> Pending and a warning; repeat the request with
    /// the same <c>intentId</c> or poll the status to reconcile.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The launch runs under the caller's workflow authority (the token's
    /// subject until the token expires, or the local operator when authorization is disabled); HTTP 403
    /// <c>WorkflowAuthorityDenied</c> means that authority does not allow the launch. Send <c>leaseToken</c> to work
    /// under your own project lease. Without it the operation uses the project lease your identity already holds, or
    /// else takes a five-minute project lease for its own duration; if another identity holds the lease, it waits when
    /// that lease ends within 30 seconds and otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the workflow node (a WorkflowDefinition node), as returned in <c>nodes[].id</c>.
    /// </param>
    /// <param name="request">Launch intent, optional backend, simulated steps, requester and lease token.</param>
    /// <response code="200">
    /// The launch was admitted: the reserved <c>runId</c>, the intent and a status snapshot, possibly with warnings.
    /// </response>
    /// <response code="400">
    /// <c>WorkflowIntentRequired</c> (empty intent), <c>WorkflowNodeRequired</c> (not a workflow node),
    /// <c>WorkflowParentNodeMissing</c>, <c>WorkflowMetadataMissing</c>, <c>WorkflowDefinitionInactive</c> or
    /// <c>WorkflowDefinitionInvalid</c>; nothing was started.
    /// </response>
    /// <response code="403">
    /// <c>WorkflowAuthorityDenied</c>: the caller's workflow authority does not allow the launch.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>NodeNotFound</c> or <c>WorkflowDefinitionNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>WorkflowAdmissionConflict</c> (the intent belongs to another launch source), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. A launch may have been admitted; repeat the request with the same <c>intentId</c> to
    /// reconcile.
    /// </response>
    internal static Task<IResult> StartWorkflowNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureWorkflowNodeStartInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-start-workflow",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.StartWorkflowNodeAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read the status of the latest run of a workflow node.
    /// </summary>
    /// <remarks>
    /// Returns the status of the node's current launch (or, for nodes launched before launch admissions were recorded,
    /// its last run): run state, the status the node shows, step position, message, an execution summary with artifacts
    /// and created items, up to 12 recent events, and how far the result was delivered to the node. Before the first
    /// launch <c>runId</c> is null. Poll it until <c>state</c> is final (4 Completed, 5 Failed or 6 Cancelled);
    /// 2 WaitingForInput means the run waits for a response. Nothing is changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the workflow node (a WorkflowDefinition node), as returned in <c>nodes[].id</c>.
    /// </param>
    /// <response code="200">The status snapshot of the node's latest run.</response>
    /// <response code="400">
    /// <c>WorkflowNodeRequired</c> (not a workflow node), <c>WorkflowParentNodeMissing</c> or
    /// <c>WorkflowMetadataMissing</c>.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>NodeNotFound</c>, <c>WorkflowDefinitionNotFound</c> or <c>WorkflowRunNotFound</c>
    /// (the node's recorded run no longer exists).
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetWorkflowNodeStatusAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-workflow-status",
            projectId,
            nodeId,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.GetWorkflowNodeStatusAsync(projectId, nodeId, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Delete a node with all its descendants, or finish the pending cleanup of an earlier deletion of that node.
    /// </summary>
    /// <remarks>
    /// Without <c>durableMutationId</c>, removes the node and its whole subtree from the structure and then cleans up
    /// related records and managed files as <c>managedStorageDisposition</c> says. This requires the
    /// <c>expectedProjectAdmission</c> from the latest structure read. Files that cannot be deleted are kept and
    /// reported in <c>deletionWarnings</c>. Repeating a deletion that already completed returns its recorded result
    /// again, so a retry after an unclear failure is safe.
    ///
    /// When the structure change is committed but the cleanup cannot finish, the response is HTTP 409
    /// <c>ProjectStructureDeletionBatchPartialCommit</c>: the nodes are gone, and <c>details</c> lists the pending
    /// cleanups. Finish each one by calling this operation again with its <c>durableMutationId</c> and the same
    /// <c>managedStorageDisposition</c> (the admission is then not required), or list them later with
    /// <c>GET /api/project-structure/projects/{projectId}/deletion-cleanups</c>. Never start a new deletion of the
    /// same root instead.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the node to delete, as returned in <c>nodes[].id</c> by the structure read, or the
    /// <c>rootNodeId</c> of a pending cleanup.
    /// </param>
    /// <param name="request">Managed-file choice, project write admission and, for a cleanup retry, its
    /// identifier.</param>
    /// <response code="200">
    /// The deletion and its cleanup are complete: the number of removed nodes and any kept managed files.
    /// </response>
    /// <response code="400">
    /// Nothing was deleted: <c>ProjectStructureManagedStorageDispositionRequired</c> (missing or Unspecified
    /// disposition; <c>details.supportedValues</c> lists the accepted values).
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>SelectedNodesNotFound</c> (the node does not exist and has no deletion to resume;
    /// <c>details.requestedNodeIds</c>) or <c>ProjectStructureDeletionRecoveryNotFound</c> (no pending cleanup with
    /// this <c>durableMutationId</c> for this node).
    /// </response>
    /// <response code="409">
    /// <c>ProjectLifetimeRefreshRequired</c> (admission missing or for another project; nothing deleted);
    /// <c>ProjectStructureDeletionBatchPartialCommit</c> or <c>ProjectStructureDeletionPartialCommit</c> (the nodes are
    /// deleted but cleanup is pending; <c>details</c> carries the recovery information);
    /// <c>ProjectStructureDeletionDispositionMismatch</c> (a cleanup retry sent another disposition than the recorded
    /// one; <c>details.persistedDisposition</c> gives it); <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The deletion may have been committed; read the structure and the pending cleanups before
    /// retrying.
    /// </response>
    internal static Task<IResult> DeleteNodeAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureNodeDeleteInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.node-delete",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => {
                if (!request.DurableMutationId.HasValue && request.ManagedStorageDisposition is
                    ProjectStructureManagedStorageDisposition.RetainManagedFiles or
                    ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles) {
                    agent = agent with {
                        ExpectedProjectAdmission = RequireProjectAdmission(projectId, request.ExpectedProjectAdmission)
                    };
                }
                return agentService.DeleteNodeDetailedAsync(
                    projectId,
                    nodeId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// List completed node deletions of a project that kept managed files.
    /// </summary>
    /// <remarks>
    /// Returns, oldest first, every completed deletion whose cleanup kept at least one managed file, with the kept
    /// files and the reasons. Deletions without kept files are not listed. Reading the list acknowledges nothing and
    /// removes nothing, so the same notices are returned again. An unknown project returns an empty list. Pending
    /// cleanups are listed by <c>GET /api/project-structure/projects/{projectId}/deletion-cleanups</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">The notices, oldest first; an empty array when there are none.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> ListDeletionCompletionNoticesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.deletion-completion-notices",
            projectId,
            null,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.ListDeletionCompletionNoticesAsync(
                projectId,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// List node deletions of a project whose cleanup is still pending.
    /// </summary>
    /// <remarks>
    /// Returns, oldest first, every deletion whose structure change is committed but whose cleanup of related records
    /// and managed files has not completed, with its <c>durableMutationId</c>, status, whether a retry can start now
    /// and the recorded managed-file choice. Finish each one with <c>POST
    /// /api/project-structure/projects/{projectId}/nodes/{rootNodeId}/delete</c>, sending the same
    /// <c>durableMutationId</c> and <c>managedStorageDisposition</c>. An unknown project returns an empty list.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">The pending cleanups, oldest first; an empty array when there are none.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> ListDeletionCleanupsAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.deletion-cleanups",
            projectId,
            null,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.ListPendingDeletionRecoveriesAsync(
                projectId,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Delete several nodes of a project, each with all its descendants.
    /// </summary>
    /// <remarks>
    /// Removes each listed node with its whole subtree (nodes inside another listed subtree go with it) and cleans up
    /// related records and managed files as <c>managedStorageDisposition</c> says. Requires the
    /// <c>expectedProjectAdmission</c> from the latest structure read. Listed nodes that are already deleted have their
    /// pending cleanup resumed. Files that cannot be deleted are kept and reported in <c>deletionWarnings</c>.
    ///
    /// When some branch cannot be completed, the response is HTTP 409 <c>ProjectStructureDeletionBatchPartialCommit</c>
    /// and <c>details</c> reports the completed node count, pending cleanups (<c>recoveries</c>) and failed branches
    /// (<c>branchFailures</c>, some with a suggested disposition to retry with). Finish pending cleanups with
    /// <c>POST /api/project-structure/projects/{projectId}/nodes/{nodeId}/delete</c> and their
    /// <c>durableMutationId</c>; read the structure before retrying anything else.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Nodes to delete, managed-file choice and project write admission.</param>
    /// <response code="200">
    /// Every deletion and cleanup is complete: the number of removed nodes and any kept managed files.
    /// </response>
    /// <response code="400">
    /// Nothing was deleted: <c>SelectedNodesRequired</c> (no identifier) or
    /// <c>ProjectStructureManagedStorageDispositionRequired</c> (missing or Unspecified disposition).
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c> or <c>SelectedNodesNotFound</c> (none of the nodes exists or has a deletion to resume;
    /// <c>details.requestedNodeIds</c>).
    /// </response>
    /// <response code="409">
    /// <c>ProjectLifetimeRefreshRequired</c> (admission missing or for another project; nothing deleted);
    /// <c>ProjectStructureDeletionBatchPartialCommit</c> (some nodes are deleted and follow-up is needed; see
    /// <c>details</c>); <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. Some nodes may have been deleted; read the structure and the pending cleanups before
    /// retrying.
    /// </response>
    internal static Task<IResult> DeleteNodesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureNodeDeleteBatchInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.nodes-delete",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => {
                if (request.ManagedStorageDisposition is
                    ProjectStructureManagedStorageDisposition.RetainManagedFiles or
                    ProjectStructureManagedStorageDisposition.DeleteOwnedManagedFiles) {
                    agent = agent with {
                        ExpectedProjectAdmission = RequireProjectAdmission(projectId, request.ExpectedProjectAdmission)
                    };
                }
                return agentService.DeleteNodesDetailedAsync(
                    projectId,
                    request,
                    agent,
                    cancellationToken);
            },
            cancellationToken);

    /// <summary>
    /// Record an approval request as a Decision node in a project structure.
    /// </summary>
    /// <remarks>
    /// Creates a Decision node with subtype <c>approval-request</c> under <c>parentNodeKey</c> (the project root when
    /// omitted) and returns it. Unless <c>metadataJson</c> is sent, its metadata records the requested operation, the
    /// estimated minutes and the requesting identity and time under <c>approvalRequest</c>. The node only records the
    /// request: nothing is blocked, paused or started by it. Repeating a request creates another node.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Title, requested operation and optional parent, details, estimate and metadata.</param>
    /// <response code="200">The committed Decision node, with every optional member filled.</response>
    /// <response code="400">
    /// Nothing was created: <c>ApprovalTitleRequired</c> (blank title) or <c>ApprovalOperationRequired</c> (blank
    /// requested operation).
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: the parent is a block whose folder lies outside the managed
    /// workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>ParentNodeNotFound</c>.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was created.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>, also returned for <c>metadataJson</c> that is not valid JSON. The node may have been
    /// created before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> CreateApprovalRequestAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureApprovalRequestCreateInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "approvals.request",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.CreateApprovalRequestAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// List the unfinished work of a project, most urgent first.
    /// </summary>
    /// <remarks>
    /// Returns the project's unfinished nodes, except project nodes and, unless <c>includePaused</c> is true, paused
    /// or stopped nodes. Items are ordered by effective priority (nodes without a priority last) and then by title, and
    /// each lists its unfinished prerequisites: ancestors below the project root, DependsOn targets and Blocks sources.
    /// A node counts as finished when its progress mode is <c>complete</c> or its progress is 100, or when its status
    /// contains done, complete, approved, ready, final or archived. Nothing is changed.
    ///
    /// Use the dependency query for the full analysis including finished nodes and dependents, and the structure read
    /// for all node members.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">The checklist; <c>items</c> is empty when no open work matches.</response>
    /// <response code="400">
    /// <c>ProjectStructureRequestInvalid</c> (malformed JSON or a member of the wrong JSON type) or
    /// <c>ProjectStructureObjectTypeInvalid</c> (an unknown value in <c>objectTypes</c>).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists in the active database.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c>: the body exceeds 262,144 bytes; <c>details.maximumBytes</c> gives
    /// the limit.
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">The query failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> QueryChecklistAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureChecklistRequest, ProjectStructureChecklistResponse>(
            httpContext,
            analyticsService,
            "checklists.query",
            projectId,
            null,
            null,
            null,
            ProjectStructureHttpBodyContracts.ChecklistQuery,
            (request, _, cancellationToken) => agentService.GetChecklistAsync(projectId, request, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Analyze the prerequisites, dependents and durations of project nodes.
    /// </summary>
    /// <remarks>
    /// For each selected node (every node when <c>nodeIds</c> is omitted) returns its prerequisites (ancestors below
    /// the project root, the targets of its DependsOn links and the sources of Blocks links to it), its dependents,
    /// whether it can start now (<c>canExecute</c>) and its known and effective duration. Items are ordered by title.
    /// Nothing is changed. Create and remove dependencies with
    /// <c>POST /api/project-structure/projects/{projectId}/dependencies/link</c> and
    /// <c>POST /api/project-structure/projects/{projectId}/dependencies/unlink</c>.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Nodes to analyze, whether to include finished nodes, the default duration and a
    /// limit.</param>
    /// <response code="200">The analysis; <c>items</c> is empty when no node matched.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists in the active database.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The query failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> QueryDependenciesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureDependencyQueryRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "dependencies.query",
            projectId,
            null,
            null,
            null,
            request,
            (_, cancellationToken) => agentService.GetDependenciesAsync(projectId, request, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a directed link of a chosen kind between two nodes of a project.
    /// </summary>
    /// <remarks>
    /// Records that the source node depends on, uses, validates, tests, blocks or derives from the target node. The
    /// operation succeeds with <c>changed</c> true also when the same link already exists. Hierarchy kinds (Contains,
    /// BelongsTo) are rejected: change the parent instead. A Uses link from a canonical task is rejected: attach task
    /// resources with <c>POST /api/project-structure/projects/{projectId}/tasks/{taskId}/resource</c>. Dependencies can
    /// also be created with <c>POST /api/project-structure/projects/{projectId}/dependencies/link</c>; cycles of
    /// DependsOn links are not detected.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Source and target nodes, link kind and an optional lease token.</param>
    /// <response code="200">The link exists; the body echoes it.</response>
    /// <response code="400">
    /// Nothing was changed: <c>SourceNodeRequired</c>, <c>TargetNodeRequired</c>, <c>InvalidStructureRelationship</c>
    /// (a node linked to itself) or <c>HierarchyLinkNotAllowed</c> (Contains or BelongsTo).
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: a hierarchy link was requested under a block whose folder lies
    /// outside the managed workspace.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c> (source or target).</response>
    /// <response code="409">
    /// <c>CanonicalTaskMutationRequiresTypedPath</c> (a Uses link from a canonical task), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The link may have been committed before the failure; read the structure with
    /// <c>includeLinks</c> before retrying.
    /// </response>
    internal static Task<IResult> LinkNodesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureLinkInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.link-create",
            projectId,
            request.SourceNodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.LinkNodesAsync(projectId, request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Remove a directed link of a given kind between two nodes of a project.
    /// </summary>
    /// <remarks>
    /// Removes the link with this source, target and kind. When no such link exists the operation succeeds with
    /// <c>changed</c> false, so repeating it is safe. Hierarchy links are changed through the parent, not here.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Source and target nodes, link kind and an optional lease token.</param>
    /// <response code="200">
    /// The link no longer exists: <c>changed</c> is true when it was removed and false when there was none.
    /// </response>
    /// <response code="400"><c>SourceNodeRequired</c> or <c>TargetNodeRequired</c>; nothing was changed.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The link may have been removed before the failure; read the structure before retrying.
    /// </response>
    internal static Task<IResult> UnlinkNodesAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureLinkInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "structure.link-delete",
            projectId,
            request.SourceNodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UnlinkNodesAsync(projectId, request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Record that one node of a project depends on another.
    /// </summary>
    /// <remarks>
    /// Creates a DependsOn link from <c>sourceNodeId</c> (the dependent node) to <c>targetNodeId</c> (its
    /// prerequisite); <c>kind</c> in the body is ignored. The dependency query and the checklist then list the target
    /// as a prerequisite of the source. The operation succeeds with <c>changed</c> true also when the dependency
    /// already exists; cycles of dependencies are not detected.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Dependent (source) and prerequisite (target) nodes and an optional lease token.</param>
    /// <response code="200">The dependency exists; the body echoes it with kind 1 (DependsOn).</response>
    /// <response code="400">
    /// Nothing was changed: <c>SourceNodeRequired</c>, <c>TargetNodeRequired</c> or
    /// <c>InvalidStructureRelationship</c> (a node depending on itself).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c> (source or target).</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The dependency may have been committed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> LinkDependencyAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureLinkInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "dependencies.link",
            projectId,
            request.SourceNodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.LinkNodesAsync(
                projectId,
                request with { Kind = ProjectObjectLinkKind.DependsOn },
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Remove a dependency between two nodes of a project.
    /// </summary>
    /// <remarks>
    /// Removes the DependsOn link from <c>sourceNodeId</c> to <c>targetNodeId</c>; <c>kind</c> in the body is ignored.
    /// When no such dependency exists the operation succeeds with <c>changed</c> false, so repeating it is safe.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="request">Dependent (source) and prerequisite (target) nodes and an optional lease token.</param>
    /// <response code="200">
    /// The dependency no longer exists: <c>changed</c> is true when it was removed and false when there was none.
    /// </response>
    /// <response code="400"><c>SourceNodeRequired</c> or <c>TargetNodeRequired</c>; nothing was changed.</response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with this id exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>; nothing was changed.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The dependency may have been removed before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> UnlinkDependencyAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureLinkInput request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "dependencies.unlink",
            projectId,
            request.SourceNodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.UnlinkNodesAsync(
                projectId,
                request with { Kind = ProjectObjectLinkKind.DependsOn },
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read the metadata of an asset node (File, ImageAsset or VideoAsset) without its content.
    /// </summary>
    /// <remarks>
    /// Returns the asset's type, titles, stored-file location and content type, metadata and, for a revision, the asset
    /// it revises. The metadata does not prove that the content is still readable: read the bytes with
    /// <c>GET /api/project-structure/projects/{projectId}/assets/{nodeId}/content</c>. Nothing is changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the asset node, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <response code="200">The asset metadata.</response>
    /// <response code="400">
    /// <c>AssetRequired</c>: the node is not a File, ImageAsset or VideoAsset with a stored file.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c> or <c>NodeNotFound</c>.</response>
    /// <response code="409">
    /// <c>AssetRevisionLineageInvalid</c> (the asset has more than one revision-parent link) or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetAssetAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "assets.get",
            projectId,
            nodeId,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.GetAssetAsync(projectId, nodeId, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read the complete content of an asset node as base64 text, with its metadata.
    /// </summary>
    /// <remarks>
    /// Reads the asset's file from the storage it is bound to and returns it base64-encoded inside a JSON object, not
    /// as a file download, together with the asset metadata and the content length in bytes. Content over 25 MiB is not
    /// returned. Nothing is changed.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// HTTP 503 means the storage is temporarily unavailable: retry later. HTTP 404 <c>AssetContentNotFound</c> means
    /// the metadata exists but the stored content is gone.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the asset node, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <response code="200">The asset metadata and its complete content.</response>
    /// <response code="400">
    /// <c>AssetRequired</c> (not an asset with a stored file) or <c>AssetStorageReferenceInvalid</c> (the stored
    /// reference cannot be read).
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>, <c>NodeNotFound</c> or <c>AssetContentNotFound</c>.</response>
    /// <response code="409">
    /// <c>AssetStorageCatalogMissing</c> (the storage catalog the asset is bound to no longer exists),
    /// <c>AssetRevisionLineageInvalid</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413"><c>AssetContentTooLarge</c>: the content exceeds 26,214,400 bytes (25 MiB).</response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    /// <response code="503">
    /// <c>AssetStorageBootstrapUnavailable</c>, <c>AssetStorageReadUnavailable</c>,
    /// <c>AssetStorageDriverUnavailable</c> or <c>AssetContentUnavailable</c>: the storage cannot serve the content
    /// now.
    /// </response>
    internal static Task<IResult> GetAssetContentAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "assets.get-content",
            projectId,
            nodeId,
            null,
            null,
            null,
            (_, cancellationToken) => agentService.GetAssetContentAsync(projectId, nodeId, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a File, ImageAsset or VideoAsset node and store its content as a managed file.
    /// </summary>
    /// <remarks>
    /// Creates the asset node under <c>parentNodeKey</c> (required) and stores its content, taken from the first
    /// source present: inline <c>media</c>, a file in the server's managed workspace (<c>sourceWorkspacePath</c>) or a
    /// download from a public http or https address (<c>sourceUrl</c>). This is the only way to create File nodes and
    /// <c>mermaid</c> subtypes. Change an existing asset's content by creating a revision with
    /// <c>POST /api/project-structure/projects/{projectId}/assets/{nodeId}/revisions</c>. Repeating a request creates
    /// another asset.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// Content is limited to 25 MiB. With inline <c>media</c> the whole JSON body must also stay under the host's
    /// default request limit of 30,000,000 bytes, which base64 encoding reaches with files of about 21 MiB; a larger
    /// body is rejected with HTTP 400 <c>ProjectStructureRequestInvalid</c>. Use <c>sourceWorkspacePath</c> or
    /// <c>sourceUrl</c> for larger files.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <response code="200">The committed asset node, with its stored-file members filled.</response>
    /// <response code="400">
    /// Nothing was created: <c>ProjectStructureRequestInvalid</c>, <c>ProjectStructureObjectTypeInvalid</c>,
    /// <c>AssetTypeRequired</c> (not File, ImageAsset or VideoAsset), <c>AssetParentRequired</c>,
    /// <c>MediaSourceRequired</c> (no source), an invalid <c>media</c> payload (<c>FileNameRequired</c>,
    /// <c>MediaPayloadRequired</c>, <c>InvalidBase64Payload</c>, <c>InvalidSvgRoot</c>, <c>InvalidSvgXml</c>), an
    /// invalid workspace source (<c>SourceWorkspacePathInvalid</c>) or URL source (<c>SourceUrlEmpty</c>,
    /// <c>SourceUrlInvalid</c>, <c>SourceUrlNotAllowed</c>).
    /// </response>
    /// <response code="403">
    /// <c>SourceWorkspaceScopeDenied</c> (the workspace file belongs to another project's managed folder) or
    /// <c>ProjectBlockRootOutsideExecutionScope</c>.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>ParentNodeNotFound</c> or <c>SourceWorkspaceFileNotFound</c>.
    /// </response>
    /// <response code="409">
    /// <c>SourceWorkspaceFileUnavailable</c> (the workspace file cannot be read now), <c>LeaseConflict</c>,
    /// <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413">
    /// <c>ProjectStructureRequestBodyTooLarge</c> (declared body length over 36,001,112 bytes),
    /// <c>MediaPayloadTooLarge</c>, <c>SourceWorkspaceFileTooLarge</c> or <c>SourceUrlTooLarge</c> (content over
    /// 25 MiB).
    /// </response>
    /// <response code="415"><c>ProjectStructureContentTypeUnsupported</c>: the Content-Type is not JSON.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The asset may have been created before the failure; read the structure before retrying.
    /// </response>
    /// <response code="502"><c>SourceUrlDownloadFailed</c>: the source URL could not be downloaded.</response>
    /// <response code="504"><c>SourceUrlTimeout</c>: the download did not finish within 60 seconds.</response>
    internal static Task<IResult> CreateAssetAsync(
        Guid projectId,
        HttpContext httpContext,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteJsonRequestAsync<ProjectStructureAssetCreateInput, ProjectStructureNodeSummary>(
            httpContext,
            analyticsService,
            "assets.create",
            projectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            ProjectStructureHttpBodyContracts.AssetCreate,
            (request, agent, cancellationToken) => agentService.CreateAssetAsync(
                projectId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Create a revision of an asset: a new asset node with new content, linked to the original.
    /// </summary>
    /// <remarks>
    /// Stores the new content as a new asset node of the same type under the original asset and links it to the
    /// original with DerivedFrom; the result's <c>revisionParentNodeId</c> names the original. The original node and
    /// its content stay unchanged. Omitted subtype and metadata are copied from the original. Always send <c>media</c>:
    /// a null value creates a revision node without content and then fails with HTTP 400 <c>AssetRequired</c>, leaving
    /// that node in the structure. Repeating a request creates another revision.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Send <c>leaseToken</c> to work under your own project lease. Without
    /// it the operation uses the project lease your identity already holds, or else takes a five-minute project lease
    /// for its own duration; if another identity holds the lease, it waits when that lease ends within 30 seconds and
    /// otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="projectId">
    /// Identifier of the project, as returned in <c>id</c> by <c>GET /api/project-structure/projects</c> or by project
    /// creation.
    /// </param>
    /// <param name="nodeId">
    /// String identifier of the asset to revise, as returned in <c>nodes[].id</c> by the structure read.
    /// </param>
    /// <param name="request">Titles, new content and optional subtype, metadata and lease token of the
    /// revision.</param>
    /// <response code="200">Metadata of the committed revision node.</response>
    /// <response code="400">
    /// An invalid <c>media</c> payload (<c>FileNameRequired</c>, <c>MediaPayloadRequired</c>,
    /// <c>InvalidBase64Payload</c>, <c>InvalidSvgRoot</c>, <c>InvalidSvgXml</c>; nothing was created) or
    /// <c>AssetRequired</c> (the node is not an asset, or <c>media</c> was null and a content-less revision node was
    /// created).
    /// </response>
    /// <response code="403">
    /// <c>ProjectBlockRootOutsideExecutionScope</c>: the asset is under a block whose folder lies outside the managed
    /// workspace.
    /// </response>
    /// <response code="404">
    /// <c>ProjectNotFound</c>, <c>ParentNodeNotFound</c> or <c>NodeNotFound</c> (the asset does not exist).
    /// </response>
    /// <response code="409">
    /// <c>AssetRevisionLineageInvalid</c>, <c>LeaseConflict</c>, <c>LeaseMissing</c> or
    /// <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="413"><c>MediaPayloadTooLarge</c>: the content exceeds 25 MiB.</response>
    /// <response code="500">
    /// <c>UnhandledError</c>. The revision may have been created before the failure; read the structure before
    /// retrying.
    /// </response>
    internal static Task<IResult> CreateAssetRevisionAsync(
        Guid projectId,
        string nodeId,
        HttpContext httpContext,
        ProjectStructureAssetRevisionRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "assets.create-revision",
            projectId,
            nodeId,
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.CreateAssetRevisionAsync(
                projectId,
                nodeId,
                request,
                agent,
                cancellationToken),
            cancellationToken);

    /// <summary>
    /// Import an outline (Mermaid, Word, XMind or JSON) as new nodes of a project structure.
    /// </summary>
    /// <remarks>
    /// Creates a container ProjectBlock titled <c>title</c> under <c>parentNodeKey</c> (by default the project root),
    /// stores <c>sourceAsset</c> in it as a File node when one is sent, and creates one node per outline item below
    /// the container: items with children become ProjectBlock nodes (subtype <c>containerBlockSubtype</c>) and leaves
    /// become WorkItem nodes (subtype <c>leafWorkItemSubtype</c>, by default <c>task</c>, which makes them canonical
    /// tasks). Mermaid flowchart edges become DependsOn links. The new nodes are then laid out automatically. The
    /// import is not idempotent: repeating it creates another container.
    ///
    /// The import is not atomic. Each node is saved separately, and the source is read only after the container and
    /// the source file node were created. A failure after that point, including most HTTP 400 errors below, leaves the
    /// nodes created so far in the structure: read the structure and delete the partial container before retrying.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. The lease is on the body's <c>projectId</c>: send <c>leaseToken</c> to
    /// work under your own project lease. Without it the operation uses the project lease your identity already holds,
    /// or else takes a five-minute project lease for its own duration; if another identity holds the lease, it waits
    /// when that lease ends within 30 seconds and otherwise fails with HTTP 409 <c>LeaseConflict</c>.
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a source kind name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">
    /// The target project and parent, the container title, the source format and the source as text or as a file.
    /// </param>
    /// <response code="200">
    /// The committed import: the container, the source file node and every created node, with notes on how the source
    /// was read.
    /// </response>
    /// <response code="400">
    /// <c>ImportTitleRequired</c> (nothing was created). The following are returned after the container was created:
    /// <c>UnsupportedImportSource</c>, <c>SourceAssetRequired</c> (DOCX and XMind need <c>sourceAsset</c>; Mermaid and
    /// JSON need <c>sourceText</c> or <c>sourceAsset</c>), <c>SourcePayloadRequired</c>, <c>InvalidBase64Payload</c>,
    /// <c>UnsupportedMermaidDiagram</c> (neither a mindmap nor a flowchart), <c>EmptyMermaidMindmap</c>,
    /// <c>InvalidDocxSource</c>, <c>EmptyDocxSource</c>, <c>EmptyXmindSource</c>, <c>InvalidXmindJson</c>,
    /// <c>XmindTopicTitleRequired</c>, <c>InvalidJsonOutline</c>, <c>InvalidJsonOutlineNode</c> or
    /// <c>JsonOutlineTitleRequired</c>.
    /// </response>
    /// <response code="404"><c>ProjectNotFound</c>: no project with the body's <c>projectId</c> exists.</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>, <c>LeaseMissing</c> or <c>ProjectStructureConcurrentMutation</c>.
    /// </response>
    /// <response code="500">
    /// <c>UnhandledError</c>, for example for an unknown <c>parentNodeKey</c> (then nothing was created), text that is
    /// not valid JSON in a JSON outline, or an unreadable DOCX or XMind file. Nodes created before the failure stay;
    /// read the structure before retrying.
    /// </response>
    internal static Task<IResult> ImportAsync(
        HttpContext httpContext,
        ProjectStructureImportRequest request,
        ProjectStructureAgentService agentService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "imports.run",
            request.ProjectId,
            null,
            ProjectStructureLeaseScopeKind.Project,
            request.ProjectId.ToString(),
            request,
            (agent, cancellationToken) => agentService.ImportAsync(request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Search the built-in project-management guidance.
    /// </summary>
    /// <remarks>
    /// Returns entries from a fixed set of guidance that ships with the application (mission, planning, estimation,
    /// approval, reporting, risk and collaboration advice), filtered by category and text. The result depends only on
    /// the request and the application version; no project data is read and no lease is involved.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// A body the framework cannot bind (malformed JSON, or a member of the wrong JSON type such as a category name
    /// instead of its integer) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">
    /// Optional categories, search text and maximum number of entries; <c>{}</c> returns up to 10 entries of every
    /// category.
    /// </param>
    /// <response code="200">The matching entries: mission anchors first, then by category and title.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> QueryKnowledgeAsync(
        HttpContext httpContext,
        ProjectManagementGuidanceQueryRequest request,
        ProjectManagementKnowledgeService knowledgeService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "knowledge.query",
            null,
            null,
            null,
            null,
            request,
            async (_, cancellationToken) =>
            {
                var entries = await knowledgeService.QueryAsync(
                    new Modules.Workspace.ProjectManagementKnowledgeQuery(
                        request.Categories?.Select(MapGuidanceCategory).ToList(),
                        request.Query,
                        request.Take),
                    cancellationToken);

                return new ProjectManagementGuidanceResponse(entries
                    .Select(entry => new ProjectManagementGuidanceEntry(
                        entry.Id,
                        MapGuidanceCategory(entry.Category),
                        entry.Title,
                        entry.Summary,
                        entry.Guidance,
                        entry.Tags,
                        entry.IsMissionAnchor))
                    .ToList());
            },
            cancellationToken);

    /// <summary>
    /// Acquire or extend a lease on a project, a project node or a repository branch for the calling identity.
    /// </summary>
    /// <remarks>
    /// A lease tells other writers that a scope is being changed. For a Project lease, send the returned
    /// <c>leaseToken</c> as <c>leaseToken</c> with that project's mutations to make several changes under one lease.
    /// Mutations sent without a token take a five-minute project lease for their own duration and release it
    /// afterwards; they fail with HTTP 409 <c>LeaseConflict</c> while another identity holds the project lease.
    /// ProjectNode and RepoBranch leases only coordinate callers that look for them.
    ///
    /// When the calling identity already holds the active lease on the scope, the lease is extended from now, its
    /// reason is replaced and the same token is returned; otherwise a new lease with a new token is created. Released
    /// or expired leases are never revived. The holder is the calling identity (the bearer token's subject, or the
    /// shared identity <c>local-api-operator</c> when API authorization is disabled), so every caller with that
    /// identity can use the lease.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. A lease is neither an authorization nor a project write admission.
    ///
    /// Renew the lease with <c>POST /api/project-structure/leases/renew</c> before <c>expiresAtUtc</c> and release it
    /// with <c>POST /api/project-structure/leases/release</c> when done. A body the framework cannot bind (malformed
    /// JSON or an unknown <c>scopeKind</c>) is rejected with HTTP 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">Scope to lease, the reason shown to other callers and the requested duration.</param>
    /// <response code="200">The active lease held by the caller, with its token and expiry.</response>
    /// <response code="400">
    /// The scope is invalid: <c>ScopeKeyRequired</c> (blank key) or <c>InvalidProjectScope</c> (a Project key that is
    /// not a GUID). Nothing was written.
    /// </response>
    /// <response code="404">
    /// The scope target does not exist: <c>ProjectNotFound</c> (Project lease) or <c>ProjectNodeNotFound</c>
    /// (ProjectNode lease). Nothing was written.
    /// </response>
    /// <response code="409">
    /// <c>LeaseConflict</c>: another identity holds an active lease on the scope; read it with
    /// <c>GET /api/project-structure/leases/current</c> and retry after its <c>expiresAtUtc</c>.
    /// <c>AmbiguousProjectNodeScope</c>: the node key exists in more than one project.
    /// <c>ProjectStructureConcurrentMutation</c>: the project changed concurrently; retry.
    /// </response>
    /// <response code="500">
    /// The operation failed unexpectedly (<c>UnhandledError</c>). Read the current lease before retrying.
    /// </response>
    internal static Task<IResult> AcquireLeaseAsync(
        HttpContext httpContext,
        ProjectStructureLeaseAcquireRequest request,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "leases.acquire",
            null,
            null,
            request.ScopeKind,
            request.ScopeKey,
            request,
            (agent, cancellationToken) => leaseService.AcquireAsync(request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Extend an active lease that the calling identity holds.
    /// </summary>
    /// <remarks>
    /// Moves the lease expiry to now plus <c>durationMinutes</c> (1 through 120, default 15); the token, holder and
    /// reason stay unchanged. Only an unreleased lease that has not expired can be renewed; after expiry, acquire a new
    /// lease with <c>POST /api/project-structure/leases/acquire</c>, which issues a new token.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Only the identity that holds the lease can renew it, even with the
    /// correct token.
    ///
    /// A body the framework cannot bind (malformed JSON or an unknown <c>scopeKind</c>) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">Scope and token of the lease, and its new duration.</param>
    /// <response code="200">The renewed lease with its new <c>expiresAtUtc</c>.</response>
    /// <response code="400">
    /// <c>LeaseTokenRequired</c> (blank token), <c>ScopeKeyRequired</c> (blank key) or <c>InvalidProjectScope</c> (a
    /// Project key that is not a GUID).
    /// </response>
    /// <response code="404">
    /// <c>LeaseNotFound</c>: no unreleased, unexpired lease with this token exists on the scope. <c>ProjectNotFound</c>
    /// or <c>ProjectNodeNotFound</c>: the scope target does not exist.
    /// </response>
    /// <response code="409">
    /// <c>LeaseConflict</c>: the lease belongs to another identity. <c>AmbiguousProjectNodeScope</c>: the node key
    /// exists in more than one project. <c>ProjectStructureConcurrentMutation</c>: the project changed concurrently;
    /// retry.
    /// </response>
    /// <response code="500">
    /// The operation failed unexpectedly (<c>UnhandledError</c>). Read the current lease before retrying.
    /// </response>
    internal static Task<IResult> RenewLeaseAsync(
        HttpContext httpContext,
        ProjectStructureLeaseRenewRequest request,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "leases.renew",
            null,
            null,
            request.ScopeKind,
            request.ScopeKey,
            request,
            (agent, cancellationToken) => leaseService.RenewAsync(request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Release a lease that the calling identity holds.
    /// </summary>
    /// <remarks>
    /// Ends the lease at once: its expiry is set to now and it becomes inactive, so other identities can acquire the
    /// scope. A lease that expired without being released can still be released. When no unreleased lease with this
    /// token exists on the scope (already released, unknown token or another scope key), the operation changes nothing
    /// and returns HTTP 200 with an empty body, so repeating a release is safe.
    ///
    /// Release only leases you acquired with <c>POST /api/project-structure/leases/acquire</c>; the five-minute leases
    /// that mutations take for themselves are released by those mutations.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope. Only the identity that holds the lease can release it.
    ///
    /// A body the framework cannot bind (malformed JSON or an unknown <c>scopeKind</c>) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">Scope and token of the lease to release.</param>
    /// <response code="200">
    /// The released lease with <c>isActive</c> false, or an empty body (not JSON) when no unreleased lease with this
    /// token exists on the scope.
    /// </response>
    /// <response code="400"><c>LeaseTokenRequired</c> (blank token) or <c>ScopeKeyRequired</c> (blank key).</response>
    /// <response code="409">
    /// <c>LeaseConflict</c>: the lease belongs to another identity and stays active.
    /// <c>ProjectStructureConcurrentMutation</c>: the lease store changed concurrently; retry.
    /// </response>
    /// <response code="500">
    /// The operation failed unexpectedly (<c>UnhandledError</c>). Read the current lease to see whether it was
    /// released.
    /// </response>
    internal static Task<IResult> ReleaseLeaseAsync(
        HttpContext httpContext,
        ProjectStructureLeaseReleaseRequest request,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "leases.release",
            null,
            null,
            request.ScopeKind,
            request.ScopeKey,
            request,
            (agent, cancellationToken) => leaseService.ReleaseAsync(request, agent, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read the active lease on a project, a project node or a repository branch.
    /// </summary>
    /// <remarks>
    /// Returns the active lease on the scope, whoever holds it, including its token, holder and expiry. Use it to see
    /// who blocks a mutation that failed with HTTP 409 <c>LeaseConflict</c> and when that lease ends. When no lease is
    /// active, the response is HTTP 200 with an empty body. Knowing another holder's token does not let you renew,
    /// release or use that lease, because those checks also require the holder's identity.
    ///
    /// Both query parameters are required. A missing parameter, or a <c>scopeKind</c> name in other casing than
    /// <c>Project</c>, <c>ProjectNode</c> or <c>RepoBranch</c>, is rejected by the framework with HTTP 400 without an
    /// envelope.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    /// </remarks>
    /// <param name="scopeKind">
    /// Kind of the scope: the exact name <c>Project</c>, <c>ProjectNode</c> or <c>RepoBranch</c>, or its integer
    /// 0, 1 or 2.
    /// </param>
    /// <param name="scopeKey">
    /// Key of the scope as used to acquire the lease, for example a hyphenated project id. It is trimmed and
    /// lower-cased before the lookup (RepoBranch keys also get forward slashes instead of backslashes).
    /// </param>
    /// <response code="200">
    /// The active lease, or an empty body (not JSON) when no lease is active on the scope.
    /// </response>
    /// <response code="400">
    /// <c>ScopeKeyRequired</c> (blank key) or <c>InvalidScopeKind</c> (an integer that is not a defined kind).
    /// </response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: the lease store changed while it was read; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> GetCurrentLeaseAsync(
        HttpContext httpContext,
        ProjectStructureLeaseScopeKind scopeKind,
        string scopeKey,
        ProjectStructureLeaseService leaseService,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "leases.current",
            null,
            null,
            scopeKind,
            scopeKey,
            new { scopeKind, scopeKey },
            (_, cancellationToken) => leaseService.GetActiveLeaseAsync(scopeKind, scopeKey, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Read the log of recent Project Structure operation calls, newest first.
    /// </summary>
    /// <remarks>
    /// Every call of a Project Structure operation, successful or failed, is recorded when it finishes: reads
    /// included, calls made by in-process agent tools included, and calls of this query itself included. Filter by
    /// project, operation name, caller identity and outcome; the filters combine. At most 200 entries are returned
    /// and there is no paging.
    ///
    /// Every caller with access sees the entries of all callers, but the recorded request and response bodies,
    /// warnings, internal error message and repository root are returned only for the caller's own calls (the same
    /// bearer token subject, or <c>local-api-operator</c> when API authorization is disabled). Entries of other callers,
    /// including in-process agent tool calls, return <c>{}</c> bodies, an empty warning list, a null error message and an
    /// empty repository root. The caller's own bodies can contain notes, metadata, inline file content, lease tokens and
    /// internal failure messages; treat the response as sensitive.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the <c>api</c> or
    /// <c>api.project-structure.write</c> scope (reads require the same scope).
    ///
    /// A body the framework cannot bind (malformed JSON or a member of the wrong JSON type) is rejected with HTTP
    /// 400 before the operation runs and has no envelope.
    /// </remarks>
    /// <param name="request">
    /// Optional filters and the maximum number of entries; <c>{}</c> returns the 50 newest entries.
    /// </param>
    /// <response code="200">The matching entries, newest first; empty when none matched.</response>
    /// <response code="409">
    /// <c>ProjectStructureConcurrentMutation</c>: a concurrent change interfered; read again.
    /// </response>
    /// <response code="500">The read failed unexpectedly (<c>UnhandledError</c>).</response>
    internal static Task<IResult> QueryAnalyticsAsync(
        HttpContext httpContext,
        ProjectStructureAnalyticsQueryRequest request,
        ProjectStructureAnalyticsService analyticsService,
        CancellationToken cancellationToken)
        => ExecuteAsync(
            httpContext,
            analyticsService,
            "analytics.query",
            request.ProjectId,
            null,
            null,
            null,
            request,
            async (caller, cancellationToken) => RedactOtherCallersAnalytics(
                await analyticsService.QueryAsync(request, cancellationToken),
                caller.AgentId),
            cancellationToken);

    // Entries of other callers, including in-process agent tool calls, keep the facts of the call but not its recorded
    // request and response bodies, warnings, internal error message or server directory: those can hold notes, inline
    // file content, lease tokens and internal failures of someone else's call.
    private static ProjectStructureAnalyticsResponse RedactOtherCallersAnalytics(
        ProjectStructureAnalyticsResponse response,
        string callerId)
        => new(response.Entries
            .Select(entry => string.Equals(entry.AgentId, callerId.Trim(), StringComparison.Ordinal)
                ? entry
                : entry with
                {
                    RepositoryRoot = string.Empty,
                    ErrorMessage = null,
                    RequestSummaryJson = "{}",
                    ResponseSummaryJson = "{}",
                    WarningsJson = "[]"
                })
            .ToList());

    private static ProjectStructureReadRequest ResolveHttpReadRequest(
        ProjectStructureReadRequest request)
    {
        return request.Source switch
        {
            ProjectStructureReadSource.ContextDefault or
                ProjectStructureReadSource.CanonicalCurrent
                => request with
                {
                    Source = ProjectStructureReadSource.CanonicalCurrent
                },
            ProjectStructureReadSource.InvocationSnapshot
                => throw new ProjectStructureAgentException(
                    StatusCodes.Status400BadRequest,
                    ReadSourceUnavailableErrorCode,
                    "Invocation snapshots are bound to an active in-process agent invocation and are not available through the Project Structure HTTP API.",
                    new ProjectStructureReadSourceRejectionDetails(
                        request.Source,
                        ProjectStructureReadSource.CanonicalCurrent)),
            _ => throw new ProjectStructureAgentException(
                StatusCodes.Status400BadRequest,
                ReadSourceInvalidErrorCode,
                $"Project Structure read source '{request.Source}' is invalid.",
                new ProjectStructureReadSourceRejectionDetails(
                    request.Source,
                    ProjectStructureReadSource.CanonicalCurrent))
        };
    }

    private static async Task EnsureGenericNodeUpdateAllowedAsync(
        ProjectStructureAgentService agentService,
        Guid projectId,
        string nodeId,
        ProjectObjectType? requestedObjectType,
        string? requestedObjectSubtype,
        CancellationToken cancellationToken)
    {
        var node = await GetNodeForGenericUpdateAsync(
            agentService,
            projectId,
            nodeId,
            cancellationToken);
        ProjectStructureCanonicalTaskMutationPolicy.EnsureGenericUpdateAllowed(
            node,
            requestedObjectType,
            requestedObjectSubtype);
    }

    private static async Task<ProjectStructureNodeSummary> GetNodeForGenericUpdateAsync(
        ProjectStructureAgentService agentService,
        Guid projectId,
        string nodeId,
        CancellationToken cancellationToken)
    {
        var response = await agentService.GetStructureAsync(
            projectId,
            new ProjectStructureReadRequest(NodeIds: [nodeId]),
            cancellationToken);
        return response.Nodes.FirstOrDefault(node =>
                string.Equals(node.Id, nodeId, StringComparison.Ordinal))
            ?? throw new ProjectStructureAgentException(
                StatusCodes.Status404NotFound,
                "NodeNotFound",
                $"Project-structure node '{nodeId}' was not found in project '{projectId:D}'.");
    }

    private static async Task<IResult> ExecuteAsync<T>(
        HttpContext httpContext,
        ProjectStructureAnalyticsService analyticsService,
        string operationName,
        Guid? projectId,
        string? nodeId,
        ProjectStructureLeaseScopeKind? scopeKind,
        string? scopeKey,
        object? requestSummary,
        Func<ProjectStructureAgentContext, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken,
        Func<T, Guid?>? projectIdSelector = null)
    {
        var agent = ResolveAgentContext(httpContext);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await action(agent, cancellationToken);
            stopwatch.Stop();
            var warnings = ExtractWarnings(response);
            var effectiveProjectId = projectId ?? projectIdSelector?.Invoke(response);
            try {
                await analyticsService.RecordAsync(
                    new ProjectStructureAnalyticsWriteRequest(
                        operationName,
                        effectiveProjectId,
                        nodeId,
                        scopeKind,
                        scopeKey,
                        agent,
                        true,
                        stopwatch.ElapsedMilliseconds,
                        warnings,
                        null,
                        null,
                        ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                        ProjectStructureAnalyticsService.SerializeResponseSummary(response)),
                    cancellationToken);
            } catch (Exception exception) when (response is ProjectStructureWorkflowNodeStartResult) {
                var admitted = (ProjectStructureWorkflowNodeStartResult)(object)response;
                httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ProjectStructureWorkflowLaunch")
                    .LogWarning(exception, "Workflow {RunId} retained its admission; analytics acknowledgement failed.", admitted.RunId);
                return Results.Json(admitted with {
                    Warnings = [.. admitted.Warnings, "The workflow admission is retained; analytics acknowledgement remains pending."],
                    ObservationException = admitted.ObservationException ?? exception
                }, ProjectStructureHttpJsonContract.SerializerOptions);
            }
            return Results.Json(
                response,
                ProjectStructureHttpJsonContract.SerializerOptions);
        }
        catch (ProjectStructureAgentException ex)
        {
            stopwatch.Stop();
            await analyticsService.RecordAsync(
                new ProjectStructureAnalyticsWriteRequest(
                    operationName,
                    projectId,
                    nodeId,
                    scopeKind,
                    scopeKey,
                    agent,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    [],
                    ex.ErrorCode,
                    ex.Message,
                    ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                    ProjectStructureAnalyticsService.SerializeSummary(ex.Details)),
                cancellationToken);
            return Results.Json(
                new
                {
                    Error = new
                    {
                        ex.ErrorCode,
                        ex.Message,
                        ex.Details
                    }
                },
                ProjectStructureHttpJsonContract.SerializerOptions,
                statusCode: ex.StatusCode);
        }
        catch (Exception ex) when (SerializableMutationScope.IsConflict(ex) || ex is ProjectWriteAdmissionRejectedException)
        {
            const string errorCode = "ProjectStructureConcurrentMutation";
            const string message =
                "The project structure changed concurrently. Reload the authoritative project state and retry the mutation.";
            stopwatch.Stop();
            await analyticsService.RecordAsync(
                new ProjectStructureAnalyticsWriteRequest(
                    operationName,
                    projectId,
                    nodeId,
                    scopeKind,
                    scopeKey,
                    agent,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    [],
                    errorCode,
                    message,
                    ProjectStructureAnalyticsService.SerializeSummary(
                        requestSummary),
                    ProjectStructureAnalyticsService.SerializeSummary(new
                    {
                        FailureType = ex.GetType().Name
                    })),
                cancellationToken);
            return Results.Json(
                new
                {
                    Error = new
                    {
                        ErrorCode = errorCode,
                        Message = message
                    }
                },
                ProjectStructureHttpJsonContract.SerializerOptions,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await analyticsService.RecordAsync(
                new ProjectStructureAnalyticsWriteRequest(
                    operationName,
                    projectId,
                    nodeId,
                    scopeKind,
                    scopeKey,
                    agent,
                    false,
                    stopwatch.ElapsedMilliseconds,
                    [],
                    "UnhandledError",
                    ex.Message,
                    ProjectStructureAnalyticsService.SerializeSummary(requestSummary),
                    ProjectStructureAnalyticsService.SerializeSummary(new { ex.Message })),
                cancellationToken);
            return Results.Json(
                new
                {
                    Error = new
                    {
                        ErrorCode = "UnhandledError",
                        Message = "The project-structure API request failed unexpectedly."
                    }
                },
                ProjectStructureHttpJsonContract.SerializerOptions,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> ExecuteJsonRequestAsync<TRequest, TResponse>(
        HttpContext httpContext,
        ProjectStructureAnalyticsService analyticsService,
        string operationName,
        Guid? projectId,
        string? nodeId,
        ProjectStructureLeaseScopeKind? scopeKind,
        string? scopeKey,
        ProjectStructureHttpBodyContract bodyContract,
        Func<TRequest, ProjectStructureAgentContext, CancellationToken, Task<TResponse>> action,
        CancellationToken cancellationToken,
        Func<TResponse, Guid?>? projectIdSelector = null)
    {
        TRequest request;
        try
        {
            request = await ProjectStructureHttpJsonContract.ReadRequestAsync<TRequest>(
                httpContext.Request,
                bodyContract,
                cancellationToken);
        }
        catch (ProjectStructureAgentException exception)
        {
            return await ExecuteAsync(
                httpContext,
                analyticsService,
                operationName,
                projectId,
                nodeId,
                scopeKind,
                scopeKey,
                null,
                (_, _) => Task.FromException<TResponse>(exception),
                cancellationToken,
                projectIdSelector);
        }

        return await ExecuteAsync(
            httpContext,
            analyticsService,
            operationName,
            projectId,
            nodeId,
            scopeKind,
            scopeKey,
            request,
            (agent, executionCancellationToken) => action(
                request,
                agent,
                executionCancellationToken),
            cancellationToken,
            projectIdSelector);
    }

    private static ProjectWriteAdmission RequireProjectAdmission(Guid projectId, ProjectWriteAdmission? expected) {
        if (expected is null || expected.ProjectId != projectId) {
            throw new ProjectStructureAgentException(StatusCodes.Status409Conflict, ProjectLifetimeRefreshRequiredErrorCode,
                "Reload the project structure and submit its expectedProjectAdmission with the mutation.");
        }
        return expected;
    }

    private static ProjectStructureAgentContext ResolveAgentContext(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var agentId = httpContext.User.FindFirstValue("sub") ??
                          httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                          throw new ProjectStructureAgentException(
                              StatusCodes.Status403Forbidden,
                              "ApiActorIdentityMissing",
                              "The authenticated API token does not contain a subject.");
            var sessionId = httpContext.User.FindFirstValue("jti") ??
                            throw new ProjectStructureAgentException(
                                StatusCodes.Status403Forbidden,
                                "ApiSessionIdentityMissing",
                                "The authenticated API token does not contain a session identifier.");
            var agentName = httpContext.User.Identity.Name ??
                            httpContext.User.FindFirstValue(ClaimTypes.Name) ??
                            httpContext.User.FindFirstValue("name") ??
                            agentId;
            var hasExpiry = long.TryParse(httpContext.User.FindFirstValue("exp"), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var expiresAt);

            return new ProjectStructureAgentContext(
                agentId,
                agentName,
                Environment.MachineName,
                string.Empty,
                string.Empty,
                sessionId) {
                WorkflowAuthority = hasExpiry
                    ? ProjectStructureWorkflowAuthoritySource.AuthenticatedOperator(agentId, DateTimeOffset.FromUnixTimeSeconds(expiresAt))
                    : null
            };
        }

        return new ProjectStructureAgentContext(
            "local-api-operator",
            "Local API operator",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            $"runtime-{Environment.ProcessId}") {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.Api)
        };
    }

    private static IReadOnlyList<string> ExtractWarnings<T>(T response)
    {
        return response switch
        {
            ProjectStructureReadResponse readResponse => readResponse.Warnings,
            ProjectStructureChecklistResponse checklistResponse => checklistResponse.Warnings,
            ProjectStructureNodesToSubprojectResult nodesToSubprojectResult => nodesToSubprojectResult.Warnings,
            ProjectStructureDeletionResult deletionResult => deletionResult.Warnings,
            ProjectStructureImportResult importResult => importResult.Warnings,
            ProjectStructureProcessNodeStartResult processNodeStartResult => processNodeStartResult.Warnings,
            ProjectStructureWorkflowNodeStartResult workflowNodeStartResult => workflowNodeStartResult.Warnings,
            ProjectPlanSummary planSummary => planSummary.Warnings,
            _ => []
        };
    }

    private static Modules.Workspace.ProjectManagementKnowledgeCategory MapGuidanceCategory(ProjectManagementGuidanceCategory category)
    {
        return (Modules.Workspace.ProjectManagementKnowledgeCategory)(int)category;
    }

    private static ProjectManagementGuidanceCategory MapGuidanceCategory(Modules.Workspace.ProjectManagementKnowledgeCategory category)
    {
        return (ProjectManagementGuidanceCategory)(int)category;
    }

    private sealed record ProjectStructureReadSourceRejectionDetails(
        ProjectStructureReadSource RequestedSource,
        ProjectStructureReadSource SupportedSource);
}
