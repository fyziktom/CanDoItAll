using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using System.Globalization;
using System.Text;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.CapabilityAuthoring.UI;

public static class CapabilityConfigurationCodec {
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web) {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string RemoveForbiddenPlaintext(CanDoItAll.AgentFramework.Models.CapabilityKind kind, string json, out bool removed) {
        removed = false;
        if (kind is not (CanDoItAll.AgentFramework.Models.CapabilityKind.McpServer or CanDoItAll.AgentFramework.Models.CapabilityKind.Tool)
            || string.IsNullOrWhiteSpace(json)) {
            return json;
        }
        JsonObject? root;
        try {
            root = JsonNode.Parse(json) as JsonObject;
        } catch (JsonException) {
            return json;
        }
        if (root is null) {
            return json;
        }
        removed = Clean(root);
        if (kind == CanDoItAll.AgentFramework.Models.CapabilityKind.Tool) {
            foreach (var item in root.Where(item => item.Key.Equals("externalHttp", StringComparison.OrdinalIgnoreCase)
                || item.Key.Equals("externalProcess", StringComparison.OrdinalIgnoreCase))) {
                if (item.Value is JsonObject child) {
                    removed |= Clean(child);
                }
            }
        }
        return removed ? root.ToJsonString(SerializerOptions) : json;

        static bool Clean(JsonObject value) {
            var keys = value.Select(item => item.Key).Where(key => key.Equals("headers", StringComparison.OrdinalIgnoreCase)
                || key.Equals("environmentVariables", StringComparison.OrdinalIgnoreCase)).ToArray();
            foreach (var key in keys) {
                value.Remove(key);
            }
            return keys.Length > 0;
        }
    }

    public static McpCapabilityDraft ReadMcp(CapabilityEditorModel editor) {
        var configuration = Deserialize<McpCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new McpCapabilityConfigurationModel();
        var state = new McpCapabilityDraft {
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
            ArgumentsText = ToArgumentLineText(configuration.Arguments),
            AllowedToolsText = ToLineText(configuration.AllowedTools),
            AllowedWorkingDirectoriesText = ToAuthorityLineText(configuration.AllowedWorkingDirectories),
            EnvironmentVariableBindingsText = ToKeyValueText(configuration.EnvironmentVariableBindings),
            HeaderBindingsText = ToKeyValueText(configuration.HeaderBindings)
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteMcp(CapabilityEditorModel editor, McpCapabilityDraft state, StringComparer environmentNameComparer) {
        var errors = new List<string>();
        var configuration = Clone(state.Configuration) ?? new McpCapabilityConfigurationModel();
        var transport = NormalizeOptionalText(state.Transport) ?? "stdio";
        if (transport is not ("stdio" or "http" or "sse" or "logical")) {
            return ["Unsupported MCP transport. Repair the raw configuration explicitly."];
        }
        var arguments = SplitArgumentLines(state.ArgumentsText);
        var allowedTools = SplitTypedNameLines(state.AllowedToolsText);

        configuration.Transport = transport;
        configuration.Hosted = state.Hosted ? true : null;
        configuration.ServerName = NormalizeOptionalText(state.ServerName);
        configuration.Endpoint = NormalizeOptionalText(state.Endpoint);
        configuration.Command = PreserveOptionalDataValue(state.Command);
        configuration.Arguments = arguments.Count == 0 ? null : arguments;
        configuration.WorkingDirectory = PreserveOptionalDataValue(state.WorkingDirectory);
        configuration.AllowedWorkingDirectories = SplitAuthorityLines(state.AllowedWorkingDirectoriesText) is { Count: > 0 } roots ? roots : null;
        configuration.AllowedTools = allowedTools.Count == 0 ? null : allowedTools;
        configuration.ApprovalMode = NormalizeOptionalText(state.ApprovalMode) ?? "NeverRequire";
        configuration.EnvironmentVariables = null;
        configuration.Headers = null;

        var environmentVariableBindings = ParseKeyValueText(
            state.EnvironmentVariableBindingsText,
            "environment variable binding",
            environmentNameComparer,
            errors);
        var headerBindings = ParseKeyValueText(
            state.HeaderBindingsText,
            "header binding",
            StringComparer.OrdinalIgnoreCase,
            errors);
        configuration.EnvironmentVariableBindings = environmentVariableBindings.Count == 0 ? null : environmentVariableBindings;
        configuration.HeaderBindings = headerBindings.Count == 0 ? null : headerBindings;

        if (string.Equals(transport, "stdio", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(configuration.Command)) {
                errors.Add("Stdio MCP configuration requires a command.");
            }

            if (allowedTools.Count == 0) {
                errors.Add("Local MCP configuration requires at least one allowed tool.");
            }
        } else if (!string.Equals(transport, "logical", StringComparison.OrdinalIgnoreCase) &&
                 string.IsNullOrWhiteSpace(configuration.Endpoint)) {
            errors.Add("Remote MCP configuration requires an endpoint.");
        }

        if (errors.Count > 0) {
            return errors;
        }

        editor.EndpointOrPath = ResolveMcpEndpointOrPath(configuration, editor.EndpointOrPath);
        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static SkillCapabilityDraft ReadSkill(CapabilityEditorModel editor) {
        var configuration = Deserialize<SkillCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new SkillCapabilityConfigurationModel();
        var inlineSkill = configuration.InlineSkill ?? new InlineSkillConfigurationModel();
        var source = string.IsNullOrWhiteSpace(configuration.SkillSource)
            ? ResolveSkillSource(editor, configuration)
            : configuration.SkillSource.Trim();

        var state = new SkillCapabilityDraft {
            SkillSource = source,
            SkillRoot = configuration.SkillRoot ?? ResolveSkillRootFromEditor(editor),
            AllowedExternalRootsText = ToAuthorityLineText(configuration.AllowedExternalRoots),
            RegisteredSkillServiceType = configuration.RegisteredSkillServiceType ?? string.Empty,
            InlineName = inlineSkill.Name ?? string.Empty,
            InlineDescription = inlineSkill.Description ?? string.Empty,
            InlineInstructions = inlineSkill.Instructions ?? string.Empty,
            ResourcesJson = JsonSerializer.Serialize(inlineSkill.Resources ?? [], SerializerOptions),
            ScriptApproval = configuration.ScriptExecution?.ApprovalRequired ?? configuration.ScriptApproval ?? true,
            ScriptTrustLevel = configuration.ScriptExecution?.TrustLevel ?? string.Empty
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteSkill(CapabilityEditorModel editor, SkillCapabilityDraft state) {
        var errors = new List<string>();
        var configuration = Clone(state.Configuration) ?? new SkillCapabilityConfigurationModel();
        var source = NormalizeOptionalText(state.SkillSource) ?? "file";
        if (source is not ("file" or "inline" or "registered")) {
            return ["Unsupported skill source. Repair the raw configuration explicitly."];
        }
        configuration.SkillSource = source;
        configuration.AllowedExternalRoots = SplitAuthorityLines(state.AllowedExternalRootsText) is { Count: > 0 } roots ? roots : null;
        configuration.ScriptApproval = state.ScriptApproval;
        configuration.ScriptExecution ??= new FileSkillScriptExecutionConfigurationModel();
        configuration.ScriptExecution.ApprovalRequired = state.ScriptApproval;
        configuration.ScriptExecution.TrustLevel = NormalizeOptionalText(state.ScriptTrustLevel) ?? ResolveDefaultSkillTrustLevel(source, configuration.AllowedExternalRoots);

        if (string.Equals(source, "inline", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(state.InlineInstructions)) {
                errors.Add("Inline skill configuration requires instructions.");
            }

            configuration.SkillRoot = null;
            configuration.RegisteredSkillServiceType = null;
            configuration.InlineSkill ??= new InlineSkillConfigurationModel();
            configuration.InlineSkill.Name = SkillName.Normalize(
                    NormalizeOptionalText(state.InlineName) ?? NormalizeKey(editor.Key)).Value;
            configuration.InlineSkill.Description = NormalizeOptionalText(state.InlineDescription) ?? editor.Description;
            configuration.InlineSkill.Instructions = state.InlineInstructions.Trim();
            configuration.InlineSkill.Resources = JsonSerializer.Deserialize<List<InlineSkillResourceConfigurationModel>>(state.ResourcesJson, SerializerOptions)
                ?? throw new JsonException("Skill resources must be an array.");
            editor.EndpointOrPath = $"inline://{NormalizeKey(editor.Key)}";
        } else if (string.Equals(source, "registered", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(state.RegisteredSkillServiceType)) {
                errors.Add("Registered skill configuration requires a service type.");
            }

            configuration.SkillRoot = null;
            configuration.InlineSkill = null;
            configuration.RegisteredSkillServiceType = NormalizeOptionalText(state.RegisteredSkillServiceType);
            editor.EndpointOrPath = configuration.RegisteredSkillServiceType ?? string.Empty;
        } else {
            if (string.IsNullOrWhiteSpace(state.SkillRoot)) {
                errors.Add("File skill configuration requires a skill root or SKILL.md path.");
            }

            configuration.SkillSource = "file";
            configuration.SkillRoot = PreserveOptionalDataValue(state.SkillRoot);
            configuration.RegisteredSkillServiceType = null;
            configuration.InlineSkill = null;
            editor.EndpointOrPath = ResolveSkillEndpoint(configuration.SkillRoot ?? string.Empty);
        }

        if (errors.Count > 0) {
            return errors;
        }

        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static ToolCapabilityDraft ReadTool(CapabilityEditorModel editor) {
        var configuration = Deserialize<ToolCapabilityConfigurationModel>(editor.ConfigurationJson) ?? new ToolCapabilityConfigurationModel();
        var process = configuration.ExternalProcess ?? new ExternalProcessToolConfigurationModel();
        var http = configuration.ExternalHttp ?? new ExternalHttpToolConfigurationModel();
        var sideEffects = configuration.SideEffects ?? new CapabilitySideEffectConfigurationModel();
        var toolKind = string.IsNullOrWhiteSpace(configuration.ToolKind)
            ? configuration.ExternalHttp is not null ? "externalHttp" : "externalProcess"
            : NormalizeToolKind(configuration.ToolKind.Trim());

        var state = new ToolCapabilityDraft {
            ToolKind = toolKind,
            RuntimeToolName = configuration.RuntimeToolName ?? NormalizeRuntimeToolName(editor.Key),
            ImplementationKey = configuration.ImplementationKey ?? $"external.{NormalizeKey(editor.Key)}",
            OperationClassificationsText = ToLineText(configuration.OperationClassifications ?? ["externalAction"]),
            SideEffectKind = sideEffects.Kind ?? "ExternalAction",
            RequiresApprovalByDefault = sideEffects.RequiresApprovalByDefault ?? true,
            IsStateChanging = sideEffects.IsStateChanging ?? true,
            Command = process.Command ?? ResolveCommandFromEditor(editor),
            ArgumentsText = ToArgumentLineText(process.Arguments),
            WorkingDirectory = string.IsNullOrWhiteSpace(process.WorkingDirectory) ? "." : process.WorkingDirectory,
            AllowedExecutableNamesText = ToAuthorityLineText(process.AllowedExecutableNames),
            ProcessRequiredOutputPropertiesText = ToAuthorityLineText(process.RequiredOutputProperties),
            ProcessTimeoutSecondsText = (process.TimeoutSeconds ?? 30).ToString(CultureInfo.InvariantCulture),
            MaxOutputBytesText = (process.MaxOutputBytes ?? 4096).ToString(CultureInfo.InvariantCulture),
            HttpMethod = string.IsNullOrWhiteSpace(http.Method) ? "POST" : http.Method,
            Endpoint = http.Endpoint ?? ResolveEndpointFromEditor(editor),
            HeaderBindingsText = ToKeyValueText(http.HeaderBindings),
            HttpRequiredOutputPropertiesText = ToAuthorityLineText(http.RequiredOutputProperties),
            HttpTimeoutSecondsText = (http.TimeoutSeconds ?? 30).ToString(CultureInfo.InvariantCulture),
            MaxResponseBytesText = (http.MaxResponseBytes ?? 4096).ToString(CultureInfo.InvariantCulture)
        };

        state.Configuration = configuration;
        return state;
    }

    public static IReadOnlyList<string> WriteTool(CapabilityEditorModel editor, ToolCapabilityDraft state) {
        var errors = new List<string>();
        var configuration = Clone(state.Configuration) ?? new ToolCapabilityConfigurationModel();
        var toolKind = NormalizeToolKind(state.ToolKind);
        var timeoutText = toolKind == "externalHttp" ? state.HttpTimeoutSecondsText : state.ProcessTimeoutSecondsText;
        var limitText = toolKind == "externalHttp" ? state.MaxResponseBytesText : state.MaxOutputBytesText;
        if (!int.TryParse(timeoutText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeout) || timeout < 1
            || !int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) || limit < 64) {
            return ["Timeout must be a positive whole number and the output limit must be a whole number of at least 64 bytes."];
        }
        var runtimeToolName = NormalizeOptionalText(state.RuntimeToolName) ?? NormalizeRuntimeToolName(editor.Key);
        var implementationKey = NormalizeOptionalText(state.ImplementationKey) ?? $"external.{NormalizeKey(editor.Key)}";
        if (!RuntimeToolName.TryCreate(runtimeToolName, out _)) {
            errors.Add("Runtime tool name must be lower snake_case.");
        }

        if (!ImplementationKey.TryCreate(implementationKey, out _)) {
            errors.Add("Implementation key must use lower ASCII segments separated by '.', '_' or '-'.");
        }

        configuration.ToolKind = toolKind;
        configuration.RuntimeToolName = runtimeToolName;
        configuration.ImplementationKey = implementationKey;
        configuration.OperationClassifications = SplitLines(state.OperationClassificationsText) is { Count: > 0 } classifications
            ? classifications
            : ["externalAction"];
        configuration.SideEffects ??= new CapabilitySideEffectConfigurationModel();
        configuration.SideEffects.Kind = NormalizeOptionalText(state.SideEffectKind) ?? "ExternalAction";
        configuration.SideEffects.RequiresApprovalByDefault = state.RequiresApprovalByDefault;
        configuration.SideEffects.IsStateChanging = state.IsStateChanging;

        if (toolKind == "externalHttp") {
            if (string.IsNullOrWhiteSpace(state.Endpoint)) {
                errors.Add("External HTTP tool configuration requires an endpoint.");
            }

            var headerBindings = ParseKeyValueText(
                state.HeaderBindingsText,
                "header binding",
                StringComparer.OrdinalIgnoreCase,
                errors);
            configuration.ExternalHttp ??= new ExternalHttpToolConfigurationModel();
            configuration.ExternalHttp.Method = NormalizeOptionalText(state.HttpMethod) ?? "POST";
            configuration.ExternalHttp.Endpoint = NormalizeOptionalText(state.Endpoint);
            configuration.ExternalHttp.HeaderBindings = headerBindings.Count == 0 ? null : headerBindings;
            configuration.ExternalHttp.RequiredOutputProperties = SplitAuthorityLines(state.HttpRequiredOutputPropertiesText);
            configuration.ExternalHttp.TimeoutSeconds = timeout;
            configuration.ExternalHttp.MaxResponseBytes = limit;
            configuration.ExternalProcess = null;
            editor.EndpointOrPath = state.Endpoint.Trim();
        } else {
            if (string.IsNullOrWhiteSpace(state.Command)) {
                errors.Add("External process tool configuration requires a command.");
            }

            var arguments = SplitArgumentLines(state.ArgumentsText);
            if (SensitiveTextRedactor.ContainsSecretBearingArguments(arguments)) {
                errors.Add("External process arguments cannot contain persisted secret values. Use a runtime secret binding instead.");
            }

            configuration.ExternalProcess ??= new ExternalProcessToolConfigurationModel();
            configuration.ExternalProcess.Command = PreserveOptionalDataValue(state.Command);
            configuration.ExternalProcess.Arguments = arguments;
            configuration.ExternalProcess.WorkingDirectory = PreserveOptionalDataValue(state.WorkingDirectory) ?? ".";
            configuration.ExternalProcess.AllowedExecutableNames = SplitAuthorityLines(state.AllowedExecutableNamesText);
            configuration.ExternalProcess.RequiredOutputProperties = SplitAuthorityLines(state.ProcessRequiredOutputPropertiesText);
            configuration.ExternalProcess.TimeoutSeconds = timeout;
            configuration.ExternalProcess.MaxOutputBytes = limit;
            configuration.ExternalHttp = null;
            editor.EndpointOrPath = state.Command.Trim();
        }

        if (errors.Count > 0) {
            return errors;
        }

        editor.ConfigurationJson = JsonSerializer.Serialize(configuration, SerializerOptions);
        return [];
    }

    public static string NormalizeKey(string value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;
        foreach (var character in value.Trim()) {
            if (char.IsLetterOrDigit(character)) {
                if (pendingSeparator && builder.Length > 0) {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            } else if (builder.Length > 0) {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }

    public static string NormalizeRuntimeToolName(string value) {
        var key = NormalizeKey(value);
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Replace('-', '_');
    }

    public static List<string> SplitLines(string value) {
        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> SplitArgumentLines(string value) {
        if (value.Length == 0) {
            return [];
        }

        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select(DecodeArgumentLine)
            .ToList();
    }

    private static List<string> SplitAuthorityLines(string value) {
        return value
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? PreserveOptionalDataValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string ResolveMcpTransport(CapabilityEditorModel editor, McpCapabilityConfigurationModel configuration) {
        if (configuration.Hosted == true) {
            return "http";
        }

        if (!string.IsNullOrWhiteSpace(configuration.Command)) {
            return "stdio";
        }

        if (!string.IsNullOrWhiteSpace(configuration.Endpoint) ||
            Uri.TryCreate(editor.EndpointOrPath, UriKind.Absolute, out _)) {
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

    private static string ResolveMcpEndpointOrPath(McpCapabilityConfigurationModel configuration, string currentValue) {
        if (!string.IsNullOrWhiteSpace(configuration.Command)) {
            return configuration.Command.Trim();
        }

        if (!string.IsNullOrWhiteSpace(configuration.Endpoint)) {
            return configuration.Endpoint.Trim();
        }

        return currentValue.Trim();
    }

    private static string ResolveSkillSource(CapabilityEditorModel editor, SkillCapabilityConfigurationModel configuration) {
        if (configuration.InlineSkill is not null ||
            editor.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase)) {
            return "inline";
        }

        if (!string.IsNullOrWhiteSpace(configuration.RegisteredSkillServiceType)) {
            return "registered";
        }

        return "file";
    }

    private static string ResolveSkillRootFromEditor(CapabilityEditorModel editor) {
        if (string.IsNullOrWhiteSpace(editor.EndpointOrPath) ||
            editor.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase)) {
            return string.Empty;
        }

        return Path.GetFileName(editor.EndpointOrPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(editor.EndpointOrPath) ?? editor.EndpointOrPath
            : editor.EndpointOrPath;
    }

    private static string ResolveSkillEndpoint(string skillRoot) {
        if (string.IsNullOrWhiteSpace(skillRoot)) {
            return string.Empty;
        }

        return Path.GetFileName(skillRoot).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
            ? skillRoot
            : Path.Combine(skillRoot, "SKILL.md");
    }

    private static string ResolveDefaultSkillTrustLevel(string source, IReadOnlyList<string>? allowedExternalRoots) {
        if (string.Equals(source, "inline", StringComparison.OrdinalIgnoreCase)) {
            return "InlineSkill";
        }

        return allowedExternalRoots?.Count > 0 ? "ExternalSkillRoot" : "WorkspaceSkillRoot";
    }

    private static string NormalizeToolKind(string value) {
        var normalized = string.IsNullOrWhiteSpace(value) ? "externalProcess" : value.Trim();
        return normalized.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant() switch {
                "externalhttp" or "http" => "externalHttp",
                "externalprocess" or "process" => "externalProcess",
                _ => throw new JsonException("Unsupported tool configuration kind.")
            };
    }

    private static string ToLineText(IEnumerable<string>? values)
        => values is null ? string.Empty : string.Join(Environment.NewLine, values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()));

    private static List<string> SplitTypedNameLines(string value)
        => value
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string ToArgumentLineText(IEnumerable<string>? values)
        => values is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                values.Select(value => JsonSerializer.Serialize(value)));

    private static string ToAuthorityLineText(IEnumerable<string>? values)
        => values is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                values.Where(item => !string.IsNullOrWhiteSpace(item)));

    private static string DecodeArgumentLine(string line) {
        if (line.Length >= 2 && line[0] == '"' && line[^1] == '"') {
            try {
                return JsonSerializer.Deserialize<string>(line) ?? string.Empty;
            } catch (JsonException) {
            }
        }

        return line;
    }

    private static string ToKeyValueText(IDictionary<string, string>? values)
        => values is null
            ? string.Empty
            : string.Join(Environment.NewLine, values.Select(item => $"{item.Key}={item.Value}"));

    private static Dictionary<string, string> ParseKeyValueText(
        string value,
        string label,
        StringComparer keyComparer,
        ICollection<string> errors) {
        var result = new Dictionary<string, string>(keyComparer);
        foreach (var line in value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1) {
                errors.Add($"Invalid {label}. Use NAME=ENV_VAR_OR_SECRET_REFERENCE.");
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var binding = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(binding)) {
                errors.Add($"Invalid {label}. Use NAME=ENV_VAR_OR_SECRET_REFERENCE.");
                continue;
            }

            if (!result.TryAdd(key, binding)) {
                errors.Add($"Ambiguous {label} target for this host.");
            }
        }

        return result;
    }

    private static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static T? Clone<T>(T? value) => value is null ? default
        : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, SerializerOptions), SerializerOptions);

    private static T? Deserialize<T>(string json) {
        if (string.IsNullOrWhiteSpace(json)) {
            return default;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) {
            throw new JsonException("Configuration must be a JSON object.");
        }
        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

}
