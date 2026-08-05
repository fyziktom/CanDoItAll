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
    private const string ReadSourceUnavailableErrorCode =
        "ProjectStructureReadSourceUnavailable";
    private const string ReadSourceInvalidErrorCode =
        "ProjectStructureReadSourceInvalid";

    public static IEndpointRouteBuilder MapProjectStructureAgentApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/project-structure")
            .DisableAntiforgery();
        group.WithMetadata(ProjectStructureHttpResponseContract.Instance);
        group.ApplyApiAuthorization(endpoints, ApiAuthorizationPolicies.WriteProjectStructure);

        group.MapGet("/node-catalog", async (
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-catalog",
                null,
                null,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.GetNodeCatalogAsync(cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureNodeCatalogResponse>();

        group.MapGet("/projects", async (
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "projects.list",
                null,
                null,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.ListProjectsAsync(cancellationToken),
                cancellationToken));

        group.MapPost("/projects", async (
            HttpContext httpContext,
            ProjectStructureProjectSaveRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                response => response.Id));

        group.MapPut("/projects/{projectId:guid}", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureProjectSaveRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "projects.update",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.SaveProjectAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapGet("/projects/{projectId:guid}/hierarchy", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "projects.hierarchy",
                projectId,
                null,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.GetHierarchyAsync(projectId, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{parentProjectId:guid}/subprojects", async (
            Guid parentProjectId,
            HttpContext httpContext,
            ProjectStructureSubprojectChangeRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/structure/read", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureReadRequest, ProjectStructureReadResponse>(
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
                cancellationToken))
            .Accepts<ProjectStructureReadRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.StructureRead)
            .Produces<ProjectStructureReadResponse>();

        group.MapPost("/projects/{projectId:guid}/plan/summary", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectPlanSummaryQuery request,
            ProjectPlanAnalyticsQueryService planAnalyticsService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "plan.summary",
                projectId,
                null,
                null,
                null,
                request,
                (_, cancellationToken) => planAnalyticsService.GetSummaryAsync(projectId, request, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/tasks", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureTaskCreateRequest request,
            ProjectStructureTaskCreationService taskCreationService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPut("/projects/{projectId:guid}/tasks/{taskId}", async (
            Guid projectId,
            string taskId,
            HttpContext httpContext,
            ProjectStructureTaskDetailsUpdateRequest request,
            ProjectStructureTaskDetailsService taskDetailsService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "tasks.update",
                projectId,
                taskId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                async (_, cancellationToken) =>
                {
                    if (!string.Equals(taskId, request.TaskId.Value, StringComparison.Ordinal))
                    {
                        throw new ProjectStructureAgentException(
                            StatusCodes.Status400BadRequest,
                            "TaskRouteMismatch",
                            "The task id in the route must match request.taskId.");
                    }

                    try
                    {
                        return await taskDetailsService.UpdateAsync(
                            projectId,
                            request,
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/tasks/{taskId}/resource", async (
            Guid projectId,
            string taskId,
            HttpContext httpContext,
            ProjectStructureTaskResourceAttachRequest request,
            ProjectStructureTaskResourceAttachmentService attachmentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                    agent,
                    cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureNodeCreateInput, ProjectStructureNodeSummary>(
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
                cancellationToken))
            .Accepts<ProjectStructureNodeCreateOpenApiRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeCreate)
            .Produces<ProjectStructureNodeSummary>();

        group.MapPut("/projects/{projectId:guid}/nodes/{nodeId}", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureNodeEditInput, ProjectStructureNodeSummary>(
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
                cancellationToken))
            .Accepts<ProjectStructureNodeEditOpenApiRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeEdit)
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/type", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureNodeTypeInput, ProjectStructureNodeSummary>(
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
                cancellationToken))
            .Accepts<ProjectStructureNodeTypeInput>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.NodeType)
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/metadata", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureNodeMetadataInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken))
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/nodes/statuses", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureStatusBatchInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-statuses",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.UpdateNodeStatusesAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/status", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureStatusInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/progress", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureProgressBatchInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-progress",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.UpdateNodeProgressAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/progress", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureProgressInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                    new ProjectStructureProgressBatchInput([nodeId], request.ProgressMode, request.ProgressPercent, request.LeaseToken),
                    agent,
                    cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/markers", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureMarkerBatchInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-markers",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.UpdateNodeMarkerAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/markers", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureMarkerInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-marker",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.ChangeNodeMarkerAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/priorities", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructurePriorityBatchInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-priorities",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.UpdateNodePriorityAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/priority", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructurePriorityInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/move", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodeMoveInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/recompose", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodeRecomposeInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-recompose",
                projectId,
                request.RootNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.RecomposeNodeAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/reparent", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureNodeParentInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken))
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/nodes/reparent", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodeReparentInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-reparent",
                projectId,
                request.NodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.ReparentNodeAsync(projectId, request, agent, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/nodes/copy", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodesCopyInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/move-to-new-subproject", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodesToSubprojectInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.nodes-move-to-new-subproject",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.MoveNodesToNewSubprojectAsync(projectId, request, agent, cancellationToken),
                cancellationToken,
                response => response.TargetProjectId));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/move-descendants-to-project", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureSubtreeTransferInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-transfer-descendants",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.MoveDescendantsToProjectAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/command", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureNodeCommandInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-command",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.ExecuteNodeCommandAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/process-definition", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureProcessDefinitionLinkInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-link-process-definition",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.LinkProcessDefinitionAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/process/start", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureProcessNodeStartInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-start-process",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.StartProcessNodeAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow-add-options", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureWorkflowAddOptionsInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-workflow-add-options",
                projectId,
                nodeId,
                null,
                null,
                request,
                (_, cancellationToken) => agentService.GetWorkflowAddOptionsAsync(projectId, nodeId, request, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureWorkflowAddOptionsResult>();

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow-definition", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureWorkflowNodeCreateInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-create-workflow-definition",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.CreateWorkflowNodeAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureWorkflowNodeCreateResult>();

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/workflow/start", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureWorkflowNodeStartInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-start-workflow",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.StartWorkflowNodeAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken));

        group.MapGet("/projects/{projectId:guid}/nodes/{nodeId}/workflow/status", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-workflow-status",
                projectId,
                nodeId,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.GetWorkflowNodeStatusAsync(projectId, nodeId, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/{nodeId}/delete", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureNodeDeleteInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.node-delete",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.DeleteNodeDetailedAsync(
                    projectId,
                    nodeId,
                    request,
                    agent,
                    cancellationToken),
                cancellationToken));

        group.MapGet("/projects/{projectId:guid}/deletion-completion-notices", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapGet("/projects/{projectId:guid}/deletion-cleanups", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/nodes/delete", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureNodeDeleteBatchInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.nodes-delete",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.DeleteNodesDetailedAsync(
                    projectId,
                    request,
                    agent,
                    cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/approvals/request", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureApprovalRequestCreateInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "approvals.request",
                projectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.CreateApprovalRequestAsync(projectId, request, agent, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/checklists/query", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureChecklistRequest, ProjectStructureChecklistResponse>(
                httpContext,
                analyticsService,
                "checklists.query",
                projectId,
                null,
                null,
                null,
                ProjectStructureHttpBodyContracts.ChecklistQuery,
                (request, _, cancellationToken) => agentService.GetChecklistAsync(projectId, request, cancellationToken),
                cancellationToken))
            .Accepts<ProjectStructureChecklistRequest>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.ChecklistQuery)
            .Produces<ProjectStructureChecklistResponse>();

        group.MapPost("/projects/{projectId:guid}/dependencies/query", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureDependencyQueryRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "dependencies.query",
                projectId,
                null,
                null,
                null,
                request,
                (_, cancellationToken) => agentService.GetDependenciesAsync(projectId, request, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureDependencyResponse>();

        group.MapPost("/projects/{projectId:guid}/links", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureLinkInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.link-create",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.LinkNodesAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/links/unlink", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureLinkInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "structure.link-delete",
                projectId,
                request.SourceNodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.UnlinkNodesAsync(projectId, request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/dependencies/link", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureLinkInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/projects/{projectId:guid}/dependencies/unlink", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureLinkInput request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapGet("/projects/{projectId:guid}/assets/{nodeId}", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "assets.get",
                projectId,
                nodeId,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.GetAssetAsync(projectId, nodeId, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureAssetDescriptor>();

        group.MapGet("/projects/{projectId:guid}/assets/{nodeId}/content", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "assets.get-content",
                projectId,
                nodeId,
                null,
                null,
                null,
                (_, cancellationToken) => agentService.GetAssetContentAsync(projectId, nodeId, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureAssetContentDescriptor>();

        group.MapPost("/projects/{projectId:guid}/assets", async (
            Guid projectId,
            HttpContext httpContext,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteJsonRequestAsync<ProjectStructureAssetCreateInput, ProjectStructureNodeSummary>(
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
                cancellationToken))
            .Accepts<ProjectStructureAssetCreateInput>(ProjectStructureHttpJsonContract.RuntimeDispatchContentType)
            .WithMetadata(ProjectStructureHttpBodyContracts.AssetCreate)
            .Produces<ProjectStructureNodeSummary>();

        group.MapPost("/projects/{projectId:guid}/assets/{nodeId}/revisions", async (
            Guid projectId,
            string nodeId,
            HttpContext httpContext,
            ProjectStructureAssetRevisionRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "assets.create-revision",
                projectId,
                nodeId,
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.CreateAssetRevisionAsync(projectId, nodeId, request, agent, cancellationToken),
                cancellationToken))
            .Produces<ProjectStructureAssetDescriptor>();

        group.MapPost("/imports", async (
            HttpContext httpContext,
            ProjectStructureImportRequest request,
            ProjectStructureAgentService agentService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "imports.run",
                request.ProjectId,
                null,
                ProjectStructureLeaseScopeKind.Project,
                request.ProjectId.ToString(),
                request,
                (agent, cancellationToken) => agentService.ImportAsync(request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/knowledge/query", async (
            HttpContext httpContext,
            ProjectManagementGuidanceQueryRequest request,
            ProjectManagementKnowledgeService knowledgeService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
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
                cancellationToken));

        group.MapPost("/leases/acquire", async (
            HttpContext httpContext,
            ProjectStructureLeaseAcquireRequest request,
            ProjectStructureLeaseService leaseService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "leases.acquire",
                null,
                null,
                request.ScopeKind,
                request.ScopeKey,
                request,
                (agent, cancellationToken) => leaseService.AcquireAsync(request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/leases/renew", async (
            HttpContext httpContext,
            ProjectStructureLeaseRenewRequest request,
            ProjectStructureLeaseService leaseService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "leases.renew",
                null,
                null,
                request.ScopeKind,
                request.ScopeKey,
                request,
                (agent, cancellationToken) => leaseService.RenewAsync(request, agent, cancellationToken),
                cancellationToken));

        group.MapPost("/leases/release", async (
            HttpContext httpContext,
            ProjectStructureLeaseReleaseRequest request,
            ProjectStructureLeaseService leaseService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "leases.release",
                null,
                null,
                request.ScopeKind,
                request.ScopeKey,
                request,
                (agent, cancellationToken) => leaseService.ReleaseAsync(request, agent, cancellationToken),
                cancellationToken));

        group.MapGet("/leases/current", async (
            HttpContext httpContext,
            ProjectStructureLeaseScopeKind scopeKind,
            string scopeKey,
            ProjectStructureLeaseService leaseService,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "leases.current",
                null,
                null,
                scopeKind,
                scopeKey,
                new { scopeKind, scopeKey },
                (_, cancellationToken) => leaseService.GetActiveLeaseAsync(scopeKind, scopeKey, cancellationToken),
                cancellationToken));

        group.MapPost("/analytics/query", async (
            HttpContext httpContext,
            ProjectStructureAnalyticsQueryRequest request,
            ProjectStructureAnalyticsService analyticsService,
            CancellationToken cancellationToken) =>
            await ExecuteAsync(
                httpContext,
                analyticsService,
                "analytics.query",
                request.ProjectId,
                null,
                null,
                null,
                request,
                (_, cancellationToken) => analyticsService.QueryAsync(request, cancellationToken),
                cancellationToken));

        return endpoints;
    }

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
        catch (Exception ex) when (SerializableMutationScope.IsConflict(ex))
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

            return new ProjectStructureAgentContext(
                agentId,
                agentName,
                Environment.MachineName,
                string.Empty,
                string.Empty,
                sessionId);
        }

        return new ProjectStructureAgentContext(
            "local-api-operator",
            "Local API operator",
            Environment.MachineName,
            string.Empty,
            string.Empty,
            $"runtime-{Environment.ProcessId}");
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
