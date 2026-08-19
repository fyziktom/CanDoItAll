using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Modules.AgentFramework;

internal static partial class CapabilityCuratorCapabilityConfigurationMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CapabilityEditorModel BuildEditor(
        CapabilityCuratorSaveInput input,
        CapabilityEditorModel? current)
    {
        ArgumentNullException.ThrowIfNull(input);
        var normalizedKey = NormalizeKey(input.Key ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            throw new ArgumentException("Capability key is required.", nameof(input));
        }

        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ArgumentException("Capability name is required.", nameof(input));
        }

        var (endpointOrPath, configurationJson) = input.Kind switch
        {
            ModelCapabilityKind.Skill => BuildSkill(input),
            ModelCapabilityKind.Tool => BuildTool(input),
            ModelCapabilityKind.McpServer => BuildMcp(input),
            _ => BuildOther(input)
        };

        return new CapabilityEditorModel
        {
            Id = input.CapabilityId,
            ExpectedFingerprint = string.IsNullOrWhiteSpace(input.ExpectedFingerprint)
                ? null
                : input.ExpectedFingerprint.Trim(),
            Kind = input.Kind,
            Key = normalizedKey,
            Name = input.Name.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            EndpointOrPath = endpointOrPath,
            ConfigurationJson = configurationJson,
            IsBuiltIn = current?.IsBuiltIn ?? false,
            Tags = NormalizeValues(input.Tags).ToList()
        };
    }

    public static CapabilityCuratorConfiguration ReadConfiguration(CapabilityEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return editor.Kind switch
        {
            ModelCapabilityKind.Skill => new(ReadSkill(editor), null, null, null),
            ModelCapabilityKind.Tool => new(null, ReadTool(editor), null, null),
            ModelCapabilityKind.McpServer => new(null, null, ReadMcp(editor), null),
            _ => new(null, null, null, ReadOtherConfiguration(editor.ConfigurationJson))
        };
    }

    private static (string EndpointOrPath, string ConfigurationJson) BuildSkill(
        CapabilityCuratorSaveInput input)
    {
        EnsureExclusiveConfiguration(input, input.SkillConfiguration, nameof(input.SkillConfiguration));
        var configuration = input.SkillConfiguration!;
        var allowedRoots = NormalizeAuthorityValues(configuration.AllowedExternalRoots);
        var trustLevel = configuration.ScriptTrustLevel ?? configuration.Source switch
        {
            CapabilityCuratorSkillSource.Inline => CapabilityCuratorSkillTrustLevel.InlineSkill,
            _ when allowedRoots.Count > 0 => CapabilityCuratorSkillTrustLevel.ExternalSkillRoot,
            _ => CapabilityCuratorSkillTrustLevel.WorkspaceSkillRoot
        };
        var model = new SkillConfigurationModel
        {
            SkillSource = ToCamelCase(configuration.Source),
            AllowedExternalRoots = NullWhenEmpty(allowedRoots),
            ScriptApproval = configuration.ScriptApprovalRequired,
            ScriptExecution = new SkillScriptExecutionModel
            {
                ApprovalRequired = configuration.ScriptApprovalRequired,
                TrustLevel = trustLevel.ToString()
            }
        };

        string endpointOrPath;
        switch (configuration.Source)
        {
            case CapabilityCuratorSkillSource.Inline:
                if (string.IsNullOrWhiteSpace(configuration.InlineInstructions))
                {
                    throw new ArgumentException("Inline skill instructions are required.", nameof(input));
                }

                model.InlineSkill = new InlineSkillModel
                {
                    Name = SkillName.Normalize(
                        NormalizeOptional(configuration.InlineName) ?? NormalizeKey(input.Key)).Value,
                    Description = NormalizeOptional(configuration.InlineDescription) ?? input.Description?.Trim() ?? string.Empty,
                    Instructions = configuration.InlineInstructions.Trim(),
                    Resources = configuration.InlineResources?
                        .Select(resource => new InlineSkillResourceModel
                        {
                            Name = RequireText(resource.Name, "Inline skill resource name"),
                            Content = RequireText(resource.Content, "Inline skill resource content"),
                            Description = NormalizeOptional(resource.Description)
                        })
                        .ToList()
                };
                endpointOrPath = $"inline://{NormalizeKey(input.Key)}";
                break;
            case CapabilityCuratorSkillSource.Registered:
                model.RegisteredSkillServiceType = RequireText(
                    configuration.RegisteredSkillServiceType,
                    "Registered skill service type");
                endpointOrPath = model.RegisteredSkillServiceType;
                break;
            default:
                model.SkillRoot = RequireText(configuration.SkillRoot, "File skill root");
                endpointOrPath = Path.GetFileName(model.SkillRoot).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase)
                    ? model.SkillRoot
                    : Path.Combine(model.SkillRoot, "SKILL.md");
                break;
        }

        return (endpointOrPath, JsonSerializer.Serialize(model, SerializerOptions));
    }

    private static (string EndpointOrPath, string ConfigurationJson) BuildTool(
        CapabilityCuratorSaveInput input)
    {
        EnsureExclusiveConfiguration(input, input.ToolConfiguration, nameof(input.ToolConfiguration));
        var configuration = input.ToolConfiguration!;
        if (!RuntimeToolName.TryCreate(configuration.RuntimeToolName, out _))
        {
            throw new ArgumentException("Runtime tool name must be lower snake_case.", nameof(input));
        }

        if (!ImplementationKey.TryCreate(configuration.ImplementationKey, out _))
        {
            throw new ArgumentException(
                "Implementation key must use lower ASCII segments separated by '.', '_' or '-'.",
                nameof(input));
        }

        var model = new ToolConfigurationModel
        {
            ToolKind = configuration.ToolKind == CapabilityCuratorToolKind.ExternalHttp
                ? "externalHttp"
                : "externalProcess",
            RuntimeToolName = configuration.RuntimeToolName.Trim(),
            ImplementationKey = configuration.ImplementationKey.Trim(),
            OperationClassifications = NormalizeClassifications(
                configuration.OperationClassifications,
                [CapabilityOperationClassification.ExternalAction]).ToList(),
            SideEffects = new SideEffectModel
            {
                Kind = configuration.SideEffectKind.ToString(),
                RequiresApprovalByDefault = configuration.RequiresApprovalByDefault,
                IsStateChanging = configuration.IsStateChanging
            }
        };

        string endpointOrPath;
        if (configuration.ToolKind == CapabilityCuratorToolKind.ExternalHttp)
        {
            if (configuration.ExternalProcess is not null || configuration.ExternalHttp is null)
            {
                throw new ArgumentException(
                    "External HTTP tool configuration requires only ExternalHttp settings.",
                    nameof(input));
            }

            var http = configuration.ExternalHttp;
            var endpoint = RequireAbsoluteHttpUri(http.Endpoint, "External HTTP endpoint");
            ValidateBounds(http.TimeoutSeconds, http.MaxResponseBytes, nameof(input));
            model.ExternalHttp = new ExternalHttpModel
            {
                Method = RequireText(http.Method, "External HTTP method").ToUpperInvariant(),
                Endpoint = endpoint,
                HeaderBindings = NormalizeHeaderBindings(http.HeaderBindings),
                RequiredOutputProperties = NullWhenEmpty(NormalizeAuthorityValues(http.RequiredOutputProperties)),
                TimeoutSeconds = http.TimeoutSeconds,
                MaxResponseBytes = http.MaxResponseBytes
            };
            endpointOrPath = endpoint;
        }
        else
        {
            if (configuration.ExternalHttp is not null || configuration.ExternalProcess is null)
            {
                throw new ArgumentException(
                    "External process tool configuration requires only ExternalProcess settings.",
                    nameof(input));
            }

            var process = configuration.ExternalProcess;
            ValidateBounds(process.TimeoutSeconds, process.MaxOutputBytes, nameof(input));
            ValidateNoInlineSecretArguments(process.Arguments, "External process arguments");
            model.ExternalProcess = new ExternalProcessModel
            {
                Command = RequireText(process.Command, "External process command"),
                Arguments = NullWhenEmpty(PreserveSequence(process.Arguments)),
                WorkingDirectory = PreserveOptionalDataValue(process.WorkingDirectory) ?? ".",
                AllowedExecutableNames = NullWhenEmpty(NormalizeAuthorityValues(process.AllowedExecutableNames)),
                RequiredOutputProperties = NullWhenEmpty(NormalizeAuthorityValues(process.RequiredOutputProperties)),
                TimeoutSeconds = process.TimeoutSeconds,
                MaxOutputBytes = process.MaxOutputBytes
            };
            endpointOrPath = model.ExternalProcess.Command;
        }

        return (endpointOrPath, JsonSerializer.Serialize(model, SerializerOptions));
    }

    private static (string EndpointOrPath, string ConfigurationJson) BuildMcp(
        CapabilityCuratorSaveInput input)
    {
        EnsureExclusiveConfiguration(input, input.McpConfiguration, nameof(input.McpConfiguration));
        var configuration = input.McpConfiguration!;
        ValidateBounds(configuration.TimeoutSeconds, 64, nameof(input));
        if (!string.IsNullOrWhiteSpace(configuration.ServerName) &&
            !McpServerKey.TryCreate(configuration.ServerName, out _))
        {
            throw new ArgumentException("MCP server name must be lower kebab-case.", nameof(input));
        }

        var model = new McpConfigurationModel
        {
            Transport = configuration.Transport switch
            {
                CapabilityCuratorMcpTransport.Stdio => "stdio",
                CapabilityCuratorMcpTransport.Http => "http",
                _ => "logical"
            },
            Hosted = configuration.Transport == CapabilityCuratorMcpTransport.Logical ? true : null,
            ServerName = NormalizeOptional(configuration.ServerName),
            AllowedTools = NullWhenEmpty(NormalizeTypedNames(configuration.AllowedTools)),
            ApprovalMode = configuration.ApprovalMode.ToString(),
            TimeoutSeconds = configuration.TimeoutSeconds,
            OperationClassifications = NormalizeClassifications(
                configuration.OperationClassifications,
                [CapabilityOperationClassification.McpTool, CapabilityOperationClassification.ExternalAction]).ToList()
        };

        string endpointOrPath;
        if (configuration.Transport == CapabilityCuratorMcpTransport.Stdio)
        {
            model.Command = RequireText(configuration.Command, "Stdio MCP command");
            if (model.AllowedTools is not { Count: > 0 })
            {
                throw new ArgumentException("Stdio MCP configuration requires at least one allowed tool.", nameof(input));
            }

            ValidateNoInlineSecretArguments(configuration.Arguments, "Stdio MCP arguments");
            model.Arguments = NullWhenEmpty(PreserveSequence(configuration.Arguments));
            model.WorkingDirectory = PreserveOptionalDataValue(configuration.WorkingDirectory) ?? ".";
            model.MessageFraming = configuration.MessageFraming.ToString();
            model.AllowedWorkingDirectories = NullWhenEmpty(
                NormalizeAuthorityValues(configuration.AllowedWorkingDirectories));
            model.EnvironmentVariableBindings = NormalizeEnvironmentBindings(configuration.EnvironmentVariableBindings);
            endpointOrPath = model.Command;
        }
        else if (configuration.Transport == CapabilityCuratorMcpTransport.Http)
        {
            model.Endpoint = RequireAbsoluteHttpUri(configuration.Endpoint, "Remote MCP endpoint");
            model.HeaderBindings = NormalizeHeaderBindings(configuration.HeaderBindings);
            endpointOrPath = model.Endpoint;
        }
        else
        {
            endpointOrPath = NormalizeOptional(input.EndpointOrPath) ?? string.Empty;
        }

        return (endpointOrPath, JsonSerializer.Serialize(model, SerializerOptions));
    }

    private static (string EndpointOrPath, string ConfigurationJson) BuildOther(
        CapabilityCuratorSaveInput input)
    {
        if (input.SkillConfiguration is not null || input.ToolConfiguration is not null || input.McpConfiguration is not null)
        {
            throw new ArgumentException("Typed Skill, Tool, and MCP configuration cannot be used for this capability kind.", nameof(input));
        }

        var configuration = input.OtherConfiguration ?? ReadJsonObject("{}");
        if (configuration.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Other capability configuration must be a JSON object.", nameof(input));
        }

        EnsureNoLiteralSecretProperties(configuration);
        return (
            NormalizeOptional(input.EndpointOrPath) ?? string.Empty,
            JsonSerializer.Serialize(configuration, SerializerOptions));
    }

    private static CapabilityCuratorSkillConfigurationInput ReadSkill(CapabilityEditorModel editor)
    {
        var model = Deserialize<SkillConfigurationModel>(editor.ConfigurationJson);
        var source = ParseSkillSource(model.SkillSource, editor.EndpointOrPath);
        return new CapabilityCuratorSkillConfigurationInput(
            source,
            model.SkillRoot,
            model.AllowedExternalRoots,
            model.RegisteredSkillServiceType,
            model.InlineSkill?.Name,
            model.InlineSkill?.Description,
            model.InlineSkill?.Instructions,
            model.InlineSkill?.Resources?
                .Select(resource => new CapabilityCuratorInlineSkillResourceInput(
                    resource.Name ?? string.Empty,
                    resource.Content ?? string.Empty,
                    resource.Description))
                .ToArray(),
            model.ScriptExecution?.ApprovalRequired ?? model.ScriptApproval ?? true,
            ParseEnum<CapabilityCuratorSkillTrustLevel>(model.ScriptExecution?.TrustLevel));
    }

    private static CapabilityCuratorToolConfigurationInput ReadTool(CapabilityEditorModel editor)
    {
        var model = Deserialize<ToolConfigurationModel>(editor.ConfigurationJson);
        var kind = string.Equals(model.ToolKind, "externalHttp", StringComparison.OrdinalIgnoreCase) ||
                   model.ExternalHttp is not null
            ? CapabilityCuratorToolKind.ExternalHttp
            : CapabilityCuratorToolKind.ExternalProcess;
        return new CapabilityCuratorToolConfigurationInput(
            kind,
            model.RuntimeToolName ?? NormalizeKey(editor.Key).Replace('-', '_'),
            model.ImplementationKey ?? $"external.{NormalizeKey(editor.Key)}",
            model.ExternalProcess is null
                ? null
                : new CapabilityCuratorExternalProcessToolInput(
                    model.ExternalProcess.Command ?? editor.EndpointOrPath,
                    model.ExternalProcess.Arguments,
                    model.ExternalProcess.WorkingDirectory ?? ".",
                    model.ExternalProcess.AllowedExecutableNames,
                    model.ExternalProcess.RequiredOutputProperties,
                    model.ExternalProcess.TimeoutSeconds ?? 30,
                    model.ExternalProcess.MaxOutputBytes ?? 4096),
            model.ExternalHttp is null
                ? null
                : new CapabilityCuratorExternalHttpToolInput(
                    model.ExternalHttp.Method ?? "POST",
                    model.ExternalHttp.Endpoint ?? editor.EndpointOrPath,
                    model.ExternalHttp.HeaderBindings,
                    model.ExternalHttp.RequiredOutputProperties,
                    model.ExternalHttp.TimeoutSeconds ?? 30,
                    model.ExternalHttp.MaxResponseBytes ?? 4096),
            ParseClassifications(model.OperationClassifications),
            ParseEnum(model.SideEffects?.Kind, CapabilitySideEffectKind.ExternalAction),
            model.SideEffects?.RequiresApprovalByDefault ?? true,
            model.SideEffects?.IsStateChanging ?? true);
    }

    private static CapabilityCuratorMcpConfigurationInput ReadMcp(CapabilityEditorModel editor)
    {
        var model = Deserialize<McpConfigurationModel>(editor.ConfigurationJson);
        var transport = !string.IsNullOrWhiteSpace(model.Command) ||
                        string.Equals(model.Transport, "stdio", StringComparison.OrdinalIgnoreCase)
            ? CapabilityCuratorMcpTransport.Stdio
            : !string.IsNullOrWhiteSpace(model.Endpoint) ||
              string.Equals(model.Transport, "http", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(model.Transport, "sse", StringComparison.OrdinalIgnoreCase)
                ? CapabilityCuratorMcpTransport.Http
                : CapabilityCuratorMcpTransport.Logical;
        return new CapabilityCuratorMcpConfigurationInput(
            transport,
            model.ServerName,
            model.Endpoint,
            model.Command,
            model.Arguments,
            model.WorkingDirectory ?? ".",
            ParseEnum(model.MessageFraming, McpStdioMessageFraming.ContentLength),
            model.AllowedWorkingDirectories,
            model.EnvironmentVariableBindings,
            model.HeaderBindings,
            model.AllowedTools,
            ParseEnum(model.ApprovalMode, McpApprovalMode.NeverRequire),
            model.TimeoutSeconds ?? 30,
            ParseClassifications(model.OperationClassifications));
    }

    private static JsonElement ReadJsonObject(string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }

        using var document = JsonDocument.Parse(configurationJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Capability configuration JSON must be an object.");
        }

        return document.RootElement.Clone();
    }

    private static JsonElement ReadOtherConfiguration(string configurationJson)
    {
        var configuration = ReadJsonObject(configurationJson);
        EnsureNoLiteralSecretProperties(configuration);
        return configuration;
    }

    private static T Deserialize<T>(string configurationJson)
        where T : new()
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new T();
        }

        try
        {
            return JsonSerializer.Deserialize<T>(configurationJson, SerializerOptions) ?? new T();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Capability configuration JSON is invalid.", exception);
        }
    }

    private static void EnsureExclusiveConfiguration(
        CapabilityCuratorSaveInput input,
        object? expected,
        string expectedName)
    {
        if (expected is null)
        {
            throw new ArgumentException($"{expectedName} is required for capability kind '{input.Kind}'.", nameof(input));
        }

        var suppliedCount = new object?[]
        {
            input.SkillConfiguration,
            input.ToolConfiguration,
            input.McpConfiguration,
            input.OtherConfiguration
        }.Count(value => value is not null);
        if (suppliedCount != 1)
        {
            throw new ArgumentException("Exactly one configuration payload must be supplied.", nameof(input));
        }
    }

    private static void EnsureNoLiteralSecretProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var normalized = property.Name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
                if (normalized is "headers" or "environmentvariables" or "apikey" or "password" or "secret" or "token" or "authorization")
                {
                    throw new ArgumentException(
                        $"Configuration property '{property.Name}' can contain literal secrets. Use a typed binding-reference field instead.");
                }

                EnsureNoLiteralSecretProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                EnsureNoLiteralSecretProperties(item);
            }
        }
    }

    private static Dictionary<string, string>? NormalizeEnvironmentBindings(
        IReadOnlyDictionary<string, string>? bindings)
        => NormalizeBindings(
            bindings,
            new WorkspaceCommandEnvironmentPolicy().EnvironmentNameComparer,
            "environment variable");

    private static Dictionary<string, string>? NormalizeHeaderBindings(
        IReadOnlyDictionary<string, string>? bindings)
        => NormalizeBindings(bindings, StringComparer.OrdinalIgnoreCase, "header");

    private static Dictionary<string, string>? NormalizeBindings(
        IReadOnlyDictionary<string, string>? bindings,
        StringComparer targetComparer,
        string targetKind)
    {
        if (bindings is null || bindings.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, string>(targetComparer);
        foreach (var (destination, source) in bindings)
        {
            var normalizedDestination = RequireText(destination, "Binding destination");
            var normalizedSource = RequireText(source, "Binding source environment variable");
            if (!EnvironmentVariableNameRegex().IsMatch(normalizedSource))
            {
                throw new ArgumentException(
                    $"Binding source '{normalizedSource}' must be an environment-variable name, not a literal value.");
            }

            if (!result.TryAdd(normalizedDestination, normalizedSource))
            {
                throw new ArgumentException(
                    $"Binding destination '{normalizedDestination}' is ambiguous for the current host's {targetKind} semantics.");
            }
        }

        return result;
    }

    private static IReadOnlyList<string> NormalizeClassifications(
        IReadOnlyList<CapabilityOperationClassification>? classifications,
        IReadOnlyList<CapabilityOperationClassification> fallback)
        => (classifications is { Count: > 0 } ? classifications : fallback)
            .Distinct()
            .Select(ToCamelCase)
            .ToArray();

    private static IReadOnlyList<CapabilityOperationClassification> ParseClassifications(
        IReadOnlyList<string>? values)
        => values?
            .Select(value => ParseEnum<CapabilityOperationClassification>(value))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .Distinct()
            .ToArray() ?? [];

    private static IReadOnlyList<string> NormalizeValues(
        IEnumerable<string>? values,
        bool preserveCase = false)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => preserveCase
                ? value.Trim()
                : value.Trim().TrimStart('#').ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    private static IReadOnlyList<string> PreserveSequence(IEnumerable<string>? values)
        => values?.ToArray() ?? [];

    private static IReadOnlyList<string> NormalizeTypedNames(IEnumerable<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? [];

    private static IReadOnlyList<string> NormalizeAuthorityValues(IEnumerable<string>? values)
        => values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? [];

    private static string? PreserveOptionalDataValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string>? NullWhenEmpty(IReadOnlyList<string> values)
        => values.Count == 0 ? null : values.ToList();

    private static string RequireText(string? value, string label)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{label} is required.")
            : value.Trim();

    private static string RequireAbsoluteHttpUri(string? value, string label)
    {
        var candidate = RequireText(value, label);
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"{label} must be an absolute HTTP or HTTPS URI.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException($"{label} cannot contain user-info credentials.");
        }

        foreach (var queryPart in uri.Query.TrimStart('?').Split(['&', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = queryPart.IndexOf('=');
            var encodedKey = separatorIndex >= 0 ? queryPart[..separatorIndex] : queryPart;
            var queryKey = Uri.UnescapeDataString(encodedKey.Replace('+', ' '));
            if (IsSecretBearingName(queryKey))
            {
                throw new ArgumentException(
                    $"{label} query parameter '{queryKey}' can contain a literal secret. Use a binding reference instead.");
            }
        }

        return uri.AbsoluteUri;
    }

    private static void ValidateNoInlineSecretArguments(
        IEnumerable<string>? arguments,
        string label)
    {
        if (arguments is null)
        {
            return;
        }

        foreach (var argument in arguments.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var candidate = argument.Trim();
            if (candidate.StartsWith("Bearer", StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains("Authorization:", StringComparison.OrdinalIgnoreCase) ||
                candidate.Contains("Authorization=", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{label} cannot contain inline authorization credentials.");
            }

            var option = candidate.TrimStart('-', '/');
            var separatorIndex = option.IndexOfAny(['=', ':', ' ', '\t']);
            var optionName = separatorIndex >= 0 ? option[..separatorIndex] : option;
            if (IsSecretBearingName(optionName))
            {
                throw new ArgumentException(
                    $"{label} cannot contain inline secret option '{optionName}'. Use a binding reference instead.");
            }
        }
    }

    private static bool IsSecretBearingName(string value)
    {
        var normalized = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
        return normalized is
            "token" or
            "accesstoken" or
            "refreshtoken" or
            "apikey" or
            "clientsecret" or
            "secretkey" or
            "accesskey" or
            "subscriptionkey" or
            "password" or
            "secret" or
            "authorization" or
            "auth" or
            "credential" or
            "credentials" or
            "signature" or
            "sig" ||
            normalized.EndsWith("token", StringComparison.Ordinal) ||
            normalized.EndsWith("apikey", StringComparison.Ordinal) ||
            normalized.EndsWith("secret", StringComparison.Ordinal) ||
            normalized.EndsWith("password", StringComparison.Ordinal) ||
            normalized.EndsWith("signature", StringComparison.Ordinal);
    }

    private static void ValidateBounds(int timeoutSeconds, int maximumBytes, string parameterName)
    {
        if (timeoutSeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Timeout must be between 1 and 300 seconds.");
        }

        if (maximumBytes is < 64 or > 4_194_304)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Payload bound must be between 64 and 4194304 bytes.");
        }
    }

    private static string NormalizeKey(string value)
    {
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

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ToCamelCase<TEnum>(TEnum value)
        where TEnum : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct, Enum
        => ParseEnum<TEnum>(value) ?? fallback;

    private static TEnum? ParseEnum<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsed) ? parsed : null;
    }

    private static CapabilityCuratorSkillSource ParseSkillSource(string? value, string endpointOrPath)
    {
        if (string.Equals(value, "inline", StringComparison.OrdinalIgnoreCase) ||
            endpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase))
        {
            return CapabilityCuratorSkillSource.Inline;
        }

        return string.Equals(value, "registered", StringComparison.OrdinalIgnoreCase)
            ? CapabilityCuratorSkillSource.Registered
            : CapabilityCuratorSkillSource.File;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)]
    private static partial Regex EnvironmentVariableNameRegex();

    private sealed class SkillConfigurationModel
    {
        public string? SkillSource { get; set; }
        public string? SkillRoot { get; set; }
        public List<string>? AllowedExternalRoots { get; set; }
        public string? RegisteredSkillServiceType { get; set; }
        public InlineSkillModel? InlineSkill { get; set; }
        public bool? ScriptApproval { get; set; }
        public SkillScriptExecutionModel? ScriptExecution { get; set; }
    }

    private sealed class SkillScriptExecutionModel
    {
        public bool? ApprovalRequired { get; set; }
        public string? TrustLevel { get; set; }
    }

    private sealed class InlineSkillModel
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Instructions { get; set; }
        public List<InlineSkillResourceModel>? Resources { get; set; }
    }

    private sealed class InlineSkillResourceModel
    {
        public string? Name { get; set; }
        public string? Content { get; set; }
        public string? Description { get; set; }
    }

    private sealed class ToolConfigurationModel
    {
        public string? ToolKind { get; set; }
        public string? RuntimeToolName { get; set; }
        public string? ImplementationKey { get; set; }
        public List<string>? OperationClassifications { get; set; }
        public SideEffectModel? SideEffects { get; set; }
        public ExternalProcessModel? ExternalProcess { get; set; }
        public ExternalHttpModel? ExternalHttp { get; set; }
    }

    private sealed class SideEffectModel
    {
        public string? Kind { get; set; }
        public bool? RequiresApprovalByDefault { get; set; }
        public bool? IsStateChanging { get; set; }
    }

    private sealed class ExternalProcessModel
    {
        public string? Command { get; set; }
        public List<string>? Arguments { get; set; }
        public string? WorkingDirectory { get; set; }
        public List<string>? AllowedExecutableNames { get; set; }
        public List<string>? RequiredOutputProperties { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? MaxOutputBytes { get; set; }
    }

    private sealed class ExternalHttpModel
    {
        public string? Method { get; set; }
        public string? Endpoint { get; set; }
        public Dictionary<string, string>? HeaderBindings { get; set; }
        public List<string>? RequiredOutputProperties { get; set; }
        public int? TimeoutSeconds { get; set; }
        public int? MaxResponseBytes { get; set; }
    }

    private sealed class McpConfigurationModel
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
        public Dictionary<string, string>? HeaderBindings { get; set; }
        public List<string>? AllowedTools { get; set; }
        public string? ApprovalMode { get; set; }
        public int? TimeoutSeconds { get; set; }
        public List<string>? OperationClassifications { get; set; }
    }
}
