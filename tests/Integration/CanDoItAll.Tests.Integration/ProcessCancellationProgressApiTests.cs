using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationProgressApiTests {
    [Fact]
    public async Task Current_read_permission_is_required_each_time_and_private_recovery_material_never_crosses_HTTP() {
        await using var host = await ApiTestHost.CreateAsync(true);
        await using var context = Context(host.App.Services);
        var root = await RootAsync(context);
        var queue = Queue(host.App.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        var intent = Intent(claim);
        claim = (await queue.CheckpointAsync(claim, new(claim.Position, intent,
            ProcessCancellationRecoveryPending.UnresolvedEffects))).Claim!;
        var retained = await RowsAsync(context);
        var route = $"/api/processes/runs/{root.RunId.Value:D}/cancellation";
        using var anonymous = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ExecuteProcesses]);
        using var forbidden = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        var view = JsonSerializer.Deserialize<ProcessCancellationApiView>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(root.RunId.Value, view.RunId);
        Assert.Equal(nameof(ProcessCancellationTracking.Pending), view.Tracking);
        Assert.Equal(nameof(ProcessRuntimeStatus.CancelRequested), view.RuntimeStatus);
        Assert.Equal(claim.BarrierCommandId!.Value.Value, view.Recovery!.BarrierCommandId);
        Assert.Equal(nameof(ProcessCancellationRecoveryPending.UnresolvedEffects), view.Recovery.Pending);
        Assert.Equal(claim.ProgressRevision.ToString(System.Globalization.CultureInfo.InvariantCulture), view.Recovery.Revision);
        Assert.DoesNotContain(claim.LeaseToken.ToString("D"), body, StringComparison.Ordinal);
        Assert.DoesNotContain(intent.Context.CommandId.Value.ToString("D"), body, StringComparison.Ordinal);
        Assert.DoesNotContain("intent", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lease", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("positionJson", body, StringComparison.OrdinalIgnoreCase);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.WriteProcesses]);
        using var laterDenied = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, laterDenied.StatusCode);
        Assert.Equal(retained, await RowsAsync(context));
        using var scope = host.App.Services.CreateScope();
        Assert.IsType<EfProcessCancellationProgressReader>(scope.ServiceProvider.GetRequiredService<IProcessCancellationProgressReader>());
    }

    [Fact]
    public async Task Unknown_and_empty_identities_have_explicit_HTTP_outcomes_without_creating_recovery() {
        await using var host = await ApiTestHost.CreateAsync(true);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var missing = await host.Client.GetAsync($"/api/processes/runs/{Guid.NewGuid():D}/cancellation");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("no-store", missing.Headers.CacheControl?.ToString());
        using var invalid = await host.Client.GetAsync($"/api/processes/runs/{Guid.Empty:D}/cancellation");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await using var context = Context(host.App.Services);
        Assert.Empty(await RowsAsync(context));
        using var contract = JsonDocument.Parse(await host.Client.GetStringAsync("/api/processes/contract"));
        Assert.Contains(contract.RootElement.GetProperty("endpoints").EnumerateArray(),
            endpoint => endpoint.GetString() == "GET /api/processes/runs/{runId}/cancellation");
    }

    [Fact]
    public async Task Cancelled_legacy_runtime_is_untracked_over_HTTP_even_with_recovery_disabled() {
        await using var host = await ApiTestHost.CreateAsync(true);
        await using var context = Context(host.App.Services);
        var legacy = await RootAsync(context, legacy: true);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var response = await host.Client.GetAsync($"/api/processes/runs/{legacy.RunId.Value:D}/cancellation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var view = (await response.Content.ReadFromJsonAsync<ProcessCancellationApiView>())!;
        Assert.Equal(nameof(ProcessRuntimeStatus.Cancelled), view.RuntimeStatus);
        Assert.Equal(nameof(ProcessCancellationTracking.LegacyUntracked), view.Tracking);
        Assert.Null(view.Recovery);
        Assert.Empty(await RowsAsync(context));
        Assert.False(await context.CancellationRecoverySeeds.AnyAsync(row => row.Completed));
    }
}
