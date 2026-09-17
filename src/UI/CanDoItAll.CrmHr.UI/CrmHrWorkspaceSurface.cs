using Microsoft.AspNetCore.Components;

namespace CanDoItAll.CrmHr.UI;

// The part of a workspace view every workspace surface needs: a way to ask the host to render. The host owns the
// state a surface binds to, so an event handled inside the surface ends with the host rendering, and the host renders
// the surface with it.
public interface ICrmHrWorkspaceView
{
    void RequestRender();
}

// Base of the routed workspace surfaces. A surface renders one view contract that its host implements: state and
// drafts as properties, effects as methods. It injects no service and owns no business state. Events whose handler is
// a host method are dispatched to the host by Blazor itself; events handled inside the surface (bindings into a host
// draft, lambdas that call a host method) are followed by a host render, exactly as when the markup lived in the host.
public abstract class CrmHrWorkspaceSurface<TView> : ComponentBase, IHandleEvent
    where TView : class, ICrmHrWorkspaceView
{
    [Parameter, EditorRequired]
    public TView View { get; set; } = default!;

    // Host-owned chrome: the CRM / HR area tabs navigate, so the routed host composes them here.
    [Parameter]
    public RenderFragment? SecondaryNavigation { get; set; }

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        var task = callback.InvokeAsync(arg);
        var completed = task.Status is TaskStatus.RanToCompletion or TaskStatus.Canceled;
        View.RequestRender();
        return completed ? Task.CompletedTask : RenderAfterAsync(task);
    }

    private async Task RenderAfterAsync(Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            // The host renders whether the handler completed or faulted; a fault still reaches the renderer.
            View.RequestRender();
        }
    }
}
