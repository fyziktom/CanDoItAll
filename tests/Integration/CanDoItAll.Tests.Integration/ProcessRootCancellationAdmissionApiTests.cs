using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessRootCancellationAdmissionApiTests {
    internal static Dictionary<string, string?> Configuration(bool enabled = true, bool suppressed = false) => new() {
        [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = suppressed ? LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind : "Application",
        [$"{ProcessCancellationRecoveryOptions.ConfigurationSectionName}:{nameof(ProcessCancellationRecoveryOptions.Enabled)}"] = enabled.ToString(),
        [$"{ProcessCancellationRecoveryOptions.ConfigurationSectionName}:{nameof(ProcessCancellationRecoveryOptions.IdleDelay)}"] = "00:00:00.025"
    };

    internal static string Route(Guid runId) => $"/api/processes/runs/{runId:D}/cancellation";

    [Fact]
    public async Task HTTP_admission_requires_both_current_grants_rejects_a_body_and_records_the_authenticated_actor() {
        await using var host = await ApiTestHost.CreateAsync(true, apiConfiguration: Configuration());
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var route = Route(initial.State.RunId.Value);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        using var anonymous = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var grant in new[] { ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses, ApiAccessScopeNames.WriteProcesses }) {
            ProcessPlanningReviewHttpAccessTests.UseToken(host, [grant]);
            using var denied = await host.Client.PostAsync(route, null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        using var body = await host.Client.PostAsJsonAsync(route, new { requestedBy = "untrusted-actor" });
        Assert.Equal(HttpStatusCode.BadRequest, body.StatusCode);
        using var empty = await host.Client.PostAsync(Route(Guid.Empty), null);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var missing = await host.Client.PostAsync(Route(Guid.NewGuid()), null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
        using var accepted = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal("no-store", accepted.Headers.CacheControl?.ToString());
        Assert.Equal(route, accepted.Headers.Location?.OriginalString);
        var admission = (await accepted.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>())!;
        Assert.Equal(nameof(ProcessRootCancellationAdmissionStatus.Accepted), admission.Status);
        Assert.NotNull(admission.BarrierCommandId);
        var recorded = await context.RuntimeEvents.AsNoTracking().SingleAsync(item => item.ActorId == "process-review-manager");
        Assert.Equal(nameof(ProcessEventActorKind.User), recorded.ActorKind);
        using var replay = await host.Client.PostAsync(route, null);
        Assert.Contains(replay.StatusCode, new[] { HttpStatusCode.Accepted, HttpStatusCode.OK });
        var recovered = (await replay.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>())!;
        Assert.Equal(admission.BarrierCommandId, recovered.BarrierCommandId);
        Assert.Equal(1, await context.RuntimeEvents.CountAsync(item => item.ActorId == "process-review-manager"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task HTTP_admission_refuses_disabled_recovery_or_a_suppressed_worker_lane_without_changing_runtime(bool enabled, bool suppressed) {
        await using var host = await ApiTestHost.CreateAsync(true, apiConfiguration: Configuration(enabled, suppressed));
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        using var refused = await host.Client.PostAsync(Route(initial.State.RunId.Value), null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        var result = (await refused.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>())!;
        Assert.Equal(nameof(ProcessRootCancellationAdmissionStatus.Unavailable), result.Status);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
        Assert.Equal(!suppressed, host.App.Services.GetServices<IHostedService>().OfType<ProcessCancellationRecoveryWorker>().Any());
    }

    [Fact]
    public async Task HTTP_child_admission_does_not_cancel_its_root_or_sibling() {
        await using var host = await ApiTestHost.CreateAsync(true, apiConfiguration: Configuration());
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, host.App.Services);
        var root = await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(root.CommittedNow);
        root = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(root.State));
        Assert.True(root.CommittedNow);
        var child = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        Assert.True(child.CommittedNow);
        var sibling = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        Assert.True(sibling.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, root.State);
        using var refused = await host.Client.PostAsync(Route(child.State.RunId.Value), null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var result = (await refused.Content.ReadFromJsonAsync<ProcessCancellationAdmissionApiView>())!;
        Assert.Equal(nameof(ProcessRootCancellationAdmissionStatus.NotRoot), result.Status);
        Assert.Equal(nameof(ProcessCancellationTracking.ChildRun), result.Progress!.Tracking);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, root.State));
        Assert.Equal(ProcessRuntimeStatus.Created, (await owner.LoadAsync(child.State.RunId))!.Status);
        Assert.Equal(ProcessRuntimeStatus.Created, (await owner.LoadAsync(sibling.State.RunId))!.Status);
    }
}
