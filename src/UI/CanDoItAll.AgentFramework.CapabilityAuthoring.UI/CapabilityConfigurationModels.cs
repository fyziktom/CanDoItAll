using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

internal sealed class McpCapabilityConfigurationModel {
    public string? Transport { get; set; }

    public bool? Hosted { get; set; }

    public string? ServerName { get; set; }

    public string? Endpoint { get; set; }

    public string? Command { get; set; }

    public List<string>? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? MessageFraming { get; set; }

    public List<string>? AllowedWorkingDirectories { get; set; }

    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    public Dictionary<string, string>? EnvironmentVariableBindings { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public Dictionary<string, string>? HeaderBindings { get; set; }

    public List<string>? AllowedTools { get; set; }

    public string? ApprovalMode { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ToolCapabilityConfigurationModel {
    public string? ToolKind { get; set; }

    public string? RuntimeToolName { get; set; }

    public string? ImplementationKey { get; set; }

    public List<string>? OperationClassifications { get; set; }

    public CapabilitySideEffectConfigurationModel? SideEffects { get; set; }

    public ExternalProcessToolConfigurationModel? ExternalProcess { get; set; }

    public ExternalHttpToolConfigurationModel? ExternalHttp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class CapabilitySideEffectConfigurationModel {
    public string? Kind { get; set; }

    public bool? RequiresApprovalByDefault { get; set; }

    public bool? IsStateChanging { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ExternalProcessToolConfigurationModel {
    public string? Command { get; set; }

    public List<string>? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public List<string>? AllowedExecutableNames { get; set; }

    public List<string>? RequiredOutputProperties { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? MaxOutputBytes { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ExternalHttpToolConfigurationModel {
    public string? Method { get; set; }

    public string? Endpoint { get; set; }

    public Dictionary<string, string>? HeaderBindings { get; set; }

    public List<string>? RequiredOutputProperties { get; set; }

    public int? TimeoutSeconds { get; set; }

    public int? MaxResponseBytes { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class SkillCapabilityConfigurationModel {
    public string? SkillSource { get; set; }

    public string? SkillRoot { get; set; }

    public List<string>? AllowedExternalRoots { get; set; }

    public string? RegisteredSkillServiceType { get; set; }

    public InlineSkillConfigurationModel? InlineSkill { get; set; }

    public bool? ScriptApproval { get; set; }

    public FileSkillScriptExecutionConfigurationModel? ScriptExecution { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class FileSkillScriptExecutionConfigurationModel {
    public bool? ApprovalRequired { get; set; }

    public string? TrustLevel { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class InlineSkillConfigurationModel {
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    public List<InlineSkillResourceConfigurationModel>? Resources { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class InlineSkillResourceConfigurationModel {
    public string? Name { get; set; }

    public string? Content { get; set; }

    public string? Description { get; set; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.Ordinal);
}
