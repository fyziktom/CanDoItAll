using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessLegacyCancellationApiTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_root_request_requires_migration_without_any_mutation(bool recoveryEnabled) {
        await using var host = await ApiTestHost.CreateAsync(true,
            apiConfiguration: ProcessRootCancellationAdmissionApiTests.Configuration(recoveryEnabled));
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, host.App.Services);
        var root = await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial());
        root = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(root.State));
        var child = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        var sibling = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        Assert.True(root.CommittedNow && child.CommittedNow && sibling.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, root.State);

        using var response = await host.Client.PostAsJsonAsync(Route(root.State.RunId.Value), new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(ProcessRootCancellationAdmissionApiTests.Route(root.State.RunId.Value), response.Headers.Location?.OriginalString);
        Assert.Contains("process.root_cancellation_requires_admission", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, root.State));
        Assert.Equal(ProcessRuntimeStatus.Created, (await owner.LoadAsync(child.State.RunId))!.Status);
        Assert.Equal(ProcessRuntimeStatus.Created, (await owner.LoadAsync(sibling.State.RunId))!.Status);
    }

    [Fact]
    public async Task Legacy_child_request_preserves_root_and_sibling_and_keeps_precise_missing_identity_errors() {
        await using var host = await ApiTestHost.CreateAsync(true,
            apiConfiguration: ProcessRootCancellationAdmissionApiTests.Configuration(false));
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, host.App.Services);
        var root = await owner.CommitAsync(ProcessProjectAdmissionFixture.Initial());
        root = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(root.State));
        var child = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        var sibling = await owner.CommitAsync(ProcessRootCancellationFixture.Descendant(root.State));
        Assert.True(root.CommittedNow && child.CommittedNow && sibling.CommittedNow);
        var rootBefore = JsonSerializer.Serialize(root.State);
        var siblingBefore = JsonSerializer.Serialize(sibling.State);

        using var response = await host.Client.PostAsJsonAsync(Route(child.State.RunId.Value), new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<ProcessRuntimeCancelApiResponse>())!;
        Assert.Equal(child.State.RunId.Value, receipt.RunId);
        Assert.Equal(nameof(ProcessRuntimeTransitionOutcome.Applied), receipt.Outcome);
        Assert.Equal(nameof(ProcessRuntimeStatus.Cancelled), receipt.Status);
        context.ChangeTracker.Clear();
        Assert.Equal(rootBefore, JsonSerializer.Serialize(await owner.LoadAsync(root.State.RunId)));
        Assert.Equal(siblingBefore, JsonSerializer.Serialize(await owner.LoadAsync(sibling.State.RunId)));
        Assert.Equal(ProcessRuntimeStatus.Cancelled, (await owner.LoadAsync(child.State.RunId))!.Status);
        Assert.Empty(await context.CancellationRecoveries.ToArrayAsync());
        using var missing = await host.Client.PostAsJsonAsync(Route(Guid.NewGuid()), new { });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var empty = await host.Client.PostAsJsonAsync(Route(Guid.Empty), new { });
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    private static string Route(Guid runId) => $"/api/processes/runs/{runId:D}/cancel";
}
