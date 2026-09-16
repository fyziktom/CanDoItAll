using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CanDoItAll.Tests.Components.Prompts;

// Renders a child fragment until Hide is called, so tests can unmount and dispose a real component instance.
public sealed class ConditionalRenderHost : ComponentBase
{
    private bool visible = true;

    [Parameter, EditorRequired]
    public RenderFragment ChildContent { get; set; } = default!;

    public void Hide()
    {
        visible = false;
        StateHasChanged();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (visible)
        {
            builder.AddContent(0, ChildContent);
        }
    }
}
