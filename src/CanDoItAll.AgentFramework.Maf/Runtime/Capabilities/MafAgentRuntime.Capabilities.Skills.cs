using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class SkillCapabilityBuilder(MafAgentRuntime owner)
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

                var fullPath = owner.ResolvePathFromWorkspace(preferredSkillRoot, allowExternal: false);
                if (Directory.Exists(fullPath))
                {
                    resolved.Add(fullPath);
                }
            }

            foreach (var capability in capabilities.Where(capability => capability.Kind == CapabilityKind.Skill))
            {
                var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
                var skillRoot = configuration?.SkillRoot ?? capability.EndpointOrPath;
                if (string.IsNullOrWhiteSpace(skillRoot))
                {
                    continue;
                }

                var allowedExternalRoots = configuration?.AllowedExternalRoots ?? [];
                var fullPath = owner.ResolvePathFromWorkspace(skillRoot, allowExternal: allowedExternalRoots.Count > 0, allowedExternalRoots: allowedExternalRoots);
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
                var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
                var skillRoot = configuration?.SkillRoot ?? capability.EndpointOrPath;
                if (string.IsNullOrWhiteSpace(skillRoot))
                {
                    continue;
                }

                var allowedExternalRoots = configuration?.AllowedExternalRoots ?? [];
                string fullPath;
                try
                {
                    fullPath = owner.ResolvePathFromWorkspace(skillRoot, allowExternal: allowedExternalRoots.Count > 0, allowedExternalRoots: allowedExternalRoots);
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
                var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
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
                var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
                if (string.IsNullOrWhiteSpace(configuration?.RegisteredSkillServiceType))
                {
                    continue;
                }

                var registeredSkillServiceType = configuration.RegisteredSkillServiceType.Trim();
                if (owner.IsRetiredRegisteredSkillCapability(capability, registeredSkillServiceType))
                {
                    owner.LogSkippedRetiredRegisteredSkill(
                        capability.Name,
                        registeredSkillServiceType,
                        "is retired and no longer participates in runtime skill composition.");
                    continue;
                }

                var serviceType = Type.GetType(registeredSkillServiceType, throwOnError: false);
                if (serviceType is null)
                {
                    if (owner.IsRetiredRegisteredSkillServiceType(registeredSkillServiceType))
                    {
                        owner.LogSkippedRetiredRegisteredSkill(
                            capability.Name,
                            registeredSkillServiceType,
                            "points to the retired sandbox assembly and could not be resolved in the current runtime.");
                        continue;
                    }

                    throw new InvalidOperationException($"Registered skill type '{registeredSkillServiceType}' for capability '{capability.Name}' could not be resolved.");
                }

                var service = owner.services.GetService(serviceType);
                if (service is null)
                {
                    if (owner.IsRetiredRegisteredSkillServiceType(registeredSkillServiceType))
                    {
                        owner.LogSkippedRetiredRegisteredSkill(
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
            var configuration = DeserializeConfiguration<SkillCapabilityConfiguration>(capability.ConfigurationJson);
            return configuration?.ScriptExecution?.ApprovalRequired ?? configuration?.ScriptApproval == true;
        }
    }

    private bool IsRetiredRegisteredSkillCapability(CapabilityCatalogItem capability, string? serviceTypeName)
    {
        if (string.Equals(capability.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Workspace Delivery Skill", StringComparison.OrdinalIgnoreCase))
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

    private bool IsRetiredRegisteredSkillServiceType(string? serviceTypeName)
    {
        return !string.IsNullOrWhiteSpace(serviceTypeName) &&
               serviceTypeName.Trim().Contains("CanDoItAll.AgentFramework.Sandbox", StringComparison.OrdinalIgnoreCase);
    }

    private void LogSkippedRetiredRegisteredSkill(
        string capabilityName,
        string serviceTypeName,
        string reason)
    {
        if (services.GetService(typeof(Microsoft.Extensions.Logging.ILoggerFactory)) is not Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            return;
        }

        var logger = loggerFactory.CreateLogger(nameof(MafAgentRuntime));
        Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(
            logger,
            "Skipping retired registered skill capability {CapabilityName} because service type {ServiceType} {Reason}",
            capabilityName,
            serviceTypeName,
            reason);
    }
}
