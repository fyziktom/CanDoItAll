using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Mvc;

namespace CanDoItAll.Web.Api;

internal static class DevelopmentAgentsApi
{
    public static RouteGroupBuilder MapDevelopmentAgentsApi(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Development Agents");

        agents.MapGet("/", async (
                bool includeTemplates,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListAgentsAsync(includeTemplates, cancellationToken)))
            .WithName("ListDevelopmentAgents");

        agents.MapGet("/bootstrap", async (
                bool includeTemplates,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatPageBootstrapAsync(includeTemplates, cancellationToken)))
            .WithName("GetDevelopmentAgentBootstrap");

        agents.MapGet("/{agentId:guid}", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetAgentEditorAsync(agentId, cancellationToken)))
            .WithName("GetDevelopmentAgentEditor");

        agents.MapPost("/", async (
                AgentEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveAgentAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentAgent");

        agents.MapDelete("/{agentId:guid}", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteAgentAsync(agentId, cancellationToken);
            return Results.Ok(new DevelopmentApiAck(true));
        })
        .WithName("DeleteDevelopmentAgent");

        agents.MapPost("/{agentId:guid}/clone", async (
                Guid agentId,
                AgentCloneApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.CloneAgentAsync(agentId, request.CloneName, cancellationToken)))
            .WithName("CloneDevelopmentAgent");

        agents.MapPost("/{agentId:guid}/convert-to-template", async (
                Guid agentId,
                AgentTemplateConversionApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ConvertToTemplateAsync(agentId, request.TemplateKey, cancellationToken)))
            .WithName("ConvertDevelopmentAgentToTemplate");

        agents.MapGet("/{agentId:guid}/export", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ExportAgentAsync(agentId, cancellationToken)))
            .WithName("ExportDevelopmentAgent");

        agents.MapPost("/import", async (
                AgentImportApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ImportAgentAsync(request.PackagePath, cancellationToken)))
            .WithName("ImportDevelopmentAgent");

        MapProviderEndpoints(agents);
        MapCapabilityEndpoints(agents);
        MapMemoryEndpoints(agents);
        MapChatEndpoints(agents);
        MapExecutionEndpoints(agents);

