using System.Net;
using System.Net.Http.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Api;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationNativeHost;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessCancellationNativeClientTests {
    [Fact]
    public async Task Native_read_and_write_require_current_grants_and_return_the_original_root_barrier() {
        await using var host = await CreateAsync();
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var route = $"{Route}/{initial.State.RunId.Value:D}";
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        var read = (await host.Client.GetFromJsonAsync<View>(route))!;
        Assert.Equal(initial.State.RunId.Value, read.RunId);
        Assert.Equal(ProcessCancellationTracking.NotRequested, read.Tracking);
        Assert.False(read.CanRequest);
        Assert.True(read.RootRecoveryAvailable);
        using var denied = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        using var accepted = await host.Client.PostAsync(route, null);
        accepted.EnsureSuccessStatusCode();
        var receipt = (await accepted.Content.ReadFromJsonAsync<Receipt>())!;
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, receipt.Admission);
        Assert.NotNull(receipt.Barrier);
        Assert.False(receipt.ChildStopped);
        var actor = await context.RuntimeEvents.AsNoTracking().SingleAsync(item => item.ActorId == "process-review-manager");
        Assert.Equal(nameof(ProcessEventActorKind.User), actor.ActorKind);
        using var repeat = await host.Client.PostAsync(route, null);
        repeat.EnsureSuccessStatusCode();
        Assert.Equal(receipt.Barrier, (await repeat.Content.ReadFromJsonAsync<Receipt>())!.Barrier);
        Assert.Equal(1, await context.RuntimeEvents.CountAsync(item => item.ActorId == "process-review-manager"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Only_a_trusted_anonymous_interactive_transport_receives_local_operator_authority(bool trusted) {
        await using var host = await CreateAsync();
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        using var response = await host.Client.PostAsync($"{Route}/local/{trusted}/{initial.State.RunId.Value:D}", null);
        Assert.Equal(trusted ? HttpStatusCode.OK : HttpStatusCode.Forbidden, response.StatusCode);
        if (!trusted) {
            Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
            return;
        }
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Accepted, (await response.Content.ReadFromJsonAsync<Receipt>())!.Admission);
        var actor = await context.RuntimeEvents.AsNoTracking().SingleAsync(item => item.ActorId == LocalOperatorAuthenticationStateProvider.ActorId);
        Assert.Equal(nameof(ProcessEventActorKind.User), actor.ActorKind);
    }

    [Fact]
    public async Task Trusted_transport_does_not_expand_an_authenticated_read_only_principal() {
        await using var host = await CreateAsync();
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var response = await host.Client.PostAsync($"{Route}/local/true/{initial.State.RunId.Value:D}", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_credential_revoked_after_the_native_read_cannot_publish_the_read_or_admit_a_write(bool mutation) {
        ReadProbe reads = new();
        await using var host = await CreateAsync(reads);
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        var registry = host.App.Services.GetRequiredService<IApiTokenRegistry>();
        var credential = Assert.Single((await registry.SearchAsync(new("process-review-manager"))).Items);
        reads.AfterRead = async () => { await registry.DeleteAsync(credential.Id); };
        var route = $"{Route}/{initial.State.RunId.Value:D}";
        using var response = mutation ? await host.Client.PostAsync(route, null) : await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(reads.Calls > 0);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
    }

    [Fact]
    public async Task Child_cancellation_keeps_the_root_and_sibling_active_and_returns_a_child_receipt() {
        await using var host = await CreateAsync();
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
        using var response = await host.Client.PostAsync($"{Route}/{child.State.RunId.Value:D}", null);
        response.EnsureSuccessStatusCode();
        var receipt = (await response.Content.ReadFromJsonAsync<Receipt>())!;
        Assert.True(receipt.ChildStopped);
        Assert.Null(receipt.Admission);
        Assert.Null(receipt.Barrier);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, (await owner.LoadAsync(child.State.RunId))!.Status);
        Assert.Equal(root.State.Status, (await owner.LoadAsync(root.State.RunId))!.Status);
        Assert.Equal(ProcessRuntimeStatus.Created, (await owner.LoadAsync(sibling.State.RunId))!.Status);
        Assert.Empty(await context.CancellationRecoveries.ToArrayAsync());
    }

    [Fact]
    public async Task Disabled_recovery_is_visible_to_the_native_client_and_refuses_root_admission() {
        await using var host = await CreateAsync(enabled: false);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses, ApiAccessScopeNames.ExecuteProcesses]);
        await using var context = Context(host.App.Services);
        var initial = await new EfProcessRuntimeUnitOfWork(context).CommitAsync(ProcessProjectAdmissionFixture.Initial());
        Assert.True(initial.CommittedNow);
        var retained = await ProcessRootCancellationFixture.RetainedAsync(context, initial.State);
        var route = $"{Route}/{initial.State.RunId.Value:D}";
        var view = (await host.Client.GetFromJsonAsync<View>(route))!;
        Assert.True(view.CanRequest);
        Assert.False(view.RootRecoveryAvailable);
        using var response = await host.Client.PostAsync(route, null);
        response.EnsureSuccessStatusCode();
        Assert.Equal(ProcessRootCancellationAdmissionStatus.Unavailable, (await response.Content.ReadFromJsonAsync<Receipt>())!.Admission);
        Assert.Equal(retained, await ProcessRootCancellationFixture.RetainedAsync(context, initial.State));
    }
}
