using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceSettingsHttpOutcomeTests {
    [Fact]
    public async Task Postcommit_activity_failure_returns_the_saved_six_field_snapshot_and_pending_header() {
        var activity = new ActivityFault();
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton<IActivityStream>(activity));
        activity.Fail = true;
        var request = new WorkspaceSettingsModel {
            WorkspaceName = " Normalized HTTP ", DefaultPromptOutputFormat = " JSON ",
            CurrencyCode = "eur", CurrencyCultureName = "de-DE", Notes = " Six fields "
        };
        using var saved = await host.Client.PutAsJsonAsync("/api/settings/workspace", request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("pending", Assert.Single(saved.Headers.GetValues("X-CanDoItAll-Read-Back")));
        var json = await saved.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(6, json!.Count);
        var observed = await host.Client.GetFromJsonAsync<WorkspaceSettingsModel>("/api/settings/workspace");
        Assert.Equal("Normalized HTTP", observed!.WorkspaceName);
        Assert.Equal("JSON", observed.DefaultPromptOutputFormat);
        Assert.Equal("EUR", observed.CurrencyCode);
        Assert.Equal("de-DE", observed.CurrencyCultureName);
        Assert.Equal("Six fields", observed.Notes);
        Assert.Null(observed.DefaultProviderProfileId);
        json["isAdmin"] = true;
        using var rejected = await host.Client.PutAsJsonAsync("/api/settings/workspace", json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var missing = await host.Client.PutAsJsonAsync("/api/settings/workspace", new { workspaceName = "Incomplete" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(1, activity.SettingsWrites);
    }

    private sealed class ActivityFault : IActivityStream {
        public bool Fail { get; set; }
        public int SettingsWrites { get; private set; }
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (!Fail) {
                return Task.CompletedTask;
            }
            SettingsWrites++;
            throw new IOException("Owned activity acknowledgement failure.");
        }
    }
}
