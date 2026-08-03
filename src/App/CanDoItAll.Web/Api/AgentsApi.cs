using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class AgentsApi
{
    public static RouteGroupBuilder MapAgentsApi(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents");

        agents.MapGet("/", async (
                bool includeTemplates,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListAgentsAsync(includeTemplates, cancellationToken)))
            .WithName("ListAgents")
            .Produces<AgentDefinition[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/bootstrap", async (
                bool includeTemplates,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatPageBootstrapAsync(includeTemplates, cancellationToken)))
            .WithName("GetAgentBootstrap");

        agents.MapGet("/{agentId:guid}", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetAgentEditorAsync(agentId, cancellationToken)))
            .WithName("GetAgentEditor");

        agents.MapPost("/", async (
                AgentEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await SaveAgentResultAsync(request, workspaceService, cancellationToken))
            .WithName("SaveAgent")
            .Accepts<AgentEditorModel>("application/json")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapDelete("/{agentId:guid}", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            try
            {
                await workspaceService.DeleteAgentAsync(agentId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            }
            catch (AgentDeletionConflictException exception)
            {
                return ApiEndpointResults.Conflict(
                    exception.Message,
                    exception.Kind == AgentDeletionConflictKind.ManagedSeedAgent
                        ? "agents.delete-managed-seed"
                        : "agents.delete-active-execution");
            }
        })
        .WithName("DeleteAgent")
        .Produces<ApiAck>(StatusCodes.Status200OK)
        .ProducesApiErrors(
            StatusCodes.Status401Unauthorized,
            StatusCodes.Status403Forbidden,
            StatusCodes.Status409Conflict);

        agents.MapPost("/{agentId:guid}/clone", async (
                Guid agentId,
                AgentCloneApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.CloneAgentAsync(agentId, request.CloneName, cancellationToken)))
            .WithName("CloneAgent");

        agents.MapPost("/{agentId:guid}/convert-to-template", async (
                Guid agentId,
                AgentTemplateConversionApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ConvertToTemplateAsync(agentId, request.TemplateKey, cancellationToken)))
            .WithName("ConvertAgentToTemplate");

        agents.MapGet("/{agentId:guid}/export", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ExportAgentAsync(agentId, cancellationToken)))
            .WithName("ExportAgent");

        agents.MapPost("/import", async (
                AgentImportApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ImportAgentAsync(request.PackagePath, cancellationToken)))
            .WithName("ImportAgent");

        agents.MapAgentPackageImportApi();
        agents.MapAgentExternalProvisioningApi();
        MapTeamEndpoints(agents);
        MapProviderEndpoints(agents);
        MapCapabilityEndpoints(agents);
        MapMemoryEndpoints(agents);
        MapChatEndpoints(agents);
        MapExecutionEndpoints(agents);

        return group;
    }

    private static void MapTeamEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/teams", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListAgentTeamsAsync(cancellationToken)))
            .WithName("ListAgentTeams");

        agents.MapGet("/teams/{teamId:guid}", async (
                Guid teamId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            var team = (await workspaceService.ListAgentTeamsAsync(cancellationToken))
                .FirstOrDefault(item => item.Id == teamId);
            return team is null
                ? ApiEndpointResults.NotFound("Agent team was not found.", "agents.team-not-found")
                : Results.Ok(team);
        })
        .WithName("GetAgentTeam");

        agents.MapGet("/teams/{teamId:guid}/editor", async (
                Guid teamId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetAgentTeamEditorAsync(teamId, cancellationToken)))
            .WithName("GetAgentTeamEditor");

        agents.MapGet("/teams/{teamId:guid}/agents", async (
                Guid teamId,
                bool includeTemplates,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            var team = (await workspaceService.ListAgentTeamsAsync(cancellationToken))
                .FirstOrDefault(item => item.Id == teamId);
            if (team is null)
            {
                return ApiEndpointResults.NotFound("Agent team was not found.", "agents.team-not-found");
            }

            var teamAgentIds = team.AgentIds.ToHashSet();
            var teamAgents = (await workspaceService.ListAgentsAsync(includeTemplates, cancellationToken))
                .Where(agent => teamAgentIds.Contains(agent.Id))
                .ToList();
            return Results.Ok(teamAgents);
        })
        .WithName("ListAgentTeamAgents");

        agents.MapPost("/teams", async (
                AgentTeamEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveAgentTeamAsync(request, cancellationToken)))
            .WithName("SaveAgentTeam");

        agents.MapPut("/teams/{teamId:guid}", async (
                Guid teamId,
                AgentTeamEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            request.Id = teamId;
            return Results.Ok(await workspaceService.SaveAgentTeamAsync(request, cancellationToken));
        })
        .WithName("UpdateAgentTeam");

        agents.MapPost("/teams/{teamId:guid}/members", async (
                Guid teamId,
                AgentTeamMembersApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.UpdateAgentTeamMembersAsync(teamId, request.AgentIds, cancellationToken)))
            .WithName("UpdateAgentTeamMembers");

        agents.MapPut("/teams/{teamId:guid}/members", async (
                Guid teamId,
                AgentTeamMembersApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.UpdateAgentTeamMembersAsync(teamId, request.AgentIds, cancellationToken)))
            .WithName("ReplaceAgentTeamMembers");

        agents.MapDelete("/teams/{teamId:guid}", async (
                Guid teamId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteAgentTeamAsync(teamId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteAgentTeam");
    }

    private static void MapProviderEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/providers", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListProvidersAsync(cancellationToken)))
            .WithName("ListAgentProviders")
            .Produces<ProviderProfile[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/providers/{providerId:guid}/editor", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetProviderEditorAsync(providerId, cancellationToken)))
            .WithName("GetAgentProviderEditor");

        agents.MapPost("/providers", async (
                ProviderProfileEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveProviderAsync(request, cancellationToken)))
            .WithName("SaveAgentProvider");

        agents.MapDelete("/providers/{providerId:guid}", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteProviderAsync(providerId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteAgentProvider");

        agents.MapPost("/providers/{providerId:guid}/test", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.TestProviderAsync(providerId, cancellationToken)))
            .WithName("TestAgentProvider");

        agents.MapPost("/providers/{providerId:guid}/test-chat", async (
                Guid providerId,
                ProviderTestChatRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.RunProviderTestChatAsync(providerId, request, cancellationToken)))
            .WithName("RunAgentProviderTestChat");

        agents.MapPost("/providers/{providerId:guid}/ollama-modelfile", async (
                Guid providerId,
                ProviderModelMaintenanceEditorRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.CreateOrUpdateProviderModelAsync(providerId, request, cancellationToken)))
            .WithName("CreateAgentProviderModelMaintenance");
    }

    private static void MapCapabilityEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/capabilities", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListCapabilitiesAsync(cancellationToken)))
            .WithName("ListAgentCapabilities");

        agents.MapGet("/capabilities/{capabilityId:guid}/editor", async (
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetCapabilityEditorAsync(capabilityId, cancellationToken)))
            .WithName("GetAgentCapabilityEditor");

        agents.MapPost("/capabilities", async (
                CapabilityEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveCapabilityAsync(request, cancellationToken)))
            .WithName("SaveAgentCapability");

        agents.MapDelete("/capabilities/{capabilityId:guid}", async (
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteCapabilityAsync(capabilityId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteAgentCapability");

        agents.MapPost("/{agentId:guid}/capabilities/{capabilityId:guid}/verify", async (
                Guid agentId,
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("VerifyAgentCapability");

        agents.MapPost("/capabilities/setup-tests/tool", async (
                CapabilityToolSetupTestRequest request,
                IAgentCapabilitySetupFlowService setupFlowService,
                CancellationToken cancellationToken) =>
            Results.Ok(await setupFlowService.TestToolSetupAsync(request, cancellationToken)))
            .WithName("TestAgentToolCapabilitySetup");

        agents.MapPost("/capabilities/setup-tests/mcp", async (
                CapabilityMcpSetupTestRequest request,
                IAgentCapabilitySetupFlowService setupFlowService,
                CancellationToken cancellationToken) =>
            Results.Ok(await setupFlowService.TestMcpSetupAsync(request, cancellationToken)))
            .WithName("TestAgentMcpCapabilitySetup");

        agents.MapPost("/capabilities/access-preview", async (
                CapabilityAccessPreviewRequest request,
                IAgentCapabilitySetupFlowService setupFlowService,
                CancellationToken cancellationToken) =>
            Results.Ok(await setupFlowService.PreviewAccessAsync(request, cancellationToken)))
            .WithName("PreviewAgentCapabilityAccess");
    }

    private static void MapMemoryEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/memory", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListMemoryAsync(agentId, cancellationToken)))
            .WithName("ListAgentMemory");

        agents.MapPost("/memory", async (
                MemoryEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveMemoryAsync(request, cancellationToken)))
            .WithName("SaveAgentMemory");

        agents.MapDelete("/memory/{memoryId:guid}", async (
                Guid memoryId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteMemoryAsync(memoryId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        })
        .WithName("DeleteAgentMemory");
    }

    private static void MapChatEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/chat-sessions", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListChatSessionsAsync(agentId, cancellationToken)))
            .WithName("ListAgentChatSessions");

        agents.MapPost("/{agentId:guid}/chat-sessions", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("CreateAgentChatSession");

        agents.MapPost("/{agentId:guid}/chat-sessions/{chatSessionId:guid}/rename", async (
                Guid agentId,
                Guid chatSessionId,
                ChatSessionRenameApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.RenameChatSessionAsync(agentId, chatSessionId, request.Title, cancellationToken)))
            .WithName("RenameAgentChatSession");

        agents.MapGet("/{agentId:guid}/chat-workspace", async (
                Guid agentId,
                Guid? preferredSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId, cancellationToken)))
            .WithName("GetAgentChatWorkspace");

        agents.MapPost("/{agentId:guid}/chat", async (
                Guid agentId,
                AgentChatApiRequest request,
                HttpResponse response,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var validation = AgentApiRequestValidation.ValidateCommand(
                    agentId,
                    request.ChatSessionId,
                    request.Prompt);
                if (validation is not null)
                {
                    return validation;
                }

                var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
                AgentActivityApiResults.SetOperationIdHeader(response, operationId);
                try
                {
                    var result = await workspaceService.SendMessageAsync(
                        agentId,
                        request.ChatSessionId,
                        request.Prompt,
                        new AgentChatRunOptions(operationId),
                        cancellationToken,
                        request.AttachmentPaths);
                    return Results.Ok(result);
                }
                catch (AgentExecutionActivityAdmissionException exception)
                {
                    return AgentActivityApiResults.FromAdmissionException(exception);
                }
            })
            .WithName("SendAgentChatMessage")
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/execution-runs/{executionRunId:guid}/pending-approvals", async (
                Guid executionRunId,
                PendingApprovalApiRequest request,
                HttpResponse response,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var validation = AgentApiRequestValidation.ValidateExecutionRun(executionRunId);
                if (validation is not null)
                {
                    return validation;
                }

                var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
                AgentActivityApiResults.SetOperationIdHeader(response, operationId);
                try
                {
                    var result = await workspaceService.ContinueExecutionRunAsync(
                        executionRunId,
                        operationId,
                        request.Approved,
                        request.AutoApprovePendingToolCalls,
                        cancellationToken);
                    return Results.Ok(result);
                }
                catch (AgentExecutionActivityAdmissionException exception)
                {
                    return AgentActivityApiResults.FromAdmissionException(exception);
                }
            })
            .WithName("RespondToAgentExecutionApprovals")
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapExecutionEndpoints(RouteGroupBuilder agents)
    {
        agents.MapPost("/execution-runs", async (
                AgentExecutionRunApiRequest request,
                HttpResponse response,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await StartExecutionRunAsync(
                new ExecutionRunRequest(
                    AgentId: request.AgentId,
                    Prompt: request.Prompt,
                    InitialActivityOperationId: request.ActivityOperationId ?? AgentExecutionOperationId.New(),
                    ChatSessionId: request.ChatSessionId,
                    Context: request.Context,
                    AutoApprovePendingToolCalls: request.AutoApprovePendingToolCalls,
                    InputAttachmentPaths: request.InputAttachmentPaths,
                    JsonSchemaOutput: request.StructuredOutput),
                workspaceService,
                response,
                cancellationToken))
            .WithName("StartAgentExecutionRun")
            .Accepts<AgentExecutionRunApiRequest>("application/json")
            .Produces<ExecutionRunResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/{agentId:guid}/execution-runs", async (
                Guid agentId,
                AgentExecutionRunStartApiRequest request,
                HttpResponse response,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await StartExecutionRunAsync(
                new ExecutionRunRequest(
                    AgentId: agentId,
                    Prompt: request.Prompt,
                    InitialActivityOperationId: request.ActivityOperationId ?? AgentExecutionOperationId.New(),
                    ChatSessionId: request.ChatSessionId,
                    Context: request.Context,
                    AutoApprovePendingToolCalls: request.AutoApprovePendingToolCalls,
                    InputAttachmentPaths: request.InputAttachmentPaths,
                    JsonSchemaOutput: request.StructuredOutput),
                workspaceService,
                response,
                cancellationToken))
            .WithName("StartAgentScopedExecutionRun")
            .Accepts<AgentExecutionRunStartApiRequest>("application/json")
            .Produces<ExecutionRunResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapGet("/execution-runs", async (
                [AsParameters] AgentExecutionRunApiQuery query,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionRunsAsync(query.ToExecutionRunQuery(), cancellationToken)))
            .WithName("ListAgentExecutionRuns");

        agents.MapGet("/{agentId:guid}/execution-runs", async (
                Guid agentId,
                [AsParameters] AgentExecutionRunApiQuery query,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionRunsAsync(query.ToExecutionRunQuery(agentId), cancellationToken)))
            .WithName("ListAgentScopedExecutionRuns");

        agents.MapGet("/execution-runs/{executionRunId:guid}", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken)))
            .WithName("GetAgentExecutionRunDetail");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail,
                cancellationToken))
            .WithName("GetAgentScopedExecutionRunDetail")
            .Produces<ExecutionRunDetail>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/artifacts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionArtifactsAsync(executionRunId, cancellationToken)))
            .WithName("ListAgentExecutionArtifacts");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/artifacts", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.Artifacts,
                cancellationToken))
            .WithName("ListAgentScopedExecutionArtifacts");

        agents.MapGet("/execution-runs/{executionRunId:guid}/checkpoints", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionWorkflowCheckpointsAsync(executionRunId, cancellationToken)))
            .WithName("ListAgentExecutionCheckpoints");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/checkpoints", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.Checkpoints,
                cancellationToken))
            .WithName("ListAgentScopedExecutionCheckpoints");

        agents.MapGet("/execution-runs/{executionRunId:guid}/tool-receipts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListToolExecutionReceiptsAsync(executionRunId, cancellationToken)))
            .WithName("ListAgentExecutionToolReceipts");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/tool-receipts", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.ToolReceipts,
                cancellationToken))
            .WithName("ListAgentScopedExecutionToolReceipts");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/log", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.ExecutionLog,
                cancellationToken))
            .WithName("ListAgentScopedExecutionLog");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/metrics", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.Metrics,
                cancellationToken))
            .WithName("ListAgentScopedExecutionMetrics");

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/approvals", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => detail.Approvals,
                cancellationToken))
            .WithName("ListAgentScopedExecutionApprovals");

        agents.MapGet("/execution-runs/{executionRunId:guid}/approvals", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var detail = await workspaceService.GetExecutionRunDetailAsync(
                    executionRunId,
                    cancellationToken);
                return Results.Ok(detail.Approvals);
            })
            .WithName("ListAgentExecutionApprovals");

        agents.MapGet("/{agentId:guid}/execution-log", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionLogAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("ListAgentExecutionLog");

        agents.MapGet("/{agentId:guid}/runtime-snapshot", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("GetAgentRuntimeSnapshot");

        agents.MapGet("/{agentId:guid}/metrics", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListMetricsAsync(agentId, cancellationToken)))
            .WithName("ListAgentMetrics");
    }

    private static async Task<IResult> StartExecutionRunAsync(
        ExecutionRunRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            request.AgentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return validation;
        }

        AgentActivityApiResults.SetOperationIdHeader(
            response,
            request.InitialActivityOperationId);
        try
        {
            var result = await workspaceService.ExecuteRunAsync(request, cancellationToken);
            return Results.Ok(result);
        }
        catch (AgentJsonSchemaOutputContractException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, exception.Code);
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(exception);
        }
    }

    private static async Task<IResult> SaveAgentResultAsync(
        AgentEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await workspaceService.SaveAgentAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "agents.request-invalid");
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(exception.Message, "agents.request-invalid");
        }
    }

    private static async Task<IResult> GetAgentExecutionRunPartAsync<T>(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        Func<ExecutionRunDetail, T> select,
        CancellationToken cancellationToken)
    {
        try
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
            return detail.Run.AgentId == agentId
                ? Results.Ok(select(detail))
                : ApiEndpointResults.NotFound(
                    "Agent execution run was not found.",
                    "agents.execution-run-not-found");
        }
        catch (InvalidOperationException)
        {
            return ApiEndpointResults.NotFound(
                "Agent execution run was not found.",
                "agents.execution-run-not-found");
        }
    }
}

