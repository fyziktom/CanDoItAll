using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.JSInterop;

namespace CanDoItAll.Web.Infrastructure;

public sealed class BrowserWorkspaceStateStore(IJSRuntime jsRuntime) : IWorkbenchStateStore
{
    private const string StorageKey = "candoitall.workbench.session";
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var payload = await jsRuntime.InvokeAsync<string?>("CanDoItAll.browserState.load", cancellationToken, StorageKey);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkbenchSessionSnapshot>(payload, _serializerOptions);
    }

    public async ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(snapshot, _serializerOptions);
        await jsRuntime.InvokeVoidAsync("CanDoItAll.browserState.save", cancellationToken, StorageKey, payload);
    }
}
