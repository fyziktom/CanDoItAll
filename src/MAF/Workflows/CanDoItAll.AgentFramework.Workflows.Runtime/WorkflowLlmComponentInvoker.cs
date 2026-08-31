using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Workflows.Runtime;

/// <summary>
/// Executes ordinary workflow LLM-call nodes over the lightweight, provider-neutral <see cref="ILlmInvocationPort"/>.
/// This invoker never constructs an agent definition or session, never assembles capabilities/memory/context
/// contributors, and never infers workspace/authority scope from workflow payload content - the port is a
/// stateless single-turn transformation, not a reduced agent runtime.
/// </summary>
public sealed class WorkflowLlmComponentInvoker(
    ILlmInvocationPort llmInvocationPort,
    IProviderRuntimeProfileSource providerSource,
    IProviderProfileService providerProfileService,
    TimeProvider? timeProvider = null) : IWorkflowLlmComponentInvoker
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

        var effectiveComponent = ApplyNodeExecutionOverrides(node, component);
        var provider = await ResolveProviderAsync(effectiveComponent, cancellationToken);
        var model = ResolveEffectiveModel(effectiveComponent, provider);
        var clock = timeProvider ?? TimeProvider.System;
        var now = clock.GetUtcNow();
        var invocationId = Guid.NewGuid();

        if (WorkflowInstructionSnapshotPolicy.RequiresComponentBackfill(node.Settings.Instructions))
        {
            throw new InvalidOperationException(
                $"LLM workflow node '{node.Id}' has no usable immutable instruction snapshot. " +
                "Its instructions are blank or contain the legacy template placeholder and must be backfilled from the component before execution.");
        }

        var requiresJson = RequiresJsonOutput(effectiveComponent);
        var messages = new[]
        {
            new LlmMessage(LlmMessageRole.System, node.Settings.Instructions.Trim()),
            new LlmMessage(LlmMessageRole.User, BuildPrompt(definition, node, effectiveComponent, input))
        };
        var request = new LlmInvocationRequest(
            provider,
            model,
            messages,
            responseFormat: requiresJson
                ? new LlmResponseFormat(
                    true,
                    ResolveResponseFormatJsonSchema(effectiveComponent),
                    "workflow_llm_component_result",
                    $"Workflow LLM component '{effectiveComponent.Name}' JSON result.")
                : null,
            settings: new LlmModelSettings(effectiveComponent.ModelSettings.Temperature),
            correlationId: $"workflow:{definition.Id:N}:{node.Id}") {
                History = WorkflowHistoryInvocation.Create(invocationId)
            };

        LlmInvocationResult response;
        try
        {
            response = await llmInvocationPort.InvokeAsync(request, cancellationToken);
        }
        catch (Exception exception)
        {
            var failureCompletedAtUtc = clock.GetUtcNow();
            var failureUsage = (exception as LlmInvocationException)?.Usage;
            var unavailable = WorkflowUsageObservationFactory.FromProviderResponseMetrics(
                CreateUsageContext(definition, node, component, invocationId, now, failureCompletedAtUtc),
                provider,
                model,
                inputTokens: failureUsage?.InputTokens ?? 0,
                cachedInputTokens: failureUsage?.CachedInputTokens ?? 0,
                outputTokens: failureUsage?.OutputTokens ?? 0,
                reasoningTokens: 0,
                totalTokens: failureUsage is null
                    ? 0
                    : checked(failureUsage.InputTokens + failureUsage.OutputTokens),
                toolCallCount: 0,
                failureCompletedAtUtc);
            unavailable = WorkflowHistoryInvocation.Attach(unavailable, request.History);
            if (exception is OperationCanceledException cancelled) {
                throw new WorkflowUsageCancellationException(cancelled, [unavailable]);
            }
            throw new WorkflowUsageObservationException(exception.Message, exception, [unavailable]);
        }

        var completedAtUtc = clock.GetUtcNow();
        var usageContext = CreateUsageContext(
            definition,
            node,
            component,
            invocationId,
            now,
            completedAtUtc);
        var usageObservations = new[]
        {
            WorkflowUsageObservationFactory.FromProviderResponseMetrics(
                usageContext,
                provider,
                model,
                response.Usage.InputTokens,
                response.Usage.CachedInputTokens,
                response.Usage.OutputTokens,
                reasoningTokens: 0,
                totalTokens: response.Usage.InputTokens + response.Usage.OutputTokens,
                toolCallCount: 0,
                completedAtUtc)
        };

        usageObservations[0] = WorkflowHistoryInvocation.Attach(usageObservations[0], request.History);
        var payload = response.ResponseText.Trim();
        try
        {
            if (RequiresJsonOutput(component))
            {
                ValidateJsonPayload(payload, node, component);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new WorkflowUsageObservationException(exception.Message, exception, usageObservations);
        }

        return new WorkflowNodeExecutionResult(
            node.Id,
            payload,
            component.ResultShape)
        {
            Usage = WorkflowUsageCompatibilityProjection.Project(usageObservations, provider.Name, model),
            UsageObservations = usageObservations
        };
    }

    private static WorkflowUsageObservationContext CreateUsageContext(
        WorkflowDefinition definition,
        WorkflowNode node,
        LlmCallComponent component,
        Guid invocationId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
        => new(
            WorkflowExecutorExecutionAuditScope.CurrentRunId,
            definition.Id,
            definition.VersionId,
            node.Id,
            ExecutorId: null,
            component.Id,
            WorkflowUsageProducerKind.LlmComponent,
            invocationId,
            Attempt: 1,
            startedAtUtc,
            completedAtUtc);

    private async Task<ProviderProfile> ResolveProviderAsync(
        LlmCallComponent component,
        CancellationToken cancellationToken)
    {
        if (component.ProviderProfileId is { } providerId)
        {
            var provider = await providerSource.GetProviderAsync(providerId, cancellationToken)
                ?? throw new InvalidOperationException($"LLM workflow component '{component.Id}' references provider '{providerId:D}', but that provider is not registered.");

            return ValidateProvider(provider, component);
        }

        var providers = await providerSource.ListProvidersAsync(cancellationToken);
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

    private static string ResolveEffectiveModel(
        LlmCallComponent component,
        ProviderProfile provider)
    {
        return string.IsNullOrWhiteSpace(component.Model)
            ? provider.DefaultModel
            : component.Model.Trim();
    }

    private static LlmCallComponent ApplyNodeExecutionOverrides(
        WorkflowNode node,
        LlmCallComponent component)
    {
        return component with
        {
            ProviderProfileId = node.Settings.ProviderProfileId ?? component.ProviderProfileId,
            Model = string.IsNullOrWhiteSpace(node.Settings.Model)
                ? component.Model
                : node.Settings.Model.Trim()
        };
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

    private static bool RequiresJsonOutput(LlmCallComponent component)
        => component.ModelSettings.RequireJsonOutput ||
           component.ResultShape.Kind == WorkflowValueShapeKind.Json;

    private static string ResolveResponseFormatJsonSchema(LlmCallComponent component)
        => string.IsNullOrWhiteSpace(component.ModelSettings.ResponseFormatJsonSchema)
            ? string.Empty
            : component.ModelSettings.ResponseFormatJsonSchema.Trim();

    private static void ValidateJsonPayload(
        string payload,
        WorkflowNode node,
        LlmCallComponent component)
    {
        var schemaJson = ResolveResponseFormatJsonSchema(component);
        if (!string.IsNullOrWhiteSpace(schemaJson))
        {
            AgentJsonSchemaOutputResult validation;
            try
            {
                validation = AgentJsonSchemaOutputValidator.Validate(
                    schemaJson,
                    payload,
                    schemaName: "workflow_llm_component_result");
            }
            catch (AgentJsonSchemaOutputContractException exception)
            {
                throw new InvalidOperationException(
                    $"LLM workflow node '{node.Id}' component '{component.Id}' has an invalid response JSON Schema " +
                    $"({exception.Code}): {exception.Message}",
                    exception);
            }

            if (validation.ValidationStatus != AgentJsonSchemaOutputValidationStatus.Valid)
            {
                throw new InvalidOperationException(
                    $"LLM workflow node '{node.Id}' component '{component.Id}' returned output that failed the configured " +
                    $"response JSON Schema validation ({validation.ValidationStatus}): " +
                    FormatSchemaValidationErrors(validation.ValidationErrors));
            }

            return;
        }

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

    private static string FormatSchemaValidationErrors(
        IReadOnlyList<AgentJsonSchemaOutputValidationError> errors)
    {
        const int maximumReportedErrors = 8;
        if (errors.Count == 0)
        {
            return "No validation details were reported.";
        }

        var summary = string.Join(
            "; ",
            errors.Take(maximumReportedErrors)
                .Select(error => $"[{error.Code}] {error.Path}: {error.Message}"));
        var remaining = errors.Count - maximumReportedErrors;
        return remaining > 0
            ? $"{summary}; plus {remaining} more validation error(s)."
            : summary;
    }
}
