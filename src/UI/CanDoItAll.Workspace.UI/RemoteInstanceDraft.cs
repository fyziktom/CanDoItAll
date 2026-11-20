using CanDoItAll.RemoteInstances;

namespace CanDoItAll.Workspace.UI;

public sealed class RemoteInstanceDraft(RemoteInstanceConnection? original = null) {
    public Guid Id { get; } = original?.Id ?? Guid.NewGuid();
    public Guid? ExpectedVersion { get; } = original?.ConcurrencyToken;
    public string Name { get; set; } = original?.Name ?? string.Empty;
    public string Address { get; set; } = original?.BaseUri.AbsoluteUri ?? string.Empty;
    public Guid CredentialId { get; set; } = original?.ApiTokenSecretId ?? Guid.Empty;
    public bool Enabled { get; set; } = original?.IsEnabled ?? true;
    public bool AllowPrivateNetwork { get; set; } = original?.AllowPrivateNetwork ?? false;
}
