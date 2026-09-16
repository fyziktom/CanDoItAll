namespace CanDoItAll.AgentFramework.Core;

public sealed record ToolCapabilityOperationEffects {
    public static ToolCapabilityOperationEffects None { get; } = new([]);

    public ToolCapabilityOperationEffects(IReadOnlyList<string> targetScopeRequirements, bool canMutateProduct = false,
        bool canExecuteExternalAction = false, bool canWriteManagedArtifact = false) {
        ArgumentNullException.ThrowIfNull(targetScopeRequirements);
        if (targetScopeRequirements.Any(scope => string.IsNullOrWhiteSpace(scope) || scope != scope.Trim())) {
            throw new ArgumentException("Owner target-scope identifiers must be non-empty and normalized.", nameof(targetScopeRequirements));
        }
        TargetScopeRequirements = Array.AsReadOnly(targetScopeRequirements.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray());
        CanMutateProduct = canMutateProduct;
        CanExecuteExternalAction = canExecuteExternalAction;
        CanWriteManagedArtifact = canWriteManagedArtifact;
    }

    public IReadOnlyList<string> TargetScopeRequirements { get; }
    public bool CanMutateProduct { get; }
    public bool CanExecuteExternalAction { get; }
    public bool CanWriteManagedArtifact { get; }
}
