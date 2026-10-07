using CanDoItAll.AgentFramework.Models;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public partial class CapabilitySetupWizardDialog : IDisposable {
    [Parameter] public CapabilityKind InitialKind { get; set; } = CapabilityKind.McpServer;
    [Parameter] public IReadOnlyList<string> TagSuggestions { get; set; } = [];
    [Parameter] public CancellationToken OwnerCancellationToken { get; set; }
    private NativeCapabilityAuthoringHost? host;

    public void Dispose() => host?.Dispose();
}
