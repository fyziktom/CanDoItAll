using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using System.Text.Json;
using CanDoItAll.SharedKernel;
using Microsoft.JSInterop;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Infrastructure;

public sealed class BrowserWorkspaceStateStore(
    IJSRuntime jsRuntime,
    IOptions<WorkbenchOptions> options,
    IActiveDatabaseProfileResolver activeDatabaseProfileResolver) : IWorkbenchStateStore
{
    private readonly WorkbenchOptions _options = options.Value;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var activeProfile = activeDatabaseProfileResolver.ResolveCurrentProfile();
        var payload = await jsRuntime.InvokeAsync<string?>(
            "CanDoItAll.browserState.load",
            cancellationToken,
            BuildStorageKey(activeProfile));
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<WorkbenchSessionSnapshot>(payload, _serializerOptions);
        if (snapshot is null)
        {
            return null;
        }

        if (snapshot.ProfileId.HasValue && snapshot.ProfileId != activeProfile.Profile.Id)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ProfileFingerprint) &&
            !string.Equals(snapshot.ProfileFingerprint, activeProfile.Profile.Runtime.Fingerprint, StringComparison.Ordinal))
        {
            return null;
        }

        return snapshot;
    }

    public async ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var activeProfile = activeDatabaseProfileResolver.ResolveCurrentProfile();
        var payload = JsonSerializer.Serialize(
            snapshot with
            {
                ProfileId = activeProfile.Profile.Id,
                ProfileFingerprint = activeProfile.Profile.Runtime.Fingerprint
            },
            _serializerOptions);

        await jsRuntime.InvokeVoidAsync(
            "CanDoItAll.browserState.save",
            cancellationToken,
            BuildStorageKey(activeProfile),
            payload);
    }

    private string BuildStorageKey(ResolvedDatabaseProfile profile)
    {
        var baseKey = string.IsNullOrWhiteSpace(_options.BrowserStorageKey)
            ? "candoitall.workbench.session"
            : _options.BrowserStorageKey.Trim();
        return $"{baseKey}:{profile.Profile.Id:N}";
    }
}


