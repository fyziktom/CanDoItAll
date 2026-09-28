namespace CanDoItAll.Modules.Prompts;

/// <summary>
/// Why a compatibility evaluation is made, as a JSON integer: 0 Selection (choosing an item; item-kind and model
/// mismatches are warnings that a saved suppression can mark), 1 Execution (running the item; every issue is an error
/// and the item needs a version).
/// </summary>
public enum PromptGalleryCompatibilityPurpose
{
    Selection,
    Execution
}

/// <summary>
/// Kind of compatibility issue, as a JSON integer: 0 Archived, 1 MissingFinalVersion, 2 ConsumerNotSupported,
/// 3 ItemKindMismatch, 4 ProviderModelNotSupported. Only ItemKindMismatch and ProviderModelNotSupported can be
/// suppressed, and only for Selection.
/// </summary>
public enum PromptCompatibilityIssueCode
{
    Archived,
    MissingFinalVersion,
    ConsumerNotSupported,
    ItemKindMismatch,
    ProviderModelNotSupported
}

/// <summary>
/// Severity of a compatibility issue, as a JSON integer: 0 Warning (the item can still be used), 1 Error (the item
/// cannot be used in this context).
/// </summary>
public enum PromptCompatibilitySeverity
{
    Warning,
    Error
}

/// <summary>
/// The context in which a Prompt Gallery item would be used, for a compatibility evaluation. Members other than
/// <c>consumer</c> may be omitted; an omitted member takes the default stated on it.
/// </summary>
/// <param name="Consumer">
/// Consumer that would use the item, as a JSON integer: 0 Workflow, 1 AgentRuntime, 2 Chat, 3 ProjectWorkbench;
/// required.
/// </param>
/// <param name="Purpose">
/// Why the item is evaluated, as a JSON integer: 0 Selection, 1 Execution. Omitted means Selection.
/// </param>
/// <param name="RequiredKind">
/// Kind of item the consumer needs, as a JSON integer: 0 FullPrompt, 1 Part. Null or omitted skips the kind check.
/// </param>
/// <param name="Provider">
/// Provider that would run the item, for example <c>OpenAi</c>; at most 120 characters, compared case-insensitively
/// with the declared supported models. Null or blank, together with a blank <c>model</c>, skips the model check.
/// </param>
/// <param name="Model">
/// Model that would run the item; at most 200 characters, compared case-insensitively. Null or blank matches any model
/// of the provider.
/// </param>
/// <param name="RequiresFinalVersion">
/// True requires a version also for Selection. Omitted means false; Execution always requires one.
/// </param>
public sealed record PromptGalleryConsumerContext(
    PromptGalleryConsumer Consumer,
    PromptGalleryCompatibilityPurpose Purpose = PromptGalleryCompatibilityPurpose.Selection,
    PromptGalleryItemKind? RequiredKind = null,
    string? Provider = null,
    string? Model = null,
    bool RequiresFinalVersion = false);

/// <summary>
/// One problem found by a compatibility evaluation.
/// </summary>
/// <param name="Code">
/// Kind of issue, as a JSON integer: 0 Archived, 1 MissingFinalVersion, 2 ConsumerNotSupported, 3 ItemKindMismatch,
/// 4 ProviderModelNotSupported.
/// </param>
/// <param name="Severity">Severity, as a JSON integer: 0 Warning, 1 Error.</param>
/// <param name="Message">English explanation, for display; its wording can change.</param>
/// <param name="IsSuppressible">
/// True for a warning that a saved suppression can mark: ItemKindMismatch or ProviderModelNotSupported during
/// Selection.
/// </param>
/// <param name="IsSuppressed">
/// True when a saved suppression of the item for the evaluated consumer marks this warning.
/// </param>
public sealed record PromptCompatibilityIssue(
    PromptCompatibilityIssueCode Code,
    PromptCompatibilitySeverity Severity,
    string Message,
    bool IsSuppressible,
    bool IsSuppressed);

/// <summary>
/// Result of a compatibility evaluation of a Prompt Gallery item; the evaluation changes nothing.
/// </summary>
/// <param name="Issues">The issues found; an empty array when the item fits the context.</param>
public sealed record PromptCompatibilityResult(IReadOnlyList<PromptCompatibilityIssue> Issues)
{
    /// <summary>
    /// True when no issue has severity Error, so the item can be used in the evaluated context.
    /// </summary>
    public bool CanUse => Issues.All(issue => issue.Severity != PromptCompatibilitySeverity.Error);

    /// <summary>
    /// True when at least one warning is not marked by a saved suppression.
    /// </summary>
    public bool HasVisibleWarnings => Issues.Any(issue =>
        issue.Severity == PromptCompatibilitySeverity.Warning && !issue.IsSuppressed);
}

