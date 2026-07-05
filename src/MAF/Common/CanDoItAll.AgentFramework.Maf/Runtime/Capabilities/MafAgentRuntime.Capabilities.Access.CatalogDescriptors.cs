using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Mcp;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Skills;
using CanDoItAll.AgentFramework.Skills.Abstractions;
using CanDoItAll.AgentFramework.Tools;
using CanDoItAll.AgentFramework.Tools.Abstractions;
using AccessCapabilityKind = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityKind;
using IsolatedInlineSkillResource = CanDoItAll.AgentFramework.Skills.Abstractions.InlineSkillResource;
using IsolatedSkillScriptExecutionPolicy = CanDoItAll.AgentFramework.Skills.Abstractions.SkillScriptExecutionPolicy;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private CapabilityExposureDescriptor CreateCatalogCapabilityDescriptor(CapabilityCatalogItem capability)
        => capability.Kind switch
        {
            ModelCapabilityKind.Skill => CreateSkillCatalogCapabilityDescriptor(capability),
            ModelCapabilityKind.Tool => CreateToolCatalogCapabilityDescriptor(capability),
            ModelCapabilityKind.McpServer => CreateMcpCatalogCapabilityDescriptor(capability),
            _ => CreateCompatibilityCatalogCapabilityDescriptor(capability)
        };

    private CapabilityExposureDescriptor CreateToolCatalogCapabilityDescriptor(CapabilityCatalogItem capability)
    {
        var runtimeToolName = ResolveCatalogRuntimeToolName(capability);
        if (runtimeToolName is null)
        {
            return CreateCompatibilityCatalogCapabilityDescriptor(capability);
        }

        var classifications = ResolveRuntimeToolOperationClassifications(runtimeToolName.Value.Value);
        var tags = ResolveCatalogTags(capability, runtimeToolName, classifications);
        var sideEffectProfile = ResolveRuntimeToolSideEffectProfile(runtimeToolName.Value.Value);
        ToolDescriptor descriptor = ToolCapabilityRegistry.Classify(runtimeToolName.Value.Value) == ToolInvocationClassification.HostedProviderNative
            ? ToolDescriptorFactory.ProviderNative(
                CapabilityKey.Create(capability.Key),
                runtimeToolName.Value,
                CreateMafImplementationKey("tool", capability.Key),
                tags,
                classifications,
                sideEffectProfile)
            : ToolDescriptorFactory.Internal(
                CapabilityKey.Create(capability.Key),
                runtimeToolName.Value,
                CreateMafImplementationKey("tool", capability.Key),
                tags,
                classifications,
                sideEffectProfile);

        return ToolExposureDescriptorFactory.Create(descriptor) with
        {
            DisplayName = ResolveCapabilityDisplayName(capability),
            Description = ResolveCapabilityDescription(capability),
            AvailabilityState = ResolveCatalogAvailability(capability)
        };
    }

    private CapabilityExposureDescriptor CreateSkillCatalogCapabilityDescriptor(CapabilityCatalogItem capability)
    {
        var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
        var availability = ResolveCatalogAvailability(capability);
        var inlineSkill = configuration?.InlineSkill;
        if (inlineSkill is not null && !string.IsNullOrWhiteSpace(inlineSkill.Instructions))
        {
            var classifications = ToClassificationSet();
            var descriptor = SkillDescriptorFactory.Inline(
                CapabilityKey.Create(capability.Key),
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                string.IsNullOrWhiteSpace(inlineSkill.Name) ? capability.Key : inlineSkill.Name,
                inlineSkill.Instructions,
                ResolveInlineSkillResources(inlineSkill),
                ResolveCatalogTags(capability, null, classifications),
                classifications,
                availability);

            return SkillExposureDescriptorFactory.Create(descriptor);
        }

        var classificationsWithScript = ToClassificationSet(CapabilityOperationClassification.ScriptExecution);
        var registeredSkillServiceType = configuration?.RegisteredSkillServiceType;
        if (!string.IsNullOrWhiteSpace(registeredSkillServiceType))
        {
            var descriptor = SkillDescriptorFactory.Registered(
                CapabilityKey.Create(capability.Key),
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                CreateMafImplementationKey("registered-skill", capability.Key),
                ResolveCatalogTags(capability, null, classificationsWithScript),
                classificationsWithScript,
                availability);

            return SkillExposureDescriptorFactory.Create(descriptor);
        }

        var skillRoot = configuration?.SkillRoot ?? capability.EndpointOrPath;
        if (!string.IsNullOrWhiteSpace(skillRoot))
        {
            var descriptor = SkillDescriptorFactory.File(
                CapabilityKey.Create(capability.Key),
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                skillRoot,
                configuration?.AllowedExternalRoots ?? [],
                ResolveSkillScriptExecutionPolicy(configuration),
                ResolveCatalogTags(capability, null, classificationsWithScript),
                classificationsWithScript,
                availability);

            return SkillExposureDescriptorFactory.Create(descriptor);
        }

        var fallbackDescriptor = SkillDescriptorFactory.Registered(
            CapabilityKey.Create(capability.Key),
            ResolveCapabilityDisplayName(capability),
            ResolveCapabilityDescription(capability),
            CreateMafImplementationKey("skill", capability.Key),
            ResolveCatalogTags(capability, null, classificationsWithScript),
            classificationsWithScript,
            availability);

        return SkillExposureDescriptorFactory.Create(fallbackDescriptor);
    }

    private CapabilityExposureDescriptor CreateMcpCatalogCapabilityDescriptor(CapabilityCatalogItem capability)
    {
        var configuration = DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson) ?? new McpCapabilityConfiguration();
        var classifications = ResolveCatalogOperationClassifications(capability, runtimeToolName: null);
        var tags = ResolveCatalogTags(capability, runtimeToolName: null, classifications);
        var key = CapabilityKey.Create(capability.Key);
        var serverKey = ResolveMcpServerKey(capability, configuration);
        var approvalMode = ResolveMcpApprovalMode(configuration);
        var timeout = ResolveMcpTimeout(configuration);
        var allowedTools = ResolveMcpAllowedTools(configuration);

        McpServerDescriptor descriptor;
        if (!string.IsNullOrWhiteSpace(configuration.Command))
        {
            descriptor = McpDescriptorFactory.LocalStdio(
                key,
                serverKey,
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                configuration.Command,
                configuration.Arguments ?? [],
                string.IsNullOrWhiteSpace(configuration.WorkingDirectory) ? "." : configuration.WorkingDirectory,
                configuration.AllowedWorkingDirectories ?? [],
                allowedTools,
                configuration.EnvironmentVariableBindings ?? new Dictionary<string, string>(),
                configuration.EnvironmentVariables ?? new Dictionary<string, string>(),
                approvalMode,
                timeout,
                tags,
                classifications,
                ResolveMcpMessageFraming(configuration.MessageFraming, capability));
        }
        else if (TryResolveMcpEndpoint(capability, configuration, out var endpoint))
        {
            descriptor = McpDescriptorFactory.RemoteHttp(
                key,
                serverKey,
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                endpoint,
                allowedTools,
                configuration.HeaderBindings ?? new Dictionary<string, string>(),
                configuration.Headers ?? new Dictionary<string, string>(),
                approvalMode,
                timeout,
                tags,
                classifications);
        }
        else
        {
            descriptor = McpDescriptorFactory.InternalHosted(
                key,
                serverKey,
                ResolveCapabilityDisplayName(capability),
                ResolveCapabilityDescription(capability),
                CreateMafImplementationKey("mcp", capability.Key),
                allowedTools,
                approvalMode,
                timeout,
                tags,
                classifications);
        }

        return McpExposureDescriptorFactory.CreateServer(descriptor) with
        {
            AvailabilityState = ResolveCatalogAvailability(capability)
        };
    }

    private CapabilityExposureDescriptor CreateCompatibilityCatalogCapabilityDescriptor(CapabilityCatalogItem capability)
    {
        var identity = CreateCatalogCapabilityIdentity(capability);
        var runtimeToolName = ResolveCatalogRuntimeToolName(capability);
        var mcpServerKey = capability.Kind == ModelCapabilityKind.McpServer
            ? ResolveMcpServerKey(capability, DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson) ?? new McpCapabilityConfiguration())
            : (McpServerKey?)null;
        var operationClassifications = ResolveCatalogOperationClassifications(capability, runtimeToolName);
        return new CapabilityExposureDescriptor(
            identity,
            ResolveCapabilityDisplayName(capability),
            ResolveCapabilityDescription(capability),
            ImplementationKey: null,
            runtimeToolName,
            mcpServerKey,
            McpToolName: null,
            ResolveCatalogTags(capability, runtimeToolName, operationClassifications),
            operationClassifications,
            ResolveCatalogSideEffectProfile(capability, runtimeToolName),
            ResolveCatalogAvailability(capability),
            SourcePath: null);
    }

    private static IReadOnlyList<IsolatedInlineSkillResource> ResolveInlineSkillResources(
        InlineSkillDefinition inlineSkill)
        => (inlineSkill.Resources ?? [])
            .Where(resource => !string.IsNullOrWhiteSpace(resource.Name) &&
                               !string.IsNullOrWhiteSpace(resource.Content))
            .Select(resource => new IsolatedInlineSkillResource(
                resource.Name!,
                resource.Content!,
                resource.Description))
            .ToList();

    private static IsolatedSkillScriptExecutionPolicy ResolveSkillScriptExecutionPolicy(
        SkillCapabilityConfiguration? configuration)
    {
        var approvalRequired = configuration?.ScriptExecution?.ApprovalRequired ?? configuration?.ScriptApproval ?? true;
        return new IsolatedSkillScriptExecutionPolicy(
            approvalRequired,
            ResolveSkillScriptTrustLevel(configuration?.ScriptExecution?.TrustLevel));
    }

    private static SkillScriptTrustLevel ResolveSkillScriptTrustLevel(string? value)
        => Enum.TryParse<SkillScriptTrustLevel>(value, ignoreCase: true, out var trustLevel)
            ? trustLevel
            : SkillScriptTrustLevel.WorkspaceSkillRoot;

    private static McpServerKey ResolveMcpServerKey(
        CapabilityCatalogItem capability,
        McpCapabilityConfiguration configuration)
    {
        if (McpServerKey.TryCreate(configuration.ServerName, out var configuredKey))
        {
            return configuredKey;
        }

        if (McpServerKey.TryCreate(capability.Key, out var capabilityKey))
        {
            return capabilityKey;
        }

        return McpServerKey.Create(ToKebab(capability.Name));
    }

    private static IReadOnlySet<McpToolName> ResolveMcpAllowedTools(McpCapabilityConfiguration configuration)
        => (configuration.AllowedTools ?? [])
            .Select(tool => McpToolName.TryCreate(tool, out var parsed) ? parsed : (McpToolName?)null)
            .Where(tool => tool.HasValue)
            .Select(tool => tool!.Value)
            .ToHashSet();

    private static McpApprovalMode ResolveMcpApprovalMode(McpCapabilityConfiguration configuration)
        => string.Equals(configuration.ApprovalMode, "AlwaysRequire", StringComparison.OrdinalIgnoreCase)
            ? McpApprovalMode.AlwaysRequire
            : McpApprovalMode.NeverRequire;

    private static TimeSpan ResolveMcpTimeout(McpCapabilityConfiguration configuration)
    {
        const int defaultTimeoutSeconds = 30;
        const int minimumTimeoutSeconds = 1;
        const int maximumTimeoutSeconds = 600;

        var timeoutSeconds = configuration.TimeoutSeconds.GetValueOrDefault(defaultTimeoutSeconds);
        return TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, minimumTimeoutSeconds, maximumTimeoutSeconds));
    }

    private static McpStdioMessageFraming ResolveMcpMessageFraming(
        string? value,
        CapabilityCatalogItem capability)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return McpStdioMessageFraming.ContentLength;
        }

        var normalized = value
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized switch
        {
            "contentlength" => McpStdioMessageFraming.ContentLength,
            "newlinedelimitedjson" or "newlinejson" or "newline" or "ndjson" => McpStdioMessageFraming.NewlineDelimitedJson,
            _ => throw new InvalidOperationException(
                $"MCP capability '{capability.Name}' has unsupported messageFraming '{value.Trim()}'. Use contentLength or newlineDelimitedJson.")
        };
    }

    private static bool TryResolveMcpEndpoint(
        CapabilityCatalogItem capability,
        McpCapabilityConfiguration configuration,
        out Uri endpoint)
    {
        var configured = string.IsNullOrWhiteSpace(configuration.Endpoint)
            ? capability.EndpointOrPath
            : configuration.Endpoint;
        return Uri.TryCreate(configured, UriKind.Absolute, out endpoint!);
    }

    private static ImplementationKey CreateMafImplementationKey(
        string adapterKind,
        string capabilityKey)
        => ImplementationKey.Create($"maf.{adapterKind}.{capabilityKey}");

    private static string ResolveCapabilityDisplayName(CapabilityCatalogItem capability)
        => string.IsNullOrWhiteSpace(capability.Name)
            ? capability.Key
            : capability.Name;

    private static string ResolveCapabilityDescription(CapabilityCatalogItem capability)
    {
        if (!string.IsNullOrWhiteSpace(capability.Description))
        {
            return capability.Description;
        }

        return string.IsNullOrWhiteSpace(capability.Name)
            ? capability.Key
            : capability.Name;
    }

    private static CapabilityIdentity CreateCatalogCapabilityIdentity(CapabilityCatalogItem capability)
        => new(
            MapCapabilityKind(capability.Kind),
            CapabilityKey.Create(capability.Key));

    private static AccessCapabilityKind MapCapabilityKind(ModelCapabilityKind kind)
        => kind switch
        {
            ModelCapabilityKind.Skill => AccessCapabilityKind.Skill,
            ModelCapabilityKind.Tool => AccessCapabilityKind.Tool,
            ModelCapabilityKind.McpServer => AccessCapabilityKind.McpServer,
            ModelCapabilityKind.Plugin => AccessCapabilityKind.Plugin,
            ModelCapabilityKind.Rag => AccessCapabilityKind.Rag,
            ModelCapabilityKind.AiContext => AccessCapabilityKind.AiContext,
            ModelCapabilityKind.Memory => AccessCapabilityKind.Memory,
            _ => throw new InvalidOperationException($"Unsupported capability kind '{kind}'.")
        };

    private static RuntimeToolName? ResolveCatalogRuntimeToolName(CapabilityCatalogItem capability)
    {
        if (capability.Kind != ModelCapabilityKind.Tool &&
            capability.Kind != ModelCapabilityKind.Plugin)
        {
            return null;
        }

        var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson);
        var toolKey = configuration?.Tool ?? capability.Key;
        return TryCreateRuntimeToolName(toolKey, out var runtimeToolName)
            ? runtimeToolName
            : null;
    }

    private IReadOnlySet<CapabilityOperationClassification> ResolveCatalogOperationClassifications(
        CapabilityCatalogItem capability,
        RuntimeToolName? runtimeToolName)
    {
        if (runtimeToolName is not null)
        {
            return ResolveRuntimeToolOperationClassifications(runtimeToolName.Value.Value);
        }

        return capability.Kind switch
        {
            ModelCapabilityKind.Skill => ToClassificationSet(CapabilityOperationClassification.ScriptExecution),
            ModelCapabilityKind.McpServer when IsBrowserMcpCapability(capability) => ToClassificationSet(
                CapabilityOperationClassification.McpTool,
                CapabilityOperationClassification.BrowserAccess),
            ModelCapabilityKind.McpServer => ToClassificationSet(CapabilityOperationClassification.McpTool),
            ModelCapabilityKind.Rag or ModelCapabilityKind.AiContext or ModelCapabilityKind.Memory => ToClassificationSet(CapabilityOperationClassification.Read),
            _ => ToClassificationSet(CapabilityOperationClassification.Read)
        };
    }

    private CapabilitySideEffectProfile ResolveCatalogSideEffectProfile(
        CapabilityCatalogItem capability,
        RuntimeToolName? runtimeToolName)
    {
        if (runtimeToolName is not null &&
            ToolCapabilityRegistry.TryResolve(runtimeToolName.Value.Value, out var metadata))
        {
            return new CapabilitySideEffectProfile(
                MapSideEffectKind(metadata.SideEffectKind),
                metadata.RequiresApprovalByDefault,
                metadata.IsStateChanging);
        }

        return capability.Kind switch
        {
            ModelCapabilityKind.Skill => new CapabilitySideEffectProfile(CapabilitySideEffectKind.LocalProcessExecution, true, true),
            ModelCapabilityKind.McpServer => new CapabilitySideEffectProfile(CapabilitySideEffectKind.McpTool, true, false),
            _ => new CapabilitySideEffectProfile(CapabilitySideEffectKind.None, false, false)
        };
    }

    private CapabilityAvailabilityState ResolveCatalogAvailability(CapabilityCatalogItem capability)
    {
        if (capability.Kind == ModelCapabilityKind.Skill)
        {
            var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            if (IsRetiredRegisteredSkillCapability(capability, configuration?.RegisteredSkillServiceType))
            {
                return CapabilityAvailabilityState.Retired;
            }
        }

        return CapabilityAvailabilityState.Available;
    }

    private static IReadOnlySet<CapabilityTag> ResolveCatalogTags(
        CapabilityCatalogItem capability,
        RuntimeToolName? runtimeToolName,
        IReadOnlySet<CapabilityOperationClassification> classifications)
    {
        var tags = new HashSet<CapabilityTag>
        {
            CapabilityTag.Create("catalog"),
            CapabilityTag.Create(ToKebab(capability.Kind.ToString()))
        };
        foreach (var tag in capability.Tags)
        {
            if (CapabilityTag.TryCreate(tag, out var parsed))
            {
                tags.Add(parsed);
            }
        }

        if (runtimeToolName is not null)
        {
            tags.Add(CapabilityTag.Create("tool"));
            if (AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(runtimeToolName.Value.Value, out _))
            {
                tags.Add(CapabilityTag.Create("workspace"));
            }
        }

        foreach (var classification in classifications)
        {
            tags.Add(CapabilityTag.Create(ToKebab(classification.ToString())));
        }

        return tags;
    }

    private bool IsBrowserMcpCapability(CapabilityCatalogItem capability)
    {
        if (capability.Kind != ModelCapabilityKind.McpServer)
        {
            return false;
        }

        var configuration = DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson);
        return capability.Key.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
               capability.Name.Contains("playwright", StringComparison.OrdinalIgnoreCase) ||
               capability.EndpointOrPath.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase) ||
               (configuration?.Arguments?.Any(argument =>
                   argument.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase)) ?? false);
    }
}
