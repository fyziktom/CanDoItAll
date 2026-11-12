using System.ComponentModel;

namespace CanDoItAll.Modules.Workspace;

[Description("Workspace business defaults from the selected database; contains no credentials or deployment switches.")]
public sealed class WorkspaceSettingsModel {
    [Description("Default provider profile GUID, or null when no default is selected.")]
    public Guid? DefaultProviderProfileId { get; set; }

    [Description("Human-readable workspace name.")]
    public string WorkspaceName { get; set; } = "CanDoItAll";

    [Description("Default output format suggested for prompts, such as Markdown.")]
    public string DefaultPromptOutputFormat { get; set; } = "Markdown";

    [Description("Three-letter uppercase currency code used by the workspace.")]
    public string CurrencyCode { get; set; } = "USD";

    [Description(".NET culture name used to format currency values.")]
    public string CurrencyCultureName { get; set; } = "en-US";

    [Description("Operator-authored notes about the workspace.")]
    public string Notes { get; set; } = string.Empty;
}

public static class WorkspaceSettingsSnapshots {
    public static WorkspaceSettingsModel Copy(this WorkspaceSettingsModel value) => new() {
        WorkspaceName = value.WorkspaceName, DefaultProviderProfileId = value.DefaultProviderProfileId,
        DefaultPromptOutputFormat = value.DefaultPromptOutputFormat, CurrencyCode = value.CurrencyCode,
        CurrencyCultureName = value.CurrencyCultureName, Notes = value.Notes
    };
}
