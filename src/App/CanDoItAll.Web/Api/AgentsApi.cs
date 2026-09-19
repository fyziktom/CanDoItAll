using System.ComponentModel;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CapabilitySetupTestResult = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilitySetupTestResult;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;
using McpSetupTestResult = CanDoItAll.AgentFramework.Mcp.Abstractions.McpSetupTestResult;
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

        agents.MapGet("/", ListAgentsAsync)
            .WithName("ListAgents")
            .Produces<AgentDefinition[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/bootstrap", GetAgentBootstrapAsync)
            .WithName("GetAgentBootstrap")
            .Produces<AgentChatPageBootstrapApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}", GetAgentEditorAsync)
            .WithName("GetAgentEditor")
            .Produces<AgentEditorModel>(StatusCodes.Status200OK);

        agents.MapPost("/", SaveAgentAsync)
            .WithName("SaveAgent")
            .Accepts<AgentEditorModel>("application/json")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapDelete("/{agentId:guid}", DeleteAgentAsync)
            .WithName("DeleteAgent")
            .Produces<ApiAck>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict);

        agents.MapPost("/{agentId:guid}/clone", CloneAgentAsync)
            .WithName("CloneAgent")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/convert-to-template", ConvertAgentToTemplateAsync)
            .WithName("ConvertAgentToTemplate")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/export", ExportAgentAsync)
            .WithName("ExportAgent")
            .Produces<AgentExportResult>(StatusCodes.Status200OK);

        agents.MapPost("/import", ImportAgentAsync)
            .WithName("ImportAgent")
            .Produces<Guid>(StatusCodes.Status200OK);

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
        agents.MapGet("/teams", ListAgentTeamsAsync)
            .WithName("ListAgentTeams")
            .Produces<AgentTeamDefinition[]>(StatusCodes.Status200OK);

        agents.MapGet("/teams/{teamId:guid}", GetAgentTeamAsync)
            .WithName("GetAgentTeam")
            .Produces<AgentTeamDefinition>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet("/teams/{teamId:guid}/editor", GetAgentTeamEditorAsync)
            .WithName("GetAgentTeamEditor")
            .Produces<AgentTeamEditorModel>(StatusCodes.Status200OK);

        agents.MapGet("/teams/{teamId:guid}/agents", ListAgentTeamAgentsAsync)
            .WithName("ListAgentTeamAgents")
            .Produces<AgentDefinition[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapPost("/teams", SaveAgentTeamAsync)
            .WithName("SaveAgentTeam")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapPut("/teams/{teamId:guid}", UpdateAgentTeamAsync)
            .WithName("UpdateAgentTeam")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapPost("/teams/{teamId:guid}/members", UpdateAgentTeamMembersAsync)
            .WithName("UpdateAgentTeamMembers")
            .Produces<AgentTeamDefinition>(StatusCodes.Status200OK);

        agents.MapPut("/teams/{teamId:guid}/members", ReplaceAgentTeamMembersAsync)
            .WithName("ReplaceAgentTeamMembers")
            .Produces<AgentTeamDefinition>(StatusCodes.Status200OK);

        agents.MapDelete("/teams/{teamId:guid}", DeleteAgentTeamAsync)
            .WithName("DeleteAgentTeam")
            .Produces<ApiAck>(StatusCodes.Status200OK);
    }

    private static void MapProviderEndpoints(RouteGroupBuilder agents)
    {
        agents.MapPost("/providers/mutations/verify", VerifyAgentProviderMutationAsync)
            .WithName("VerifyAgentProviderMutation")
            .Produces<ProviderVerificationApiResponse>(StatusCodes.Status200OK)
            .Produces<ProviderMutationReceiptInvalidApiResponse>(StatusCodes.Status400BadRequest);

        agents.MapGet("/providers", ListAgentProvidersAsync)
            .WithName("ListAgentProviders")
            .Produces<ProviderProfile[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden);

        agents.MapGet("/providers/{providerId:guid}/editor", GetAgentProviderEditorAsync)
            .WithName("GetAgentProviderEditor")
            .Produces<ProviderProfileEditorModel>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound, StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers", SaveAgentProviderAsync)
            .WithName("SaveAgentProvider")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict);

        agents.MapDelete("/providers/{providerId:guid}", DeleteAgentProviderAsync)
            .WithName("DeleteAgentProvider")
            .Produces<ApiAck>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status409Conflict);

        agents.MapPost("/providers/{providerId:guid}/test", TestAgentProviderAsync)
            .WithName("TestAgentProvider")
            .Produces<ProviderHealthResult>(StatusCodes.Status200OK)
            .Produces<ProviderCommittedApiResponse>(StatusCodes.Status202Accepted)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status502BadGateway);

        agents.MapPost("/providers/{providerId:guid}/test-chat", RunAgentProviderTestChatAsync)
            .WithName("RunAgentProviderTestChat")
            .Produces<ProviderTestChatResult>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound, StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/providers/{providerId:guid}/ollama-modelfile", CreateAgentProviderModelMaintenanceAsync)
            .WithName("CreateAgentProviderModelMaintenance")
            .Produces<ProviderModelMaintenanceEditorResult>(StatusCodes.Status200OK)
            .Produces<ProviderCommittedApiResponse>(StatusCodes.Status202Accepted)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapCapabilityEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/capabilities", ListAgentCapabilitiesAsync)
            .WithName("ListAgentCapabilities")
            .Produces<CapabilityCatalogItem[]>(StatusCodes.Status200OK);

        agents.MapGet("/capabilities/{capabilityId:guid}/editor", GetAgentCapabilityEditorAsync)
            .WithName("GetAgentCapabilityEditor")
            .Produces<CapabilityEditorModel>(StatusCodes.Status200OK);

        agents.MapPost("/capabilities", SaveAgentCapabilityAsync)
            .WithName("SaveAgentCapability")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapDelete("/capabilities/{capabilityId:guid}", DeleteAgentCapabilityAsync)
            .WithName("DeleteAgentCapability")
            .Produces<ApiAck>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/capabilities/{capabilityId:guid}/verify", VerifyAgentCapabilityAsync)
            .WithName("VerifyAgentCapability")
            .Produces<ApiAck>()
            .Produces<CapabilityVerificationApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<CapabilityVerificationApiResponse>(StatusCodes.Status409Conflict);

        agents.MapPost("/capabilities/setup-tests/tool", TestAgentToolCapabilitySetupAsync)
            .WithName("TestAgentToolCapabilitySetup")
            .Produces<CapabilitySetupTestResult>(StatusCodes.Status200OK);

        agents.MapPost("/capabilities/setup-tests/mcp", TestAgentMcpCapabilitySetupAsync)
            .WithName("TestAgentMcpCapabilitySetup")
            .Produces<McpSetupTestResult>(StatusCodes.Status200OK);

        agents.MapPost("/capabilities/access-preview", PreviewAgentCapabilityAccessAsync)
            .WithName("PreviewAgentCapabilityAccess")
            .Produces<CapabilityAccessPreviewResult>(StatusCodes.Status200OK);
    }

    private static void MapMemoryEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/memory", ListAgentMemoryAsync)
            .WithName("ListAgentMemory")
            .Produces<AgentMemoryRecord[]>(StatusCodes.Status200OK);

        agents.MapPost("/memory", SaveAgentMemoryAsync)
            .WithName("SaveAgentMemory")
            .Produces<Guid>(StatusCodes.Status200OK);

        agents.MapDelete("/memory/{memoryId:guid}", DeleteAgentMemoryAsync)
            .WithName("DeleteAgentMemory")
            .Produces<ApiAck>(StatusCodes.Status200OK);
    }

    private static void MapChatEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/chat-sessions", ListAgentChatSessionsAsync)
            .WithName("ListAgentChatSessions")
            .Produces<AgentChatSessionApiResponse[]>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat-sessions", CreateAgentChatSessionAsync)
            .WithName("CreateAgentChatSession")
            .Produces<AgentChatSessionApiResponse>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat-sessions/{chatSessionId:guid}/rename", RenameAgentChatSessionAsync)
            .WithName("RenameAgentChatSession")
            .Produces<AgentChatSessionApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/chat-workspace", GetAgentChatWorkspaceAsync)
            .WithName("GetAgentChatWorkspace")
            .Produces<AgentChatWorkspaceApiResponse>(StatusCodes.Status200OK);

        agents.MapPost("/{agentId:guid}/chat", SendAgentChatMessageAsync)
            .WithName("SendAgentChatMessage")
            .Produces<AgentChatRunApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable);

        agents.MapPost("/execution-runs/{executionRunId:guid}/recover", RecoverAgentExecutionRunAsync)
            .WithName("RecoverAgentExecutionRun")
            .Produces<AgentExecutionRunResultApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError,
                StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(agents, ApiAuthorizationPolicies.GeneralApi);

        agents.MapPost(
                "/execution-runs/{executionRunId:guid}/reconcile-cancellation",
                ReconcileCancelledAgentExecutionRunAsync)
            .WithName("ReconcileCancelledAgentExecutionRun")
            .Produces<AgentCancellationReconciliationApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict,
                StatusCodes.Status410Gone,
                StatusCodes.Status503ServiceUnavailable)
            .ApplyApiAuthorization(agents, ApiAuthorizationPolicies.GeneralApi);

        agents.MapPost(
                "/execution-runs/{executionRunId:guid}/pending-approvals",
                RespondToAgentExecutionApprovalsAsync)
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
        agents.MapPost("/execution-runs", StartAgentExecutionRunAsync)
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

        agents.MapPost("/{agentId:guid}/execution-runs", StartAgentScopedExecutionRunAsync)
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

        agents.MapGet("/execution-runs", ListAgentExecutionRunsAsync)
            .WithName("ListAgentExecutionRuns")
            .Produces<AgentExecutionRunApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs", ListAgentScopedExecutionRunsAsync)
            .WithName("ListAgentScopedExecutionRuns")
            .Produces<AgentExecutionRunApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/execution-runs/{executionRunId:guid}", GetAgentExecutionRunDetailAsync)
            .WithName("GetAgentExecutionRunDetail")
            .Produces<AgentExecutionRunDetailApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}", GetAgentScopedExecutionRunDetailAsync)
            .WithName("GetAgentScopedExecutionRunDetail")
            .Produces<AgentExecutionRunDetailApiResponse>(StatusCodes.Status200OK)
            .ProducesApiErrors(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/artifacts", ListAgentExecutionArtifactsAsync)
            .WithName("ListAgentExecutionArtifacts")
            .Produces<AgentExecutionArtifactApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet(
                "/{agentId:guid}/execution-runs/{executionRunId:guid}/artifacts",
                ListAgentScopedExecutionArtifactsAsync)
            .WithName("ListAgentScopedExecutionArtifacts")
            .Produces<AgentExecutionArtifactApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/checkpoints", ListAgentExecutionCheckpointsAsync)
            .WithName("ListAgentExecutionCheckpoints")
            .Produces<AgentExecutionCheckpointApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet(
                "/{agentId:guid}/execution-runs/{executionRunId:guid}/checkpoints",
                ListAgentScopedExecutionCheckpointsAsync)
            .WithName("ListAgentScopedExecutionCheckpoints")
            .Produces<AgentExecutionCheckpointApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/tool-receipts", ListAgentExecutionToolReceiptsAsync)
            .WithName("ListAgentExecutionToolReceipts")
            .Produces<AgentExecutionToolReceiptApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet(
                "/{agentId:guid}/execution-runs/{executionRunId:guid}/tool-receipts",
                ListAgentScopedExecutionToolReceiptsAsync)
            .WithName("ListAgentScopedExecutionToolReceipts")
            .Produces<AgentExecutionToolReceiptApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet("/{agentId:guid}/execution-runs/{executionRunId:guid}/log", ListAgentScopedExecutionLogAsync)
            .WithName("ListAgentScopedExecutionLog")
            .Produces<AgentExecutionLogApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet(
                "/{agentId:guid}/execution-runs/{executionRunId:guid}/metrics",
                ListAgentScopedExecutionMetricsAsync)
            .WithName("ListAgentScopedExecutionMetrics")
            .Produces<AgentRunMetricApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet(
                "/{agentId:guid}/execution-runs/{executionRunId:guid}/approvals",
                ListAgentScopedExecutionApprovalsAsync)
            .WithName("ListAgentScopedExecutionApprovals")
            .Produces<AgentExecutionApprovalApiResponse[]>(StatusCodes.Status200OK)
            .ProducesApiErrors(StatusCodes.Status404NotFound);

        agents.MapGet("/execution-runs/{executionRunId:guid}/approvals", ListAgentExecutionApprovalsAsync)
            .WithName("ListAgentExecutionApprovals")
            .Produces<AgentExecutionApprovalApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/execution-log", ListAgentExecutionLogAsync)
            .WithName("ListAgentExecutionLog")
            .Produces<AgentExecutionLogApiResponse[]>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/runtime-snapshot", GetAgentRuntimeSnapshotAsync)
            .WithName("GetAgentRuntimeSnapshot")
            .Produces<AgentChatRuntimeApiResponse>(StatusCodes.Status200OK);

        agents.MapGet("/{agentId:guid}/metrics", ListAgentMetricsAsync)
            .WithName("ListAgentMetrics")
            .Produces<AgentRunMetricApiResponse[]>(StatusCodes.Status200OK);
    }

    /// <summary>
    /// List the agent definitions of the current workspace, ordered by name.
    /// </summary>
    /// <remarks>
    /// Returns the technical agent definitions, the configuration and identity an agent execution run uses, in
    /// case-insensitive name order and without paging. Agents of every lifecycle status (Draft, Active, Suspended,
    /// Archived) are included; template agents are left out unless <c>includeTemplates</c> is true. The CRM/HR party
    /// that represents an agent is a projection maintained from these definitions; change the agent here, not the
    /// party.
    ///
    /// Each item is the stored definition, including its <c>configurationJson</c> string and its allowed secret
    /// references (identifiers and display names only, never secret values). To change an agent, read its editable
    /// form with <c>GET /api/agents/{agentId}</c>, which also carries the concurrency revision.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="includeTemplates">
    /// True to include template agents. Omitted or false returns only agents that are not templates.
    /// </param>
    /// <response code="200">The matching agents; an empty array when there are none.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    internal static async Task<IResult> ListAgentsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken,
        bool includeTemplates = false) =>
        Results.Ok(await workspaceService.ListAgentsAsync(includeTemplates, cancellationToken));

    /// <summary>
    /// Read the data needed to open the agent chat page: the agent list and the workspace of the initially selected
    /// agent.
    /// </summary>
    /// <remarks>
    /// Returns the agents as short catalog items in case-insensitive name order (templates only when
    /// <c>includeTemplates</c> is true), the agent to select first (the one with the most recently updated chat
    /// session, otherwise the first listed agent) and that agent's chat workspace. It only reads. For one agent's chat
    /// workspace use <c>GET /api/agents/{agentId}/chat-workspace</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="includeTemplates">
    /// True to include template agents in the list. Omitted or false lists only agents that are not templates.
    /// </param>
    /// <response code="200">
    /// The agent list, the initially selected agent and its workspace; the agent and workspace are null when there are
    /// no agents.
    /// </response>
    internal static async Task<IResult> GetAgentBootstrapAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken,
        bool includeTemplates = false) =>
        Results.Ok(AgentApiResponseMapper.ToChatPageBootstrap(
            await workspaceService.GetChatPageBootstrapAsync(includeTemplates, cancellationToken)));

    /// <summary>
    /// Read an agent definition as the editable form accepted by <c>POST /api/agents</c>.
    /// </summary>
    /// <remarks>
    /// Returns the agent's stored settings in editor form: the definition's fields, the typed access settings parsed
    /// from its <c>configurationJson</c>, the selected capability identifiers and <c>expectedUpdatedAtUtc</c>, the
    /// concurrency revision to send back when saving. This is the read step of an agent update: change only the
    /// members you intend to change and send the whole form to <c>POST /api/agents</c>.
    ///
    /// An unknown agent identifier, or stored access or thinking-effort configuration that cannot be parsed, is not
    /// translated into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent, as returned by <c>GET /api/agents</c>.</param>
    /// <response code="200">The editable form of the agent, including its current revision.</response>
    internal static async Task<IResult> GetAgentEditorAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.GetAgentEditorAsync(agentId, cancellationToken));

    /// <summary>
    /// Create an agent definition, or replace an existing one using the revision the caller last read.
    /// </summary>
    /// <remarks>
    /// A save stores the complete agent from the body: every member is taken from the request, and an omitted member
    /// falls back to its default (for example status Draft, default permissions, no capabilities, no access
    /// settings), so always send the whole editor form. Do not send JSON null for text, list or object members: it is
    /// not translated into the error envelope and fails with a generic HTTP 500.
    ///
    /// To create an agent, send <c>id</c> and <c>expectedUpdatedAtUtc</c> as null. To update one:
    ///
    /// 1. Read <c>GET /api/agents/{agentId}</c>.
    /// 2. Change only the members you intend to change; keep <c>id</c> and <c>expectedUpdatedAtUtc</c> as read.
    /// 3. Send the whole form, then read the agent again for the stored values and the new revision.
    ///
    /// When <c>expectedUpdatedAtUtc</c> is present it must equal the stored revision exactly; otherwise nothing is
    /// saved and the request is rejected with HTTP 400. When it is null, an existing agent is overwritten without a
    /// concurrency check. An <c>id</c> that does not exist yet creates the agent with that identifier.
    ///
    /// The server trims text, derives the template key from <c>templateKey</c> or, when blank, from <c>name</c>, drops
    /// unknown capability identifiers and duplicate tags, replaces <c>permissions.allowedSecrets</c> with
    /// <c>allowedSecretReferences</c>, and writes each typed access setting over its key in <c>configurationJson</c>
    /// (a value that is not a JSON object is replaced by an empty object). Saved configuration never attaches runtime
    /// tools by itself: each run still applies the agent's permissions, capabilities, access settings and invocation
    /// policy.
    ///
    /// The agent is committed before the CRM/HR directory projection is refreshed. If that refresh fails, the request
    /// fails with a generic HTTP 500 although the agent is saved; list the agents before retrying, because a repeated
    /// create is then rejected for its already used template key.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete agent form.</param>
    /// <response code="200">
    /// The agent was saved. The body is the agent identifier as a JSON string (a GUID); read the agent to get the
    /// stored values and its new revision.
    /// </response>
    /// <response code="400">
    /// Nothing was saved (<c>agents.request-invalid</c>). Causes: <c>expectedUpdatedAtUtc</c> differs from the stored
    /// revision or names an agent that does not exist; the template key has no letter or digit or already belongs to
    /// another agent or template; the provider profile does not exist; the model is not allowed by a shared provider or
    /// has no price row on the provider profile; the thinking effort is not supported by the provider and model; a
    /// project that does not exist is granted for the first time; or an access setting is invalid. A body the framework
    /// cannot bind is rejected with HTTP 400 without an envelope.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    internal static async Task<IResult> SaveAgentAsync(
        AgentEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await SaveAgentResultAsync(request, workspaceService, cancellationToken);

    /// <summary>
    /// Delete an agent definition together with its chat sessions, execution history and memory notes.
    /// </summary>
    /// <remarks>
    /// Removes the agent definition, its chat sessions, its execution runs with their log, metrics, approvals,
    /// artifacts, checkpoints and tool receipts, its workspace memory notes, its team memberships and its external-key
    /// bindings. This cannot be undone; export the agent first (<c>GET /api/agents/{agentId}/export</c>) when a copy
    /// is needed. The CRM/HR party that represented the agent is kept, with its binding to the agent marked as missing.
    ///
    /// Deleting an identifier that does not exist succeeds, so a repeated delete is harmless. The all-zero identifier
    /// is not translated into the error envelope and fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent to delete.</param>
    /// <response code="200">
    /// The agent no longer exists (it was deleted or never existed). The body is <c>{ "ok": true }</c>.
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    /// <response code="409">
    /// The agent was not deleted: it is managed by the product and cannot be deleted
    /// (<c>agents.delete-managed-seed</c>), or one of its runs is still preparing, running, waiting on a tool or
    /// persisting, or has tool effects that are not resolved (<c>agents.delete-active-execution</c>). Let the run
    /// finish or resolve it, then delete again.
    /// </response>
    internal static async Task<IResult> DeleteAgentAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Create a new agent definition that copies an existing agent or template.
    /// </summary>
    /// <remarks>
    /// Creates an agent that is not a template, with a new identifier, the name <c>cloneName</c> and a template key
    /// derived from that name. Everything else is copied from the source: its status (an archived source gives an
    /// archived clone), provider and model, instructions, permissions and allowed secret references, capability
    /// assignments with their proof state, access settings, tags and avatar. Chat sessions, runs, memory notes, team
    /// memberships and external-key bindings are not copied. Cloning a template is how a template becomes a working
    /// agent; change the clone afterwards through <c>GET /api/agents/{agentId}</c> and <c>POST /api/agents</c>.
    ///
    /// An unknown source, a name without any letter or digit, or a name whose derived template key already belongs to
    /// another agent is not translated into the error envelope: the request fails with a generic HTTP 500 and nothing
    /// is created. The clone is committed before the CRM/HR directory projection is refreshed; if that refresh fails,
    /// HTTP 500 is returned although the clone exists, so list the agents before retrying.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent or template to copy.</param>
    /// <param name="request">Name of the new agent.</param>
    /// <response code="200">
    /// The clone was created. The body is the new agent's identifier as a JSON string (a GUID).
    /// </response>
    internal static async Task<IResult> CloneAgentAsync(
        Guid agentId,
        AgentCloneApiRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.CloneAgentAsync(agentId, request.CloneName, cancellationToken));

    /// <summary>
    /// Create a template agent from an existing agent, leaving the source agent unchanged.
    /// </summary>
    /// <remarks>
    /// Despite its route, this does not convert the agent in place: it adds a new template agent with a new identifier,
    /// the name of the source followed by " template" and the given template key, copying the source's other
    /// settings. The source agent and its history are unchanged. Create working agents from the template with
    /// <c>POST /api/agents/{agentId}/clone</c>.
    ///
    /// The template key is normalized to lower-case letters and digits separated by hyphens and must not already
    /// belong to another agent or template. A blank key falls back to the source name, which normally collides with
    /// the source agent's own key, so send an explicit key. An unknown agent or an unusable or duplicate key is not
    /// translated into the error envelope: the request fails with a generic HTTP 500 and nothing is created. The
    /// template is committed before the CRM/HR directory projection is refreshed; if that refresh fails, HTTP 500 is
    /// returned although the template exists, so list the agents with <c>includeTemplates=true</c> before retrying.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent to copy into a template.</param>
    /// <param name="request">Template key of the new template.</param>
    /// <response code="200">
    /// The template was created. The body is the new template's agent identifier as a JSON string (a GUID).
    /// </response>
    internal static async Task<IResult> ConvertAgentToTemplateAsync(
        Guid agentId,
        AgentTemplateConversionApiRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ConvertToTemplateAsync(agentId, request.TemplateKey, cancellationToken));

    /// <summary>
    /// Write an export package of an agent on the server and return the package's server path.
    /// </summary>
    /// <remarks>
    /// Although this is a GET, every call writes a new ZIP package into the workspace's export folder on the server
    /// and returns its absolute server-side path. The package itself is not returned, and this API offers no download
    /// of it; the path is meant for <c>POST /api/agents/import</c> on the same host or for an operator with file
    /// access. Repeated calls create additional packages.
    ///
    /// The package contains the agent definition, its chat sessions, its execution runs with their log, metrics,
    /// approvals, artifacts, checkpoints and tool receipts, its memory notes, its own provider profile and its assigned
    /// capabilities. Secrets appear only as references, and protected run state is removed or redacted.
    ///
    /// An unknown agent identifier is not translated into the error envelope: the request fails with a generic
    /// HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent to export.</param>
    /// <response code="200">The package was written. The body gives its server path and a short summary.</response>
    internal static async Task<IResult> ExportAgentAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ExportAgentAsync(agentId, cancellationToken));

    /// <summary>
    /// Import an agent from an export package file that already exists on the server.
    /// </summary>
    /// <remarks>
    /// Reads the ZIP package at <c>packagePath</c>, a path in the server's file system such as one returned by
    /// <c>GET /api/agents/{agentId}/export</c>, and stores its agent under the agent's original identifier together
    /// with its chat sessions, runs and their evidence, and memory notes. Its provider profile and capabilities are
    /// matched to existing ones by identifier or identity and otherwise added to the agent catalog. To upload a package
    /// from the client instead, use <c>POST /api/agents/import-package</c>.
    ///
    /// If an agent with the same identifier exists, it is replaced without any check for active runs: its chat
    /// sessions, runs, evidence and memory notes are replaced by the package's copies, and its external-key bindings
    /// are deleted because packages carry none (bind it again with
    /// <c>PUT /api/agents/by-external-key/{externalNamespace}/{key}</c>). Team memberships are kept. Treat an import
    /// over an existing agent as destructive.
    ///
    /// The package must use schema version 1.0, contain only the known entries (at most 64), stay within 32 MiB
    /// compressed and 128 MiB expanded, and contain no raw secret values. A missing file, an invalid package, an
    /// ambiguous provider or capability match or a conflicting template key is not translated into the error
    /// envelope: the request fails with a generic HTTP 500. The agent is committed before the CRM/HR directory
    /// projection is refreshed; if that refresh fails, HTTP 500 is returned although the import is stored.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">Server path of the package to import.</param>
    /// <response code="200">
    /// The agent was imported. The body is its identifier as a JSON string (a GUID), the identifier stored in the
    /// package.
    /// </response>
    internal static async Task<IResult> ImportAgentAsync(
        AgentImportApiRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ImportAgentAsync(request.PackagePath, cancellationToken));

    /// <summary>
    /// List all agent teams of the current workspace, ordered by name.
    /// </summary>
    /// <remarks>
    /// An agent team is a named group of agent definitions used to organize agents; an agent can belong to several
    /// teams. A team does not grant capabilities or tools and does not change how its member agents run. Teams are
    /// returned in case-insensitive name order, each with its member agent identifiers. Use a team's <c>id</c> with
    /// the other <c>/api/agents/teams/{teamId}</c> operations.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">Every team of the workspace; an empty array when none exist.</response>
    internal static async Task<IResult> ListAgentTeamsAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ListAgentTeamsAsync(cancellationToken));

    /// <summary>
    /// Read one agent team with its member agent identifiers.
    /// </summary>
    /// <remarks>
    /// Returns the stored team. To edit it, read <c>GET /api/agents/teams/{teamId}/editor</c> and save the changed
    /// form with <c>PUT /api/agents/teams/{teamId}</c>; to see the member agents' definitions, use
    /// <c>GET /api/agents/teams/{teamId}/agents</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of the team, as returned by <c>GET /api/agents/teams</c> or a team save.</param>
    /// <response code="200">The team.</response>
    /// <response code="404">No team has this identifier (<c>agents.team-not-found</c>).</response>
    internal static async Task<IResult> GetAgentTeamAsync(
        Guid teamId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var team = (await workspaceService.ListAgentTeamsAsync(cancellationToken))
            .FirstOrDefault(item => item.Id == teamId);
        return team is null
            ? ApiEndpointResults.NotFound("Agent team was not found.", "agents.team-not-found")
            : Results.Ok(team);
    }

    /// <summary>
    /// Read an agent team as the editable form accepted by the team save operations.
    /// </summary>
    /// <remarks>
    /// Returns the team's name, description, icon and member agent identifiers in the shape expected by
    /// <c>PUT /api/agents/teams/{teamId}</c> and <c>POST /api/agents/teams</c>. The team save operations have no
    /// concurrency check: the last save wins.
    ///
    /// An unknown team identifier is not translated into the error envelope; the request fails with a generic
    /// HTTP 500. Use <c>GET /api/agents/teams/{teamId}</c> first when you need a 404 for a missing team.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of an existing team, as returned by <c>GET /api/agents/teams</c>.</param>
    /// <response code="200">The editable form of the team.</response>
    internal static async Task<IResult> GetAgentTeamEditorAsync(
        Guid teamId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.GetAgentTeamEditorAsync(teamId, cancellationToken));

    /// <summary>
    /// List the agent definitions that are members of an agent team.
    /// </summary>
    /// <remarks>
    /// Returns the full definitions of the team's member agents in case-insensitive agent-name order, the same shape as
    /// <c>GET /api/agents</c>. Template agents are members like any other agent but are left out unless
    /// <c>includeTemplates</c> is true, so a team of templates can yield an empty array. The team and the agents are
    /// read separately, so a concurrent change can make the result differ from the team's <c>agentIds</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of the team, as returned by <c>GET /api/agents/teams</c>.</param>
    /// <param name="includeTemplates">
    /// True to include member agents that are templates. Omitted or false returns only non-template agents.
    /// </param>
    /// <response code="200">The member agents; an empty array when the team has no matching members.</response>
    /// <response code="404">No team has this identifier (<c>agents.team-not-found</c>).</response>
    internal static async Task<IResult> ListAgentTeamAgentsAsync(
        Guid teamId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken,
        bool includeTemplates = false)
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
    }

    /// <summary>
    /// Create an agent team, or replace the team whose identifier the body carries.
    /// </summary>
    /// <remarks>
    /// With <c>id</c> null or omitted the server creates a team with a new identifier. With an <c>id</c> the stored
    /// team with that identifier is replaced (its creation time is kept); if none exists, a team is created with that
    /// identifier. Prefer <c>PUT /api/agents/teams/{teamId}</c> for updates so the identifier comes from the route.
    /// There is no concurrency check: the last save wins.
    ///
    /// The name is trimmed and must be non-blank and unique among teams, ignoring case. The description is trimmed.
    /// An icon outside the supported set becomes <c>groups</c>. Member agent identifiers must name existing agents;
    /// empty identifiers and duplicates are dropped, and members are stored in agent-name order.
    ///
    /// These validation failures (blank or duplicate name, unknown agent) are not translated into the error envelope:
    /// the request fails with a generic HTTP 500 and nothing is saved.
    ///
    /// A success does not always mean the values were kept: teams shipped with the product are reset to their shipped
    /// content on every catalog write, and a team sent with the all-zero identifier is discarded. Read the team back
    /// with <c>GET /api/agents/teams/{teamId}</c> to confirm the stored result.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete team to store.</param>
    /// <response code="200">
    /// The team was saved. The body is the team identifier as a JSON string (a GUID); read the team to see the stored
    /// values.
    /// </response>
    internal static async Task<IResult> SaveAgentTeamAsync(
        AgentTeamEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.SaveAgentTeamAsync(request, cancellationToken));

    /// <summary>
    /// Replace an agent team's name, description, icon and members.
    /// </summary>
    /// <remarks>
    /// Stores the body as the complete team, using the route identifier and ignoring any <c>id</c> in the body. If no
    /// team has this identifier, a team is created with it. Every member is replaced by <c>agentIds</c>; to change only
    /// the members, use <c>PUT /api/agents/teams/{teamId}/members</c>. There is no concurrency check: the last save
    /// wins.
    ///
    /// The name is trimmed and must be non-blank and unique among teams, ignoring case. An icon outside the supported
    /// set becomes <c>groups</c>. Member agent identifiers must name existing agents; empty identifiers and duplicates
    /// are dropped. These validation failures are not translated into the error envelope: the request fails with a
    /// generic HTTP 500 and nothing is saved.
    ///
    /// Teams shipped with the product are reset to their shipped content on every catalog write, so changes to them
    /// report success but are not kept; the all-zero identifier is likewise discarded. Read the team back to confirm
    /// the stored result.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of the team to replace, as returned by a team read or save.</param>
    /// <param name="request">The complete team to store; its <c>id</c> is replaced by the route value.</param>
    /// <response code="200">The team was saved. The body is the team identifier as a JSON string (a GUID).</response>
    internal static async Task<IResult> UpdateAgentTeamAsync(
        Guid teamId,
        AgentTeamEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        request.Id = teamId;
        return Results.Ok(await workspaceService.SaveAgentTeamAsync(request, cancellationToken));
    }

    /// <summary>
    /// Replace the member list of an agent team (POST form of the members replacement).
    /// </summary>
    /// <remarks>
    /// Behaves exactly like <c>PUT /api/agents/teams/{teamId}/members</c>: the team's members become the agents in
    /// <c>agentIds</c>, and agents not listed are removed from the team. This is a replacement, not an append; an
    /// empty list leaves the team without members. The team's name, description and icon are unchanged, and the
    /// member agents themselves are not modified.
    ///
    /// Every identifier must name an existing agent; empty identifiers and duplicates are dropped, and members are
    /// stored in agent-name order. An unknown team or agent is not translated into the error envelope: the request
    /// fails with a generic HTTP 500 and the team is unchanged.
    ///
    /// Teams shipped with the product are reset to their shipped members on every catalog write; for them the response
    /// shows the requested members although the stored team keeps its shipped members. Read the team back to confirm.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of an existing team, as returned by <c>GET /api/agents/teams</c>.</param>
    /// <param name="request">The complete list of member agent identifiers.</param>
    /// <response code="200">
    /// The members were replaced. The body is the team with the requested members; read it back to confirm what was
    /// stored.
    /// </response>
    internal static async Task<IResult> UpdateAgentTeamMembersAsync(
        Guid teamId,
        AgentTeamMembersApiRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.UpdateAgentTeamMembersAsync(teamId, request.AgentIds, cancellationToken));

    /// <summary>
    /// Replace the member list of an agent team.
    /// </summary>
    /// <remarks>
    /// The team's members become the agents in <c>agentIds</c>, and agents not listed are removed from the team. This
    /// is a replacement, not an append; an empty list leaves the team without members. The team's name, description
    /// and icon are unchanged, and the member agents themselves are not modified. <c>POST</c> on the same route behaves
    /// identically.
    ///
    /// Every identifier must name an existing agent; empty identifiers and duplicates are dropped, and members are
    /// stored in agent-name order. An unknown team or agent is not translated into the error envelope: the request
    /// fails with a generic HTTP 500 and the team is unchanged.
    ///
    /// Teams shipped with the product are reset to their shipped members on every catalog write; for them the response
    /// shows the requested members although the stored team keeps its shipped members. Read the team back to confirm.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of an existing team, as returned by <c>GET /api/agents/teams</c>.</param>
    /// <param name="request">The complete list of member agent identifiers.</param>
    /// <response code="200">
    /// The members were replaced. The body is the team with the requested members; read it back to confirm what was
    /// stored.
    /// </response>
    internal static async Task<IResult> ReplaceAgentTeamMembersAsync(
        Guid teamId,
        AgentTeamMembersApiRequest request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.UpdateAgentTeamMembersAsync(teamId, request.AgentIds, cancellationToken));

    /// <summary>
    /// Delete an agent team without affecting its member agents.
    /// </summary>
    /// <remarks>
    /// Removes the team record only; the member agent definitions, their runs and their memberships in other teams
    /// are unchanged. Deleting an identifier that does not exist also succeeds, so a repeated delete is harmless.
    /// Teams shipped with the product are restored by the same catalog write, so deleting one reports success but the
    /// team remains.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="teamId">Identifier of the team to delete.</param>
    /// <response code="200">
    /// The delete completed: the team is gone, or it never existed, unless it is a team shipped with the product. The
    /// body is <c>{ "ok": true }</c>.
    /// </response>
    internal static async Task<IResult> DeleteAgentTeamAsync(
        Guid teamId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        await workspaceService.DeleteAgentTeamAsync(teamId, cancellationToken);
        return Results.Ok(new ApiAck(true));
    }

    /// <summary>
    /// Check whether an unconfirmed provider-profile write took effect.
    /// </summary>
    /// <remarks>
    /// Use it after a provider operation answered HTTP 409 with code <c>agents.provider-write-unconfirmed</c>: send
    /// that response's <c>attempt</c> object unchanged as the body. The server compares the receipt with the stored
    /// provider profile and reports <c>Committed</c>, <c>DefinitelyNotCommitted</c> or <c>StillUnconfirmed</c>. The
    /// check only reads: it never repeats or undoes the write, and it can be called again.
    ///
    /// Next step: after <c>Committed</c>, read the profile with <c>GET /api/agents/providers/{providerId}/editor</c>
    /// and continue from its stored state; after <c>DefinitelyNotCommitted</c>, read the profile and submit the
    /// intended change again if it is still wanted; after <c>StillUnconfirmed</c>, verify again later instead of
    /// writing.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="attempt">The receipt from the unconfirmed-write response, unchanged.</param>
    /// <response code="200">
    /// The verification result. It reflects the stored profile at the time of the check and is never cached.
    /// </response>
    /// <response code="400">
    /// The receipt has an all-zero <c>attemptId</c> or <c>providerId</c> or an undefined <c>kind</c>
    /// (<c>agents.provider-receipt-invalid</c>). The body has <c>code</c> and <c>message</c> members, not the
    /// <c>errors</c> envelope. A body the framework cannot bind is rejected with HTTP 400 without this body.
    /// </response>
    internal static async Task<IResult> VerifyAgentProviderMutationAsync(
        ProviderMutationAttempt attempt,
        HttpContext context,
        IProviderMutationVerification verification,
        CancellationToken cancellationToken)
    {
        if (attempt.ProviderId == Guid.Empty || attempt.AttemptId == Guid.Empty || !Enum.IsDefined(attempt.Kind))
        {
            return Results.BadRequest(new
            {
                Code = "agents.provider-receipt-invalid",
                Message = "The mutation receipt is invalid."
            });
        }

        var result = await verification.VerifyAsync(attempt, cancellationToken);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new ProviderVerificationApiResponse(result.ProviderId, result.Disposition,
            result.ConcurrencyToken, false));
    }

    /// <summary>
    /// List the model provider profiles that agents can use.
    /// </summary>
    /// <remarks>
    /// A provider profile is a configured connection to a model provider (OpenAI-compatible, Azure OpenAI, Ollama or
    /// ComfyUI) that an agent references through <c>providerProfileId</c>; the model an agent uses is a separate string
    /// within the profile. Returns every local profile, enabled or not, and every profile imported from a shared
    /// provider source (<c>isSourceManaged</c> true), including imports that are currently unavailable
    /// (<c>isEnabled</c> false, <c>healthStatus</c> naming the reason), ordered by name. A built-in fallback profile
    /// for a remote Ollama server is included unless the host disables it. Publishing a local profile to other hosts (a
    /// shared-provider publication) is managed elsewhere and is not shown here.
    ///
    /// API keys are never returned: <c>apiKeyEnvironmentVariable</c> holds at most a reference to a stored secret.
    /// The stored <c>configurationJson</c> is returned as saved apart from that reference.
    ///
    /// A profile that cannot be read makes the whole list fail with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The provider profiles.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    internal static async Task<IResult> ListAgentProvidersAsync(
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        Results.Ok(await providerAdministration.ListProvidersAsync(cancellationToken));

    /// <summary>
    /// Read a provider profile as the editable form accepted by <c>POST /api/agents/providers</c>.
    /// </summary>
    /// <remarks>
    /// Returns the profile's settings together with <c>expectedConcurrencyToken</c>, the token to send back when
    /// saving a change. Profiles imported from a shared provider source can be read here but cannot be saved.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="providerId">
    /// Identifier of the provider profile, as returned by <c>GET /api/agents/providers</c>.
    /// </param>
    /// <response code="200">The editable form of the profile, including its concurrency token.</response>
    /// <response code="404">
    /// No provider profile has this identifier, or the link of an imported profile to its source is broken
    /// (<c>agents.provider-not-found</c>).
    /// </response>
    /// <response code="503">
    /// The profile is imported from a shared provider source and its import is retired
    /// (<c>agents.provider-unavailable</c>).
    /// </response>
    internal static async Task<IResult> GetAgentProviderEditorAsync(
        Guid providerId,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        await ProviderApiResults.ExecuteAsync(context, async () =>
            Results.Ok(await providerAdministration.GetProviderEditorAsync(providerId, cancellationToken)));

    /// <summary>
    /// Create a provider profile, or replace a local one using the concurrency token the caller last read.
    /// </summary>
    /// <remarks>
    /// To create a profile, send <c>id</c> null (the server assigns one) or a new identifier, with
    /// <c>expectedConcurrencyToken</c> null; send the all-zero GUID as <c>expectedConcurrencyToken</c> instead to make
    /// the create fail with HTTP 409 when a profile with that <c>id</c> already exists. To update one, read
    /// <c>GET /api/agents/providers/{providerId}/editor</c>, change what you intend to change and send the whole form
    /// with its <c>expectedConcurrencyToken</c>; a stale token is rejected with HTTP 409. Profiles imported from a
    /// shared provider source cannot be saved.
    ///
    /// The server checks the kind, transport and purpose (Ollama requires the ChatCompletions transport), the name (not
    /// blank), the base URL (an absolute http or https URL without user information, query or fragment), the credential
    /// reference (<c>apiKeyEnvironmentVariable</c> is empty or <c>secret:</c> followed by the identifier of a stored
    /// secret; OpenAI and Azure OpenAI profiles require one), <c>configurationJson</c> (a JSON object), the model
    /// prices and the thinking-effort capabilities. A blank default model becomes the connector's default model.
    /// <c>notes</c>, <c>preferFrameworkManagedChatHistory</c> and <c>supportsBackgroundResponses</c> are not stored.
    ///
    /// HTTP 200 means the profile is stored, including when only the follow-up reconciliation after the commit failed
    /// (header <c>CDA-Provider-Outcome: committed-reconciliation-pending</c>); do not send it again. When the outcome
    /// of the write is unknown, HTTP 409 carries an unconfirmed-write receipt instead of the <c>errors</c> envelope:
    /// code <c>agents.provider-write-unconfirmed</c>, members <c>providerId</c>, <c>attempt</c>,
    /// <c>automaticReplaySafe</c> (false), <c>verificationPath</c> and <c>message</c>, and the header
    /// <c>CDA-Provider-Outcome: unconfirmed-verification-required</c>. Send <c>attempt</c> to <c>POST
    /// /api/agents/providers/mutations/verify</c> before any retry.
    ///
    /// Some invalid thinking-effort capability entries and over-long model lists are not translated into the error
    /// envelope: the request fails with a generic HTTP 500 before anything is written.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete provider profile form.</param>
    /// <response code="200">
    /// The profile is stored. The body is the provider profile identifier as a JSON string (a GUID).
    /// </response>
    /// <response code="400">
    /// Nothing was saved (<c>agents.provider-request-invalid</c>): a validation rule failed, the identifier is the
    /// all-zero GUID, or the profile is imported from a shared provider source. The response does not say which rule
    /// failed. A body the framework cannot bind is rejected with HTTP 400 without an envelope.
    /// </response>
    /// <response code="409">
    /// The profile changed since it was read, or a profile with the requested identifier exists although the create
    /// demanded none (<c>agents.provider-concurrency-conflict</c>): read the editor again and reapply the change. Or
    /// the outcome is unknown and the body is an unconfirmed-write receipt (code
    /// <c>agents.provider-write-unconfirmed</c>) to verify first.
    /// </response>
    internal static async Task<IResult> SaveAgentProviderAsync(
        ProviderProfileEditorModel request,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        await SaveProviderResultAsync(
            request,
            context,
            providerAdministration,
            cancellationToken);

    /// <summary>
    /// Delete a local provider profile.
    /// </summary>
    /// <remarks>
    /// Deletes the profile. Agents that referenced it keep their definitions but lose the reference
    /// (<c>providerProfileId</c> becomes null), so their runs fail until another profile is selected. A profile that is
    /// or ever was published as a shared provider, or that is imported from a shared provider source, cannot be
    /// deleted. Deleting an identifier that does not exist also succeeds.
    ///
    /// HTTP 200 is also returned when only the follow-up reconciliation after the delete failed (header
    /// <c>CDA-Provider-Outcome: committed-reconciliation-pending</c>). When the outcome is unknown, HTTP 409 carries an
    /// unconfirmed-write receipt (see <c>POST /api/agents/providers</c>) to send to
    /// <c>POST /api/agents/providers/mutations/verify</c> before any retry.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="providerId">Identifier of the provider profile to delete.</param>
    /// <response code="200">
    /// The profile no longer exists (it was deleted or never existed). The body is <c>{ "ok": true }</c>.
    /// </response>
    /// <response code="409">
    /// The profile was not deleted because it is referenced by a shared-provider publication or import
    /// (<c>agents.provider-reference-conflict</c>) or changed concurrently
    /// (<c>agents.provider-concurrency-conflict</c>); or the outcome of the delete is unknown and the body is an
    /// unconfirmed-write receipt (code <c>agents.provider-write-unconfirmed</c>) to verify first.
    /// </response>
    internal static async Task<IResult> DeleteAgentProviderAsync(
        Guid providerId,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        await ProviderApiResults.ExecuteAsync(
            context,
            async () =>
            {
                await providerAdministration.DeleteProviderAsync(providerId, cancellationToken);
                return Results.Ok(new ApiAck(true));
            },
            _ => Results.Ok(new ApiAck(true)));

    /// <summary>
    /// Run a health check against a provider profile and store the result on the profile.
    /// </summary>
    /// <remarks>
    /// Calls the provider with the profile's connection settings, for example by listing its models and sending a
    /// short chat request (which the provider may bill) for an OpenAI-compatible chat profile, or by listing the models
    /// and sending a chat request to an Ollama server. Local profiles are tested even when disabled, and the result is
    /// stored on the profile: <c>healthStatus</c> becomes Healthy or Unhealthy, <c>lastCheckedAtUtc</c> is set, the
    /// suggested models are replaced by the discovered ones and, for Ollama, discovered thinking-effort capabilities
    /// are stored. Storing the result changes the profile's concurrency token. Profiles imported from a shared provider
    /// source are checked without storing anything, and an unavailable import reports a failure without calling it.
    ///
    /// A provider that answers with an error yields HTTP 200 with <c>success</c> false. The check is recorded in the
    /// provider request history as a diagnostic.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="providerId">Identifier of the provider profile to test.</param>
    /// <response code="200">
    /// The check finished; <c>success</c> tells whether the provider answered as expected. For a local profile the
    /// result is stored.
    /// </response>
    /// <response code="202">
    /// The result was stored, but the follow-up reconciliation did not finish (header
    /// <c>CDA-Provider-Outcome: committed-reconciliation-pending</c>). The check result itself is not returned; read
    /// the profile list for the stored health.
    /// </response>
    /// <response code="400">
    /// The result could not be stored because the stored profile no longer passes validation, for example because its
    /// secret was deleted (<c>agents.provider-request-invalid</c>).
    /// </response>
    /// <response code="404">No provider profile has this identifier (<c>agents.provider-not-found</c>).</response>
    /// <response code="409">
    /// Storing the result conflicted with a concurrent change (<c>agents.provider-concurrency-conflict</c>), or its
    /// outcome is unknown and the body is an unconfirmed-write receipt (code <c>agents.provider-write-unconfirmed</c>,
    /// kind 3 HealthPersistence) to verify with <c>POST /api/agents/providers/mutations/verify</c>.
    /// </response>
    /// <response code="502">
    /// The health check itself could not be completed (<c>agents.provider-diagnostic-unavailable</c>); nothing was
    /// stored.
    /// </response>
    internal static async Task<IResult> TestAgentProviderAsync(
        Guid providerId,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        await ProviderApiResults.ExecuteAsync(context, async () =>
            Results.Ok(await providerAdministration.TestProviderAsync(providerId, cancellationToken)));

    /// <summary>
    /// Send a test chat request through a provider profile and return the model's reply.
    /// </summary>
    /// <remarks>
    /// Sends the system prompt, the earlier messages and the prompt to the provider with the profile's settings and
    /// returns the reply. It involves no agent, chat session or execution run. The call is a real provider request that
    /// the provider may bill; it does not change the profile, but it is recorded in the provider request history with
    /// the HTTP caller as the requester.
    ///
    /// The model defaults to the profile's default model, then its first suggested model. A request without prompt
    /// and messages, a provider error or timeout, a provider that cannot chat (ComfyUI) or a model not published by a
    /// shared provider is not translated into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="providerId">Identifier of the provider profile to use.</param>
    /// <param name="request">The conversation to send.</param>
    /// <response code="200">The provider's reply, the model used and the token counts it reported.</response>
    /// <response code="404">No provider profile has this identifier (<c>agents.provider-not-found</c>).</response>
    /// <response code="503">
    /// The profile is disabled or its shared provider import is unavailable (<c>llm-chat.provider-unavailable</c>).
    /// </response>
    internal static async Task<IResult> RunAgentProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await providerAdministration.RunProviderTestChatAsync(
                providerId,
                ProviderHistoryRequestContext.WithCaller(request, context),
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
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
    }

    /// <summary>
    /// Create or update a model on an Ollama server and make it the provider profile's default model.
    /// </summary>
    /// <remarks>
    /// Asks the profile's Ollama server to create the model <c>targetModel</c> from <c>baseModel</c> with the given
    /// system prompt and context length (the equivalent of an Ollama Modelfile), then updates the profile: its default
    /// model becomes <c>targetModel</c>, the model is added to the suggested models and <c>healthStatus</c> shows the
    /// server's status message. The model is created on the server before the profile is updated, so a failure of the
    /// profile update leaves the model in place.
    ///
    /// Only local Ollama profiles are supported. Missing values, a context length outside 2048 to 262144 and profiles
    /// of another kind are not translated into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="providerId">Identifier of a local Ollama provider profile.</param>
    /// <param name="request">The model to create and its settings.</param>
    /// <response code="200">
    /// The model was created or updated and the profile updated. The body describes the model.
    /// </response>
    /// <response code="202">
    /// The model was created and the profile update stored, but the follow-up reconciliation did not finish (header
    /// <c>CDA-Provider-Outcome: committed-reconciliation-pending</c>).
    /// </response>
    /// <response code="400">
    /// The profile is imported from a shared provider source, or the updated profile does not pass validation
    /// (<c>agents.provider-request-invalid</c>).
    /// </response>
    /// <response code="404">No provider profile has this identifier (<c>agents.provider-not-found</c>).</response>
    /// <response code="409">
    /// The profile update conflicted with a concurrent change (<c>agents.provider-concurrency-conflict</c>), or its
    /// outcome is unknown and the body is an unconfirmed-write receipt (code <c>agents.provider-write-unconfirmed</c>,
    /// kind 4 ModelMaintenancePersistence) to verify first. The model already exists on the server.
    /// </response>
    /// <response code="503">The profile is disabled (<c>agents.provider-unavailable</c>).</response>
    internal static async Task<IResult> CreateAgentProviderModelMaintenanceAsync(
        Guid providerId,
        ProviderModelMaintenanceEditorRequest request,
        HttpContext context,
        IProviderRuntimeAdministrationService providerAdministration,
        CancellationToken cancellationToken) =>
        await ProviderApiResults.ExecuteAsync(context, async () =>
            Results.Ok(await providerAdministration.CreateOrUpdateProviderModelAsync(
                providerId,
                request,
                cancellationToken)));

    /// <summary>
    /// List the capability catalog of the current workspace.
    /// </summary>
    /// <remarks>
    /// Returns every catalog capability, the ones shipped with the product and the custom ones, ordered by kind
    /// (McpServer, Skill, Tool, Plugin, Rag, AiContext) and then by name, without paging. A catalog capability is a
    /// declaration only: it reaches an agent when it is selected in the agent's <c>selectedCapabilityIds</c>
    /// (<c>POST /api/agents</c>), and even then each run attaches tools only as the runtime's permissions and
    /// invocation policy allow. The proof fields are catalog-level results of the latest verification.
    ///
    /// Each item includes its <c>configurationJson</c> exactly as stored, without redaction.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <response code="200">The catalog capabilities.</response>
    internal static async Task<IResult> ListAgentCapabilitiesAsync(
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ListCapabilitiesAsync(cancellationToken));

    /// <summary>
    /// Read a catalog capability as the editable form accepted by <c>POST /api/agents/capabilities</c>.
    /// </summary>
    /// <remarks>
    /// Returns the capability's definition together with <c>expectedFingerprint</c>, the concurrency fingerprint to
    /// send back when saving a change. The form carries no proof fields; read them from
    /// <c>GET /api/agents/capabilities</c>.
    ///
    /// An unknown capability identifier is not translated into the error envelope: the request fails with a generic
    /// HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="capabilityId">
    /// Identifier of the capability, as returned by <c>GET /api/agents/capabilities</c>.
    /// </param>
    /// <response code="200">The editable form of the capability, including its current fingerprint.</response>
    internal static async Task<IResult> GetAgentCapabilityEditorAsync(
        Guid capabilityId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.GetCapabilityEditorAsync(capabilityId, cancellationToken));

    /// <summary>
    /// Create a catalog capability, or replace one using the fingerprint the caller last read.
    /// </summary>
    /// <remarks>
    /// Create: send <c>id</c> and <c>expectedFingerprint</c> as null; the server assigns the identifier. Update: read
    /// <c>GET /api/agents/capabilities/{capabilityId}/editor</c>, change the members you intend to change, and send
    /// the whole form with its <c>id</c> and <c>expectedFingerprint</c>. With a fingerprint the save is rejected when
    /// the stored capability changed since the read; without one the update overwrites without a check. The proof
    /// fields are kept. Changing the kind or key also updates every agent's assignment of the capability and advances
    /// those agents' revisions.
    ///
    /// The key is normalized (letters and digits lower-cased, other characters collapsed to hyphens) and must be unique
    /// per kind. Name, description and endpoint are trimmed; tags are trimmed, lower-cased and de-duplicated. The
    /// kind Memory is retired and rejected. Skill configuration must be a JSON object; the configuration of other
    /// kinds is stored as sent and is not validated.
    ///
    /// Every rejection, including a fingerprint mismatch, an unknown <c>id</c>, a fingerprint sent on create, a
    /// duplicate key or a null text member, is not translated into the error envelope: the request fails with a generic
    /// HTTP 500 and nothing is saved. Capabilities shipped with the product are restored to their shipped definition
    /// when the catalog is written, so edits to them can report success without being kept. Read the editor form
    /// again after every save.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete capability form.</param>
    /// <response code="200">
    /// The save completed. The body is the capability identifier as a JSON string (a GUID); read the editor form for
    /// the stored values and the new fingerprint.
    /// </response>
    internal static async Task<IResult> SaveAgentCapabilityAsync(
        CapabilityEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.SaveCapabilityAsync(request, cancellationToken));

    /// <summary>
    /// Delete a catalog capability and remove it from every agent that has it assigned.
    /// </summary>
    /// <remarks>
    /// Removes the capability from the catalog and its assignment from every agent, advancing those agents' revisions;
    /// assignments do not block the delete. Deleting an identifier that does not exist succeeds, so a repeated delete
    /// is harmless. Capabilities shipped with the product are restored by the same catalog write, so deleting one
    /// reports success but it remains.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="capabilityId">Identifier of the capability to delete.</param>
    /// <response code="200">
    /// The delete completed: the capability is gone, or it never existed, unless it is shipped with the product. The
    /// body is <c>{ "ok": true }</c>.
    /// </response>
    internal static async Task<IResult> DeleteAgentCapabilityAsync(
        Guid capabilityId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        await workspaceService.DeleteCapabilityAsync(capabilityId, cancellationToken);
        return Results.Ok(new ApiAck(true));
    }

    /// <summary>
    /// Run the verification diagnostic for a capability assigned to an agent and publish its proof.
    /// </summary>
    /// <remarks>
    /// Checks that the capability is assigned exactly once to the agent, runs the capability's local proof checks with
    /// the agent's current provider profile (for an HTTP endpoint this includes a GET request with a 10 second
    /// timeout), and, when the agent, the capability and the provider did not change meanwhile, stores the proof
    /// (status, notes and check time) on the agent's capability assignment and on the catalog capability. The
    /// diagnostic invokes no runtime tool and grants nothing: attaching tools to runs stays governed by the runtime.
    ///
    /// HTTP 200 means the proof was published, not that it passed: read <c>GET /api/agents</c> (with
    /// <c>includeTemplates=true</c> for a template) and inspect the agent's entry in <c>capabilities</c>, whose
    /// <c>proofStatus</c> can be Failed. Publishing advances the agent's <c>updatedAtUtc</c> revision, so an agent
    /// editor form read before the verification is stale.
    ///
    /// Failures return a <c>CapabilityVerificationApiResponse</c>, not the general <c>errors</c> envelope. Its
    /// <c>automaticReplaySafe</c> is always false: do not retry automatically. After <c>Unconfirmed</c> the proof may
    /// already be stored; read the agent before deciding whether to verify again as a new, deliberate request.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent whose capability assignment is verified.</param>
    /// <param name="capabilityId">Identifier of the catalog capability assigned to the agent.</param>
    /// <response code="200">
    /// The proof was published on the agent's assignment and on the catalog capability. The body is
    /// <c>{ "ok": true }</c>; read the agent to see the proof status, which can be Failed.
    /// </response>
    /// <response code="400">
    /// Outcome <c>Rejected</c>: the agent or capability does not exist, the capability is not assigned exactly once to
    /// the agent, or the agent's provider profile cannot be resolved. No diagnostic ran and nothing was written.
    /// </response>
    /// <response code="409">
    /// No proof was published. Outcomes: <c>InfrastructureUnavailable</c> (the catalog or provider state could not be
    /// read), <c>DiagnosticInterrupted</c> (the diagnostic failed), <c>Superseded</c> (the agent, capability or
    /// provider changed during the diagnostic), <c>PublicationNotStarted</c> (the catalog could not be read after the
    /// diagnostic), <c>Unconfirmed</c> (the publication write failed and may or may not be stored),
    /// <c>CanceledBeforeDiagnostic</c> or <c>PublicationCanceled</c> (the request was cancelled). Read the agent before
    /// deciding whether to verify again.
    /// </response>
    internal static async Task<IResult> VerifyAgentCapabilityAsync(
        Guid agentId,
        Guid capabilityId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        try
        {
            await workspaceService.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
            return Results.Ok(new ApiAck(true));
        }
        catch (CapabilityVerificationException exception)
        {
            return Results.Json(new CapabilityVerificationApiResponse(agentId, capabilityId,
                exception.Outcome.Disposition, exception.Outcome.Receipt?.AttemptId,
                exception.Outcome.Receipt?.CheckedAtUtc, AutomaticReplaySafe: false),
                statusCode: exception.Outcome.Disposition == CapabilityVerificationDisposition.Rejected
                    ? StatusCodes.Status400BadRequest : StatusCodes.Status409Conflict);
        }
    }

    /// <summary>
    /// Test a tool capability definition by calling the external process or HTTP endpoint it describes once.
    /// </summary>
    /// <remarks>
    /// Validates the tool definition in <c>capability</c> and then calls it once with <c>jsonInput</c>: it starts the
    /// configured process on the server host with the input on standard input, or sends the input as the body of a
    /// request to the configured HTTP endpoint. The definition does not have to be saved, nothing is written to the
    /// catalog, and the tool's output is not returned; the result reports success and diagnostics only.
    ///
    /// This runs the configured command or request from the server, with the server's identity and network access. The
    /// executable allow-list defaults to the command's own file name. Keep API authorization enabled on any host this
    /// operation can be reached on, and test only definitions you trust.
    ///
    /// The tool is read from <c>capability.configurationJson</c>: <c>toolKind</c> (<c>externalProcess</c>, the
    /// default, or <c>externalHttp</c>); for a process <c>externalProcess.command</c> (required), <c>arguments</c>,
    /// <c>workingDirectory</c>, <c>allowedExecutableNames</c>, <c>timeoutSeconds</c> (default 30) and
    /// <c>maxOutputBytes</c> (default 4096); for HTTP <c>externalHttp.endpoint</c> (an absolute URL), <c>method</c>
    /// (default POST), <c>headers</c>, <c>timeoutSeconds</c> and <c>maxResponseBytes</c>. The capability kind in the
    /// request is ignored, and secret bindings and secret-bearing arguments are rejected.
    ///
    /// Invalid definitions and failed calls are reported in the result's <c>diagnostics</c> with HTTP 200. An empty
    /// <c>jsonInput</c>, a null <c>capability</c> or <c>tags</c>, or an invalid HTTP method is not translated into the
    /// error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The tool definition to test and the input to send.</param>
    /// <response code="200">
    /// The test finished: <c>isSuccess</c> tells whether the call succeeded, and <c>diagnostics</c> explain a failure.
    /// </response>
    internal static async Task<IResult> TestAgentToolCapabilitySetupAsync(
        CapabilityToolSetupTestRequest request,
        IAgentCapabilitySetupFlowService setupFlowService,
        CancellationToken cancellationToken) =>
        Results.Ok(await setupFlowService.TestToolSetupAsync(request, cancellationToken));

    /// <summary>
    /// Test an MCP server capability definition by starting or connecting to the server and listing its tools.
    /// </summary>
    /// <remarks>
    /// Validates the MCP server definition in <c>capability</c>, then starts the local server process (stdio
    /// transport) or connects to the remote endpoint (HTTP transport), performs the MCP handshake, lists the server's
    /// tools, checks them against the definition's allowed tools and stops. The definition does not have to be saved
    /// and nothing is written to the catalog.
    ///
    /// This starts a process on the server host or connects from it. A local server command must be one of
    /// <c>dotnet</c>, <c>node</c>, <c>npx</c>, <c>powershell</c>, <c>pwsh</c>, <c>python</c>, <c>python3</c>,
    /// <c>uv</c> or <c>uvx</c>, and header bindings of a remote server are resolved from the server's environment
    /// variables and sent to the configured endpoint. Keep API authorization enabled on any host this operation can be
    /// reached on, and test only definitions you trust.
    ///
    /// The server is read from <c>capability.configurationJson</c>: <c>transport</c> (<c>stdio</c> for a local process;
    /// <c>http</c>, <c>sse</c> or <c>remote-http</c> for a remote server; when absent, a <c>command</c> implies stdio
    /// and an <c>endpoint</c> implies HTTP), <c>command</c>, <c>arguments</c>,
    /// <c>workingDirectory</c>, <c>endpoint</c>, <c>allowedTools</c> (required for stdio), <c>approvalMode</c>
    /// (<c>NeverRequire</c> or <c>AlwaysRequire</c>; required for stdio), <c>timeoutSeconds</c> and
    /// <c>headerBindings</c>. Raw environment variables and raw headers are rejected. The capability kind in the
    /// request is ignored.
    ///
    /// Invalid definitions and connection or listing failures are reported in the result's <c>diagnostics</c> with
    /// HTTP 200. Other failures, such as an out-of-range timeout, are not translated into the error envelope: the
    /// request fails with a generic HTTP 500 and the started server may not be stopped.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The MCP server definition to test.</param>
    /// <response code="200">
    /// The test finished: <c>isSuccess</c>, the discovered and allowed tools, diagnostics and whether the server was
    /// stopped cleanly.
    /// </response>
    internal static async Task<IResult> TestAgentMcpCapabilitySetupAsync(
        CapabilityMcpSetupTestRequest request,
        IAgentCapabilitySetupFlowService setupFlowService,
        CancellationToken cancellationToken) =>
        Results.Ok(await setupFlowService.TestMcpSetupAsync(request, cancellationToken));

    /// <summary>
    /// Preview which capabilities an access policy would allow, without saving anything.
    /// </summary>
    /// <remarks>
    /// Builds the candidate capabilities from saved catalog capabilities (<c>capabilityIds</c>, or all of them when
    /// empty) and unsaved <c>draftCapabilities</c>, compiles <c>policy</c>, evaluates it against the candidates and the
    /// <c>requiredCapabilities</c>, and returns which candidates would be allowed with the reasons for those that are
    /// suppressed. It reads the catalog and writes nothing. It previews the policy only; it does not describe what a
    /// particular agent run will receive.
    ///
    /// Evaluation: a matching Deny rule wins; with the default effect DenyAll, a candidate needs a matching Allow or
    /// Require rule; a Require rule that matches no candidate produces a diagnostic. When any candidate or the policy
    /// is invalid, <c>validationResult</c> lists the issues, the effective set is empty and no row is allowed; with an
    /// empty <c>capabilityIds</c> a single invalid saved capability therefore invalidates the whole preview.
    ///
    /// Note the two capability kind numberings in one request: <c>draftCapabilities[].kind</c> uses the catalog kind
    /// (2 is Tool), while <c>requiredCapabilities[].kind</c> uses the identity kind (1 is Tool).
    ///
    /// A null list in the request is not translated into the error envelope: the request fails with a generic
    /// HTTP 500; send empty arrays instead.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">Candidates, the policy to evaluate and the required capabilities.</param>
    /// <response code="200">
    /// The preview: validation issues, the effective allowed set and one row per candidate.
    /// </response>
    internal static async Task<IResult> PreviewAgentCapabilityAccessAsync(
        CapabilityAccessPreviewRequest request,
        IAgentCapabilitySetupFlowService setupFlowService,
        CancellationToken cancellationToken) =>
        Results.Ok(await setupFlowService.PreviewAccessAsync(request, cancellationToken));

    /// <summary>
    /// List the workspace memory notes recorded for an agent, newest first.
    /// </summary>
    /// <remarks>
    /// Returns the agent's workspace memory notes, which are simple title and content records kept in the agent
    /// catalog. They are not the memory providers configured through the agent's <c>memoryAccess</c> settings or
    /// <c>/api/memory-providers</c>, and they are not injected into agent runs: the runtime lists them only as an
    /// excluded context source. An agent without notes, or an unknown agent identifier, yields an empty array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent whose notes are listed.</param>
    /// <response code="200">The agent's memory notes, ordered by creation time, newest first.</response>
    internal static async Task<IResult> ListAgentMemoryAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.ListMemoryAsync(agentId, cancellationToken));

    /// <summary>
    /// Create a workspace memory note for an agent, or replace the note whose identifier the body carries.
    /// </summary>
    /// <remarks>
    /// With <c>id</c> null or omitted a new note is created with a new identifier; with an <c>id</c> the note with
    /// that identifier is replaced as a whole (its creation time is kept) or, if none exists, created with it. There is
    /// no concurrency check, and a replacement may move the note to another agent. Text members are trimmed; kind,
    /// importance and metadata are stored as sent without further validation. The note is not injected into agent runs
    /// (see <c>GET /api/agents/{agentId}/memory</c>).
    ///
    /// Notes shipped with the product (source <c>seed</c>) are restored whenever they are missing, so changing the
    /// title or source of such a note leaves the shipped note next to the changed copy.
    ///
    /// A note for an agent that does not exist (including an omitted <c>agentId</c>), or a null text member, is not
    /// translated into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The complete note to store.</param>
    /// <response code="200">The note was saved. The body is the note identifier as a JSON string (a GUID).</response>
    internal static async Task<IResult> SaveAgentMemoryAsync(
        MemoryEditorModel request,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(await workspaceService.SaveMemoryAsync(request, cancellationToken));

    /// <summary>
    /// Delete a workspace memory note.
    /// </summary>
    /// <remarks>
    /// Removes the note with this identifier. Deleting an identifier that does not exist also succeeds, so a repeated
    /// delete is harmless. Notes shipped with the product (source <c>seed</c>) are restored by the same catalog write,
    /// so deleting one reports success but the note remains. Memory held by memory providers is not affected.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="memoryId">Identifier of the note, as returned by <c>GET /api/agents/{agentId}/memory</c>.</param>
    /// <response code="200">
    /// The delete completed: the note is gone, or it never existed, unless it is a note shipped with the product. The
    /// body is <c>{ "ok": true }</c>.
    /// </response>
    internal static async Task<IResult> DeleteAgentMemoryAsync(
        Guid memoryId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        await workspaceService.DeleteMemoryAsync(memoryId, cancellationToken);
        return Results.Ok(new ApiAck(true));
    }

    /// <summary>
    /// List an agent's chat sessions with their full transcripts, most recently updated first.
    /// </summary>
    /// <remarks>
    /// A chat session is a conversation thread with one agent; each message sent in it starts its own agent execution
    /// run. Sessions are returned with all their messages, newest activity first, without paging. For a lighter list
    /// with previews use <c>GET /api/agents/{agentId}/chat-workspace</c>. An unknown agent yields an empty array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent.</param>
    /// <response code="200">The agent's chat sessions; an empty array when there are none.</response>
    internal static async Task<IResult> ListAgentChatSessionsAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToChatSessions(
            await workspaceService.ListChatSessionsAsync(agentId, cancellationToken)));

    /// <summary>
    /// Create an empty chat session for an agent, or read an existing session of that agent.
    /// </summary>
    /// <remarks>
    /// Without <c>chatSessionId</c> a new, empty session titled "New exploration thread" is created for the agent;
    /// every call creates another session. With <c>chatSessionId</c> the existing session is returned unchanged and
    /// nothing is created. Sending a message without a session (<c>POST /api/agents/{agentId}/chat</c>) also creates
    /// one, so this operation is only needed to prepare a session in advance.
    ///
    /// An unknown agent, or a <c>chatSessionId</c> that does not exist or belongs to another agent, is not translated
    /// into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the session.</param>
    /// <param name="chatSessionId">
    /// Optional identifier of an existing session of this agent to return instead of creating one.
    /// </param>
    /// <response code="200">The created session, or the existing session named by <c>chatSessionId</c>.</response>
    internal static async Task<IResult> CreateAgentChatSessionAsync(
        Guid agentId,
        Guid? chatSessionId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToChatSession(
            await workspaceService.GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken)));

    /// <summary>
    /// Rename an agent's chat session.
    /// </summary>
    /// <remarks>
    /// Replaces the session title: runs of white space are collapsed to single spaces and titles longer than 96
    /// characters are cut. A rename made while a run of the session is still active can be overwritten when that run
    /// finishes; read the session afterwards to confirm the title.
    ///
    /// A blank or null title, an unknown session, or a session of another agent is not translated into the error
    /// envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the session.</param>
    /// <param name="chatSessionId">Identifier of the session to rename.</param>
    /// <param name="request">The new title.</param>
    /// <response code="200">The renamed session with its transcript.</response>
    internal static async Task<IResult> RenameAgentChatSessionAsync(
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
                cancellationToken)));

    /// <summary>
    /// Read an agent's chat workspace: its session list, the selected session and its latest runs.
    /// </summary>
    /// <remarks>
    /// Returns the agent's chat sessions as summaries (newest activity first), the selected session with its
    /// transcript, a summary of the latest run and the selected session's most recent execution run. The session named
    /// by <c>preferredSessionId</c> is selected when it exists and belongs to the agent; otherwise the most recently
    /// updated session is selected. It only reads; an unknown agent yields an empty workspace.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent.</param>
    /// <param name="preferredSessionId">
    /// Optional identifier of the session to select; ignored when it does not exist or belongs to another agent.
    /// </param>
    /// <response code="200">The agent's chat workspace.</response>
    internal static async Task<IResult> GetAgentChatWorkspaceAsync(
        Guid agentId,
        Guid? preferredSessionId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToChatWorkspace(
            await workspaceService.GetChatAgentWorkspaceAsync(
                agentId,
                preferredSessionId,
                cancellationToken)));

    /// <summary>
    /// Send a prompt to an agent in a chat session and wait for the agent's reply.
    /// </summary>
    /// <remarks>
    /// Starts a chat-backed agent execution run: the prompt is added to the session transcript, the agent runs with
    /// the session history, and the request returns when the run completes, fails or stops to wait for tool approvals.
    /// Without <c>chatSessionId</c> a new session is created and titled from the prompt. A chat session is not an
    /// execution run: every message starts its own run in the session. To run the agent without a transcript, or with
    /// a structured-output contract, use <c>POST /api/agents/{agentId}/execution-runs</c>; to receive progress events
    /// in the response itself, use <c>POST /api/agents/{agentId}/chat/stream</c>.
    ///
    /// Tool calls that need approval stop the run with <c>state</c> 3 WaitingOnTool; approve or reject them with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>. Only the agent's own automatic
    /// approval setting applies here. Sending a message grants no tools: the agent's permissions, capabilities, access
    /// settings and the runtime policy decide which tools the run receives.
    ///
    /// Each command is admitted as an activity operation, identified by <c>activityOperationId</c> or a
    /// server-generated identifier and returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> response header. Progress
    /// can be followed with <c>GET /api/agents/execution-operations/{operationId}/events/stream</c> while the request
    /// runs (it answers 404 until the operation is admitted). The identifier is not an idempotency key: use a new one
    /// for every attempt.
    ///
    /// The request waits for the run; cancelling the request cancels the run, which is then stored as failed with the
    /// outcome Cancelled. After any response, read <c>GET /api/agents/execution-runs/{executionRunId}</c> for the
    /// stored state and evidence. An unknown agent or session, a session of another agent, a session that is busy with
    /// another run, an agent without a usable provider profile or an invalid attachment is not translated into the
    /// error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent to talk to.</param>
    /// <param name="request">The prompt, the optional session, attachments and activity operation identifier.</param>
    /// <response code="200">
    /// The run finished or stopped for approvals: the reply message, the run metric, the run identifier and its state
    /// (5 Completed, 6 Failed or 3 WaitingOnTool).
    /// </response>
    /// <response code="400">
    /// Rejected before any run (<c>agents.request-invalid</c>: an all-zero agent or session identifier, or a blank
    /// prompt), or the run failed because the provider rejected the request as incompatible
    /// (<c>agents.provider-request-incompatible</c>; the body names the run).
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// Retry with a new identifier.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran. Retry with a new identifier.
    /// </response>
    /// <response code="422">
    /// The run failed because the provider configuration is not usable (<c>agents.provider-configuration-invalid</c>).
    /// </response>
    /// <response code="500">
    /// The run failed outside a confirmed provider failure (<c>agents.run-failed</c>); the body names the failed run.
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>; nothing ran, retry later
    /// with a new identifier), or the run failed because the provider reported no quota
    /// (<c>agents.provider-quota-unavailable</c>), throttled the request (<c>agents.provider-rate-limited</c>) or
    /// failed (<c>agents.provider-failed</c>).
    /// </response>
    internal static async Task<IResult> SendAgentChatMessageAsync(
        Guid agentId,
        AgentChatApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
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
                new AgentChatRunOptions(operationId)
                {
                    Context = ProviderHistoryRequestContext.ForExecution(null, context)
                },
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
    }

    /// <summary>
    /// Continue an interrupted agent execution run in place from its saved original input.
    /// </summary>
    /// <remarks>
    /// Recovery continues the same execution run, after re-checking current permissions, from the original input the
    /// server saved when the run was admitted. It does not start a new conversation or a new run, and it never repeats
    /// a tool call whose outcome is unknown. Only runs started from the product's chat page or by governed process
    /// steps can be recovered: runs started through this HTTP API keep no recovery record and are rejected with HTTP
    /// 400 <c>tool-admission.legacy-run</c>.
    ///
    /// The server rechecks the run and the current authority before resuming. A run that waits for approvals, was
    /// cancelled, still has effects to reconcile, belongs to an agent that is no longer Active, or whose authority or
    /// context changed is rejected with HTTP 400 and a <c>tool-admission.</c> code. A cancelled run needs
    /// <c>POST /api/agents/execution-runs/{executionRunId}/reconcile-cancellation</c> first; pending approvals are
    /// answered with <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>. A completed run returns
    /// its stored result after a read-authority check.
    ///
    /// The command is admitted as an activity operation (<c>activityOperationId</c> or a server-generated identifier,
    /// returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header; follow it with
    /// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>). Use a new identifier for every
    /// attempt. An unknown run identifier is not translated into the error envelope and fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact <c>api</c> scope; tokens with only
    /// other scopes are refused. With authorization disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run to recover.</param>
    /// <param name="request">Optional activity operation identifier.</param>
    /// <response code="200">
    /// The run resumed and finished or stopped again, or a completed run's stored result; read the run for its state.
    /// </response>
    /// <response code="400">
    /// Nothing was resumed: an all-zero run identifier (<c>agents.request-invalid</c>), or a recovery rule failed, for
    /// example <c>tool-admission.legacy-run</c>, <c>tool-admission.cancelled-reconciliation-required</c>,
    /// <c>tool-admission.approval-pending</c>, <c>tool-admission.reconciliation-required</c>,
    /// <c>tool-admission.actor-unavailable</c> or <c>tool-admission.authority-changed</c>. Or the resumed run failed
    /// because the provider rejected the request as incompatible (<c>agents.provider-request-incompatible</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// API authorization is enabled and the bearer token lacks the <c>api</c> scope
    /// (<c>api.authorization-forbidden</c>).
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran.
    /// </response>
    /// <response code="422">
    /// The resumed run failed because the provider configuration is not usable
    /// (<c>agents.provider-configuration-invalid</c>).
    /// </response>
    /// <response code="500">
    /// The resumed run failed outside a confirmed provider failure (<c>agents.run-failed</c>).
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>), or the resumed run failed
    /// because the provider reported no quota, throttled the request or failed
    /// (<c>agents.provider-quota-unavailable</c>, <c>agents.provider-rate-limited</c>, <c>agents.provider-failed</c>).
    /// </response>
    internal static async Task<IResult> RecoverAgentExecutionRunAsync(
        Guid executionRunId,
        AgentExecutionRecoveryApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var validation = AgentApiRequestValidation.ValidateExecutionRun(context, executionRunId);
        if (validation is not null)
        {
            return validation;
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        AgentActivityApiResults.SetOperationIdHeader(context.Response, operationId);
        try
        {
            var result = await workspaceService.RecoverExecutionRunAsync(
                executionRunId,
                operationId,
                cancellationToken);
            return Results.Ok(AgentApiResponseMapper.ToExecutionRunResult(result));
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(context, exception, executionRunId: executionRunId);
        }
        catch (AgentToolAdmissionException exception)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                exception.Message,
                exception.Code,
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
    }

    /// <summary>
    /// Determine, from owner receipts, which tool effects of a cancelled agent execution run took place.
    /// </summary>
    /// <remarks>
    /// For a run whose outcome is Cancelled, examines every tool call the run proposed: calls that were never sent had
    /// no effect, read-only calls changed nothing, and for sent calls the owning service's receipt is read where the
    /// tool offers one. A confirmed receipt records the effect as committed; without one the effect stays unknown,
    /// because a transaction started earlier could still commit. The operation runs no tool and calls no model
    /// provider; it only records the resolution and adds a log entry. It does not undo effects.
    ///
    /// Use the response to decide what needs manual follow-up: <c>hasUnknownEffects</c> true means at least one effect
    /// could not be confirmed either way. Only runs with a recoverable tool-admission journal (runs admitted by the
    /// interactive chat interface or by governed process steps) can be reconciled; other runs are rejected with
    /// HTTP 400 <c>tool-admission.legacy-run</c>, and a run that was not cancelled with
    /// <c>tool-admission.denied</c>.
    ///
    /// The command is admitted as an activity operation (<c>activityOperationId</c> or a server-generated identifier,
    /// returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header). Use a new identifier for every attempt. An
    /// unknown run identifier is not translated into the error envelope and fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, a bearer token with the exact <c>api</c> scope; tokens with only
    /// other scopes are refused. With authorization disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the cancelled agent execution run.</param>
    /// <param name="request">Optional activity operation identifier.</param>
    /// <response code="200">The reconciliation result for every proposed tool call and provider dispatch.</response>
    /// <response code="400">
    /// Nothing was reconciled: an all-zero run identifier (<c>agents.request-invalid</c>), a run without a recoverable
    /// journal (<c>tool-admission.legacy-run</c>), a run that was not cancelled (<c>tool-admission.denied</c>), or a
    /// retained receipt that can no longer be read under current authority
    /// (<c>tool-admission.receipt-read-denied</c>, <c>tool-admission.receipt-read-unavailable</c>,
    /// <c>tool-admission.receipt-read-mismatch</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// API authorization is enabled and the bearer token lacks the <c>api</c> scope
    /// (<c>api.authorization-forbidden</c>).
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran.
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>); nothing ran. Retry later
    /// with a new identifier.
    /// </response>
    internal static async Task<IResult> ReconcileCancelledAgentExecutionRunAsync(
        Guid executionRunId,
        AgentExecutionRecoveryApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var validation = AgentApiRequestValidation.ValidateExecutionRun(context, executionRunId);
        if (validation is not null)
        {
            return validation;
        }

        var operationId = request.ActivityOperationId ?? AgentExecutionOperationId.New();
        AgentActivityApiResults.SetOperationIdHeader(context.Response, operationId);
        try
        {
            var result = await workspaceService.ReconcileCancelledExecutionRunAsync(
                executionRunId,
                operationId,
                cancellationToken);
            return Results.Ok(AgentApiResponseMapper.ToCancellationReconciliation(result));
        }
        catch (AgentExecutionActivityAdmissionException exception)
        {
            return AgentActivityApiResults.FromAdmissionException(context, exception, executionRunId: executionRunId);
        }
        catch (AgentToolAdmissionException exception)
        {
            return ApiEndpointResults.AgentValidationFailure(
                context,
                exception.Message,
                exception.Code,
                executionRunId: executionRunId);
        }
    }

    /// <summary>
    /// Approve or reject the pending tool calls of an agent execution run and continue the run.
    /// </summary>
    /// <remarks>
    /// Read the run's pending approvals first (<c>GET /api/agents/execution-runs/{executionRunId}/approvals</c> or the
    /// run detail) and then send exactly one decision per pending approval in <c>decisions</c>. A set with a missing,
    /// unknown or repeated approval identifier is rejected (<c>agents.approval-decision-mismatch</c>) and nothing is
    /// decided. When <c>decisions</c> is omitted or empty, the single value <c>approved</c> is applied to every
    /// approval pending when the request is handled. The decisions are recorded, the run continues with the approved
    /// calls executed and the rejected ones refused, and the request returns when the run completes, fails or stops for
    /// new approvals.
    ///
    /// <c>autoApprovePendingToolCalls</c> true, together with approving every pending call, also approves later
    /// approval requests of this run automatically; any rejection clears that setting. A run with nothing pending
    /// returns its stored result unchanged when it is completed or failed, and ignores the decisions.
    ///
    /// The command is admitted as an activity operation (<c>activityOperationId</c> or a server-generated identifier,
    /// returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header; follow it with
    /// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>). Use a new identifier for every
    /// attempt; <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals/stream</c> streams the progress
    /// instead. An unknown run, a run that is already being continued, or a run with nothing pending that is neither
    /// completed nor failed is not translated into the error envelope: the request fails with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open. Approving a call here does not widen the agent's tool
    /// access; the call still runs under the run's policy.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run waiting for approvals.</param>
    /// <param name="request">The approval decisions.</param>
    /// <response code="200">
    /// The decisions were recorded and the run continued; the body is its result (state 5 Completed, 6 Failed or
    /// 3 WaitingOnTool for new approvals).
    /// </response>
    /// <response code="400">
    /// Nothing was decided: an all-zero run identifier (<c>agents.request-invalid</c>) or a decision set that does not
    /// match the pending approvals exactly (<c>agents.approval-decision-mismatch</c>); read the approvals again. Or the
    /// continued run failed because the provider rejected the request as incompatible
    /// (<c>agents.provider-request-incompatible</c>).
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran.
    /// </response>
    /// <response code="422">
    /// The continued run failed because the provider configuration is not usable
    /// (<c>agents.provider-configuration-invalid</c>).
    /// </response>
    /// <response code="500">
    /// The continued run failed outside a confirmed provider failure (<c>agents.run-failed</c>).
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>), or the continued run
    /// failed because the provider reported no quota, throttled the request or failed.
    /// </response>
    internal static async Task<IResult> RespondToAgentExecutionApprovalsAsync(
        Guid executionRunId,
        PendingApprovalApiRequest request,
        HttpContext context,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
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
    }

    /// <summary>
    /// Start an agent execution run for the agent named in the body and wait for its result.
    /// </summary>
    /// <remarks>
    /// Runs the agent on <c>prompt</c> as a new agent execution run and returns when the run completes, fails or stops
    /// to wait for tool approvals. Without <c>chatSessionId</c> the run has no chat transcript (no assistant message
    /// is stored); with it, the run is added to that existing session of the agent. An agent execution run is not a
    /// workflow run or a process run. <c>POST /api/agents/{agentId}/execution-runs</c> is the same operation with the
    /// agent in the route, and <c>POST /api/agents/execution-runs/stream</c> streams progress instead of waiting.
    ///
    /// Optional <c>structuredOutput</c> asks the model for JSON that matches a JSON Schema; the result then carries the
    /// parsed data and its validation status, and output that fails validation ends the run as Failed with HTTP 200.
    /// <c>context</c> labels the run's origin for later filtering (see <c>GET /api/agents/execution-runs</c>).
    /// <c>autoApprovePendingToolCalls</c> true runs tool calls that would need approval without asking.
    ///
    /// Tool calls that need approval stop the run with <c>state</c> 3 WaitingOnTool; answer them with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>. Starting a run grants no tools: the
    /// agent's permissions, capabilities, access settings and the runtime policy decide which tools it receives.
    ///
    /// The command is admitted as an activity operation (<c>activityOperationId</c> or a server-generated identifier,
    /// returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header; follow it with
    /// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>). It is not an idempotency key: use a
    /// new identifier for every attempt. Cancelling the request cancels the run, which is then stored as failed with
    /// the outcome Cancelled. After any response, read <c>GET /api/agents/execution-runs/{executionRunId}</c>.
    ///
    /// An unknown agent or session, a session of another agent, a session busy with another run or waiting for
    /// approvals, an agent without a usable provider profile, an invalid attachment or an inconsistent
    /// <c>context</c> for process steps is not translated into the error envelope: the request fails with a generic
    /// HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="request">The agent, the prompt and the run options.</param>
    /// <response code="200">
    /// The run finished or stopped for approvals: its identifier, response text, metric, state (5 Completed, 6 Failed
    /// or 3 WaitingOnTool) and structured output when requested.
    /// </response>
    /// <response code="400">
    /// Rejected before any run: an all-zero agent or session identifier or a blank prompt
    /// (<c>agents.request-invalid</c>), or an invalid <c>structuredOutput</c> contract (codes starting with
    /// <c>agents.structured-output-</c>, for example <c>agents.structured-output-schema-invalid</c>). Or the run failed
    /// because the provider rejected the request as incompatible (<c>agents.provider-request-incompatible</c>; the body
    /// names the run).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// Retry with a new identifier.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran. Retry with a new identifier.
    /// </response>
    /// <response code="422">
    /// The run failed because the provider configuration is not usable (<c>agents.provider-configuration-invalid</c>).
    /// </response>
    /// <response code="500">
    /// The run failed outside a confirmed provider failure (<c>agents.run-failed</c>); the body names the failed run.
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>; nothing ran, retry later
    /// with a new identifier), or the run failed because the provider reported no quota
    /// (<c>agents.provider-quota-unavailable</c>), throttled the request (<c>agents.provider-rate-limited</c>) or
    /// failed (<c>agents.provider-failed</c>).
    /// </response>
    internal static async Task<IResult> StartAgentExecutionRunAsync(
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
            cancellationToken);

    /// <summary>
    /// Start an agent execution run for the agent in the route and wait for its result.
    /// </summary>
    /// <remarks>
    /// Same operation as <c>POST /api/agents/execution-runs</c>, with the agent taken from the route instead of the
    /// body: runs the agent on <c>prompt</c> as a new agent execution run and returns when the run completes, fails or
    /// stops for tool approvals. Without <c>chatSessionId</c> the run has no chat transcript; with it, the run is added
    /// to that existing session of the agent. <c>POST /api/agents/{agentId}/execution-runs/stream</c> streams progress
    /// instead of waiting.
    ///
    /// Optional <c>structuredOutput</c> asks for JSON that matches a JSON Schema; output that fails validation ends the
    /// run as Failed with HTTP 200. Tool calls that need approval stop the run with <c>state</c> 3 WaitingOnTool;
    /// answer them with <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>. Starting a run
    /// grants no tools.
    ///
    /// The command is admitted as an activity operation (<c>activityOperationId</c> or a server-generated identifier,
    /// returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header; follow it with
    /// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>). Use a new identifier for every
    /// attempt. Cancelling the request cancels the run. An unknown agent or session, a busy session, an agent without a
    /// usable provider profile or an invalid attachment is not translated into the error envelope: the request fails
    /// with a generic HTTP 500.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent to run.</param>
    /// <param name="request">The prompt and the run options.</param>
    /// <response code="200">
    /// The run finished or stopped for approvals: its identifier, response text, metric, state (5 Completed, 6 Failed
    /// or 3 WaitingOnTool) and structured output when requested.
    /// </response>
    /// <response code="400">
    /// Rejected before any run: an all-zero agent or session identifier or a blank prompt
    /// (<c>agents.request-invalid</c>), or an invalid <c>structuredOutput</c> contract (codes starting with
    /// <c>agents.structured-output-</c>). Or the run failed because the provider rejected the request as incompatible
    /// (<c>agents.provider-request-incompatible</c>).
    /// </response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    /// <response code="409">
    /// The activity operation identifier is already in use (<c>agents.execution-operation-duplicate</c>); nothing ran.
    /// </response>
    /// <response code="410">
    /// The activity operation identifier was used before and its stream was evicted
    /// (<c>agents.execution-operation-evicted</c>); nothing ran.
    /// </response>
    /// <response code="422">
    /// The run failed because the provider configuration is not usable (<c>agents.provider-configuration-invalid</c>).
    /// </response>
    /// <response code="500">
    /// The run failed outside a confirmed provider failure (<c>agents.run-failed</c>); the body names the failed run.
    /// </response>
    /// <response code="503">
    /// Activity capacity is exhausted (<c>agents.execution-operation-capacity-exhausted</c>), or the run failed because
    /// the provider reported no quota, throttled the request or failed.
    /// </response>
    internal static async Task<IResult> StartAgentScopedExecutionRunAsync(
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
            cancellationToken);

    /// <summary>
    /// Search agent execution runs, most recently updated first.
    /// </summary>
    /// <remarks>
    /// Returns the agent execution runs that match every given filter, ordered by last update (newest first) and
    /// limited to <c>Take</c> results; there is no paging cursor, so narrow the filters or the time range to see older
    /// runs. Text filters match the stored value exactly, ignoring case; time ranges include their bounds. Without
    /// filters, the most recently updated runs of all agents are returned. For one agent's runs you can also use
    /// <c>GET /api/agents/{agentId}/execution-runs</c>.
    ///
    /// The items are summaries; read <c>GET /api/agents/execution-runs/{executionRunId}</c> for the log, metrics,
    /// approvals, artifacts, checkpoints and tool receipts of a run.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="query">The filters and the maximum number of runs.</param>
    /// <response code="200">The matching runs, newest update first; an empty array when none match.</response>
    internal static async Task<IResult> ListAgentExecutionRunsAsync(
        [AsParameters] AgentExecutionRunApiQuery query,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToExecutionRuns(
            await workspaceService.ListExecutionRunsAsync(
                query.ToExecutionRunQuery(),
                cancellationToken)));

    /// <summary>
    /// Search the execution runs of one agent, most recently updated first.
    /// </summary>
    /// <remarks>
    /// Same search as <c>GET /api/agents/execution-runs</c>, restricted to the agent whose identifier is the
    /// <c>agentId</c> route value; an <c>AgentId</c> query value is ignored. Results are ordered by last update (newest
    /// first) and limited to <c>Take</c>. An unknown agent yields an empty array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="query">The filters and the maximum number of runs.</param>
    /// <response code="200">The matching runs, newest update first; an empty array when none match.</response>
    internal static async Task<IResult> ListAgentScopedExecutionRunsAsync(
        // Described by attribute: the filter object's AgentId also binds this route value, and an XML <param> that
        // matches two generated parameters makes the document generator fail.
        [Description("Identifier of the agent whose execution runs are searched, as returned by `GET /api/agents`.")]
        Guid agentId,
        [AsParameters] AgentExecutionRunApiQuery query,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToExecutionRuns(
            await workspaceService.ListExecutionRunsAsync(
                query.ToExecutionRunQuery(agentId),
                cancellationToken)));

    /// <summary>
    /// Read an agent execution run with its log, metrics, approvals, artifacts, checkpoints, tool receipts and usage
    /// totals.
    /// </summary>
    /// <remarks>
    /// Returns the stored state of one agent execution run and its evidence. Use it after every command response to
    /// confirm what was stored: a successful HTTP response alone is not proof that a tool effect was committed; check
    /// the tool receipts' <c>effectState</c>. The run's chat session is included with its transcript when the run
    /// belongs to one.
    ///
    /// An unknown run identifier is not translated into the error envelope: the request fails with a generic HTTP 500.
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}</c> returns HTTP 404 instead and also checks the
    /// agent.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">
    /// Identifier of the agent execution run, as returned by a run command or a run search.
    /// </param>
    /// <response code="200">The run and its evidence.</response>
    internal static async Task<IResult> GetAgentExecutionRunDetailAsync(
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToExecutionRunDetail(
            await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken)));

    /// <summary>
    /// Read an execution run of a given agent with its log, metrics, approvals, artifacts, checkpoints, tool receipts
    /// and usage totals.
    /// </summary>
    /// <remarks>
    /// Same content as <c>GET /api/agents/execution-runs/{executionRunId}</c>, but the run must belong to the agent in
    /// the route; an unknown run, or a run of another agent, yields HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run and its evidence.</response>
    /// <response code="401">
    /// API authorization is enabled and the request has no valid bearer token (<c>api.authorization-required</c>).
    /// </response>
    /// <response code="403">
    /// Only when API authorization is enabled and an authenticated token is refused by an operation policy
    /// (<c>api.authorization-forbidden</c>). This operation accepts any valid token, so it does not currently return
    /// this status.
    /// </response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> GetAgentScopedExecutionRunDetailAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            AgentApiResponseMapper.ToExecutionRunDetail,
            cancellationToken);

    /// <summary>
    /// List the artifacts produced by an agent execution run.
    /// </summary>
    /// <remarks>
    /// Returns the files the run's tools recorded as outputs, such as generated outputs and converted documents, with
    /// their workspace-relative paths. The file content is not returned. An unknown run identifier is not translated
    /// into the error envelope and fails with a generic HTTP 500;
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}/artifacts</c> returns HTTP 404 instead.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's artifacts; an empty array when it produced none.</response>
    internal static async Task<IResult> ListAgentExecutionArtifactsAsync(
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToArtifacts(
            await workspaceService.ListExecutionArtifactsAsync(executionRunId, cancellationToken)));

    /// <summary>
    /// List the artifacts produced by an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Same list as <c>GET /api/agents/execution-runs/{executionRunId}/artifacts</c>, but the run must belong to the
    /// agent in the route.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's artifacts; an empty array when it produced none.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionArtifactsAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToArtifacts(detail.Artifacts),
            cancellationToken);

    /// <summary>
    /// List the checkpoints captured while an agent execution run waited for tool approvals.
    /// </summary>
    /// <remarks>
    /// A checkpoint records the run state when the run stopped for approvals and when it was resumed; it is internal
    /// evidence of the approval wait, not a point a client can restart from. An unknown run identifier is not
    /// translated into the error envelope and fails with a generic HTTP 500; <c>GET
    /// /api/agents/{agentId}/execution-runs/{executionRunId}/checkpoints</c> returns HTTP 404 instead.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's checkpoints; an empty array when it never waited for approvals.</response>
    internal static async Task<IResult> ListAgentExecutionCheckpointsAsync(
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToCheckpoints(
            await workspaceService.ListExecutionWorkflowCheckpointsAsync(
                executionRunId,
                cancellationToken)));

    /// <summary>
    /// List the approval-wait checkpoints of an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Same list as <c>GET /api/agents/execution-runs/{executionRunId}/checkpoints</c>, but the run must belong to the
    /// agent in the route.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's checkpoints; an empty array when it never waited for approvals.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionCheckpointsAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToCheckpoints(detail.Checkpoints),
            cancellationToken);

    /// <summary>
    /// List the tool execution receipts of an agent execution run.
    /// </summary>
    /// <remarks>
    /// A receipt is the run's evidence of one tool call: which tool ran, its declared side effect, the invocation
    /// outcome and the effect state (whether a change is known to be committed, known not to have happened, or
    /// unknown). Use the effect state, not the HTTP status of the run command, to decide whether a change took place;
    /// an unknown effect requires reconciliation, not a blind retry. Receipts of workspace file, workspace process and
    /// MCP operations currently report the outcome and effect state as 0 Unknown.
    ///
    /// An unknown run identifier is not translated into the error envelope and fails with a generic HTTP 500;
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}/tool-receipts</c> returns HTTP 404 instead.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's tool receipts; an empty array when no tool call was recorded.</response>
    internal static async Task<IResult> ListAgentExecutionToolReceiptsAsync(
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToToolReceipts(
            await workspaceService.ListToolExecutionReceiptsAsync(executionRunId, cancellationToken)));

    /// <summary>
    /// List the tool execution receipts of an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Same list as <c>GET /api/agents/execution-runs/{executionRunId}/tool-receipts</c>, but the run must belong to
    /// the agent in the route.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's tool receipts; an empty array when no tool call was recorded.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionToolReceiptsAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToToolReceipts(detail.ToolReceipts),
            cancellationToken);

    /// <summary>
    /// List the execution log entries of an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Returns the progress entries (state, phase and message) recorded while the run advanced. The run must belong to
    /// the agent in the route. The same entries are part of <c>GET /api/agents/execution-runs/{executionRunId}</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's log entries.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionLogAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToExecutionLog(detail.ExecutionLog),
            cancellationToken);

    /// <summary>
    /// List the run metrics of an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Returns one metric per model segment of the run (the initial run and each continuation after approvals), with
    /// duration, token counts, tool calls and estimated cost. The run must belong to the agent in the route. The same
    /// metrics are part of <c>GET /api/agents/execution-runs/{executionRunId}</c>.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's metrics.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionMetricsAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToMetrics(detail.Metrics),
            cancellationToken);

    /// <summary>
    /// List the tool approval requests of an execution run of a given agent.
    /// </summary>
    /// <remarks>
    /// Same list as <c>GET /api/agents/execution-runs/{executionRunId}/approvals</c>, but the run must belong to the
    /// agent in the route; an unknown run, or a run of another agent, yields HTTP 404.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent that owns the run.</param>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's approval requests, pending and decided.</response>
    /// <response code="404">
    /// The run does not exist or belongs to another agent (<c>agents.execution-run-not-found</c>).
    /// </response>
    internal static async Task<IResult> ListAgentScopedExecutionApprovalsAsync(
        Guid agentId,
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        await GetAgentExecutionRunPartAsync(
            agentId,
            executionRunId,
            workspaceService,
            detail => AgentApiResponseMapper.ToExecutionApprovals(detail.Approvals),
            cancellationToken);

    /// <summary>
    /// List the tool approval requests of an agent execution run, pending and decided.
    /// </summary>
    /// <remarks>
    /// Each item is one tool call that needed approval, with its status (0 Pending, 1 Approved, 2 Rejected) and who
    /// decided it. Read this list immediately before answering the pending ones with
    /// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c>, which requires exactly one decision
    /// per pending approval. The tool arguments are not returned.
    ///
    /// An unknown run identifier is not translated into the error envelope and fails with a generic HTTP 500;
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}/approvals</c> returns HTTP 404 instead.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="executionRunId">Identifier of the agent execution run.</param>
    /// <response code="200">The run's approval requests; an empty array when none were raised.</response>
    internal static async Task<IResult> ListAgentExecutionApprovalsAsync(
        Guid executionRunId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken)
    {
        var detail = await workspaceService.GetExecutionRunDetailAsync(
            executionRunId,
            cancellationToken);
        return Results.Ok(AgentApiResponseMapper.ToExecutionApprovals(detail.Approvals));
    }

    /// <summary>
    /// List an agent's execution log entries, newest first, optionally limited to one chat session.
    /// </summary>
    /// <remarks>
    /// Returns the progress entries recorded while the agent's execution runs advanced (state, phase and message),
    /// ordered by creation time, newest first, with no paging or limit. Without <c>chatSessionId</c> every entry of the
    /// agent is returned, including runs started outside chat sessions and entries not linked to any run. The entries
    /// carry no run or session identifier; to read the log of one run use
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}/log</c>. An unknown agent yields an empty array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent.</param>
    /// <param name="chatSessionId">
    /// Optional identifier of one of the agent's chat sessions; only entries of that session are returned.
    /// </param>
    /// <response code="200">The matching log entries, newest first.</response>
    internal static async Task<IResult> ListAgentExecutionLogAsync(
        Guid agentId,
        Guid? chatSessionId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToExecutionLog(
            await workspaceService.ListExecutionLogAsync(
                agentId,
                chatSessionId,
                cancellationToken)));

    /// <summary>
    /// Read the execution log entries and run metrics of an agent's execution runs in one response.
    /// </summary>
    /// <remarks>
    /// Returns the log entries and run metrics of the agent's execution runs, each list newest first and without
    /// paging, optionally limited to one chat session. Unlike <c>GET /api/agents/{agentId}/execution-log</c>, log
    /// entries that are not linked to a run are left out. Use it to refresh a chat view's activity panel; for one run
    /// read <c>GET /api/agents/execution-runs/{executionRunId}</c>. An unknown agent yields empty lists.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent.</param>
    /// <param name="chatSessionId">
    /// Optional identifier of one of the agent's chat sessions; only runs of that session are included.
    /// </param>
    /// <response code="200">The log entries and metrics of the agent's matching runs.</response>
    internal static async Task<IResult> GetAgentRuntimeSnapshotAsync(
        Guid agentId,
        Guid? chatSessionId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToChatRuntime(
            await workspaceService.GetChatRuntimeSnapshotAsync(
                agentId,
                chatSessionId,
                cancellationToken)));

    /// <summary>
    /// List the run metrics recorded for an agent, newest first.
    /// </summary>
    /// <remarks>
    /// Returns one entry per recorded run metric (outcome, provider and model, duration, token counts, tool calls and
    /// cost) for all of the agent's execution runs, ordered by creation time, newest first, with no paging, limit or
    /// chat-session filter. For the metrics of one run use
    /// <c>GET /api/agents/{agentId}/execution-runs/{executionRunId}/metrics</c>. An unknown agent yields an empty
    /// array.
    ///
    /// Authority: when API authorization is enabled, any valid bearer token issued by this host; with authorization
    /// disabled (the development default) the route is open.
    /// </remarks>
    /// <param name="agentId">Identifier of the agent.</param>
    /// <response code="200">The agent's run metrics, newest first.</response>
    internal static async Task<IResult> ListAgentMetricsAsync(
        Guid agentId,
        IAgentFrameworkWorkspaceService workspaceService,
        CancellationToken cancellationToken) =>
        Results.Ok(AgentApiResponseMapper.ToMetrics(
            await workspaceService.ListMetricsAsync(agentId, cancellationToken)));

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

