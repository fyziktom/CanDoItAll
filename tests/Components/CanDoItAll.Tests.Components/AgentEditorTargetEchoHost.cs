using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentEditorTargetEchoHost : ComponentBase {
    public Guid? AgentId { get; private set; }
    public int Completions { get; private set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) {
        builder.OpenComponent<AgentDetailsDialog>(0);
        builder.AddAttribute(1, nameof(AgentDetailsDialog.AgentId), AgentId);
        builder.AddAttribute(2, nameof(AgentDetailsDialog.InitialProviders), Array.Empty<ProviderProfile>());
        builder.AddAttribute(3, nameof(AgentDetailsDialog.TargetChanged),
            EventCallback.Factory.Create<AgentEditorTarget>(this, target => AgentId = target.AgentId));
        builder.AddAttribute(4, nameof(AgentDetailsDialog.Saved),
            EventCallback.Factory.Create<AgentDetailsDialogResult>(this, _ => Completions++));
        builder.CloseComponent();
    }
}
