using CanDoItAll.AppComponents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace CanDoItAll.Tests.Components.AgentFramework;

/// <summary>
/// Test-only stand-in for the app shell's <c>AppToolbar</c>: renders <see cref="ChildContent"/>
/// alongside whatever the page under test registers via <c>AppToolbarActions</c>, so component tests
/// can assert on toolbar action buttons without pulling in the full <c>MainLayout</c>.
/// </summary>
public sealed class AppToolbarActionsTestHost : ComponentBase, IDisposable
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private AppToolbarState ToolbarState { get; set; } = default!;

    protected override void OnInitialized()
        => ToolbarState.Changed += HandleChanged;

    private void HandleChanged()
        => InvokeAsync(StateHasChanged);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, ChildContent);
        builder.AddContent(1, ToolbarState.ActionsContent);
    }

    public void Dispose()
        => ToolbarState.Changed -= HandleChanged;
}
