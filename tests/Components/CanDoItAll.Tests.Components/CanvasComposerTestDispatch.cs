using System.Text.Json;
using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Tests.Components;

internal static class CanvasComposerTestDispatch {
    public static async Task CreateAsync(CanvasWorkbench canvas, string json) {
        var request = JsonSerializer.Deserialize<CanvasWorkbenchCreateActionRequest>(json)!;
        var id = Guid.NewGuid();
        await canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(id, request, null)));
        await canvas.OnCreateAction(JsonSerializer.Serialize(request with { ComposerOpeningId = id }));
    }

    public static async Task EditAsync(CanvasWorkbench canvas, CanvasWorkbenchNodeEditRequest original, CanvasWorkbenchNodeEditRequest request) {
        var id = Guid.NewGuid();
        await canvas.OnComposerOpened(JsonSerializer.Serialize(new CanvasWorkbenchComposerOpening(id, null, original)));
        await canvas.OnNodeEdited(JsonSerializer.Serialize(request with { ComposerOpeningId = id }));
    }
}
