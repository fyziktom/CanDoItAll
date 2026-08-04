using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class WorkflowProviderCompatibilityService(
    IProviderProfileRegistry? providerRegistry,
    IProviderRuntimeProfileSource? providerSource,
    IProviderProfileService? providerProfileService)
{
    internal async Task<IReadOnlyList<WorkflowProviderOption>> ListProviderOptionsAsync(
        CancellationToken cancellationToken)
    {
        if (providerRegistry is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return [];
        }

        var providers = await providerRegistry.ListProvidersAsync(cancellationToken);
        return providers
            .Select(NormalizeProvider)
            .Where(provider => provider.Purpose == ProviderProfilePurpose.Chat)
            .OrderByDescending(provider => provider.IsEnabled)
            .ThenBy(provider => provider.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateProviderOption)
            .ToArray();
    }

    internal async Task<IReadOnlyList<WorkflowValidationIssue>> ValidateDefinitionAsync(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components,
        CancellationToken cancellationToken)
    {
        var issues = new List<WorkflowValidationIssue>();
        foreach (var component in ResolveEffectiveNodeComponents(definition, components))
        {
            cancellationToken.ThrowIfCancellationRequested();
            issues.AddRange(await ValidateComponentAsync(component, cancellationToken));
        }

        return issues;
    }

    internal async Task<IReadOnlyList<WorkflowValidationIssue>> ValidateComponentAsync(
        LlmCallComponent component,
        CancellationToken cancellationToken)
    {
        if (!component.ProviderProfileId.HasValue || providerSource is null)
        {
            return [];
        }

        var provider = await providerSource.GetProviderAsync(
            component.ProviderProfileId.Value,
            cancellationToken);
        if (provider is null)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{component.ProviderProfileId.Value:D}', which does not exist.")
            ];
        }

        if (!provider.IsEnabled)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references disabled provider '{provider.Name}'.")
            ];
        }

        provider = NormalizeProvider(provider);
        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' references provider '{provider.Name}', which is not a chat provider.")
            ];
        }

        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        var providerSupportsVision = featureMatrix?.SupportsVision ?? true;
        if (component.Modality is (WorkflowModality.Vision or WorkflowModality.Multimodal) &&
            !providerSupportsVision)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.UnsupportedModality,
                    $"LLM Call Component '{component.Id}' requires vision support but provider '{provider.Name}' does not support vision.")
            ];
        }

        var providerSupportsStructuredOutput = featureMatrix?.SupportsStructuredOutput ?? true;
        if (component.ModelSettings.RequireJsonOutput && !providerSupportsStructuredOutput)
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' requires structured JSON output but provider '{provider.Name}' does not support structured output.")
            ];
        }

        var effectiveModel = string.IsNullOrWhiteSpace(component.Model)
            ? provider.DefaultModel
            : component.Model.Trim();
        if (!ProviderPricingDefaults.TryFindPrice(provider.ModelPrices, effectiveModel, out _))
        {
            return
            [
                new WorkflowValidationIssue(
                    WorkflowValidationIssueCode.InvalidProviderModel,
                    $"LLM Call Component '{component.Id}' model '{effectiveModel}' requires a model price row on provider '{provider.Name}'.")
            ];
        }

        return [];
    }

    private ProviderProfile NormalizeProvider(ProviderProfile provider)
    {
        return providerProfileService?.NormalizeImportedProfile(provider) ?? provider;
    }

    private ProviderFeatureMatrix? ResolveProviderFeatureMatrix(ProviderProfile provider)
    {
        return providerProfileService?.ResolveFeatureMatrix(provider);
    }

    private WorkflowProviderOption CreateProviderOption(ProviderProfile provider)
    {
        var featureMatrix = ResolveProviderFeatureMatrix(provider);
        return new WorkflowProviderOption(
            provider.Id,
            provider.Name,
            provider.Kind,
            provider.Transport,
            provider.Purpose,
            provider.DefaultModel,
            BuildModelOptions(provider),
            provider.IsEnabled,
            provider.SupportsStreaming,
            provider.SupportsTools,
            featureMatrix?.SupportsStructuredOutput ?? true,
            featureMatrix?.SupportsVision ?? true,
            provider.SupportsBackgroundResponses);
    }

    private static IReadOnlyList<string> BuildModelOptions(ProviderProfile provider)
    {
        return provider.SuggestedModels
            .Prepend(provider.DefaultModel)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Select(model => model.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<LlmCallComponent> ResolveEffectiveNodeComponents(
        WorkflowDefinition definition,
        IReadOnlyList<LlmCallComponent> components)
    {
        var componentsById = components.ToDictionary(component => component.Id);
        return definition.Graph.Nodes
            .Where(node => node.Kind == WorkflowNodeKind.LlmCall && node.Settings.ComponentId.HasValue)
            .Select(node => (Node: node, Component: componentsById.GetValueOrDefault(node.Settings.ComponentId!.Value)))
            .Where(item => item.Component is not null)
            .Select(item => item.Component! with
            {
                ProviderProfileId = item.Node.Settings.ProviderProfileId ?? item.Component.ProviderProfileId,
                Model = string.IsNullOrWhiteSpace(item.Node.Settings.Model)
                    ? item.Component.Model
                    : item.Node.Settings.Model.Trim()
            })
            .ToArray();
    }
}
