using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using Microsoft.AspNetCore.Mvc;
using ProviderMutationAttempt = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMutationAttempt;
using IProviderMutationVerification = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderMutationVerification;

namespace CanDoItAll.Web.Api;

internal static class AgentsApi
{
    public static RouteGroupBuilder MapAgentsApi(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents");

        agents.MapGet("/", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken,
                bool includeTemplates = false) =>
            Results.Ok(await workspaceService.ListAgentsAsync(includeTemplates, cancellationToken)))
            .WithName("ListAgents")
            .Produces<AgentDefinition[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/bootstrap", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken,
                bool includeTemplates = false) =>
            Results.Ok(AgentApiResponseMapper.ToChatPageBootstrap(
                await workspaceService.GetChatPageBootstrapAsync(includeTemplates, cancellationToken))))
            .WithName("GetAgentBootstrap")
            .Produces<AgentChatPageBootstrapApiResponse>(StatusCodes.Status200OK);

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
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken,
                bool includeTemplates = false) =>
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
        agents.MapPost("/providers/mutations/verify", async (
                ProviderMutationAttempt attempt,
                HttpContext context,
                IProviderMutationVerification verification,
                CancellationToken cancellationToken) => {
            if (attempt.ProviderId == Guid.Empty || attempt.AttemptId == Guid.Empty || !Enum.IsDefined(attempt.Kind)) {
                return Results.BadRequest(new { Code = "agents.provider-receipt-invalid", Message = "The mutation receipt is invalid." });
            }
            var result = await verification.VerifyAsync(attempt, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new ProviderVerificationApiResponse(result.ProviderId, result.Disposition,
                result.ConcurrencyToken, false));
        })
            .WithName("VerifyAgentProviderMutation")
            .Produces<ProviderVerificationApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);

        agents.MapGet("/providers", async (
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            Results.Ok(await providerAdministration.ListProvidersAsync(cancellationToken)))
            .WithName("ListAgentProviders")
            .Produces<ProviderProfile[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/providers/{providerId:guid}/editor", async (
                Guid providerId,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            await ProviderApiResults.ExecuteAsync(context, async () =>
                Results.Ok(await providerAdministration.GetProviderEditorAsync(providerId, cancellationToken))))
            .WithName("GetAgentProviderEditor")
            .ProducesApiErrors(StatusCodes.Status404NotFound, StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers", async (
                ProviderProfileEditorModel request,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            await SaveProviderResultAsync(
                request,
                context,
                providerAdministration,
                cancellationToken))
            .WithName("SaveAgentProvider")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable,
                StatusCodes.Status500InternalServerError);

        agents.MapDelete("/providers/{providerId:guid}", async (
                Guid providerId,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            await ProviderApiResults.ExecuteAsync(context, async () => {
                await providerAdministration.DeleteProviderAsync(providerId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            }, _ => Results.Ok(new ApiAck(true))))
        .WithName("DeleteAgentProvider")
        .Produces<ApiAck>(StatusCodes.Status200OK)
        .ProducesApiErrors(StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers/{providerId:guid}/test", async (
                Guid providerId,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            await ProviderApiResults.ExecuteAsync(context, async () =>
                Results.Ok(await providerAdministration.TestProviderAsync(providerId, cancellationToken))))
            .WithName("TestAgentProvider")
            .Produces<ProviderHealthResult>(StatusCodes.Status200OK)
            .Produces<ProviderCommittedApiResponse>(StatusCodes.Status202Accepted)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict, StatusCodes.Status502BadGateway, StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers/{providerId:guid}/test-chat", async (
                Guid providerId,
                ProviderTestChatRequest request,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await providerAdministration.RunProviderTestChatAsync(
                    providerId,
                    ProviderHistoryRequestContext.WithCaller(request, context),
                    cancellationToken));
            }
            catch (KeyNotFoundException) {
                return ApiEndpointResults.AgentFailure(context, StatusCodes.Status404NotFound,
                    "The provider was not found.", ProviderApiResults.NotFoundCode);
            }
            catch (ProviderRuntimeProfileUnavailableException)
            {
                return ApiEndpointResults.AgentFailure(
                    context,
                    StatusCodes.Status503ServiceUnavailable,
                    "The provider runtime profile is unavailable.",
                    LlmChatErrorCodes.ProviderUnavailable);
            }
        })
        .WithName("RunAgentProviderTestChat")
        .Produces<ProviderTestChatResult>(StatusCodes.Status200OK)
        .ProducesApiErrors(StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers/{providerId:guid}/ollama-modelfile", async (
                Guid providerId,
                ProviderModelMaintenanceEditorRequest request,
                HttpContext context,
                IProviderRuntimeAdministrationService providerAdministration,
                CancellationToken cancellationToken) =>
            await ProviderApiResults.ExecuteAsync(context, async () =>
                Results.Ok(await providerAdministration.CreateOrUpdateProviderModelAsync(providerId, request, cancellationToken))))
            .WithName("CreateAgentProviderModelMaintenance")
            .Produces<ProviderModelMaintenanceEditorResult>(StatusCodes.Status200OK)
            .Produces<ProviderCommittedApiResponse>(StatusCodes.Status202Accepted)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
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
            try {
                await workspaceService.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            } catch (CapabilityVerificationException exception) {
                return Results.Json(new CapabilityVerificationApiResponse(agentId, capabilityId,
                    exception.Outcome.Disposition, exception.Outcome.Receipt?.AttemptId,
                    exception.Outcome.Receipt?.CheckedAtUtc, AutomaticReplaySafe: false),
                    statusCode: exception.Outcome.Disposition == CapabilityVerificationDisposition.Rejected
                        ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict);
            }
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
            Results.Ok(AgentApiResponseMapper.ToChatSessions(
                await workspaceService.ListChatSessionsAsync(agentId, cancellationToken))))
            .WithName("ListAgentChatSessions")
            .Produces<AgentChatSessionApiResponse[]>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat-sessions", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToChatSession(
                await workspaceService.GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken))))
            .WithName("CreateAgentChatSession")
            .Produces<AgentChatSessionApiResponse>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat-sessions/{chatSessionId:guid}/rename", async (
                Guid agentId,
                Guid chatSessionId,
                ChatSessionRenameApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToChatSession(
                await workspaceService.RenameChatSessionAsync(
                    agentId,
                    chatSessionId,
                    request.Title,
                    cancellationToken))))
            .WithName("RenameAgentChatSession")
            .Produces<AgentChatSessionApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/chat-workspace", async (
                Guid agentId,
                Guid? preferredSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToChatWorkspace(
                await workspaceService.GetChatAgentWorkspaceAsync(
                    agentId,
                    preferredSessionId,
                    cancellationToken))))
            .WithName("GetAgentChatWorkspace")
            .Produces<AgentChatWorkspaceApiResponse>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat", async (
                Guid agentId,
                AgentChatApiRequest request,
                HttpContext context,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var validation = AgentApiRequestValidation.ValidateCommand(
                    context,
                    agentId,
                    request.ChatSessionId,
                    request.Prompt);
                if (validation is not null)
                {
                    return validation;
                }

                var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
                AgentActivityApiResults.SetOperationIdHeader(context.Response, operationId);
                try
                {
                    var result = await workspaceService.SendMessageAsync(
                        agentId,
                        request.ChatSessionId,
                        request.Prompt,
                        new AgentChatRunOptions(operationId) { Context = ProviderHistoryRequestContext.ForExecution(null, context) },
                        cancellationToken,
                        request.AttachmentPaths);
                    return Results.Ok(AgentApiResponseMapper.ToChatRunResult(result));
                }
                catch (AgentExecutionActivityAdmissionException exception)
                {
                    return AgentActivityApiResults.FromAdmissionException(
                        context,
                        exception,
                        agentId,
                        chatSessionId: request.ChatSessionId);
                }
                catch (AgentChatRunFailedException exception)
                {
                    return ApiEndpointResults.AgentRunFailure(context, exception);
                }
                catch (AgentRunFailedException exception)
                {
                    return ApiEndpointResults.AgentRunFailure(context, exception);
                }
            })
            .WithName("SendAgentChatMessage")
            .Produces<AgentChatRunApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/execution-runs/{executionRunId:guid}/pending-approvals", async (
                Guid executionRunId,
                PendingApprovalApiRequest request,
                HttpContext context,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var validation = AgentApiRequestValidation.ValidateExecutionRun(
                    context,
                    executionRunId);
                if (validation is not null)
                {
                    return validation;
                }

                var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
                AgentActivityApiResults.SetOperationIdHeader(context.Response, operationId);
                try
                {
                    var decisions = await AgentApprovalDecisionRequestMapper.ResolveDecisionsAsync(
                        workspaceService,
                        executionRunId,
                        request,
                        cancellationToken);
                    var result = await workspaceService.ContinueExecutionRunAsync(
                        executionRunId,
                        operationId,
                        decisions,
                        request.AutoApprovePendingToolCalls,
                        cancellationToken);
                    return Results.Ok(AgentApiResponseMapper.ToExecutionRunResult(result));
                }
                catch (AgentExecutionActivityAdmissionException exception)
                {
                    return AgentActivityApiResults.FromAdmissionException(
                        context,
                        exception,
                        executionRunId: executionRunId);
                }
                catch (AgentApprovalDecisionMismatchException exception)
                {
                    return ApiEndpointResults.AgentValidationFailure(
                        context,
                        exception.Message,
                        "agents.approval-decision-mismatch",
                        executionRunId: executionRunId);
                }
                catch (AgentChatRunFailedException exception)
                {
                    return ApiEndpointResults.AgentRunFailure(context, exception);
                }
                catch (AgentRunFailedException exception)
                {
                    return ApiEndpointResults.AgentRunFailure(context, exception);
                }
            })
            .WithName("RespondToAgentExecutionApprovals")
            .Produces<AgentExecutionRunResultApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapExecutionEndpoints(RouteGroupBuilder agents)
    {
        agents.MapPost("/execution-runs", async (
                AgentExecutionRunApiRequest request,
                HttpContext context,
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
                context,
                cancellationToken))
            .WithName("StartAgentExecutionRun")
            .Accepts<AgentExecutionRunApiRequest>("application/json")
            .Produces<AgentExecutionRunResultApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/{agentId:guid}/execution-runs", async (
                Guid agentId,
                AgentExecutionRunStartApiRequest request,
                HttpContext context,
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
                context,
                cancellationToken))
            .WithName("StartAgentScopedExecutionRun")
            .Accepts<AgentExecutionRunStartApiRequest>("application/json")
            .Produces<AgentExecutionRunResultApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapGet("/execution-runs", async (
                [AsParameters] AgentExecutionRunApiQuery query,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToExecutionRuns(
                await workspaceService.ListExecutionRunsAsync(
                    query.ToExecutionRunQuery(),
                    cancellationToken))))
            .WithName("ListAgentExecutionRuns")
            .Produces<AgentExecutionRunApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs", async (
                Guid agentId,
                [AsParameters] AgentExecutionRunApiQuery query,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToExecutionRuns(
                await workspaceService.ListExecutionRunsAsync(
                    query.ToExecutionRunQuery(agentId),
                    cancellationToken))))
            .WithName("ListAgentScopedExecutionRuns")
            .Produces<AgentExecutionRunApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/execution-runs/{executionRunId:guid}", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToExecutionRunDetail(
                await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken))))
            .WithName("GetAgentExecutionRunDetail")
            .Produces<AgentExecutionRunDetailApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                AgentApiResponseMapper.ToExecutionRunDetail,
                cancellationToken))
            .WithName("GetAgentScopedExecutionRunDetail")
            .Produces<AgentExecutionRunDetailApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/artifacts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToArtifacts(
                await workspaceService.ListExecutionArtifactsAsync(executionRunId, cancellationToken))))
            .WithName("ListAgentExecutionArtifacts")
            .Produces<AgentExecutionArtifactApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/artifacts", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToArtifacts(detail.Artifacts),
                cancellationToken))
            .WithName("ListAgentScopedExecutionArtifacts")
            .Produces<AgentExecutionArtifactApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/execution-runs/{executionRunId:guid}/checkpoints", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToCheckpoints(
                await workspaceService.ListExecutionWorkflowCheckpointsAsync(
                    executionRunId,
                    cancellationToken))))
            .WithName("ListAgentExecutionCheckpoints")
            .Produces<AgentExecutionCheckpointApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/checkpoints", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToCheckpoints(detail.Checkpoints),
                cancellationToken))
            .WithName("ListAgentScopedExecutionCheckpoints")
            .Produces<AgentExecutionCheckpointApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/execution-runs/{executionRunId:guid}/tool-receipts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToToolReceipts(
                await workspaceService.ListToolExecutionReceiptsAsync(executionRunId, cancellationToken))))
            .WithName("ListAgentExecutionToolReceipts")
            .Produces<AgentExecutionToolReceiptApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/tool-receipts", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToToolReceipts(detail.ToolReceipts),
                cancellationToken))
            .WithName("ListAgentScopedExecutionToolReceipts")
            .Produces<AgentExecutionToolReceiptApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/log", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToExecutionLog(detail.ExecutionLog),
                cancellationToken))
            .WithName("ListAgentScopedExecutionLog")
            .Produces<AgentExecutionLogApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/metrics", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToMetrics(detail.Metrics),
                cancellationToken))
            .WithName("ListAgentScopedExecutionMetrics")
            .Produces<AgentRunMetricApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/approvals", async (
                Guid agentId,
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            await GetAgentExecutionRunPartAsync(
                agentId,
                executionRunId,
                workspaceService,
                detail => AgentApiResponseMapper.ToExecutionApprovals(detail.Approvals),
                cancellationToken))
            .WithName("ListAgentScopedExecutionApprovals")
            .Produces<AgentExecutionApprovalApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/execution-runs/{executionRunId:guid}/approvals", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            {
                var detail = await workspaceService.GetExecutionRunDetailAsync(
                    executionRunId,
                    cancellationToken);
                return Results.Ok(AgentApiResponseMapper.ToExecutionApprovals(detail.Approvals));
            })
            .WithName("ListAgentExecutionApprovals")
            .Produces<AgentExecutionApprovalApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-log", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToExecutionLog(
                await workspaceService.ListExecutionLogAsync(
                    agentId,
                    chatSessionId,
                    cancellationToken))))
            .WithName("ListAgentExecutionLog")
            .Produces<AgentExecutionLogApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/runtime-snapshot", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToChatRuntime(
                await workspaceService.GetChatRuntimeSnapshotAsync(
                    agentId,
                    chatSessionId,
                    cancellationToken))))
            .WithName("GetAgentRuntimeSnapshot")
            .Produces<AgentChatRuntimeApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/metrics", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(AgentApiResponseMapper.ToMetrics(
                await workspaceService.ListMetricsAsync(agentId, cancellationToken))))
            .WithName("ListAgentMetrics")
            .Produces<AgentRunMetricApiResponse[]>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> StartExecutionRunAsync(
        ExecutionRunRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var validation = AgentApiRequestValidation.ValidateCommand(
            context,
            request.AgentId,
            request.ChatSessionId,
            request.Prompt);
        if (validation is not null)
        {
            return validation;
        }

        AgentActivityApiResults.SetOperationIdHeader(
            context.Response,
            request.InitialActivityOperationId);
        try
        {
            var result = await workspaceService.ExecuteRunAsync(request with {
                Context = ProviderHistoryRequestContext.ForExecution(request.Context, context)
            }, cancellationToken);
            return Results.Ok(AgentApiResponseMapper.ToExecutionRunResult(result));
        }
        catch (AgentJsonSchemaOutputContractException exception)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                exception.Message,
                exception.Code,
                request.AgentId,
                chatSessionId: request.ChatSessionId);
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(
                context,
                exception,
                request.AgentId,
                chatSessionId: request.ChatSessionId);
        }
        catch (AgentChatRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
        }
        catch (AgentRunFailedException exception)
        {
            return ApiEndpointResults.AgentRunFailure(context, exception);
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
            return ApiEndpointResults.BadRequest(
                exception.Message,
                AgentApiRequestValidation.InvalidRequestCode);
        }
        catch (InvalidOperationException exception)
        {
            return ApiEndpointResults.BadRequest(
                exception.Message,
                AgentApiRequestValidation.InvalidRequestCode);
        }
    }

    private static Task<IResult> SaveProviderResultAsync(
        ProviderProfileEditorModel request,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        ProviderApiResults.ExecuteAsync(context, async () =>
            Results.Ok(await providerAdministration.SaveProviderAsync(request, cancellationToken)),
            commit => Results.Ok(commit.ProviderId));

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
    AgentExecutionOperationId? ActivityOperationId = null)
{
    /// <summary>
    /// SB15 additive per-proposal decisions. When present and non-empty, this takes precedence
    /// over <see cref="Approved"/> and must contain exactly one decision per approval currently
    /// pending on the target run. Absent for legacy clients, who keep sending only
    /// <see cref="Approved"/> — the request handler expands that against the run's pending set.
    /// </summary>
    public IReadOnlyList<PendingApprovalDecisionApiRequest>? Decisions { get; init; }
}

internal sealed record PendingApprovalDecisionApiRequest(string ApprovalId, bool Approved);

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
