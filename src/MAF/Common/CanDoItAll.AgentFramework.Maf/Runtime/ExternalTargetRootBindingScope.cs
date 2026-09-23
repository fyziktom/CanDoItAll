using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Maf;

internal static class ExternalTargetRootBindingScope
{
    public static IReadOnlyList<ExternalTargetRootBinding> Resolve(AgentDefinition agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var configuredBindings = AgentWorkspaceToolAccessMetadata
            .Read(agent.ConfigurationJson)
            .ExternalTargetRootBindings;
        var auditScope = WorkspaceExecutionAuditContext.Current;
        var invocationBindings = auditScope?.ExternalTargetRootBindings ?? [];
        var bindings = auditScope?.InvocationExternalTargetScopeIsAuthoritative == true
            ? invocationBindings.ToArray()
            : configuredBindings.Concat(invocationBindings).ToArray();
        var conflicts = bindings
            .GroupBy(binding => binding.RootId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Distinct().Count() > 1);
        if (conflicts is not null)
        {
            throw new InvalidOperationException(
                $"Conflicting external-target root bindings use identity '{conflicts.Key}'.");
        }

        return bindings
            .DistinctBy(binding => binding.RootId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