        return group;
    }

    private static void MapProviderEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/providers", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListProvidersAsync(cancellationToken)))
            .WithName("ListDevelopmentAgentProviders");

        agents.MapGet("/providers/{providerId:guid}/editor", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetProviderEditorAsync(providerId, cancellationToken)))
            .WithName("GetDevelopmentAgentProviderEditor");

        agents.MapPost("/providers", async (
                ProviderProfileEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveProviderAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentAgentProvider");

        agents.MapDelete("/providers/{providerId:guid}", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteProviderAsync(providerId, cancellationToken);
            return Results.Ok(new DevelopmentApiAck(true));
        })
        .WithName("DeleteDevelopmentAgentProvider");

        agents.MapPost("/providers/{providerId:guid}/test", async (
                Guid providerId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.TestProviderAsync(providerId, cancellationToken)))
            .WithName("TestDevelopmentAgentProvider");

        agents.MapPost("/providers/{providerId:guid}/test-chat", async (
                Guid providerId,
                ProviderTestChatRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.RunProviderTestChatAsync(providerId, request, cancellationToken)))
            .WithName("RunDevelopmentAgentProviderTestChat");

        agents.MapPost("/providers/{providerId:guid}/ollama-modelfile", async (
                Guid providerId,
                OllamaModelfileRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.CreateOrUpdateOllamaModelAsync(providerId, request, cancellationToken)))
            .WithName("CreateDevelopmentAgentProviderOllamaModelfile");
    }

    private static void MapCapabilityEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/capabilities", async (
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListCapabilitiesAsync(cancellationToken)))
            .WithName("ListDevelopmentAgentCapabilities");

        agents.MapGet("/capabilities/{capabilityId:guid}/editor", async (
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetCapabilityEditorAsync(capabilityId, cancellationToken)))
            .WithName("GetDevelopmentAgentCapabilityEditor");

        agents.MapPost("/capabilities", async (
                CapabilityEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveCapabilityAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentAgentCapability");

        agents.MapDelete("/capabilities/{capabilityId:guid}", async (
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteCapabilityAsync(capabilityId, cancellationToken);
            return Results.Ok(new DevelopmentApiAck(true));
        })
        .WithName("DeleteDevelopmentAgentCapability");

        agents.MapPost("/{agentId:guid}/capabilities/{capabilityId:guid}/verify", async (
                Guid agentId,
                Guid capabilityId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);
            return Results.Ok(new DevelopmentApiAck(true));
        })
        .WithName("VerifyDevelopmentAgentCapability");
    }

    private static void MapMemoryEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/memory", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListMemoryAsync(agentId, cancellationToken)))
            .WithName("ListDevelopmentAgentMemory");

        agents.MapPost("/memory", async (
                MemoryEditorModel request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SaveMemoryAsync(request, cancellationToken)))
            .WithName("SaveDevelopmentAgentMemory");

        agents.MapDelete("/memory/{memoryId:guid}", async (
                Guid memoryId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
        {
            await workspaceService.DeleteMemoryAsync(memoryId, cancellationToken);
            return Results.Ok(new DevelopmentApiAck(true));
        })
        .WithName("DeleteDevelopmentAgentMemory");
    }

    private static void MapChatEndpoints(RouteGroupBuilder agents)
    {
        agents.MapGet("/{agentId:guid}/chat-sessions", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListChatSessionsAsync(agentId, cancellationToken)))
            .WithName("ListDevelopmentAgentChatSessions");

        agents.MapPost("/{agentId:guid}/chat-sessions", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetOrCreateChatSessionAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("CreateDevelopmentAgentChatSession");

        agents.MapPost("/{agentId:guid}/chat-sessions/{chatSessionId:guid}/rename", async (
                Guid agentId,
                Guid chatSessionId,
                ChatSessionRenameApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.RenameChatSessionAsync(agentId, chatSessionId, request.Title, cancellationToken)))
            .WithName("RenameDevelopmentAgentChatSession");

        agents.MapGet("/{agentId:guid}/chat-workspace", async (
                Guid agentId,
                Guid? preferredSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatAgentWorkspaceAsync(agentId, preferredSessionId, cancellationToken)))
            .WithName("GetDevelopmentAgentChatWorkspace");

        agents.MapPost("/{agentId:guid}/chat", async (
                Guid agentId,
                AgentChatApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.SendMessageAsync(agentId, request.ChatSessionId, request.Prompt, cancellationToken)))
            .WithName("SendDevelopmentAgentChatMessage");

        agents.MapPost("/execution-runs/{executionRunId:guid}/pending-approvals", async (
                Guid executionRunId,
                PendingApprovalApiRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ContinueExecutionRunAsync(
                executionRunId,
                request.Approved,
                request.AutoApprovePendingToolCalls,
                cancellationToken)))
            .WithName("RespondToDevelopmentAgentExecutionApprovals");
    }

    private static void MapExecutionEndpoints(RouteGroupBuilder agents)
    {
        agents.MapPost("/execution-runs", async (
                ExecutionRunRequest request,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ExecuteRunAsync(request, cancellationToken)))
            .WithName("StartDevelopmentAgentExecutionRun");

        agents.MapGet("/execution-runs", async (
                [AsParameters] AgentExecutionRunApiQuery query,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionRunsAsync(query.ToExecutionRunQuery(), cancellationToken)))
            .WithName("ListDevelopmentAgentExecutionRuns");

        agents.MapGet("/execution-runs/{executionRunId:guid}", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken)))
            .WithName("GetDevelopmentAgentExecutionRunDetail");

        agents.MapGet("/execution-runs/{executionRunId:guid}/artifacts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionArtifactsAsync(executionRunId, cancellationToken)))
            .WithName("ListDevelopmentAgentExecutionArtifacts");

        agents.MapGet("/execution-runs/{executionRunId:guid}/checkpoints", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionWorkflowCheckpointsAsync(executionRunId, cancellationToken)))
            .WithName("ListDevelopmentAgentExecutionCheckpoints");

        agents.MapGet("/execution-runs/{executionRunId:guid}/tool-receipts", async (
                Guid executionRunId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListToolExecutionReceiptsAsync(executionRunId, cancellationToken)))
            .WithName("ListDevelopmentAgentExecutionToolReceipts");

        agents.MapGet("/{agentId:guid}/execution-log", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListExecutionLogAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("ListDevelopmentAgentExecutionLog");

        agents.MapGet("/{agentId:guid}/runtime-snapshot", async (
                Guid agentId,
                Guid? chatSessionId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.GetChatRuntimeSnapshotAsync(agentId, chatSessionId, cancellationToken)))
            .WithName("GetDevelopmentAgentRuntimeSnapshot");

        agents.MapGet("/{agentId:guid}/metrics", async (
                Guid agentId,
                IAgentFrameworkWorkspaceService workspaceService,
                CancellationToken cancellationToken) =>
            Results.Ok(await workspaceService.ListMetricsAsync(agentId, cancellationToken)))
            .WithName("ListDevelopmentAgentMetrics");
    }
}

internal sealed record AgentCloneApiRequest(string CloneName);

internal sealed record AgentTemplateConversionApiRequest(string TemplateKey);

internal sealed record AgentImportApiRequest(string PackagePath);

internal sealed record ChatSessionRenameApiRequest(string Title);

internal sealed record AgentChatApiRequest(Guid? ChatSessionId, string Prompt);

internal sealed record PendingApprovalApiRequest(bool Approved, bool AutoApprovePendingToolCalls);

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

    public ExecutionRunQuery ToExecutionRunQuery()
    {
        return new ExecutionRunQuery(
            AgentId,
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