/// <summary>
/// Name of the agent to create by <c>POST /api/agents/{agentId}/clone</c>.
/// </summary>
/// <param name="CloneName">
/// Name of the new agent, trimmed. Its template key is derived from it (lower-case letters and digits separated by
/// hyphens), so it must contain at least one letter or digit and must not normalize to the key of another agent or
/// template.
/// </param>
internal sealed record AgentCloneApiRequest(string CloneName);

/// <summary>
/// Template key of the template to create by <c>POST /api/agents/{agentId}/convert-to-template</c>.
/// </summary>
/// <param name="TemplateKey">
/// Key identifying the new template, normalized to lower-case letters and digits separated by hyphens; it must
/// contain a letter or digit and must not already belong to another agent or template. A blank value falls back to
/// the source agent's name, which normally collides with the source's own key, so send an explicit key such as
/// <c>support-triage-template</c>.
/// </param>
internal sealed record AgentTemplateConversionApiRequest(string TemplateKey);

/// <summary>
/// Location of an agent export package to import with <c>POST /api/agents/import</c>.
/// </summary>
/// <param name="PackagePath">
/// Path of the ZIP package in the server's file system, typically the <c>packagePath</c> returned by
/// <c>GET /api/agents/{agentId}/export</c> on the same host. A relative path is resolved against the server process's
/// working directory. It is not a client file; to upload a package use <c>POST /api/agents/import-package</c>.
/// </param>
internal sealed record AgentImportApiRequest(string PackagePath);

