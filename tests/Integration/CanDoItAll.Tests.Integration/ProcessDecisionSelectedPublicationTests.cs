using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionSelectedPublicationTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Composed_query_and_fence_publish_only_the_exact_trusted_selected_source(ReadBasisMode mode) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await TrustedAsync(context, services, mode);
        Assert.Same(services.GetRequiredService<EfProcessExecutionAuthorityQuery>(), services.GetRequiredService<IProcessDecisionDispatchAuthorityQuery>());
        var source = await ReadAsync(services, fixture);
        Assert.True(source.ObservedCurrentDispatch);
        Assert.NotNull(source.SourceAuthority);
        Assert.False((await services.GetRequiredService<EfProcessExecutionAuthorityQuery>().ReadDispatchAsync(fixture.Evidence)).Snapshot!.ObservedCurrentDispatch);
        await RequireAsync(services, fixture, source);
        await RequireAsync(services, fixture, source with { ObservedAtUtc = source.ObservedAtUtc.AddYears(-1), ObservedCurrentDispatch = false });
    }

    [Theory]
    [InlineData(SourceChange.Operations)]
    [InlineData(SourceChange.Target)]
    [InlineData(SourceChange.Capabilities)]
    [InlineData(SourceChange.SourcePermission)]
    [InlineData(SourceChange.SourceProfile)]
    [InlineData(SourceChange.SourcePrincipal)]
    [InlineData(SourceChange.SourceMissing)]
    [InlineData(SourceChange.Readiness)]
    [InlineData(SourceChange.Executor)]
    [InlineData(SourceChange.Claim)]
    [InlineData(SourceChange.Project)]
    public async Task An_unchanged_fingerprint_cannot_cover_substituted_source_fields(SourceChange change) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await TrustedAsync(context, services);
        var source = await ReadAsync(services, fixture);
        var changed = change switch {
            SourceChange.Operations => source with { AllowedOperations = ["substituted-operation"] },
            SourceChange.Target => source with { OperationTargetScope = source.OperationTargetScope + "-substituted" },
            SourceChange.Capabilities => source with { CapabilityScope = new() { InstructionFragments = [new() { Content = "Substituted instruction" }] } },
            SourceChange.SourcePermission => source with { SourceAuthority = source.SourceAuthority! with { CanCreateTasks = !source.SourceAuthority!.CanCreateTasks } },
            SourceChange.SourceProfile => source with { SourceAuthority = source.SourceAuthority! with { DatabaseProfileId = Guid.NewGuid() } },
            SourceChange.SourcePrincipal => source with { SourceAuthority = source.SourceAuthority! with { Principal = new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.Api) } },
            SourceChange.SourceMissing => source with { SourceAuthority = null },
            SourceChange.Readiness => source with { ReadinessHash = "substituted-readiness" },
            SourceChange.Executor => source with { Evidence = source.Evidence with { ExecutorAgentId = Guid.NewGuid() } },
            SourceChange.Claim => source with { Evidence = source.Evidence with { DispatchClaimToken = Guid.NewGuid() } },
            SourceChange.Project => source with { ProjectId = Guid.NewGuid() },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        Assert.Equal(source.OwnerFingerprint, changed.OwnerFingerprint);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, fixture, changed));
        await RequireAsync(services, fixture, source);
    }

    [Theory]
    [InlineData(BindingChange.Stamp)]
    [InlineData(BindingChange.Scope)]
    [InlineData(BindingChange.ExecutionContract)]
    [InlineData(BindingChange.ContentContract)]
    public async Task Publication_revalidates_the_original_material_and_registered_contracts(BindingChange change) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await TrustedAsync(context, services);
        var source = await ReadAsync(services, fixture);
        var binding = fixture.Original.Binding;
        var execution = binding.ExecutionContract;
        var content = fixture.Original.Material.ContentContract;
        switch (change) {
            case BindingChange.Stamp:
                binding = binding with { Material = binding.Material with { Fingerprint = new string('A', 64) } };
                break;
            case BindingChange.Scope:
                binding = binding with { Scope = binding.Scope with { WorkspaceNamespaceId = Guid.NewGuid() } };
                break;
            case BindingChange.ExecutionContract:
                execution = execution with { Version = execution.Version + 1 };
                break;
            case BindingChange.ContentContract:
                content = content! with { Version = content.Version + 1 };
                break;
        }
        await using var transaction = await context.Database.BeginTransactionAsync();
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => services.GetRequiredService<IProcessDecisionPublicationFence>()
            .RequireSelectedAsync(binding, source, execution, content, ProcessDecisionPublicationKind.Dispatch));
    }

    [Fact]
    public async Task A_legacy_preparation_without_saved_source_cannot_publish() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await ProcessDecisionDispatchAuthorityFixture.CreateAsync(context, services, prepared: true);
        var source = await ReadAsync(services, fixture);
        Assert.True(source.ObservedCurrentDispatch);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, fixture, source));
    }

    [Fact]
    public async Task Manager_review_keeps_original_source_without_reopening_dispatch_or_settling_the_child() {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await TrustedAsync(context, services);
        var source = await ReadAsync(services, fixture);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, fixture, source, ProcessDecisionPublicationKind.ManagerReview));
        await QuiesceAsync(context, services, fixture.State, ProcessDecisionPhaseChange.WaitForReview);
        await RequireAsync(services, fixture, source, ProcessDecisionPublicationKind.ManagerReview);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, fixture, source));
        var child = (await services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(fixture.Original.Binding.Child.Address))!;
        Assert.Null(child.Closure);
        Assert.Null(child.Settlement);
        Assert.False((await ReadAsync(services, fixture)).ObservedCurrentDispatch);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task The_effect_transaction_excludes_assignment_repair_until_commit_or_rollback(bool commit) {
        await using var app = await TestApplication.CreateAsync(new ProcessDecisionPublicationTestClock().Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var setup = Context(services);
        var fixture = await TrustedAsync(setup, services);
        var other = await TrustedAsync(setup, services);
        var source = await ReadAsync(services, fixture);
        await using var effect = Context(services);
        await using var transaction = await effect.Database.BeginTransactionAsync();
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(effect);
        await FenceAsync(services, fixture, source);
        var probe = new SourceCommandProbe(false);
        await using var repair = Context(services, probe);
        var store = new EfProcessRuntimeStepAssignmentStore(repair);
        var assignment = (await store.LoadAsync(fixture.State.RunId, Step(fixture.State)))!;
        var pending = store.SaveAsync([assignment with { ReadinessHash = "repaired-readiness" }]).AsTask();
        try {
            await WaitForLockAsync(setup, probe);
            Assert.False(pending.IsCompleted);
            await using var independent = app.Services.CreateAsyncScope();
            var otherSource = await ReadAsync(independent.ServiceProvider, other);
            Task independentPublication;
            using (ExecutionContext.SuppressFlow()) {
                independentPublication = Task.Run(() => RequireAsync(independent.ServiceProvider, other, otherSource));
            }
            await independentPublication.WaitAsync(TimeSpan.FromSeconds(15));
            if (commit) {
                await transaction.CommitAsync();
            } else {
                await transaction.RollbackAsync();
            }
        } finally {
            await transaction.DisposeAsync();
            await pending.WaitAsync(TimeSpan.FromSeconds(15));
        }
        participation.Dispose();
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => RequireAsync(services, fixture, source));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Expiry_or_repair_during_a_source_row_wait_revokes_publication(bool expire) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var setup = Context(services);
        var fixture = await TrustedAsync(setup, services);
        var source = await ReadAsync(services, fixture);
        await using var blocker = Context(services);
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        var assignment = (await new EfProcessRuntimeStepAssignmentStore(blocker).LoadAsync(fixture.State.RunId, Step(fixture.State)))!;
        await new EfProcessRuntimeStepAssignmentStore(blocker).SaveAsync([assignment with { ReadinessHash = "pending-repair" }]);
        var probe = new SourceCommandProbe(true);
        var pending = RequireAsync(services, fixture, source, probe: probe);
        try {
            await WaitForLockAsync(setup, probe);
            Assert.False(pending.IsCompleted);
            if (expire) {
                clock.SetUtcNow(fixture.State.Claims.Single(item => item.ClaimToken.Value == source.Evidence.DispatchClaimToken).ExpiresAtUtc);
                await transaction.RollbackAsync();
            } else {
                await transaction.CommitAsync();
            }
        } finally {
            await transaction.DisposeAsync();
        }
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => pending.WaitAsync(TimeSpan.FromSeconds(15)));
    }

    private static async Task<ProcessDecisionDispatchAuthorityFixture> TrustedAsync(ProcessPersistenceDbContext context,
        IServiceProvider services, ReadBasisMode mode = ReadBasisMode.RunSnapshot) {
        var authority = await services.GetRequiredService<IProcessLaunchOperatorAuthoritySource>().CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
        return await ProcessDecisionDispatchAuthorityFixture.CreateAsync(context, services, mode, authority: authority);
    }

    private static async Task<ProcessExecutionDispatchAuthority> ReadAsync(IServiceProvider services, ProcessDecisionDispatchAuthorityFixture fixture)
        => Assert.IsType<ProcessExecutionDispatchAuthority>((await services.GetRequiredService<IProcessDecisionDispatchAuthorityQuery>()
            .ReadDecisionDispatchAsync(fixture.Evidence, fixture.Original.Binding, fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract)).Snapshot);

    private static Task FenceAsync(IServiceProvider services, ProcessDecisionDispatchAuthorityFixture fixture, ProcessExecutionDispatchAuthority source,
        ProcessDecisionPublicationKind kind = ProcessDecisionPublicationKind.Dispatch, SourceCommandProbe? probe = null) {
        var coordinator = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var fence = probe is null ? services.GetRequiredService<IProcessDecisionPublicationFence>()
            : new EfProcessDecisionPublicationFence(Options(services, probe), coordinator, services.GetRequiredService<TimeProvider>());
        return fence.RequireSelectedAsync(fixture.Original.Binding, source, fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract, kind);
    }

    private static async Task RequireAsync(IServiceProvider services, ProcessDecisionDispatchAuthorityFixture fixture, ProcessExecutionDispatchAuthority source,
        ProcessDecisionPublicationKind kind = ProcessDecisionPublicationKind.Dispatch, SourceCommandProbe? probe = null) {
        await using var context = Context(services);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using var participation = services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await FenceAsync(services, fixture, source, kind, probe);
        await transaction.CommitAsync();
    }

    private static async Task WaitForLockAsync(ProcessPersistenceDbContext observer, SourceCommandProbe probe) {
        var backend = await probe.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!await observer.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {backend} AND wait_event_type = 'Lock') AS "Value"
            """).SingleAsync(deadline.Token)) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
        }
    }

    private sealed class SourceCommandProbe(bool share) : DbCommandInterceptor {
        public TaskCompletionSource<int> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("process_runtime_step_assignments", StringComparison.Ordinal) &&
                command.CommandText.Contains(share ? "FOR SHARE" : "UPDATE", StringComparison.Ordinal)) {
                Entered.TrySetResult(Assert.IsType<NpgsqlConnection>(command.Connection).ProcessID);
            }
            return ValueTask.FromResult(result);
        }
    }

    public enum SourceChange { Operations, Target, Capabilities, SourcePermission, SourceProfile, SourcePrincipal, SourceMissing, Readiness, Executor, Claim, Project }
    public enum BindingChange { Stamp, Scope, ExecutionContract, ContentContract }
}
