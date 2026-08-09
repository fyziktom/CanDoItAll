using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class SkillCapabilityBuilder(
    string workspaceRoot,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    IRegisteredCapabilityServiceSource registeredServices,
    ILoggerFactory loggerFactory)
{
    public IReadOnlyList<string> ResolveSkillRoots(
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        AgentRuntimeConfiguration agentConfiguration)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var preferredSkillRoot in agentConfiguration.PreferredSkillRoots ?? [])
        {
            if (string.IsNullOrWhiteSpace(preferredSkillRoot))
            {
                continue;
            }

            var fullPath = MafRuntimePathResolver.ResolvePathFromWorkspace(
                workspaceRoot,
                preferredSkillRoot,
                allowExternal: false,
                physicalPathPolicyFactory: physicalPathPolicyFactory);
            if (Directory.Exists(fullPath))
            {
                resolved.Add(fullPath);
            }
        }

        foreach (var capability in capabilities.Where(capability => capability.Kind == CapabilityKind.Skill))
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            if (ResolveSkillSource(capability, configuration) != SkillCapabilitySource.File)
            {
                continue;
            }

            var skillRoot = configuration?.SkillRoot ?? capability.EndpointOrPath;
            if (string.IsNullOrWhiteSpace(skillRoot))
            {
                continue;
            }

            var allowedExternalRoots = configuration?.AllowedExternalRoots ?? [];
            var fullPath = MafRuntimePathResolver.ResolvePathFromWorkspace(
                workspaceRoot,
                skillRoot,
                allowExternal: allowedExternalRoots.Count > 0,
                physicalPathPolicyFactory: physicalPathPolicyFactory,
                allowedExternalRoots: allowedExternalRoots);
            if (File.Exists(fullPath) && Path.GetFileName(fullPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                fullPath = Path.GetDirectoryName(fullPath)!;
            }

            if (Directory.Exists(fullPath))
            {
                resolved.Add(fullPath);
            }
        }

        return resolved.ToList();
    }

    public IReadOnlyList<FileSkillExecutionPolicy> ResolveScriptExecutionPolicies(IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var resolved = new List<FileSkillExecutionPolicy>();

        foreach (var capability in capabilities.Where(capability => capability.Kind == CapabilityKind.Skill))
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            if (ResolveSkillSource(capability, configuration) != SkillCapabilitySource.File)
            {
                continue;
            }

            var skillRoot = configuration?.SkillRoot ?? capability.EndpointOrPath;
            if (string.IsNullOrWhiteSpace(skillRoot))
            {
                continue;
            }

            var allowedExternalRoots = configuration?.AllowedExternalRoots ?? [];
            string fullPath;
            try
            {
                fullPath = MafRuntimePathResolver.ResolvePathFromWorkspace(
                    workspaceRoot,
                    skillRoot,
                    allowExternal: allowedExternalRoots.Count > 0,
                    physicalPathPolicyFactory: physicalPathPolicyFactory,
                    allowedExternalRoots: allowedExternalRoots);
            }
            catch
            {
                continue;
            }

            if (File.Exists(fullPath) && Path.GetFileName(fullPath).Equals("SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                fullPath = Path.GetDirectoryName(fullPath)!;
            }

            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            resolved.Add(new FileSkillExecutionPolicy(
                RootPath: fullPath,
                ApprovalRequired: configuration?.ScriptExecution?.ApprovalRequired ?? configuration?.ScriptApproval ?? true,
                TrustLevel: string.IsNullOrWhiteSpace(configuration?.ScriptExecution?.TrustLevel) ? "FileSkill" : configuration!.ScriptExecution!.TrustLevel!));
        }

        return resolved;
    }

    public IReadOnlyList<AgentSkill> ResolveInlineSkills(IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var resolved = new List<AgentSkill>();

        foreach (var capability in capabilities.Where(capability => capability.Kind == CapabilityKind.Skill))
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            var inlineSkill = configuration?.InlineSkill;
            if (inlineSkill is null || string.IsNullOrWhiteSpace(inlineSkill.Instructions))
            {
                continue;
            }

            var skill = new AgentInlineSkill(
                name: string.IsNullOrWhiteSpace(inlineSkill.Name) ? capability.Key : inlineSkill.Name,
                description: string.IsNullOrWhiteSpace(inlineSkill.Description) ? capability.Description : inlineSkill.Description,
                instructions: inlineSkill.Instructions);

            foreach (var resource in inlineSkill.Resources ?? [])
            {
                if (string.IsNullOrWhiteSpace(resource.Name) || string.IsNullOrWhiteSpace(resource.Content))
                {
                    continue;
                }

                skill.AddResource(resource.Name, resource.Content, resource.Description);
            }

            resolved.Add(skill);
        }

        return resolved;
    }

    public IReadOnlyList<AgentSkill> ResolveRegisteredSkills(IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var resolved = new List<AgentSkill>();

        foreach (var capability in capabilities.Where(capability => capability.Kind == CapabilityKind.Skill))
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            if (string.IsNullOrWhiteSpace(configuration?.RegisteredSkillServiceType))
            {
                continue;
            }

            var registeredSkillServiceType = configuration.RegisteredSkillServiceType.Trim();
            if (RetiredRegisteredSkillPolicy.IsRetiredRegisteredSkillCapability(capability, registeredSkillServiceType))
            {
                RetiredRegisteredSkillPolicy.LogSkippedRetiredRegisteredSkill(
                    loggerFactory,
                    capability.Name,
                    registeredSkillServiceType,
                    "is retired and no longer participates in runtime skill composition.");
                continue;
            }

            var serviceType = Type.GetType(registeredSkillServiceType, throwOnError: false);
            if (serviceType is null)
            {
                if (RetiredRegisteredSkillPolicy.IsRetiredRegisteredSkillServiceType(registeredSkillServiceType))
                {
                    RetiredRegisteredSkillPolicy.LogSkippedRetiredRegisteredSkill(
                        loggerFactory,
                        capability.Name,
                        registeredSkillServiceType,
                        "points to the retired sandbox assembly and could not be resolved in the current runtime.");
                    continue;
                }

                throw new InvalidOperationException($"Registered skill type '{registeredSkillServiceType}' for capability '{capability.Name}' could not be resolved.");
            }

            var service = registeredServices.Resolve(serviceType);
            if (service is null)
            {
                if (RetiredRegisteredSkillPolicy.IsRetiredRegisteredSkillServiceType(registeredSkillServiceType))
                {
                    RetiredRegisteredSkillPolicy.LogSkippedRetiredRegisteredSkill(
                        loggerFactory,
                        capability.Name,
                        registeredSkillServiceType,
                        "points to the retired sandbox assembly and is no longer registered in DI.");
                    continue;
                }

                throw new InvalidOperationException($"Registered skill type '{registeredSkillServiceType}' for capability '{capability.Name}' is not available in DI.");
            }

            if (service is AgentSkill singleSkill)
            {
                resolved.Add(singleSkill);
                continue;
            }

            if (service is IEnumerable<AgentSkill> skillCollection)
            {
                resolved.AddRange(skillCollection);
                continue;
            }

            throw new InvalidOperationException($"Registered skill service '{registeredSkillServiceType}' for capability '{capability.Name}' is not an AgentSkill.");
        }

        return resolved;
    }

    public bool RequiresSkillScriptApproval(CapabilityCatalogItem capability)
    {
        var configuration = MafRuntimeJson.DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
        return configuration?.ScriptExecution?.ApprovalRequired ?? configuration?.ScriptApproval == true;
    }

    private static SkillCapabilitySource ResolveSkillSource(
        CapabilityCatalogItem capability,
        SkillCapabilityConfiguration? configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration?.SkillSource))
        {
            if (Enum.TryParse<SkillCapabilitySource>(
                    configuration.SkillSource.Trim(),
                    ignoreCase: true,
                    out var source))
            {
                return source;
            }

            throw new InvalidOperationException(
                $"Skill capability '{capability.Name}' declares unsupported source '{configuration.SkillSource}'.");
        }

        if (configuration?.InlineSkill is not null ||
            capability.EndpointOrPath.StartsWith("inline://", StringComparison.OrdinalIgnoreCase))
        {
            return SkillCapabilitySource.Inline;
        }

        if (!string.IsNullOrWhiteSpace(configuration?.RegisteredSkillServiceType))
        {
            return SkillCapabilitySource.Registered;
        }

        return SkillCapabilitySource.File;
    }
}

