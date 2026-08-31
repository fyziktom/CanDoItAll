using System.Globalization;

namespace CanDoItAll.AgentFramework.Models;

internal sealed record OpenAiThinkingEffortModelDefinition(
    string Model,
    AgentThinkingEffortSupportStatus Status,
    IReadOnlyList<AgentReasoningEffortLevel> AllowedEfforts,
    IReadOnlyList<ProviderTransportKind> AllowedTransports);

internal static class OpenAiThinkingEffortModelRegistry
{
    private static readonly IReadOnlyList<ProviderTransportKind> BothTransports =
    [
        ProviderTransportKind.Responses,
        ProviderTransportKind.ChatCompletions
    ];

    private static readonly IReadOnlyList<ProviderTransportKind> ResponsesOnly =
    [
        ProviderTransportKind.Responses
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> NoneThroughHigh =
    [
        AgentReasoningEffortLevel.None,
        AgentReasoningEffortLevel.Low,
        AgentReasoningEffortLevel.Medium,
        AgentReasoningEffortLevel.High
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> NoneThroughExtraHigh =
    [
        .. NoneThroughHigh,
        AgentReasoningEffortLevel.ExtraHigh
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> NoneThroughMax =
    [
        .. NoneThroughExtraHigh,
        AgentReasoningEffortLevel.Max
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> MinimalThroughHigh =
    [
        AgentReasoningEffortLevel.Minimal,
        AgentReasoningEffortLevel.Low,
        AgentReasoningEffortLevel.Medium,
        AgentReasoningEffortLevel.High
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> LowThroughHigh =
    [
        AgentReasoningEffortLevel.Low,
        AgentReasoningEffortLevel.Medium,
        AgentReasoningEffortLevel.High
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> LowThroughExtraHigh =
    [
        .. LowThroughHigh,
        AgentReasoningEffortLevel.ExtraHigh
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> MediumThroughExtraHigh =
    [
        AgentReasoningEffortLevel.Medium,
        AgentReasoningEffortLevel.High,
        AgentReasoningEffortLevel.ExtraHigh
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> HighOnly =
    [
        AgentReasoningEffortLevel.High
    ];

    private static readonly OpenAiThinkingEffortModelDefinition[] Definitions =
    [
        Supported(OpenAiModelIds.Gpt56Sol, NoneThroughMax, ResponsesOnly),
        Supported(OpenAiModelIds.Gpt56Terra, NoneThroughMax, ResponsesOnly),
        Supported(OpenAiModelIds.Gpt56Luna, NoneThroughMax, ResponsesOnly),
        Supported(OpenAiModelIds.Gpt56, NoneThroughMax, ResponsesOnly),
        Supported("gpt-5.5-pro", MediumThroughExtraHigh, ResponsesOnly),
        Supported("gpt-5.5", NoneThroughExtraHigh),
        Supported("gpt-5.4-pro", MediumThroughExtraHigh, ResponsesOnly),
        Supported(OpenAiModelIds.Gpt54Mini, NoneThroughExtraHigh),
        Supported("gpt-5.4-nano", NoneThroughExtraHigh),
        Supported("gpt-5.4", NoneThroughExtraHigh),
        Supported("gpt-5.3-codex", LowThroughExtraHigh),
        Supported("gpt-5.2-pro", MediumThroughExtraHigh, ResponsesOnly),
        Supported("gpt-5.2", NoneThroughExtraHigh),
        Supported("gpt-5.1", NoneThroughHigh),
        Supported("gpt-5-pro", HighOnly, ResponsesOnly),
        Supported("gpt-5-mini", MinimalThroughHigh),
        Supported("gpt-5-nano", MinimalThroughHigh),
        Supported("gpt-5", MinimalThroughHigh),
        Supported("o1-mini", LowThroughHigh),
        Supported("o1-preview", LowThroughHigh),
        Supported("o1", LowThroughHigh),
        Supported("o3-mini", LowThroughHigh),
        Supported("o3", LowThroughHigh),
        Supported("o4-mini", LowThroughHigh),
        Unsupported("gpt-4.1"),
        Unsupported("gpt-4.1-mini"),
        Unsupported("gpt-4.1-nano"),
        Unsupported("gpt-4o"),
        Unsupported("gpt-4o-mini")
    ];

    internal static IReadOnlyList<OpenAiThinkingEffortModelDefinition> All => Definitions;

    public static OpenAiThinkingEffortModelDefinition? Find(string? model)
    {
        var normalizedModel = model?.Trim() ?? string.Empty;
        return Definitions.FirstOrDefault(definition =>
            MatchesModelOrSnapshot(normalizedModel, definition.Model));
    }

    private static OpenAiThinkingEffortModelDefinition Supported(
        string model,
        IReadOnlyList<AgentReasoningEffortLevel> allowedEfforts,
        IReadOnlyList<ProviderTransportKind>? allowedTransports = null)
    {
        return new OpenAiThinkingEffortModelDefinition(
            model,
            AgentThinkingEffortSupportStatus.Supported,
            allowedEfforts,
            allowedTransports ?? BothTransports);
    }

    private static OpenAiThinkingEffortModelDefinition Unsupported(string model)
    {
        return new OpenAiThinkingEffortModelDefinition(
            model,
            AgentThinkingEffortSupportStatus.Unsupported,
            [],
            BothTransports);
    }

    private static bool MatchesModelOrSnapshot(string model, string definedModel)
    {
        if (string.Equals(model, definedModel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var snapshotPrefix = definedModel + "-";
        if (!model.StartsWith(snapshotPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            model[snapshotPrefix.Length..],
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }
}
