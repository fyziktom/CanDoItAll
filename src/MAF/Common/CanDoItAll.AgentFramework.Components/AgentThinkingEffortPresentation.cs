using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;

namespace CanDoItAll.AgentFramework.Components;

public sealed class AgentThinkingEffortPresentation {
    private AgentThinkingEffortPresentation(ProviderProfile? provider, string? model, AgentReasoningEffortLevel? value) {
        Provider = provider;
        Model = model;
        Value = value;
        Prepare();
    }

    public static ConversationThinkingEffortPresentation<AgentReasoningEffortLevel?> Create(
        ProviderProfile? provider, string? model, AgentReasoningEffortLevel? value) {
        var presentation = new AgentThinkingEffortPresentation(provider, model, value);
        return new(presentation.options.AsReadOnly(), presentation.SelectedChoiceValue, presentation.IsSelectorDisabled,
            presentation.SupportAlertStyle, presentation.supportMessage);
    }

    private const string ProviderDefaultChoiceValue = "__provider_default__";

    private readonly List<ConversationThinkingEffortOption<AgentReasoningEffortLevel?>> options = [];
    private ProviderModelThinkingEffortCapability capability = CreateUnknownCapability();
    private string effectiveModel = string.Empty;
    private string? providerDefaultError;
    private string supportMessage = string.Empty;
    private AlertStyle SupportAlertStyle { get; set; } = AlertStyle.Info;

    private ProviderProfile? Provider { get; }

    private string? Model { get; }

    private AgentReasoningEffortLevel? Value { get; }

    private bool IsSelectorDisabled =>
        Value is null && capability.Status != AgentThinkingEffortSupportStatus.Supported;

    private string SelectedChoiceValue => Value is null
        ? ProviderDefaultChoiceValue
        : AgentThinkingEffortPolicy.FormatEffort(Value.Value);

    private bool HasIncompatibleValue =>
        Value is { } value &&
        (capability.Status != AgentThinkingEffortSupportStatus.Supported ||
         !capability.AllowedEfforts.Contains(value));

    private void Prepare() {
        effectiveModel = ResolveEffectiveModel();
        capability = Provider is null
            ? CreateUnknownCapability()
            : AgentThinkingEffortPolicy.ResolveCapability(Provider, effectiveModel);
        providerDefaultError = null;

        RebuildOptions();
        ResolveSupportGuidance();
    }

    private void RebuildOptions() {
        options.Clear();
        options.Add(new ConversationThinkingEffortOption<AgentReasoningEffortLevel?>(
            ProviderDefaultChoiceValue,
            null,
            ResolveProviderDefaultLabel()));

        if (capability.Status == AgentThinkingEffortSupportStatus.Supported) {
            options.AddRange(capability.AllowedEfforts.Select(effort =>
                new ConversationThinkingEffortOption<AgentReasoningEffortLevel?>(
                    AgentThinkingEffortPolicy.FormatEffort(effort),
                    effort,
                    FormatEffortLabel(effort))));
        }

        if (HasIncompatibleValue) {
            options.Add(new ConversationThinkingEffortOption<AgentReasoningEffortLevel?>(
                AgentThinkingEffortPolicy.FormatEffort(Value!.Value),
                Value,
                $"{FormatEffortLabel(Value!.Value)} (currently configured; unavailable)"));
        }
    }

    private string ResolveProviderDefaultLabel() {
        if (Provider is null || capability.Status != AgentThinkingEffortSupportStatus.Supported) {
            return "Provider default";
        }

        try {
            var providerDefault = AgentThinkingEffortPolicy.ResolveProviderDefault(Provider, effectiveModel);
            return providerDefault is null
                ? "Provider default"
                : $"Provider default ({FormatProviderDefaultEffort(providerDefault.Value)})";
        }
        catch (InvalidOperationException exception) {
            providerDefaultError = exception.Message;
            return "Provider default (unavailable)";
        }
    }

