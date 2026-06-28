using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class CapabilityTemplateSeedAssignmentValidator
{
    public static CapabilityValidationResult ValidateAgentAssignments(
        AgentTemplatePack agentPack,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        ArgumentNullException.ThrowIfNull(agentPack);
        ArgumentNullException.ThrowIfNull(capabilities);

        var issues = new List<CapabilityValidationIssue>();
        var capabilitiesByKey = capabilities
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var member in agentPack.Teams.SelectMany(team => team.MemberTemplates))
        {
            for (var index = 0; index < member.Skills.CapabilityKeys.Count; index++)
            {
                var capabilityKey = member.Skills.CapabilityKeys[index]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(capabilityKey) || capabilitiesByKey.Contains(capabilityKey))
                {
                    continue;
                }

                issues.Add(new CapabilityValidationIssue(
                    CapabilityDiagnosticCategory.TemplateValidation,
                    CapabilityValidationSeverity.Error,
                    null,
                    CapabilityKey.TryCreate(capabilityKey, out var key) ? key : null,
                    TemplatePath.Create(Path.Combine(member.RootPath, "skills.json")),
                    $"$.capabilityKeys[{index}]",
                    $"Agent template '{member.Key}' references missing capability '{capabilityKey}'.",
                    "Add the capability to Templates/Capabilities or remove the assignment from the agent skills template."));
            }
        }

        return new CapabilityValidationResult(issues);
    }
}