internal sealed record AgentCloneApiRequest(string CloneName);

internal sealed record AgentTemplateConversionApiRequest(string TemplateKey);

internal sealed record AgentImportApiRequest(string PackagePath);

internal sealed record AgentTeamMembersApiRequest(IReadOnlyList<Guid> AgentIds);

internal sealed record ChatSessionRenameApiRequest(string Title);

internal sealed record AgentChatApiRequest(
    Guid? ChatSessionId,
    string Prompt,
    IReadOnlyList<string>? AttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

internal sealed record PendingApprovalApiRequest(
    bool Approved,
    bool AutoApprovePendingToolCalls,
    AgentExecutionOperationId? ActivityOperationId = null);

internal sealed record AgentExecutionRunApiRequest(
    Guid AgentId,
    string Prompt,
    Guid? ChatSessionId = null,
    ExecutionInvocationContext? Context = null,
    bool AutoApprovePendingToolCalls = false,
    AgentJsonSchemaOutputContract? StructuredOutput = null,
    IReadOnlyList<string>? InputAttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

internal sealed record AgentExecutionRunStartApiRequest(
    string Prompt,
    Guid? ChatSessionId = null,
    ExecutionInvocationContext? Context = null,
    bool AutoApprovePendingToolCalls = false,
    AgentJsonSchemaOutputContract? StructuredOutput = null,
    IReadOnlyList<string>? InputAttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

internal sealed class AgentExecutionRunApiQuery
{
    public Guid? AgentId { get; set; }

    public Guid? ChatSessionId { get; set; }

    public string? CorrelationId { get; set; }

    public string? SourceKind { get; set; }

    public string? SourceId { get; set; }

    public int? Take { get; set; } = 50;

    public string? ProcessRunId { get; set; }

    public string? ProcessStepId { get; set; }

    public string? SchedulerRunId { get; set; }

    public string? MessageId { get; set; }

    public ExecutionState? State { get; set; }

    public RunOutcome? Outcome { get; set; }

    public ExecutionApprovalStatus? ApprovalStatus { get; set; }

    public DateTimeOffset? CreatedFromUtc { get; set; }

    public DateTimeOffset? CreatedToUtc { get; set; }

    public DateTimeOffset? UpdatedFromUtc { get; set; }

    public DateTimeOffset? UpdatedToUtc { get; set; }

    public ExecutionRunQuery ToExecutionRunQuery(Guid? agentId = null)
    {
        return new ExecutionRunQuery(
            agentId ?? AgentId,
            ChatSessionId,
            CorrelationId,
            SourceKind,
            SourceId,
            Math.Clamp(Take.GetValueOrDefault(50), 1, 500),
            ProcessRunId,
            ProcessStepId,
            SchedulerRunId,
            MessageId,
            State,
            Outcome,
            ApprovalStatus,
            CreatedFromUtc,
            CreatedToUtc,
            UpdatedFromUtc,
            UpdatedToUtc);
    }
}