internal static class RetiredRegisteredSkillPolicy
{
    public static bool IsRetiredRegisteredSkillCapability(CapabilityCatalogItem capability, string? serviceTypeName)
    {
        if (string.Equals(capability.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Workspace Delivery Skill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(capability.Key, "candoitall-bundle-workflow", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Bundle Workflow Skill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.EndpointOrPath) &&
            capability.EndpointOrPath.Contains("CanDoItAll.AgentFramework.Sandbox", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.ConfigurationJson) &&
            capability.ConfigurationJson.Contains("WorkspaceDeliverySkill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IsRetiredRegisteredSkillServiceType(serviceTypeName);
    }

    public static bool IsRetiredRegisteredSkillServiceType(string? serviceTypeName)
    {
        return !string.IsNullOrWhiteSpace(serviceTypeName) &&
               serviceTypeName.Trim().Contains("CanDoItAll.AgentFramework.Sandbox", StringComparison.OrdinalIgnoreCase);
    }

    public static void LogSkippedRetiredRegisteredSkill(
        ILoggerFactory loggerFactory,
        string capabilityName,
        string serviceTypeName,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var logger = loggerFactory.CreateLogger(nameof(MafAgentRuntime));
        LoggerExtensions.LogWarning(
            logger,
            "Skipping retired registered skill capability {CapabilityName} because service type {ServiceType} {Reason}",
            capabilityName,
            serviceTypeName,
            reason);
    }
}