/// <summary>
/// Complete member list of an agent team, sent to <c>POST</c> or <c>PUT /api/agents/teams/{teamId}/members</c>. It
/// replaces the current members; it does not append.
/// </summary>
/// <param name="AgentIds">
/// Identifiers of every agent that should be a member; agents not listed are removed from the team. Each must name an
/// existing agent; empty identifiers and duplicates are dropped. Always send this member: an empty array, and also an
/// omitted or null list, removes all members.
/// </param>
internal sealed record AgentTeamMembersApiRequest(IReadOnlyList<Guid> AgentIds);

/// <summary>
/// New title of an agent chat session, sent to <c>POST /api/agents/{agentId}/chat-sessions/{chatSessionId}/rename</c>.
/// </summary>
/// <param name="Title">
/// The title. Runs of white space become single spaces and text beyond 96 characters is cut. It must not be blank.
/// </param>
internal sealed record ChatSessionRenameApiRequest(string Title);

/// <summary>
/// Chat message to an agent, sent to <c>POST /api/agents/{agentId}/chat</c> and to its streaming variant
/// <c>POST /api/agents/{agentId}/chat/stream</c>. Each message starts a new chat-backed agent execution run.
/// </summary>
/// <param name="ChatSessionId">
/// Identifier of an existing chat session of the agent in the route to continue, or null or omitted to create a new
/// session titled from the prompt. The all-zero GUID is rejected (<c>agents.request-invalid</c>).
/// </param>
/// <param name="Prompt">
/// The message to the agent; it must not be blank (<c>agents.request-invalid</c>) and is trimmed. No length limit is
/// applied.
/// </param>
/// <param name="AttachmentPaths">
/// Optional images to show the model: paths of at most 8 files (.png, .jpg, .jpeg, .gif or .webp, each up to 10 MiB)
/// inside the workspace or a registered external folder, typically the <c>relativePath</c> returned by
/// <c>POST /api/agents/attachments/images</c>. Blank entries and duplicates are ignored. Null or omitted means none.
/// </param>
/// <param name="ActivityOperationId">
/// Optional identifier of the activity operation for this command, as a GUID string. Choose it to follow the progress
/// with <c>GET /api/agents/execution-operations/{operationId}/events/stream</c> while the command runs; when null or
/// omitted the server generates one. It is returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header. It is not an
/// idempotency key: an identifier the server still retains is rejected with HTTP 409
/// <c>agents.execution-operation-duplicate</c> or 410 <c>agents.execution-operation-evicted</c>, so use a new one for
/// every command. The all-zero GUID or a non-GUID value is rejected before the command runs.
/// </param>
internal sealed record AgentChatApiRequest(
    Guid? ChatSessionId,
    string Prompt,
    IReadOnlyList<string>? AttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

/// <summary>
/// Options of an execution-run recovery or cancellation reconciliation command
/// (<c>POST /api/agents/execution-runs/{executionRunId}/recover</c>,
/// <c>POST /api/agents/execution-runs/{executionRunId}/reconcile-cancellation</c>). An empty object is valid.
/// </summary>
/// <param name="ActivityOperationId">
/// Optional identifier of the activity operation for this command, as a GUID string; when null or omitted the server
/// generates one. It is returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header and can be followed with
/// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>. Use a new identifier for every attempt.
/// </param>
internal sealed record AgentExecutionRecoveryApiRequest(AgentExecutionOperationId? ActivityOperationId = null);

/// <summary>
/// Decisions on the pending tool approvals of an agent execution run, sent to
/// <c>POST /api/agents/execution-runs/{executionRunId}/pending-approvals</c> and to its streaming variant. Prefer
/// <c>decisions</c> with one entry per pending approval; <c>approved</c> is the older form that applies one answer to
/// everything pending.
/// </summary>
/// <param name="Approved">
/// Answer applied to every approval pending when the request is handled, used only when <c>decisions</c> is null or
/// empty: true approves them all, false rejects them all. Always send it; it is ignored when <c>decisions</c> is given.
/// </param>
/// <param name="AutoApprovePendingToolCalls">
/// True to approve this run's later approval requests automatically as well; it takes effect only when every pending
/// call is approved, and any rejection clears it. Always send it (false when not wanted).
/// </param>
/// <param name="ActivityOperationId">
/// Optional identifier of the activity operation for this command, as a GUID string; when null or omitted the server
/// generates one. It is returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header. Use a new identifier for every
/// attempt.
/// </param>
internal sealed record PendingApprovalApiRequest(
    bool Approved,
    bool AutoApprovePendingToolCalls,
    AgentExecutionOperationId? ActivityOperationId = null)
{
    /// <summary>
    /// One decision per approval currently pending on the run, identified by the <c>approvalId</c> values read from
    /// the run's approvals. When present and not empty it replaces <c>approved</c>, and it must name every pending
    /// approval exactly once (compared exactly, including case); a missing, unknown or repeated identifier rejects the
    /// whole request, with HTTP 400 <c>agents.approval-decision-mismatch</c> on the JSON route and a failed-command
    /// event (<c>agents.command-failed</c>) on the streaming route. Null or omitted applies <c>approved</c> to all.
    /// </summary>
    /// <remarks>
    /// Additive per-proposal contract: clients that still send only <see cref="Approved"/> keep working, because the
    /// request mapper expands it against the run's pending set.
    /// </remarks>
    public IReadOnlyList<PendingApprovalDecisionApiRequest>? Decisions { get; init; }
}

/// <summary>
/// Decision on one pending tool approval of an agent execution run.
/// </summary>
/// <param name="ApprovalId">
/// Identifier of the pending approval, copied exactly from <c>approvalId</c> of the run's approvals or of an
/// <c>agent.approval.required</c> stream event; an opaque string.
/// </param>
/// <param name="Approved">True to approve the tool call; false to reject it.</param>
internal sealed record PendingApprovalDecisionApiRequest(string ApprovalId, bool Approved);

/// <summary>
/// Request to start an agent execution run for the agent named in the body, sent to
/// <c>POST /api/agents/execution-runs</c> and to its streaming variant <c>POST /api/agents/execution-runs/stream</c>.
/// </summary>
/// <param name="AgentId">Identifier of the agent to run; it must exist and must not be the all-zero GUID.</param>
/// <param name="Prompt">The task or message for the agent; it must not be blank and is trimmed.</param>
/// <param name="ChatSessionId">
/// Optional identifier of an existing chat session of the agent to add the run to; null or omitted runs without a
/// chat transcript. The session must not be busy with another run or waiting for approvals.
/// </param>
/// <param name="Context">
/// Optional origin labels of the run (source, correlation, requester), stored with the run and usable as search
/// filters. Null or omitted records a manual run.
/// </param>
/// <param name="AutoApprovePendingToolCalls">
/// True to run tool calls that would need approval without asking, including approvals raised later in this run.
/// False (the default) stops the run for approval.
/// </param>
/// <param name="StructuredOutput">
/// Optional JSON Schema the model's final answer must satisfy; null or omitted returns free text only.
/// </param>
/// <param name="InputAttachmentPaths">
/// Optional images to show the model: paths of at most 8 files (.png, .jpg, .jpeg, .gif or .webp, each up to 10 MiB)
/// inside the workspace or a registered external folder, typically staged with
/// <c>POST /api/agents/attachments/images</c>. Null or omitted means none.
/// </param>
/// <param name="ActivityOperationId">
/// Optional identifier of the activity operation for this command, as a GUID string; when null or omitted the server
/// generates one. It is returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header and can be followed with
/// <c>GET /api/agents/execution-operations/{operationId}/events/stream</c>. It is not an idempotency key: use a new
/// identifier for every attempt.
/// </param>
internal sealed record AgentExecutionRunApiRequest(
    Guid AgentId,
    string Prompt,
    Guid? ChatSessionId = null,
    ExecutionInvocationContext? Context = null,
    bool AutoApprovePendingToolCalls = false,
    AgentJsonSchemaOutputContract? StructuredOutput = null,
    IReadOnlyList<string>? InputAttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

/// <summary>
/// Request to start an agent execution run for the agent in the route, sent to
/// <c>POST /api/agents/{agentId}/execution-runs</c> and to its streaming variant
/// <c>POST /api/agents/{agentId}/execution-runs/stream</c>.
/// </summary>
/// <param name="Prompt">The task or message for the agent; it must not be blank and is trimmed.</param>
/// <param name="ChatSessionId">
/// Optional identifier of an existing chat session of the agent to add the run to; null or omitted runs without a
/// chat transcript. The session must not be busy with another run or waiting for approvals.
/// </param>
/// <param name="Context">
/// Optional origin labels of the run (source, correlation, requester), stored with the run and usable as search
/// filters. Null or omitted records a manual run.
/// </param>
/// <param name="AutoApprovePendingToolCalls">
/// True to run tool calls that would need approval without asking, including approvals raised later in this run.
/// False (the default) stops the run for approval.
/// </param>
/// <param name="StructuredOutput">
/// Optional JSON Schema the model's final answer must satisfy; null or omitted returns free text only.
/// </param>
/// <param name="InputAttachmentPaths">
/// Optional images to show the model: paths of at most 8 files (.png, .jpg, .jpeg, .gif or .webp, each up to 10 MiB)
/// inside the workspace or a registered external folder, typically staged with
/// <c>POST /api/agents/attachments/images</c>. Null or omitted means none.
/// </param>
/// <param name="ActivityOperationId">
/// Optional identifier of the activity operation for this command, as a GUID string; when null or omitted the server
/// generates one. It is returned in the <c>X-CanDoItAll-Agent-Operation-Id</c> header. Use a new identifier for every
/// attempt.
/// </param>
internal sealed record AgentExecutionRunStartApiRequest(
    string Prompt,
    Guid? ChatSessionId = null,
    ExecutionInvocationContext? Context = null,
    bool AutoApprovePendingToolCalls = false,
    AgentJsonSchemaOutputContract? StructuredOutput = null,
    IReadOnlyList<string>? InputAttachmentPaths = null,
    AgentExecutionOperationId? ActivityOperationId = null);

/// <summary>
/// Query-string filters for searching agent execution runs. Every filter is optional and all given filters must match.
/// Text filters compare the whole stored value, ignoring case; blank values are ignored.
/// </summary>
internal sealed class AgentExecutionRunApiQuery
{
    /// <summary>
    /// Only runs of this agent. Ignored by <c>GET /api/agents/{agentId}/execution-runs</c>, which uses the route agent.
    /// </summary>
    public Guid? AgentId { get; set; }

    /// <summary>Only runs that belong to this chat session.</summary>
    public Guid? ChatSessionId { get; set; }

    /// <summary>Only runs whose context correlation identifier equals this value.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Only runs whose context source kind equals this value, for example <c>manual</c> (runs started without a
    /// context, including chat messages sent through this API) or <c>process-step</c>.
    /// </summary>
    public string? SourceKind { get; set; }

    /// <summary>Only runs whose context source identifier equals this value.</summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// Maximum number of runs to return, from 1 through 500; omitted means 50. Smaller values, including 0, return 1
    /// run and larger values return at most 500.
    /// </summary>
    public int? Take { get; set; } = 50;

    /// <summary>Only runs whose context process run identifier equals this value.</summary>
    public string? ProcessRunId { get; set; }

    /// <summary>Only runs whose context process step identifier equals this value.</summary>
    public string? ProcessStepId { get; set; }

    /// <summary>Only runs whose context scheduler run identifier equals this value.</summary>
    public string? SchedulerRunId { get; set; }

    /// <summary>Only runs whose context message identifier equals this value.</summary>
    public string? MessageId { get; set; }

    /// <summary>
    /// Only runs in this state, sent as the member name or its integer value: 0 Idle, 1 Preparing, 2 Running,
    /// 3 WaitingOnTool, 4 Persisting, 5 Completed, 6 Failed.
    /// </summary>
    public ExecutionState? State { get; set; }

    /// <summary>
    /// Only runs with this outcome, sent as the member name or its integer value: 0 Succeeded, 1 Failed, 2 Cancelled.
    /// </summary>
    public RunOutcome? Outcome { get; set; }

    /// <summary>
    /// Only runs with an approval request in this status, sent as the member name or its integer value: 0 Pending
    /// (including runs that still list pending approvals), 1 Approved, 2 Rejected.
    /// </summary>
    public ExecutionApprovalStatus? ApprovalStatus { get; set; }

    /// <summary>Only runs created at or after this instant (ISO 8601 with offset); inclusive.</summary>
    public DateTimeOffset? CreatedFromUtc { get; set; }

    /// <summary>Only runs created at or before this instant (ISO 8601 with offset); inclusive.</summary>
    public DateTimeOffset? CreatedToUtc { get; set; }

    /// <summary>Only runs last updated at or after this instant (ISO 8601 with offset); inclusive.</summary>
    public DateTimeOffset? UpdatedFromUtc { get; set; }

    /// <summary>Only runs last updated at or before this instant (ISO 8601 with offset); inclusive.</summary>
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