    private void ResolveSupportGuidance() {
        if (Value is null && providerDefaultError is not null) {
            SupportAlertStyle = AlertStyle.Danger;
            supportMessage =
                $"The provider default cannot be applied to {ResolveProviderModelLabel()}. " +
                $"Select a supported override or update the provider configuration. {providerDefaultError}";
            return;
        }

        if (HasIncompatibleValue) {
            SupportAlertStyle = AlertStyle.Danger;
            if (providerDefaultError is not null) {
                supportMessage =
                    $"The configured thinking-effort override '{FormatEffortLabel(Value!.Value)}' cannot be applied to {ResolveProviderModelLabel()}, " +
                    "and the provider default is also unavailable. Select a supported override or update the provider configuration. " +
                    $"{providerDefaultError}";
                return;
            }

            supportMessage =
                $"The configured thinking-effort override '{FormatEffortLabel(Value!.Value)}' cannot be applied to {ResolveProviderModelLabel()}. " +
                $"{capability.Summary} " + (capability.Status == AgentThinkingEffortSupportStatus.Unknown
                    ? ResolveUnknownCapabilityAction()
                    : "Select Provider default to remove this override.");
            return;
        }

        switch (capability.Status) {
            case AgentThinkingEffortSupportStatus.Supported:
                SupportAlertStyle = AlertStyle.Info;
                var disableGuidance = capability.AllowedEfforts.Contains(AgentReasoningEffortLevel.None)
                    ? " None explicitly disables thinking."
                    : string.Empty;
                supportMessage =
                    $"{capability.Summary} Provider default inherits the provider setting.{disableGuidance}";
                return;
            case AgentThinkingEffortSupportStatus.Unsupported:
                SupportAlertStyle = AlertStyle.Warning;
                supportMessage =
                    $"{capability.Summary} This agent must use Provider default because the selected model cannot accept an override.";
                return;
            case AgentThinkingEffortSupportStatus.Unknown:
                SupportAlertStyle = AlertStyle.Warning;
                supportMessage = Provider is null
                    ? capability.Summary
                    : $"{capability.Summary} {ResolveUnknownCapabilityAction()}";
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported thinking-effort capability status '{capability.Status}'.");
        }
    }

    private string ResolveUnknownCapabilityAction() {
        if (Provider?.IsSourceManaged == true) {
            return "Refresh capabilities on the source, then synchronize the shared connection.";
        }
        return Provider?.Kind switch {
            ProviderKind.AzureOpenAi =>
                "Define this deployment's allowed thinking-effort values in the provider configuration before setting an override.",
            ProviderKind.Ollama =>
                "Test the provider to discover capabilities, or configure this model in the provider's Thinking tab.",
            ProviderKind.OpenAi =>
                "Configure this model in the provider's Thinking tab, or use a model with a verified capability definition.",
            _ => "Use a provider and model with verified thinking-effort capabilities before setting an override."
        };
    }

    private string ResolveEffectiveModel() {
        var model = string.IsNullOrWhiteSpace(Model)
            ? Provider?.DefaultModel
            : Model;
        return model?.Trim() ?? string.Empty;
    }

    private string ResolveProviderModelLabel() {
        var providerName = string.IsNullOrWhiteSpace(Provider?.Name)
            ? "the selected provider"
            : $"provider '{Provider.Name}'";
        return string.IsNullOrWhiteSpace(effectiveModel)
            ? providerName
            : $"{providerName} model '{Provider?.GetModelDisplayName(effectiveModel) ?? effectiveModel}'";
    }

    private static ProviderModelThinkingEffortCapability CreateUnknownCapability()
        => new(
            string.Empty,
            AgentThinkingEffortSupportStatus.Unknown,
            AgentThinkingEffortCapabilitySource.Defined,
            [],
            Summary: "Select a provider and model to determine whether thinking effort can be overridden.");

    private string FormatEffortLabel(AgentReasoningEffortLevel effort) {
        if (effort == AgentReasoningEffortLevel.Medium &&
            capability.ControlMode == AgentThinkingEffortControlMode.BooleanToggle) {
            return "Enabled";
        }

        return effort switch {
            AgentReasoningEffortLevel.None => "None (disable thinking)",
            AgentReasoningEffortLevel.Minimal => "Minimal",
            AgentReasoningEffortLevel.Low => "Low",
            AgentReasoningEffortLevel.Medium => "Medium",
            AgentReasoningEffortLevel.High => "High",
            AgentReasoningEffortLevel.ExtraHigh => "Extra high",
            AgentReasoningEffortLevel.Max => "Max",
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported thinking effort.")
        };
    }

    private string FormatProviderDefaultEffort(AgentReasoningEffortLevel effort) {
        return capability.ControlMode == AgentThinkingEffortControlMode.BooleanToggle &&
               effort == AgentReasoningEffortLevel.Medium
            ? "enabled"
            : AgentThinkingEffortPolicy.FormatEffort(effort);
    }

}
