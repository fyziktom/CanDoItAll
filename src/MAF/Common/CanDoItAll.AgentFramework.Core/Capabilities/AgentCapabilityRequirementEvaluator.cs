using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class AgentCapabilityRequirementEvaluator
{
    private const string RetiredSandboxAssemblyName = "CanDoItAll.AgentFramework.Sandbox";

    public static AgentCapabilityRequirementEvaluation Evaluate(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> catalogCapabilities,
        IReadOnlyList<AgentCapabilityRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(catalogCapabilities);
        ArgumentNullException.ThrowIfNull(requirements);

        var catalogById = catalogCapabilities.ToDictionary(item => item.Id);
        var diagnostics = new List<AgentCapabilityDiagnostic>();

        foreach (var requirement in requirements)
        {
            ValidateRequirement(requirement);

            var assignment = agent.Capabilities.FirstOrDefault(item =>
                item.Kind == requirement.Kind &&
                string.Equals(item.CapabilityKey, requirement.CapabilityKey, StringComparison.OrdinalIgnoreCase));
            if (assignment is null)
            {
                diagnostics.Add(CreateDiagnostic(
                    AgentCapabilityDiagnosticCode.MissingRequiredCapability,
                    agent,
                    requirement,
                    $"Agent '{agent.Name}' does not have required {requirement.Kind} capability '{requirement.CapabilityKey}' for role '{ResolveRoleLabel(agent, requirement)}'. Reason: {requirement.Reason}"));
                continue;
            }

            if (!catalogById.TryGetValue(assignment.CapabilityId, out var catalogItem))
            {
                diagnostics.Add(CreateDiagnostic(
                    AgentCapabilityDiagnosticCode.MissingCatalogCapability,
                    agent,
                    requirement,
                    $"Agent '{agent.Name}' has required {requirement.Kind} capability '{requirement.CapabilityKey}', but catalog item '{assignment.CapabilityId:D}' is missing. Runtime composition will not expose the capability."));
                continue;
            }

            if (catalogItem.Kind != requirement.Kind ||
                !string.Equals(catalogItem.Key, requirement.CapabilityKey, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CreateDiagnostic(
                    AgentCapabilityDiagnosticCode.StaleCapabilityAssignment,
                    agent,
                    requirement,
                    $"Agent '{agent.Name}' has stale capability assignment '{requirement.CapabilityKey}'. Catalog item '{assignment.CapabilityId:D}' is {catalogItem.Kind} '{catalogItem.Key}', but role '{ResolveRoleLabel(agent, requirement)}' requires {requirement.Kind} '{requirement.CapabilityKey}'."));
                continue;
            }

            if (IsRetiredCapability(catalogItem))
            {
                diagnostics.Add(CreateDiagnostic(
                    AgentCapabilityDiagnosticCode.RetiredCapability,
                    agent,
                    requirement,
                    $"Agent '{agent.Name}' has required {requirement.Kind} capability '{requirement.CapabilityKey}', but the catalog item is retired and filtered before execution."));
            }
        }

        return new AgentCapabilityRequirementEvaluation(diagnostics);
    }

    public static bool IsRetiredCapability(CapabilityCatalogItem capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (LegacyMemoryCapabilityPolicy.IsRetired(capability.Kind))
        {
            return true;
        }

        if (capability.Kind != CapabilityKind.Skill)
        {
            return false;
        }

        if (string.Equals(capability.Key, "workspace-delivery-skill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(capability.Name, "Workspace Delivery Skill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.EndpointOrPath) &&
            capability.EndpointOrPath.Contains(RetiredSandboxAssemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(capability.ConfigurationJson) &&
            capability.ConfigurationJson.Contains("WorkspaceDeliverySkill", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return TryReadConfigurationString(capability.ConfigurationJson, "registeredSkillServiceType", out var serviceTypeName) &&
               !string.IsNullOrWhiteSpace(serviceTypeName) &&
               serviceTypeName.Contains(RetiredSandboxAssemblyName, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateRequirement(AgentCapabilityRequirement requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement.RoleKey))
        {
            throw new ArgumentException("Capability requirement role key is required.", nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.CapabilityKey))
        {
            throw new ArgumentException("Capability requirement key is required.", nameof(requirement));
        }

        if (string.IsNullOrWhiteSpace(requirement.Reason))
        {
            throw new ArgumentException("Capability requirement reason is required.", nameof(requirement));
        }
    }

    private static AgentCapabilityDiagnostic CreateDiagnostic(
        AgentCapabilityDiagnosticCode code,
        AgentDefinition agent,
        AgentCapabilityRequirement requirement,
        string message)
    {
        return new AgentCapabilityDiagnostic(
            code,
            AgentCapabilityDiagnosticSeverity.Error,
            agent.Id,
            agent.Name,
            requirement.RoleKey,
            agent.RoleTitle,
            requirement.Kind,
            requirement.CapabilityKey,
            message);
    }

    private static string ResolveRoleLabel(
        AgentDefinition agent,
        AgentCapabilityRequirement requirement)
    {
        return string.IsNullOrWhiteSpace(requirement.RoleKey)
            ? agent.RoleTitle
            : requirement.RoleKey;
    }

    private static bool TryReadConfigurationString(
        string? configurationJson,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (!document.RootElement.TryGetProperty(propertyName, out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = valueElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
