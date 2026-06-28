using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class CapabilityConfigurationEditorSupport
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static McpCapabilityEditorState ReadMcp(CapabilityEditorModel editor)
    {
        var configuration = Deserialize<McpCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new McpCapabilityConfigurationModel();
        var state = new McpCapabilityEditorState
        {
            Transport = string.IsNullOrWhiteSpace(configuration.Transport)
                ? ResolveMcpTransport(editor, configuration)
                : configuration.Transport.Trim(),
            Hosted = configuration.Hosted == true,
            ServerName = configuration.ServerName ?? string.Empty,
            Endpoint = configuration.Endpoint ?? ResolveEndpointFromEditor(editor),
            Command = configuration.Command ?? ResolveCommandFromEditor(editor),
            WorkingDirectory = configuration.WorkingDirectory ?? string.Empty,
            ApprovalMode = string.IsNullOrWhiteSpace(configuration.ApprovalMode)
                ? "NeverRequire"
                : configuration.ApprovalMode.Trim(),
            ArgumentsText = ToLineText(configuration.Arguments),
            AllowedToolsText = ToLineText(configuration.AllowedTools),
            AllowedWorkingDirectoriesText = ToLineText(configuration.AllowedWorkingDirectories),
            EnvironmentVariableBindingsText = ToKeyValueText(configuration.EnvironmentVariableBindings),
            HeaderBindingsText = ToKeyValueText(configuration.HeaderBindings)
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteMcp(CapabilityEditorModel editor, McpCapabilityEditorState state)
    {
        var errors = new List<string>();
        var configuration = state.Configuration ?? new McpCapabilityConfigurationModel();
        var transport = NormalizeOptionalText(state.Transport) ?? "stdio";
        var arguments = SplitLines(state.ArgumentsText);
        var allowedTools = SplitLines(state.AllowedToolsText);

        configuration.Transport = transport;
        configuration.Hosted = state.Hosted ? true : null;
        configuration.ServerName = NormalizeOptionalText(state.ServerName);
        configuration.Endpoint = NormalizeOptionalText(state.Endpoint);
        configuration.Command = NormalizeOptionalText(state.Command);
        configuration.Arguments = arguments.Count == 0 ? null : arguments;
        configuration.WorkingDirectory = NormalizeOptionalText(state.WorkingDirectory);
        configuration.AllowedWorkingDirectories = SplitLines(state.AllowedWorkingDirectoriesText) is { Count: > 0 } roots ? roots : null;
        configuration.AllowedTools = allowedTools.Count == 0 ? null : allowedTools;
        configuration.ApprovalMode = NormalizeOptionalText(state.ApprovalMode) ?? "NeverRequire";
        configuration.EnvironmentVariables = null;
        configuration.Headers = null;

        var environmentVariableBindings = ParseKeyValueText(state.EnvironmentVariableBindingsText, "environment variable binding", errors);
        var headerBindings = ParseKeyValueText(state.HeaderBindingsText, "header binding", errors);
        configuration.EnvironmentVariableBindings = environmentVariableBindings.Count == 0 ? null : environmentVariableBindings;
        configuration.HeaderBindings = headerBindings.Count == 0 ? null : headerBindings;

        if (string.Equals(transport, "stdio", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(configuration.Command))
            {
                errors.Add("Stdio MCP configuration requires a command.");
            }

            if (allowedTools.Count == 0)
            {
                errors.Add("Local MCP configuration requires at least one allowed tool.");
            }
        }
        else if (!string.Equals(transport, "logical", StringComparison.OrdinalIgnoreCase) &&
                 string.IsNullOrWhiteSpace(configuration.Endpoint))
        {
            errors.Add("Remote MCP configuration requires an endpoint.");
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        editor.EndpointOrPath = ResolveMcpEndpointOrPath(configuration, editor.EndpointOrPath);
        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static SkillCapabilityEditorState ReadSkill(CapabilityEditorModel editor)
    {
        var configuration = Deserialize<SkillCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new SkillCapabilityConfigurationModel();
        var inlineSkill = configuration.InlineSkill ?? new InlineSkillConfigurationModel();
        var source = string.IsNullOrWhiteSpace(configuration.SkillSource)
            ? ResolveSkillSource(editor, configuration)
            : configuration.SkillSource.Trim();

        var state = new SkillCapabilityEditorState
        {
            SkillSource = source,
            SkillRoot = configuration.SkillRoot ?? ResolveSkillRootFromEditor(editor),
            AllowedExternalRootsText = ToLineText(configuration.AllowedExternalRoots),
            RegisteredSkillServiceType = configuration.RegisteredSkillServiceType ?? string.Empty,
            InlineName = inlineSkill.Name ?? string.Empty,
            InlineDescription = inlineSkill.Description ?? string.Empty,
            InlineInstructions = inlineSkill.Instructions ?? string.Empty,
            ScriptApproval = configuration.ScriptExecution?.ApprovalRequired ?? configuration.ScriptApproval ?? true,
            ScriptTrustLevel = configuration.ScriptExecution?.TrustLevel ?? string.Empty
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteSkill(CapabilityEditorModel editor, SkillCapabilityEditorState state)
    {
        var errors = new List<string>();
        var configuration = state.Configuration ?? new SkillCapabilityConfigurationModel();
        var source = NormalizeOptionalText(state.SkillSource) ?? "file";
        configuration.SkillSource = source;
        configuration.AllowedExternalRoots = SplitLines(state.AllowedExternalRootsText) is { Count: > 0 } roots ? roots : null;
        configuration.ScriptApproval = state.ScriptApproval;
        configuration.ScriptExecution = new FileSkillScriptExecutionConfigurationModel
        {
            ApprovalRequired = state.ScriptApproval,
            TrustLevel = NormalizeOptionalText(state.ScriptTrustLevel) ?? ResolveDefaultSkillTrustLevel(source, configuration.AllowedExternalRoots)
        };

        if (string.Equals(source, "inline", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(state.InlineInstructions))
            {
                errors.Add("Inline skill configuration requires instructions.");
            }

            configuration.SkillRoot = null;
            configuration.RegisteredSkillServiceType = null;
            configuration.InlineSkill = new InlineSkillConfigurationModel
            {
                Name = NormalizeOptionalText(state.InlineName) ?? NormalizeKey(editor.Key),
                Description = NormalizeOptionalText(state.InlineDescription) ?? editor.Description,
                Instructions = state.InlineInstructions.Trim(),
                Resources = configuration.InlineSkill?.Resources
            };
            editor.EndpointOrPath = $"inline://{NormalizeKey(editor.Key)}";
        }
        else if (string.Equals(source, "registered", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(state.RegisteredSkillServiceType))
            {
                errors.Add("Registered skill configuration requires a service type.");
            }

            configuration.SkillRoot = null;
            configuration.InlineSkill = null;
            configuration.RegisteredSkillServiceType = NormalizeOptionalText(state.RegisteredSkillServiceType);
            editor.EndpointOrPath = configuration.RegisteredSkillServiceType ?? string.Empty;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(state.SkillRoot))
            {
                errors.Add("File skill configuration requires a skill root or SKILL.md path.");
            }

            configuration.SkillSource = "file";
            configuration.SkillRoot = NormalizeOptionalText(state.SkillRoot);
            configuration.RegisteredSkillServiceType = null;
            configuration.InlineSkill = null;
            editor.EndpointOrPath = ResolveSkillEndpoint(configuration.SkillRoot ?? string.Empty);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static ToolCapabilityEditorState ReadTool(CapabilityEditorModel editor)
    {
        var configuration = Deserialize<ToolCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new ToolCapabilityConfigurationModel();
        var process = configuration.ExternalProcess ?? new ExternalProcessToolConfigurationModel();
        var http = configuration.ExternalHttp ?? new ExternalHttpToolConfigurationModel();
        var sideEffects = configuration.SideEffects ?? new CapabilitySideEffectConfigurationModel();
        var toolKind = string.IsNullOrWhiteSpace(configuration.ToolKind)
            ? configuration.ExternalHttp is not null ? "externalHttp" : "externalProcess"
            : configuration.ToolKind.Trim();

        var state = new ToolCapabilityEditorState
        {
            ToolKind = toolKind,
            RuntimeToolName = configuration.RuntimeToolName ?? NormalizeRuntimeToolName(editor.Key),
            ImplementationKey = configuration.ImplementationKey ?? $"external.{NormalizeKey(editor.Key)}",
            OperationClassificationsText = ToLineText(configuration.OperationClassifications ?? ["externalAction"]),
            SideEffectKind = sideEffects.Kind ?? "ExternalAction",
            RequiresApprovalByDefault = sideEffects.RequiresApprovalByDefault ?? true,
            IsStateChanging = sideEffects.IsStateChanging ?? true,
            Command = process.Command ?? ResolveCommandFromEditor(editor),
            ArgumentsText = ToLineText(process.Arguments),
            WorkingDirectory = string.IsNullOrWhiteSpace(process.WorkingDirectory) ? "." : process.WorkingDirectory,
            AllowedExecutableNamesText = ToLineText(process.AllowedExecutableNames),
            ProcessRequiredOutputPropertiesText = ToLineText(process.RequiredOutputProperties),
            ProcessTimeoutSeconds = process.TimeoutSeconds ?? 30,
            MaxOutputBytes = process.MaxOutputBytes ?? 4096,
            HttpMethod = string.IsNullOrWhiteSpace(http.Method) ? "POST" : http.Method,
            Endpoint = http.Endpoint ?? ResolveEndpointFromEditor(editor),
            HeaderBindingsText = ToKeyValueText(http.HeaderBindings),
            HttpRequiredOutputPropertiesText = ToLineText(http.RequiredOutputProperties),
            HttpTimeoutSeconds = http.TimeoutSeconds ?? 30,
            MaxResponseBytes = http.MaxResponseBytes ?? 4096
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteTool(CapabilityEditorModel editor, ToolCapabilityEditorState state)
    {
        var errors = new List<string>();
        var configuration = state.Configuration ?? new ToolCapabilityConfigurationModel();
        var toolKind = NormalizeToolKind(state.ToolKind);
        var runtimeToolName = NormalizeOptionalText(state.RuntimeToolName) ?? NormalizeRuntimeToolName(editor.Key);
        var implementationKey = NormalizeOptionalText(state.ImplementationKey) ?? $"external.{NormalizeKey(editor.Key)}";
        if (!RuntimeToolName.TryCreate(runtimeToolName, out _))
        {
            errors.Add("Runtime tool name must be lower snake_case.");
        }

        if (!ImplementationKey.TryCreate(implementationKey, out _))
        {
            errors.Add("Implementation key must use lower ASCII segments separated by '.', '_' or '-'.");
        }

        configuration.ToolKind = toolKind;
        configuration.RuntimeToolName = runtimeToolName;
        configuration.ImplementationKey = implementationKey;
        configuration.OperationClassifications = SplitLines(state.OperationClassificationsText) is { Count: > 0 } classifications
            ? classifications
            : ["externalAction"];
        configuration.SideEffects = new CapabilitySideEffectConfigurationModel
        {
            Kind = NormalizeOptionalText(state.SideEffectKind) ?? "ExternalAction",
            RequiresApprovalByDefault = state.RequiresApprovalByDefault,
            IsStateChanging = state.IsStateChanging
        };

        if (toolKind == "externalHttp")
        {
            if (string.IsNullOrWhiteSpace(state.Endpoint))
            {
                errors.Add("External HTTP tool configuration requires an endpoint.");
            }

            var headerBindings = ParseKeyValueText(state.HeaderBindingsText, "header binding", errors);
            configuration.ExternalHttp = new ExternalHttpToolConfigurationModel
            {
                Method = NormalizeOptionalText(state.HttpMethod) ?? "POST",
                Endpoint = NormalizeOptionalText(state.Endpoint),
                HeaderBindings = headerBindings.Count == 0 ? null : headerBindings,
                RequiredOutputProperties = SplitLines(state.HttpRequiredOutputPropertiesText),
                TimeoutSeconds = Math.Max(1, state.HttpTimeoutSeconds),
                MaxResponseBytes = Math.Max(64, state.MaxResponseBytes)
            };
            configuration.ExternalProcess = null;
            editor.EndpointOrPath = state.Endpoint.Trim();
        }
        else
        {
            if (string.IsNullOrWhiteSpace(state.Command))
            {
                errors.Add("External process tool configuration requires a command.");
            }

            configuration.ExternalProcess = new ExternalProcessToolConfigurationModel
            {
                Command = NormalizeOptionalText(state.Command),
                Arguments = SplitLines(state.ArgumentsText),
                WorkingDirectory = NormalizeOptionalText(state.WorkingDirectory) ?? ".",
                AllowedExecutableNames = SplitLines(state.AllowedExecutableNamesText),
                RequiredOutputProperties = SplitLines(state.ProcessRequiredOutputPropertiesText),
                TimeoutSeconds = Math.Max(1, state.ProcessTimeoutSeconds),
                MaxOutputBytes = Math.Max(64, state.MaxOutputBytes)
            };
            configuration.ExternalHttp = null;
            editor.EndpointOrPath = state.Command.Trim();
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }

    public static string NormalizeRuntimeToolName(string value)
    {
        var key = NormalizeKey(value);
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Replace('-', '_');
    }

    public static List<string> SplitLines(string value)
    {
        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveMcpTransport(CapabilityEditorModel editor, McpCapabilityConfigurationModel configuration)
    {
        if (configuration.Hosted == true)
        {
            return "http";
        }

        if (!string.IsNullOrWhiteSpace(configuration.Command))
        {
            return "stdio";
        }

        if (!string.IsNullOrWhiteSpace(configuration.Endpoint) ||
            Uri.TryCreate(editor.EndpointOrPath, UriKind.Absolute, out _))
        {
            return "http";
        }

        return "logical";
    }

    private static string ResolveEndpointFromEditor(CapabilityEditorModel editor)
        => Uri.TryCreate(editor.EndpointOrPath, UriKind.Absolute, out _)
            ? editor.EndpointOrPath
            : string.Empty;

    private static string ResolveCommandFromEditor(CapabilityEditorModel editor)
        => Uri.TryCreate(editor.EndpointOrPath, UriKind.Absolute, out _)
            ? string.Empty
            : editor.EndpointOrPath;

    private static string ResolveMcpEndpointOrPath(McpCapabilityConfigurationModel configuration, string currentValue)
    {
        if (!string.IsNullOrWhiteSpace(configuration.Command))
        {
            return configuration.Command.Trim();
        }

        if (!string.IsNullOrWhiteSpace(configuration.Endpoint))
        {
            return configuration.Endpoint.Trim();
        }

        return currentValue.Trim();
    }

    private static string ResolveSkillSource(CapabilityEditorModel editor, SkillCapabilityConfigurationModel configuration)
    {
        if (configuration.InlineSkill is not null ||
            editor.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase))
        {
            return "inline";
        }

        if (!string.IsNullOrWhiteSpace(configuration.RegisteredSkillServiceType))
        {
            return "registered";
        }

        return "file";
    }

    private static string ResolveSkillRootFromEditor(CapabilityEditorModel editor)
    {
        if (string.IsNullOrWhiteSpace(editor.EndpointOrPath) ||
            editor.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return Path.GetFileName(editor.EndpointOrPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(editor.EndpointOrPath) ?? editor.EndpointOrPath
            : editor.EndpointOrPath;
    }

    private static string ResolveSkillEndpoint(string skillRoot)
    {
        if (string.IsNullOrWhiteSpace(skillRoot))
        {
            return string.Empty;
        }

        var trimmed = skillRoot.Trim();
        return Path.GetFileName(trimmed).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : Path.Combine(trimmed, "SKILL.md");
    }

    private static string ResolveDefaultSkillTrustLevel(string source, IReadOnlyList<string>? allowedExternalRoots)
    {
        if (string.Equals(source, "inline", StringComparison.OrdinalIgnoreCase))
        {
            return "InlineSkill";
        }

        return allowedExternalRoots?.Count > 0 ? "ExternalSkillRoot" : "WorkspaceSkillRoot";
    }

    private static string NormalizeToolKind(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "externalProcess" : value.Trim();
        return normalized.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant() switch
            {
                "externalhttp" or "http" => "externalHttp",
                _ => "externalProcess"
            };
    }

    private static string ToLineText(IEnumerable<string>? values)
        => values is null ? string.Empty : string.Join(Environment.NewLine, values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));

    private static string ToKeyValueText(IDictionary<string, string>? values)
        => values is null
            ? string.Empty
            : string.Join(Environment.NewLine, values.Select(item => $"{item.Key}={item.Value}"));

    private static Dictionary<string, string> ParseKeyValueText(string value, string label, ICollection<string> errors)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitLines(value))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                errors.Add($"Invalid {label} '{line}'. Use NAME=ENV_VAR_OR_SECRET_REFERENCE.");
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var binding = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(binding))
            {
                errors.Add($"Invalid {label} '{line}'. Use NAME=ENV_VAR_OR_SECRET_REFERENCE.");
                continue;
            }

            result[key] = binding;
        }

        return result;
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal sealed class McpCapabilityConfigurationModel
    {
        public string? Transport { get; set; }

        public bool? Hosted { get; set; }

        public string? ServerName { get; set; }

        public string? Endpoint { get; set; }

        public string? Command { get; set; }

        public List<string>? Arguments { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<string>? AllowedWorkingDirectories { get; set; }

        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        public Dictionary<string, string>? EnvironmentVariableBindings { get; set; }

        public Dictionary<string, string>? Headers { get; set; }

        public Dictionary<string, string>? HeaderBindings { get; set; }

        public List<string>? AllowedTools { get; set; }

        public string? ApprovalMode { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ToolCapabilityConfigurationModel
    {
        public string? ToolKind { get; set; }

        public string? RuntimeToolName { get; set; }

        public string? ImplementationKey { get; set; }

        public List<string>? OperationClassifications { get; set; }

        public CapabilitySideEffectConfigurationModel? SideEffects { get; set; }

        public ExternalProcessToolConfigurationModel? ExternalProcess { get; set; }

        public ExternalHttpToolConfigurationModel? ExternalHttp { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class CapabilitySideEffectConfigurationModel
    {
        public string? Kind { get; set; }

        public bool? RequiresApprovalByDefault { get; set; }

        public bool? IsStateChanging { get; set; }
    }

    internal sealed class ExternalProcessToolConfigurationModel
    {
        public string? Command { get; set; }

        public List<string>? Arguments { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<string>? AllowedExecutableNames { get; set; }

        public List<string>? RequiredOutputProperties { get; set; }

        public int? TimeoutSeconds { get; set; }

        public int? MaxOutputBytes { get; set; }
    }

    internal sealed class ExternalHttpToolConfigurationModel
    {
        public string? Method { get; set; }

        public string? Endpoint { get; set; }

        public Dictionary<string, string>? HeaderBindings { get; set; }

        public List<string>? RequiredOutputProperties { get; set; }

        public int? TimeoutSeconds { get; set; }

        public int? MaxResponseBytes { get; set; }
    }

    internal sealed class SkillCapabilityConfigurationModel
    {
        public string? SkillSource { get; set; }

        public string? SkillRoot { get; set; }

        public List<string>? AllowedExternalRoots { get; set; }

        public string? RegisteredSkillServiceType { get; set; }

        public InlineSkillConfigurationModel? InlineSkill { get; set; }

        public bool? ScriptApproval { get; set; }

        public FileSkillScriptExecutionConfigurationModel? ScriptExecution { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class FileSkillScriptExecutionConfigurationModel
    {
        public bool? ApprovalRequired { get; set; }

        public string? TrustLevel { get; set; }
    }

    internal sealed class InlineSkillConfigurationModel
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Instructions { get; set; }

        public List<InlineSkillResourceConfigurationModel>? Resources { get; set; }
    }

    internal sealed class InlineSkillResourceConfigurationModel
    {
        public string? Name { get; set; }

        public string? Content { get; set; }

        public string? Description { get; set; }
    }

    internal sealed class McpCapabilityEditorState
    {
        private McpCapabilityConfigurationModel? configuration;

        internal McpCapabilityConfigurationModel? Configuration
        {
            get => configuration;
            set => configuration = value;
        }

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

    internal sealed class SkillCapabilityEditorState
    {
        private SkillCapabilityConfigurationModel? configuration;

        internal SkillCapabilityConfigurationModel? Configuration
        {
            get => configuration;
            set => configuration = value;
        }

        public string SkillSource { get; set; } = "file";

        public string SkillRoot { get; set; } = string.Empty;

        public string AllowedExternalRootsText { get; set; } = string.Empty;

        public string RegisteredSkillServiceType { get; set; } = string.Empty;

        public string InlineName { get; set; } = string.Empty;

        public string InlineDescription { get; set; } = string.Empty;

        public string InlineInstructions { get; set; } = string.Empty;

        public bool ScriptApproval { get; set; } = true;

        public string ScriptTrustLevel { get; set; } = string.Empty;
    }

    internal sealed class ToolCapabilityEditorState
    {
        private ToolCapabilityConfigurationModel? configuration;

        internal ToolCapabilityConfigurationModel? Configuration
        {
            get => configuration;
            set => configuration = value;
        }

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

        public int ProcessTimeoutSeconds { get; set; } = 30;

        public int MaxOutputBytes { get; set; } = 4096;

        public string HttpMethod { get; set; } = "POST";

        public string Endpoint { get; set; } = string.Empty;

        public string HeaderBindingsText { get; set; } = string.Empty;

        public string HttpRequiredOutputPropertiesText { get; set; } = string.Empty;

        public int HttpTimeoutSeconds { get; set; } = 30;

        public int MaxResponseBytes { get; set; } = 4096;

        public string TestInputJson { get; set; } = "{}";
    }
}
