using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class MafWorkflowLlmComponentInvoker(
    IAgentRuntime agentRuntime,
    IProviderProfileRegistry providerRegistry,
    IProviderProfileService providerProfileService) : IWorkflowLlmComponentInvoker
{
    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowDefinition definition,
        WorkflowNode node,
        LlmCallComponent component,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(input);

        var provider = await ResolveProviderAsync(component, cancellationToken);
        var agent = CreateAgent(component, provider);
        var now = DateTimeOffset.UtcNow;
        var session = new ChatSessionRecord(
            Id: Guid.NewGuid(),
            AgentId: agent.Id,
            Title: $"Workflow node {node.Id}",
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            Messages: [],
            PendingApprovals: []);
        var response = await agentRuntime.RunAsync(
            agent,
            provider,
            session,
            capabilities: [],
            memory: [],
            BuildPrompt(definition, node, component, input),
            runtimeSessionKey: null,
            static (_, _, _) => Task.CompletedTask,
            cancellationToken,
            suppressApprovalRequirements: true,
            executionOptions: CreateExecutionOptions(component, input));

        var payload = response.ResponseText.Trim();
        if (RequiresJsonOutput(component))
        {
            ValidateJsonPayload(payload, node, component);
        }

        return new WorkflowNodeExecutionResult(
            node.Id,
            payload,
            component.ResultShape);
    }

    private async Task<ProviderProfile> ResolveProviderAsync(
        LlmCallComponent component,
        CancellationToken cancellationToken)
    {
        if (component.ProviderProfileId is { } providerId)
        {
            var provider = await providerRegistry.GetProviderAsync(providerId, cancellationToken)
                ?? throw new InvalidOperationException($"LLM workflow component '{component.Id}' references provider '{providerId:D}', but that provider is not registered.");

            return ValidateProvider(provider, component);
        }

        var providers = await providerRegistry.ListProvidersAsync(cancellationToken);
        var matchingProvider = providers
            .Select(providerProfileService.NormalizeImportedProfile)
            .Where(provider => provider.IsEnabled && provider.Purpose == ProviderProfilePurpose.Chat)
            .FirstOrDefault(provider => ModelMatches(provider, component.Model));
        if (matchingProvider is not null)
        {
            return matchingProvider;
        }

        throw new InvalidOperationException(
            $"LLM workflow component '{component.Id}' does not specify a provider, and no enabled chat provider offers model '{component.Model}'.");
    }

    private ProviderProfile ValidateProvider(ProviderProfile provider, LlmCallComponent component)
    {
        provider = providerProfileService.NormalizeImportedProfile(provider);
        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"LLM workflow component '{component.Id}' references disabled provider '{provider.Name}'.");
        }

        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            throw new InvalidOperationException($"LLM workflow component '{component.Id}' references provider '{provider.Name}', which is not a chat provider.");
        }

        return provider;
    }

    private static bool ModelMatches(ProviderProfile provider, string model)
        => string.Equals(provider.DefaultModel, model, StringComparison.OrdinalIgnoreCase)
           || provider.SuggestedModels.Any(item => string.Equals(item, model, StringComparison.OrdinalIgnoreCase));

    private static AgentDefinition CreateAgent(LlmCallComponent component, ProviderProfile provider)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: component.Name,
            RoleTitle: "Workflow LLM Component",
            Summary: $"Workflow LLM component '{component.Name}'.",
            Instructions: component.Instructions,
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: provider.Id,
            Model: component.Model,
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: component.ModelSettings.Temperature ?? 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: component.Permissions with
            {
                CanUseTools = false,
                CanAskOtherAgents = false,
                CanEscalateToHuman = false,
                CanObserveOtherAgents = false,
                CanScheduleWork = false,
                RequiresApprovalForExternalCalls = false,
                AutoApproveExternalCallsByDefault = false
            },
            Capabilities: [],
            Tags: ["workflow", "llm-component"],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static string BuildPrompt(
        WorkflowDefinition definition,
        WorkflowNode node,
        LlmCallComponent component,
        WorkflowNodeInput input)
        => $"""
           Execute workflow LLM component '{component.Name}' for workflow '{definition.Name}' node '{node.Id}'.

           Follow the component instructions exactly. Transform only the workflow input payload below.
           Return only the final component result. Do not explain the workflow runtime.

           Workflow input payload:
           {input.PayloadJson}
           """;

    private static AgentRuntimeExecutionOptions CreateExecutionOptions(
        LlmCallComponent component,
        WorkflowNodeInput input)
    {
        var requiresJson = RequiresJsonOutput(component);
        return new(
            StructuredOutput: null,
            FinalizerMode: AgentFinalizerMode.Disabled,
            RequireStructuredOutputValidation: true,
            MaxStructuredOutputRepairAttempts: 0,
            ContextWorkspaceScope: TryResolveProjectScope(input, out var projectScope)
                ? projectScope
                : null,
            RequireJsonResponseFormat: requiresJson,
            ResponseFormatJsonSchema: requiresJson ? ResolveResponseFormatJsonSchema(component) : string.Empty,
            ResponseFormatSchemaName: requiresJson ? "workflow_llm_component_result" : string.Empty,
            ResponseFormatSchemaDescription: requiresJson ? $"Workflow LLM component '{component.Name}' JSON result." : string.Empty);
    }

    private static bool RequiresJsonOutput(LlmCallComponent component)
        => component.ModelSettings.RequireJsonOutput ||
           component.ResultShape.Kind == WorkflowValueShapeKind.Json;

    private static string ResolveResponseFormatJsonSchema(LlmCallComponent component)
        => string.IsNullOrWhiteSpace(component.ModelSettings.ResponseFormatJsonSchema)
            ? string.Empty
            : component.ModelSettings.ResponseFormatJsonSchema.Trim();

    private static bool TryResolveProjectScope(
        WorkflowNodeInput input,
        out WorkspaceScopeDescriptor scope)
    {
        if (TryResolveProjectId(input.PayloadJson, out var projectId))
        {
            scope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
            return true;
        }

        scope = WorkspaceScopeDescriptor.Sandbox;
        return false;
    }

    private static bool TryResolveProjectId(
        string payloadJson,
        out Guid projectId)
    {
        projectId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;
            if (TryReadGuidProperty(root, "projectId", out projectId))
            {
                return true;
            }

            return root.ValueKind == JsonValueKind.Object &&
                   root.TryGetProperty("project", out var project) &&
                   TryReadGuidProperty(project, "id", out projectId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadGuidProperty(
        JsonElement element,
        string propertyName,
        out Guid value)
    {
        value = Guid.Empty;
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String &&
               Guid.TryParse(property.GetString(), out value) &&
               value != Guid.Empty;
    }

    private static void ValidateJsonPayload(
        string payload,
        WorkflowNode node,
        LlmCallComponent component)
    {
        try
        {
            using var _ = JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"LLM workflow node '{node.Id}' component '{component.Id}' returned invalid JSON: {exception.Message}",
                exception);
        }
    }
}
