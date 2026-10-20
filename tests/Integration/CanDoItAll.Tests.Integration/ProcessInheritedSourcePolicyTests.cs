using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessInheritedSourcePolicyTests(Xunit.Abstractions.ITestOutputHelper output) {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Registered_source_admits_and_publishes_the_exact_child_then_reads_its_original_receipt(bool agentSource, bool sameExecutor) {
        var probe = new QueryProbe();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource, sameExecutor, probe, registeredSource: true);
        probe.Commands.Clear();
        var started = Stopwatch.GetTimestamp();
        var observed = await fixture.SourcePolicy.ObserveAsync(fixture.Original, fixture.Profile);
        output.WriteLine($"Selected source observation: {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} ms; {probe.Commands.Count} Process queries.");
        Assert.True(observed.ReadAllowed);
        Assert.True(observed.DispatchAllowed);
        Assert.Equal(fixture.Original.ToolAdmission!.Session.Reference.BackgroundSource!.OwnerFingerprint, observed.OwnerFingerprint);
        Assert.Equal(4, probe.Commands.Count);
        Assert.All(probe.Commands, sql => {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("StrategyResultReceipts", sql, StringComparison.Ordinal);
        });
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.PublishAsync(await fixture.PrepareAsync()));
        fixture.ExpireClaim();
        var run = await fixture.ReadAsync();
        var history = await fixture.Sql.Services.GetRequiredService<IAgentHistoricalReceiptReviewService>()
            .ReadSavedEffectReceiptsAsync(new(run.Id, null, run.AgentId, run.CreatedAtUtc));
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, history.Stop);
        Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, Assert.Single(history.Observations).Knowledge);
        await fixture.Sql.VerifyAsync(1, 1, accepted.Acknowledgement);
    }

    [Fact]
    public async Task Ordinary_initial_admission_and_reservation_do_not_borrow_selected_child_authority() {
        var probe = new QueryProbe();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(processReadProbe: probe, registeredSource: true);
        var candidate = fixture.Original with { Id = Guid.NewGuid(), ToolAdmission = null };
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Journal.CreateForNewBackgroundRunAsync(candidate, "Ordinary input"));
        probe.Commands.Clear();
        var policy = Assert.IsType<ProcessToolBackgroundSourcePolicy>(fixture.SourcePolicy);
        await Assert.ThrowsAsync<NotSupportedException>(() => policy.ObserveReservationAsync(fixture.Original, fixture.Profile).AsTask());
        Assert.Empty(probe.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_original_launcher_revocation_preserves_only_the_remaining_read_permission(bool revokeRead) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var original = await fixture.SourcePolicy.ObserveAsync(fixture.Original, fixture.Profile);
        await fixture.RevokeAsync(fixture.Launcher!.Id, agent => {
            var access = revokeRead ? new AgentProjectStructureAccessSettings()
                : AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            access.CanWrite = false;
            return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
        });
        var storedLauncher = Assert.Single((await fixture.Store.LoadCatalogAsync()).Agents, agent => agent.Id == fixture.Launcher.Id);
        var storedAccess = AgentProjectStructureAccessMetadata.Read(storedLauncher.ConfigurationJson);
        Assert.Equal(!revokeRead, storedAccess.CanRead);
        Assert.False(storedAccess.CanWrite);
        var current = await fixture.SourcePolicy.ObserveAsync(fixture.Original, fixture.Profile);
        Assert.Equal(original.OwnerFingerprint, current.OwnerFingerprint);
        Assert.Equal(!revokeRead, current.ReadAllowed);
        Assert.False(current.DispatchAllowed);
    }

    [Fact]
    public async Task Expired_parent_claim_keeps_original_read_but_cannot_authorize_dispatch() {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var original = await fixture.SourcePolicy.ObserveAsync(fixture.Original, fixture.Profile);
        fixture.ExpireClaim();
        var current = await fixture.SourcePolicy.ObserveAsync(fixture.Original, fixture.Profile);
        Assert.Equal(original.OwnerFingerprint, current.OwnerFingerprint);
        Assert.True(current.ReadAllowed);
        Assert.False(current.DispatchAllowed);
        await Assert.ThrowsAsync<AgentToolAdmissionException>(() => fixture.Journal.RequireBackgroundDispatchAsync(fixture.Original));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Source_await_cannot_leave_an_earlier_generation_or_canceled_operation_authorized(bool initial, bool cancel) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        using var cancellation = new CancellationTokenSource();
        var probe = new SourceProbe(fixture.Sql.Services.GetRequiredService<IProcessSourceAuthorityObservationPolicy>(), () => {
            if (cancel) {
                cancellation.Cancel();
            } else {
                fixture.Generations.Advance();
            }
        });
        var policy = Policy(fixture, probe);
        var pending = initial ? policy.ObserveInitialInheritedAsync(fixture.Original with { ToolAdmission = null }, fixture.Profile,
            fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding, cancellation.Token).AsTask()
            : policy.ObserveAsync(fixture.Original, fixture.Profile, cancellation.Token).AsTask();
        if (cancel) {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        } else {
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => pending);
        }
        Assert.Equal(1, probe.Observations);
    }

    [Fact]
    public async Task Initial_selected_source_still_rejects_forged_execution_lineage_before_SQL() {
        var probe = new QueryProbe();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(processReadProbe: probe, registeredSource: true);
        probe.Commands.Clear();
        var forged = fixture.Original with { ToolAdmission = null, CorrelationId = Guid.NewGuid().ToString("D") };
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fixture.SourcePolicy.ObserveInitialInheritedAsync(
            forged, fixture.Profile, fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding).AsTask());
        Assert.Empty(probe.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_child_cannot_replace_the_original_parent_freshness_policy(bool historical) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        var original = decision.InheritedOrigin!;
        var policy = original.ParentPolicy;
        var changed = original with { ParentPolicy = new(policy.ReadBasisMode, PublicationFreshness.PinnedObservation, policy.OnBasisChange, policy.Budget) };
        var run = fixture.Original with { ToolAdmission = fixture.Original.ToolAdmission with {
            PlanningDecision = decision with { InheritedOrigin = changed, Policy = changed.ResolvePolicy() }
        } };
        run.ToolAdmission.Validate();
        if (historical) {
            var services = fixture.Sql.Services;
            var reader = new ProcessHistoricalReceiptSourcePolicy(services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>(),
                services.GetRequiredService<ICanonicalRuntimeDatabase>(), fixture.Generations, new ForbiddenHistoricalSource(),
                services.GetServices<ProcessDecisionExecutionRegistration>());
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() =>
                reader.ObserveHistoricalReadAsync(run, new(fixture.Profile, fixture.Scope)).AsTask());
        } else {
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.SourcePolicy.ObserveAsync(run, fixture.Profile).AsTask());
        }
    }

    [Fact]
    public async Task Missing_execution_registration_refuses_initial_and_saved_paths_before_SQL() {
        var probe = new QueryProbe();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(processReadProbe: probe, registeredSource: true);
        var services = fixture.Sql.Services;
        var policy = new ProcessToolBackgroundSourcePolicy(services.GetRequiredService<EfProcessExecutionAuthorityQuery>(),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), fixture.Generations,
            services.GetRequiredService<IProcessSourceAuthorityObservationPolicy>(), []);
        probe.Commands.Clear();
        await Assert.ThrowsAsync<NotSupportedException>(() => policy.ObserveInitialInheritedAsync(fixture.Original with { ToolAdmission = null },
            fixture.Profile, fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!.ParentBinding).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => policy.ObserveAsync(fixture.Original, fixture.Profile).AsTask());
        Assert.Empty(probe.Commands);
    }

    private static ProcessToolBackgroundSourcePolicy Policy(ProjectTaskPlanProcessFixture fixture, IProcessSourceAuthorityObservationPolicy source) {
        var services = fixture.Sql.Services;
        return new(services.GetRequiredService<EfProcessExecutionAuthorityQuery>(), services.GetRequiredService<ICanonicalRuntimeDatabase>(),
            fixture.Generations, source, services.GetServices<ProcessDecisionExecutionRegistration>());
    }

    private sealed class QueryProbe : DbCommandInterceptor {
        internal ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SourceProbe(IProcessSourceAuthorityObservationPolicy inner, Action after) : IProcessSourceAuthorityObservationPolicy {
        internal int Observations { get; private set; }
        public async Task<ProcessSourceAuthorityObservation> ObserveAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default) {
            var result = await inner.ObserveAsync(authority, cancellationToken);
            Observations++;
            after();
            return result;
        }
    }

    private sealed class ForbiddenHistoricalSource : IProcessHistoricalSourceAuthorityObserver {
        public Task<ProcessSourceAuthorityObservation> ObserveHistoricalReadAsync(ProcessLaunchAuthority authority,
            ProcessHistoricalReadContext currentContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Changed original policy must be rejected before source disclosure authorization.");
    }
}
