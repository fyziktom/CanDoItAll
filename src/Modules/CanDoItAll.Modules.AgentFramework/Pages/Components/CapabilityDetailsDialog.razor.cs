using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed record CapabilityDetailsDialogResult(Guid CapabilityId);

public partial class CapabilityDetailsDialog : IDisposable {
    [Parameter] public Guid CapabilityId { get; set; }
    [Parameter] public IReadOnlyList<string> TagSuggestions { get; set; } = [];
    [Parameter] public CancellationToken OwnerCancellationToken { get; set; }
    private NativeCapabilityAuthoringHost? host;

    public void Dispose() => host?.Dispose();
}
