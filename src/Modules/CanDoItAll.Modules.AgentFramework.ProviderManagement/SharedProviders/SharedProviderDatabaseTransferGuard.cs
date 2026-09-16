namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderDatabaseTransferGuard : IProviderDatabaseTransferGuard {
    public Task<string?> FindBlockReasonAsync(ProviderDatabaseTransferInspection inspection,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(inspection);
        cancellationToken.ThrowIfCancellationRequested();
        var causes = new List<string>();
        if (inspection.SourceHasSharedProviderReferences) {
            causes.Add("the source contains provider profiles referenced by shared-provider publications or imports");
        }
        if (inspection.TargetHasSharedProviderReferences) {
            causes.Add("the target contains provider profiles referenced by shared-provider publications or imports");
        }
        if (inspection.TargetUsesTransferredSecret) {
            causes.Add("a target shared-provider source uses a secret that the transfer would replace");
        }
        return Task.FromResult(causes.Count == 0
            ? null
            : $"AI provider transfer is blocked because {string.Join("; ", causes)}. Transfer shared-provider state through its owning workflow first.");
    }
}
