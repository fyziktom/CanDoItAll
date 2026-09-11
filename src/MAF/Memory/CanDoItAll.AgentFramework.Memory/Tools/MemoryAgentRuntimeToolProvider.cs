using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Abstractions;
using Microsoft.Extensions.AI;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Memory.Tools;

public sealed class MemoryAgentRuntimeToolProvider : IAgentRuntimeToolProvider
{
    public const string ProviderKey = "memory.runtime-tools";

    private const int ProviderOrder = 925;

    private readonly MemoryAgentQueryTools queryTools;
    private readonly MemoryAgentStatusTool statusTool;
    private readonly IMemoryOperationHandler operationHandler;
    private readonly TimeProvider timeProvider;
    private readonly IAgentCatalogReadLeaseStore? catalogLeases;
    private readonly IAgentToolAdmissionVerifier? admissionVerifier;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        Converters = { new JsonStringEnumConverter() }
    };

    public MemoryAgentRuntimeToolProvider(
        IMemoryOperationHandler operationHandler,
        TimeProvider timeProvider,
        IAgentCatalogReadLeaseStore? catalogLeases = null,
        IAgentToolAdmissionVerifier? admissionVerifier = null)
    {
        this.operationHandler = operationHandler;
        this.timeProvider = timeProvider;
        this.catalogLeases = catalogLeases;
        this.admissionVerifier = admissionVerifier;
        queryTools = new MemoryAgentQueryTools(operationHandler, timeProvider);
        statusTool = new MemoryAgentStatusTool(operationHandler, timeProvider);
    }

    public int Order => ProviderOrder;

    public AgentRuntimeToolProviderDescriptor Descriptor { get; } = new(
        ProviderKey,
        "Memory runtime tools",
        "Provides generic Memory Protocol v1 tools backed by typed agent memory settings.",
        ["agent-framework", "memory"],
        [
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            AgentRuntimeToolProviderPurpose.GovernedProcessAutomation,
            AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive
        ]);

    public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(
        AgentRuntimeToolProviderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var access = AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson);
        if (access.InvocationMode != AgentMemoryInvocationMode.Automatic ||
            !access.CanUseMemoryTools ||
            !context.Agent.Permissions.CanUseTools)
        {
            return ValueTask.FromResult<IReadOnlyList<AITool>>([]);
        }

        var providerAliases = string.Join(
            ", ",
            access.ProviderBindings.Select(binding => binding.Alias.Value));
        var providerHint = providerAliases.Length == 0
            ? "No provider aliases are configured."
            : $"Configured provider aliases: {providerAliases}.";
        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                (MemoryContextQueryToolInput input, CancellationToken token = default) =>
                    queryTools.QueryAsync(context, access, input, token),
                MemoryAgentRuntimeToolNames.ContextQuery,
                "Queries a bound memory provider. ProviderInstanceId accepts an alias or exact bound id. " + providerHint),
            AIFunctionFactory.Create(
                (MemoryOperationStatusToolInput input, CancellationToken token = default) =>
                    statusTool.GetStatusAsync(context, access, input, token),
                MemoryAgentRuntimeToolNames.OperationStatus,
                "Reads the status and persisted final result of a memory query owned by this agent context.")
        };

        return ValueTask.FromResult<IReadOnlyList<AITool>>(tools);
    }

    public IReadOnlyList<AgentRuntimeToolMetadata> GetToolMetadata(
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var access = AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson);
        if (access.InvocationMode != AgentMemoryInvocationMode.Automatic ||
            !access.CanUseMemoryTools ||
            !context.Agent.Permissions.CanUseTools)
        {
            return [];
        }

        return
        [
            CreateMetadata(context, MemoryAgentRuntimeToolNames.ContextQuery, AgentRuntimeToolOperationKind.Read),
            CreateMetadata(context, MemoryAgentRuntimeToolNames.OperationStatus, AgentRuntimeToolOperationKind.Read)
        ];
    }

    private AgentRuntimeToolMetadata CreateMetadata(
        AgentRuntimeToolProviderContext context,
        string toolName,
        AgentRuntimeToolOperationKind operationKind)
    {
        return new AgentRuntimeToolMetadata(
            ProviderKey,
            toolName,
            operationKind,
            requiresApprovalByDefault: false,
            ["memory", "generic-memory"]) {
            AuthorizeResultDisclosureAsync = (disclosure, token) => AuthorizeResultDisclosureAsync(context, toolName, disclosure, token)
        };
    }

    private async ValueTask<IAsyncDisposable?> AuthorizeResultDisclosureAsync(AgentRuntimeToolProviderContext context,
        string toolName, AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        if (catalogLeases is null || admissionVerifier is null || context.AdmittedToolSession is null) {
            throw new AgentToolAdmissionException("memory.result-authority-unavailable",
                "Current canonical Agent grants and the original admitted session are required to disclose a saved Memory result.");
        }
        var admission = await admissionVerifier.RequireSessionAsync(context.AdmittedToolSession, cancellationToken);
        var held = await catalogLeases.AcquireAgentReadLeaseAsync(context.Agent.Id, cancellationToken);
        try {
            var agent = held.Agent;
            if (admission.AgentId != context.Agent.Id || held.Scope != WorkspaceScopeDescriptor.Organization(admission.Profile.ProfileId.ToString("N")) ||
                    agent is null || agent.Id != context.Agent.Id || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active ||
                    !agent.Permissions.CanUseTools) {
                throw DisclosureDenied();
            }
            await RequireReadableResultAsync(context, context with { Agent = agent }, toolName, disclosure, cancellationToken);
            return held;
        } catch {
            await held.DisposeAsync();
            throw;
        }
    }

    private async Task RequireReadableResultAsync(AgentRuntimeToolProviderContext context, AgentRuntimeToolProviderContext currentContext,
        string toolName, AgentToolResultDisclosure disclosure, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        var access = AgentMemoryAccessMetadata.Read(context.Agent.ConfigurationJson);
        var currentAccess = AgentMemoryAccessMetadata.Read(currentContext.Agent.ConfigurationJson);
        if (!context.Agent.Permissions.CanUseTools || access.InvocationMode != AgentMemoryInvocationMode.Automatic ||
                !access.CanUseMemoryTools || currentAccess.InvocationMode != AgentMemoryInvocationMode.Automatic ||
                !currentAccess.CanUseMemoryTools || disclosure.Payload.ToolName != toolName) {
            throw DisclosureDenied();
        }
        if (disclosure.EffectState == AgentToolEffectState.NotCommitted) {
            return;
        }

        using var arguments = JsonDocument.Parse(disclosure.Payload.ArgumentsJson);
        var input = arguments.RootElement.GetProperty("input");
        Guid? operationId;
        string? providerId;
        MemoryCapabilityId capability;
        var isQuery = toolName == MemoryAgentRuntimeToolNames.ContextQuery;
        if (isQuery) {
            var request = input.Deserialize<MemoryContextQueryToolInput>(SerializerOptions) ?? throw DisclosureDenied();
            var result = disclosure.Result.Deserialize<MemoryContextQueryToolResult>(SerializerOptions) ?? throw DisclosureDenied();
            operationId = result.OperationId;
            providerId = result.ProviderInstanceId;
            capability = request.AllowAsync ? MemoryCapabilityIds.ContextQueryAsync : MemoryCapabilityIds.ContextQuerySync;
            if (operationId is null && !result.Success && !result.DispatchAttempted &&
                    result.Summary.Length == 0 && result.Sections.Count == 0 && result.FeedbackHandle is null && result.AsyncOperation is null) {
                return;
            }
            if (result.AsyncOperation is { } accepted && accepted.OperationId != operationId) {
                throw DisclosureDenied();
            }
        } else {
            var request = input.Deserialize<MemoryOperationStatusToolInput>(SerializerOptions) ?? throw DisclosureDenied();
            var result = disclosure.Result.Deserialize<MemoryOperationStatusToolResult>(SerializerOptions) ?? throw DisclosureDenied();
            operationId = result.OperationId;
            providerId = result.ProviderInstanceId;
            capability = MemoryCapabilityIds.OperationStatus;
            if (operationId is null && !result.Success && !result.DispatchAttempted &&
                    result.FinalResult is null && result.FeedbackHandle is null) {
                return;
            }
            if (request.OperationId != operationId) {
                throw DisclosureDenied();
            }
        }
        if (operationId is null || operationId == Guid.Empty || string.IsNullOrWhiteSpace(providerId)) {
            throw DisclosureDenied();
        }

        var policy = MemoryAgentToolPolicyFactory.Resolve(context, access, capability, providerId, providerRequired: true);
        var currentPolicy = MemoryAgentToolPolicyFactory.Resolve(currentContext, currentAccess, capability, providerId, providerRequired: true);
        if (policy.Resolution.Rejection is not null || currentPolicy.Resolution.Rejection is not null) {
            throw DisclosureDenied();
        }
        var current = await operationHandler.GetStatusAsync(MemoryOperationRequestBuilder.Status(
            MemoryAgentToolPolicyFactory.CreateCaller(currentPolicy, toolName), currentPolicy.Resolution.SelectionPolicy,
            new(new(operationId.Value)), MemoryMafRetentionPolicyFactory.Create(timeProvider)), cancellationToken);
        var operation = current.OperationRecord;
        if (current.Status != MemoryOperationHandlerStatus.Completed || current.DriverDispatchAttempted ||
                !current.Selection.DispatchAllowed || operation is null || operation.OperationId.Value != operationId ||
                operation.ProviderInstanceId.Value != providerId ||
                isQuery && (operation.OperationKind != MemoryOperationKind.ContextQuery || operation.RequestedCapability != capability)) {
            throw DisclosureDenied();
        }
        if (operation.OperationKind == MemoryOperationKind.ContextQuery) {
            var original = operation.GetRequiredMemoryRequestContext();
            foreach (var present in new[] { policy.RequestContext, currentPolicy.RequestContext }) {
                if (original.Workspace.WorkspaceId != present.Workspace.WorkspaceId || original.Workspace.Domain != present.Workspace.Domain ||
                        original.Execution.ProjectId != present.Execution.ProjectId || original.Execution.ProcessId != present.Execution.ProcessId ||
                        original.Execution.ProcessStepId != present.Execution.ProcessStepId || original.Execution.WorkflowId != present.Execution.WorkflowId ||
                        original.Execution.WorkflowNodeId != present.Execution.WorkflowNodeId ||
                        original.Policy.AllowedSourceScopes.Any(scope => !present.Policy.AllowedSourceScopes.Contains(scope))) {
                    throw DisclosureDenied();
                }
            }
        }
    }

    private static UnauthorizedAccessException DisclosureDenied()
        => new("The saved Memory result is no longer readable by this caller, provider binding or source scope.");
}