public sealed class PromptGalleryCompatibilityEvaluator
{
    public PromptCompatibilityResult Evaluate(
        PromptGalleryItemDetails item,
        PromptGalleryConsumerContext context,
        IReadOnlySet<PromptCompatibilityIssueCode>? suppressedIssueCodes = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        return EvaluateCore(
            item.Kind,
            item.IsArchived,
            item.CurrentVersionNumber,
            item.SupportedModels,
            item.SupportedConsumers,
            context,
            suppressedIssueCodes);
    }

    public PromptCompatibilityResult Evaluate(
        PromptGalleryCompatibilitySnapshot item,
        PromptGalleryConsumerContext context,
        IReadOnlySet<PromptCompatibilityIssueCode>? suppressedIssueCodes = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        return EvaluateCore(
            item.Kind,
            item.IsArchived,
            item.CurrentVersionNumber,
            item.SupportedModels,
            item.SupportedConsumers,
            context,
            suppressedIssueCodes);
    }

    private static PromptCompatibilityResult EvaluateCore(
        PromptGalleryItemKind kind,
        bool isArchived,
        int currentVersionNumber,
        IReadOnlyList<PromptProviderModel> supportedModels,
        IReadOnlyList<PromptGalleryConsumer> supportedConsumers,
        PromptGalleryConsumerContext context,
        IReadOnlySet<PromptCompatibilityIssueCode>? suppressedIssueCodes)
    {
        ArgumentNullException.ThrowIfNull(context);

        var issues = new List<PromptCompatibilityIssue>();

        if (isArchived)
        {
            AddError(
                PromptCompatibilityIssueCode.Archived,
                "Archived Gallery items cannot be selected or executed.");
        }

        if ((context.RequiresFinalVersion || context.Purpose == PromptGalleryCompatibilityPurpose.Execution) &&
            currentVersionNumber <= 0)
        {
            AddError(
                PromptCompatibilityIssueCode.MissingFinalVersion,
                "This Gallery item has no immutable final version.");
        }

        if (supportedConsumers.Count > 0 && !supportedConsumers.Contains(context.Consumer))
        {
            AddError(
                PromptCompatibilityIssueCode.ConsumerNotSupported,
                $"This Gallery item does not support the {context.Consumer} consumer.");
        }

        if (context.RequiredKind.HasValue && kind != context.RequiredKind.Value)
        {
            AddContextualIssue(
                PromptCompatibilityIssueCode.ItemKindMismatch,
                $"This consumer requires a {context.RequiredKind.Value} item, but the selected item is {kind}.");
        }

        if (supportedModels.Count > 0 &&
            (!string.IsNullOrWhiteSpace(context.Provider) || !string.IsNullOrWhiteSpace(context.Model)) &&
            !supportedModels.Any(model => Matches(model, context.Provider, context.Model)))
        {
            AddContextualIssue(
                PromptCompatibilityIssueCode.ProviderModelNotSupported,
                "The selected provider and model are not declared as supported by this Gallery item.");
        }

        return new PromptCompatibilityResult(issues);

        void AddError(PromptCompatibilityIssueCode code, string message)
        {
            issues.Add(new PromptCompatibilityIssue(
                code,
                PromptCompatibilitySeverity.Error,
                message,
                IsSuppressible: false,
                IsSuppressed: false));
        }

        void AddContextualIssue(PromptCompatibilityIssueCode code, string message)
        {
            var isExecution = context.Purpose == PromptGalleryCompatibilityPurpose.Execution;
            var isSuppressible = !isExecution && CanSuppress(code);
            var isSuppressed = isSuppressible && suppressedIssueCodes?.Contains(code) == true;
            issues.Add(new PromptCompatibilityIssue(
                code,
                isExecution ? PromptCompatibilitySeverity.Error : PromptCompatibilitySeverity.Warning,
                message,
                isSuppressible,
                isSuppressed));
        }
    }

    public static bool CanSuppress(PromptCompatibilityIssueCode issueCode)
        => issueCode is PromptCompatibilityIssueCode.ItemKindMismatch or
            PromptCompatibilityIssueCode.ProviderModelNotSupported;

    private static bool Matches(PromptProviderModel supported, string? provider, string? model)
    {
        var providerMatches = string.IsNullOrWhiteSpace(provider) ||
            string.Equals(supported.Provider, provider.Trim(), StringComparison.OrdinalIgnoreCase);
        var modelMatches = string.IsNullOrWhiteSpace(model) ||
            string.Equals(supported.Model, model.Trim(), StringComparison.OrdinalIgnoreCase);
        return providerMatches && modelMatches;
    }
}
