namespace CanDoItAll.Modules.AgentFramework;

public sealed partial class AgentCapabilitySetupFlowService
{
    private sealed class CapabilityToolConfigurationModel
    {
        public string? ToolKind { get; set; }

        public string? RuntimeToolName { get; set; }

        public string? ImplementationKey { get; set; }

        public List<string>? OperationClassifications { get; set; }

        public CapabilitySideEffectConfigurationModel? SideEffects { get; set; }

        public ExternalProcessToolConfigurationModel? ExternalProcess { get; set; }

        public ExternalHttpToolConfigurationModel? ExternalHttp { get; set; }
    }

    private sealed class CapabilitySideEffectConfigurationModel
    {
        public string? Kind { get; set; }

        public bool? RequiresApprovalByDefault { get; set; }

        public bool? IsStateChanging { get; set; }
    }

    private sealed class ExternalProcessToolConfigurationModel
    {
        public string? Command { get; set; }

        public List<string>? Arguments { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<string>? AllowedExecutableNames { get; set; }

        public List<string>? RequiredOutputProperties { get; set; }

        public int? TimeoutSeconds { get; set; }

        public int? MaxOutputBytes { get; set; }
    }

    private sealed class ExternalHttpToolConfigurationModel
    {
        public string? Method { get; set; }

        public string? Endpoint { get; set; }

        public Dictionary<string, string>? Headers { get; set; }

        public Dictionary<string, string>? HeaderBindings { get; set; }

        public List<string>? RequiredOutputProperties { get; set; }

        public int? TimeoutSeconds { get; set; }

        public int? MaxResponseBytes { get; set; }
    }

    private sealed class McpCapabilityConfigurationModel
    {
        public string? Transport { get; set; }

        public bool? Hosted { get; set; }

        public string? ServerName { get; set; }

        public string? Endpoint { get; set; }

        public string? Command { get; set; }

        public List<string>? Arguments { get; set; }

        public string? WorkingDirectory { get; set; }

        public string? MessageFraming { get; set; }

        public List<string>? AllowedWorkingDirectories { get; set; }

        public Dictionary<string, string>? EnvironmentVariableBindings { get; set; }

        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        public Dictionary<string, string>? HeaderBindings { get; set; }

        public Dictionary<string, string>? Headers { get; set; }

        public List<string>? AllowedTools { get; set; }

        public string? ApprovalMode { get; set; }

        public int? TimeoutSeconds { get; set; }

        public List<string>? OperationClassifications { get; set; }
    }
}
