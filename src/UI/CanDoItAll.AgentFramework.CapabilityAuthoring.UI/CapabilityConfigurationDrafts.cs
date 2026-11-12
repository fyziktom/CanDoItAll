namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public sealed class McpCapabilityDraft {
    internal McpCapabilityConfigurationModel? Configuration { get; set; }

    public string Transport { get; set; } = "stdio";

    public bool Hosted { get; set; }

    public string ServerName { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public string ArgumentsText { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string AllowedWorkingDirectoriesText { get; set; } = string.Empty;

    public string AllowedToolsText { get; set; } = string.Empty;

    public string ApprovalMode { get; set; } = "NeverRequire";

    public string EnvironmentVariableBindingsText { get; set; } = string.Empty;

    public string HeaderBindingsText { get; set; } = string.Empty;
}

public sealed class SkillCapabilityDraft {
    internal SkillCapabilityConfigurationModel? Configuration { get; set; }

    public string SkillSource { get; set; } = "file";

    public string SkillRoot { get; set; } = string.Empty;

    public string AllowedExternalRootsText { get; set; } = string.Empty;

    public string RegisteredSkillServiceType { get; set; } = string.Empty;

    public string InlineName { get; set; } = string.Empty;

    public string InlineDescription { get; set; } = string.Empty;

    public string InlineInstructions { get; set; } = string.Empty;

    public string ResourcesJson { get; set; } = "[]";

    public bool ScriptApproval { get; set; } = true;

    public string ScriptTrustLevel { get; set; } = string.Empty;
}

public sealed class ToolCapabilityDraft {
    internal ToolCapabilityConfigurationModel? Configuration { get; set; }

    public string ToolKind { get; set; } = "externalProcess";

    public string RuntimeToolName { get; set; } = string.Empty;

    public string ImplementationKey { get; set; } = string.Empty;

    public string OperationClassificationsText { get; set; } = "externalAction";

    public string SideEffectKind { get; set; } = "ExternalAction";

    public bool RequiresApprovalByDefault { get; set; } = true;

    public bool IsStateChanging { get; set; } = true;

    public string Command { get; set; } = string.Empty;

    public string ArgumentsText { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = ".";

    public string AllowedExecutableNamesText { get; set; } = string.Empty;

    public string ProcessRequiredOutputPropertiesText { get; set; } = string.Empty;

    public string ProcessTimeoutSecondsText { get; set; } = "30";

    public string MaxOutputBytesText { get; set; } = "4096";

    public string HttpMethod { get; set; } = "POST";

    public string Endpoint { get; set; } = string.Empty;

    public string HeaderBindingsText { get; set; } = string.Empty;

    public string HttpRequiredOutputPropertiesText { get; set; } = string.Empty;

    public string HttpTimeoutSecondsText { get; set; } = "30";

    public string MaxResponseBytesText { get; set; } = "4096";

    public string TestInputJson { get; set; } = "{}";
}
