namespace CanDoItAll.Modules.Prompts;

public enum PromptGalleryCompatibilityPurpose
{
    Selection,
    Execution
}

public enum PromptCompatibilityIssueCode
{
    Archived,
    MissingFinalVersion,
    ConsumerNotSupported,
    ItemKindMismatch,
    ProviderModelNotSupported
}

public enum PromptCompatibilitySeverity
{
    Warning,
    Error
}

public sealed record PromptGalleryConsumerContext(
    PromptGalleryConsumer Consumer,
    PromptGalleryCompatibilityPurpose Purpose = PromptGalleryCompatibilityPurpose.Selection,
    PromptGalleryItemKind? RequiredKind = null,
    string? Provider = null,
    string? Model = null,
    bool RequiresFinalVersion = false);

public sealed record PromptCompatibilityIssue(
    PromptCompatibilityIssueCode Code,
    PromptCompatibilitySeverity Severity,
    string Message,
    bool IsSuppressible,
    bool IsSuppressed);

public sealed record PromptCompatibilityResult(IReadOnlyList<PromptCompatibilityIssue> Issues)
{
    public bool CanUse => Issues.All(issue => issue.Severity != PromptCompatibilitySeverity.Error);

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
