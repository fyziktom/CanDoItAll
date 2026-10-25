using System.Diagnostics;
using System.Collections.Concurrent;
using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Integration.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanProcessHistoryTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Registered_history_reads_original_inherited_receipt_after_expiry_without_locks_or_writes(bool agentSource, bool sameExecutor) {
        var sourceReads = new SourceReadProbe();
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(agentSource, sameExecutor, sourceReads);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.PublishAsync(await fixture.PrepareAsync()));
        fixture.ExpireClaim();
        var run = await fixture.ReadAsync();
        sourceReads.Commands.Clear();
        fixture.Sql.Probe.Reset();
        var root = fixture.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        using var files = await HistoricalFileObservation.StartAsync(root);
        var catalogReads = 0;
        using var trace = new Activity("inherited-receipt-history").SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded).Start();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId == trace.TraceId && activity.OperationName == "workspace.historical.read" &&
                    activity.GetTagItem("payload.type") is "HistoricalCatalog") {
                    Interlocked.Increment(ref catalogReads);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        await using (var coordination = new FileStream(Layout(fixture).WorkspaceLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            var review = await ReadAsync(fixture, run);
            AssertAccepted(review);
        }
        await files.AssertNoChangesAsync();
        Assert.Equal(agentSource && !sameExecutor ? 3 : 2, catalogReads);
        Assert.Equal(12, sourceReads.Commands.Count);
        Assert.All(sourceReads.Commands, sql => {
            Assert.StartsWith("SELECT", sql.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", sql, StringComparison.Ordinal);
        });
        Assert.Single(fixture.Sql.Probe.Commands, IsReceipt);
        Assert.Equal(run.ToolAdmission!.PlanningDecision!.Work, (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work);
        await fixture.Sql.VerifyAsync(1, 1, accepted.Acknowledgement);
    }

    [Theory]
    [InlineData(SourceChange.Read)]
    [InlineData(SourceChange.Tools)]
    [InlineData(SourceChange.Missing)]
    public async Task Original_launcher_read_revocation_denies_the_whole_page(SourceChange change) {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        await fixture.RevokeAsync(fixture.Launcher!.Id, agent => change switch {
            SourceChange.Read => agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson,
                new AgentProjectStructureAccessSettings()) },
            SourceChange.Tools => agent with { Permissions = agent.Permissions with { CanUseTools = false } },
            SourceChange.Missing => null,
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        });
        AssertDenied(await ReadAsync(fixture, run));
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_executor_must_remain_active_and_non_template_for_history(bool template) {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        await fixture.RevokeAsync(fixture.Executor.Id, agent => template
            ? agent with { IsTemplate = true, TemplateKey = "historical-template" }
            : agent with { Status = AgentLifecycleStatus.Archived });
        AssertDenied(await ReadAsync(fixture, run));
    }

    [Theory]
    [InlineData(ParentChange.Executor)]
    [InlineData(ParentChange.Readiness)]
    [InlineData(ParentChange.Policy)]
    [InlineData(ParentChange.Closed)]
    public async Task Registered_receipt_history_keeps_original_source_after_assignment_repair_or_child_close(ParentChange change) {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        await using var context = Context(fixture.Sql.Services);
        if (change == ParentChange.Closed) {
            var child = fixture.Process.Parent.Child;
            var work = run.ToolAdmission!.PlanningDecision!.Work;
            Assert.Null(work.Open);
            await Owner(context, fixture.Sql.Services).CommitAsync(await ClosureRequestAsync(context, fixture.Process.State,
                new(child.Address.Reference, child.Address.ChildId, child.Owner, child.AllocationId),
                new(work.Revision, ProcessDecisionChildOutcome.Accepted, new(fixture.PublishedCommand!.Command.Operation.Id)), work.ClosedWorkTicks));
        } else {
            var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == fixture.Process.State.RunId.Value);
            if (change == ParentChange.Executor) {
                assignment.ExecutorId = Guid.NewGuid().ToString("D");
            } else if (change == ParentChange.Readiness) {
                assignment.ReadinessHash = "sha256:repaired-task-executor";
            } else {
                var policy = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(assignment.DecisionDataPolicyJson!);
                assignment.DecisionDataPolicyJson = ProcessDecisionDataPolicyAdmissionCodec.Serialize(policy with { WorkspaceHeadRevision = Guid.NewGuid() });
            }
            await context.SaveChangesAsync();
        }
        fixture.ExpireClaim();
        AssertAccepted(await ReadAsync(fixture, run));
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Lost_acknowledgement_history_finds_original_receipt_without_reopening_work() {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var waiting = await fixture.PrepareAsync();
        fixture.Sql.Faults.Commit.AfterCommit = () => {
            fixture.Sql.Probe.BeforeCommand = (command, _) => IsReceipt(command.CommandText)
                ? Task.FromException(new IOException("historical-inherited-receipt-unavailable")) : Task.CompletedTask;
            throw new IOException("historical-inherited-commit-acknowledgement-lost");
        };
        Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(await fixture.PublishAsync(waiting));
        fixture.Sql.Probe.BeforeCommand = null;
        var run = await fixture.ReadAsync();
        fixture.ExpireClaim();
        AssertAccepted(await ReadAsync(fixture, run));
        Assert.Equal(run.ToolAdmission!.PlanningDecision!.Work, (await fixture.ReadAsync()).ToolAdmission!.PlanningDecision!.Work);
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Receipt_outage_is_unavailable_and_does_not_turn_into_absence_or_new_work() {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        var reads = 0;
        fixture.Sql.Probe.BeforeCommand = (command, _) => {
            if (IsReceipt(command.CommandText)) {
                reads++;
                throw new IOException("historical-inherited-owner-outage");
            }
            return Task.CompletedTask;
        };
        var review = await ReadAsync(fixture, run);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, review.Stop);
        Assert.Equal(AgentHistoricalReceiptKnowledge.ReadUnavailable, Assert.Single(review.Observations).Knowledge);
        Assert.Equal(1, reads);
        fixture.Sql.Probe.BeforeCommand = null;
        await fixture.Sql.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Catalog_change_during_original_receipt_lookup_discards_the_whole_page() {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        var changed = false;
        fixture.Sql.Probe.BeforeCommand = async (command, _) => {
            if (!changed && IsReceipt(command.CommandText)) {
                changed = true;
                await fixture.RevokeAsync(fixture.Launcher!.Id, agent => agent with {
                    ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson,
                        new AgentProjectStructureAccessSettings())
                });
            }
        };
        var review = await ReadAsync(fixture, run);
        Assert.True(changed);
        Assert.Equal(AgentHistoricalReceiptReviewStop.SnapshotChanged, review.Stop);
        Assert.Empty(review.Observations);
        Assert.Null(review.RunRevision);
    }

    [Fact]
    public async Task Profile_generation_change_during_receipt_lookup_discards_the_whole_page() {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        var changed = false;
        fixture.Sql.Probe.BeforeCommand = (command, _) => {
            if (!changed && IsReceipt(command.CommandText)) {
                changed = true;
                fixture.Generations.Generation = new(fixture.Generations.Generation.Value + 1);
            }
            return Task.CompletedTask;
        };
        var review = await ReadAsync(fixture, run);
        Assert.True(changed);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ContextChanged, review.Stop);
        Assert.Empty(review.Observations);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_or_recreated_project_cannot_disclose_the_original_inherited_receipt(bool recreate) {
        await using var fixture = await PublishedAsync();
        var run = await fixture.ReadAsync();
        await new ProjectLifecycleTestDriver(fixture.Sql.Services).DeleteAsync(fixture.Sql.Project.ProjectId);
        if (recreate) {
            await using var context = await fixture.Sql.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            context.Add(new Project { Id = fixture.Sql.Project.ProjectId, Name = "Different inherited lifetime", Slug = Guid.NewGuid().ToString("N") });
            await context.SaveChangesAsync();
        }
        AssertDenied(await ReadAsync(fixture, run));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Historical_source_policy_refuses_unregistered_contract_or_unknown_parent_format(bool changeFormat) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        var run = await fixture.PrepareAsync();
        if (changeFormat) {
            var decision = run.ToolAdmission!.PlanningDecision!;
            var origin = decision.InheritedOrigin!;
            run = run with { ToolAdmission = run.ToolAdmission with { PlanningDecision = decision with { InheritedOrigin = origin with {
                ParentBinding = AgentToolProtocolEnvelope.Create("unsupported-process-parent", 1, origin.ParentBinding.PayloadJson)
            } } } };
        }
        var services = fixture.Sql.Services;
        var policy = new ProcessHistoricalReceiptSourcePolicy(services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>(),
            services.GetRequiredService<ICanonicalRuntimeDatabase>(), fixture.Generations, new ForbiddenSourceRead(),
            changeFormat ? services.GetServices<ProcessDecisionExecutionRegistration>() : []);
        await Assert.ThrowsAsync<NotSupportedException>(() => policy.ObserveHistoricalReadAsync(run,
            new(fixture.Profile, fixture.Scope)).AsTask());
    }

    private static async Task<ProjectTaskPlanProcessFixture> PublishedAsync() {
        var fixture = await ProjectTaskPlanProcessFixture.CreateAsync();
        try {
            Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.PublishAsync(await fixture.PrepareAsync()));
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private static async Task<AgentHistoricalReceiptReview> ReadAsync(ProjectTaskPlanProcessFixture fixture, ExecutionRunRecord run) {
        var path = Layout(fixture).RunPath(run.Id);
        var before = await File.ReadAllBytesAsync(path);
        var review = await fixture.Sql.Services.GetRequiredService<IAgentHistoricalReceiptReviewService>()
            .ReadSavedEffectReceiptsAsync(new(run.Id, null, run.AgentId, run.CreatedAtUtc));
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
        return review;
    }

    private static FileSandboxWorkspaceStorageLayout Layout(ProjectTaskPlanProcessFixture fixture)
        => new(fixture.Sql.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot(), fixture.Scope);

    private static bool IsReceipt(string sql) => sql.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal);

    private static void AssertAccepted(AgentHistoricalReceiptReview review) {
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, review.Stop);
        Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, Assert.Single(review.Observations).Knowledge);
    }

    private static void AssertDenied(AgentHistoricalReceiptReview review) {
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, review.Stop);
        Assert.Empty(review.Observations);
        Assert.Null(review.RunRevision);
    }

    public enum SourceChange { Read, Tools, Missing }
    public enum ParentChange { Executor, Readiness, Policy, Closed }

    private sealed class ForbiddenSourceRead : IProcessHistoricalSourceAuthorityObserver {
        public Task<ProcessSourceAuthorityObservation> ObserveHistoricalReadAsync(ProcessLaunchAuthority authority,
            ProcessHistoricalReadContext currentContext, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unsupported inherited contracts must not reach source disclosure authorization.");
    }

    private sealed class SourceReadProbe : DbCommandInterceptor {
        internal ConcurrentQueue<string> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Enqueue(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }
}
