using System.Collections.Frozen;

namespace CanDoItAll.AgentFramework.Core;

public sealed class AgentToolPolicyCatalog {
    private readonly FrozenDictionary<string, ToolCapabilityMetadata> capabilities;

    public static AgentToolPolicyCatalog BuiltIn { get; } = new([]);

    public AgentToolPolicyCatalog(IEnumerable<ToolCapabilityMetadata> contributions) {
        ArgumentNullException.ThrowIfNull(contributions);
        var records = new Dictionary<string, ToolCapabilityMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var capability in ToolCapabilityRegistry.Capabilities.Concat(contributions)) {
            ArgumentNullException.ThrowIfNull(capability);
            var name = ToolContractCatalog.NormalizeToolName(capability.Name);
            if (string.IsNullOrWhiteSpace(name) || capability.Classification == ToolInvocationClassification.Unknown) {
                throw new ArgumentException("A tool policy requires a stable name and an explicit classification.", nameof(contributions));
            }
            if (capability.BusinessArgumentRetentionScheme is { } scheme &&
                (string.IsNullOrWhiteSpace(scheme) || !string.Equals(scheme, scheme.Trim(), StringComparison.Ordinal))) {
                throw new ArgumentException($"Tool '{name}' has an invalid argument retention scheme.", nameof(contributions));
            }
            var immutable = capability with {
                Name = name,
                OperationRequirements = Array.AsReadOnly(capability.OperationRequirements
                    .Select(requirement => new ToolCapabilityProcessOperationRequirement(Array.AsReadOnly(requirement.AnyOf.ToArray())))
                    .ToArray()),
                TargetScopeRequirements = Array.AsReadOnly(capability.TargetScopeRequirements.ToArray())
            };
            if (!records.TryAdd(name, immutable)) {
                throw new ArgumentException($"Tool '{name}' has more than one registered invocation policy.", nameof(contributions));
            }
        }
        capabilities = records.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public bool TryResolve(string? toolName, out ToolCapabilityMetadata metadata) {
        if (capabilities.TryGetValue(ToolContractCatalog.NormalizeToolName(toolName), out var value)) {
            metadata = value;
            return true;
        }
        metadata = null!;
        return false;
    }

    public ToolInvocationClassification Classify(string? toolName) {
        return TryResolve(toolName, out var metadata)
            ? metadata.Classification
            : ToolCapabilityRegistry.Classify(toolName);
    }

    public bool RequiresApprovalByDefault(string? toolName) {
        return TryResolve(toolName, out var metadata) && metadata.RequiresApprovalByDefault;
    }
}
