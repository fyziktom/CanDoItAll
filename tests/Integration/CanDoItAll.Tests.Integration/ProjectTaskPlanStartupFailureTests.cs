using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Integration.Processes;
using CanDoItAll.Tests.Integration.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectTaskPlanStartupFailureTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false, 2)]
    [InlineData(ReadBasisMode.StepSnapshot, false, 2)]
    [InlineData(ReadBasisMode.RunSnapshot, true, 2)]
    [InlineData(ReadBasisMode.StepSnapshot, true, 2)]
    [InlineData(ReadBasisMode.RunSnapshot, false, 31)]
    [InlineData(ReadBasisMode.StepSnapshot, false, 31)]
    public async Task Known_failed_startup_settles_original_child_and_parent_once(ReadBasisMode mode, bool cancelled, int seconds) {
        await using var fixture = await Fixture.CreateAsync(mode);
        var evidence = await fixture.FailAsync(cancelled: cancelled, seconds: seconds);
        var command = fixture.Command(evidence, cancelled ? StrategyOutcome.Canceled : StrategyOutcome.Failed);
        var coordinator = fixture.Services.GetRequiredService<ProcessDecisionResultCoordinator>();
        var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(fixture.Process.State.RunId))!;
        var result = await coordinator.SubmitAsync(state, fixture.ResultContext, command);
        Assert.True(result.Succeeded);
        Assert.Equal(cancelled ? ProcessRuntimeStepStatus.Cancelled : ProcessRuntimeStepStatus.Failed, result.State.Steps.Single().Status);
        Assert.Equal(ProcessDecisionChildOutcome.NoEffect, result.Decision!.Child!.Closure!.Outcome);
        Assert.Equal(evidence.Value.WorkTicks, result.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Equal(0, result.DecisionBudget.Change.Balance.ReservedWorkTicks);
        Assert.Equal(evidence.Value.WorkTicks > fixture.Parent.Child.Grant.WorkTicks, result.DecisionBudget.Change.Balance.OverrunDetected);
        Assert.Equal(0, result.Decision.Current.UnclosedChildCount);
        Assert.Empty(result.State.AppliedResults.Single().ProducedArtifacts);
        Assert.Empty(fixture.Entry.Wire.Requests);
        var replay = await coordinator.SubmitAsync(result.State, fixture.ResultContext,
            command with { Result = command.Result with { OwnerEvidence = null } });
        Assert.Equal(ProcessRuntimeTransitionOutcome.Duplicate, replay.Outcome);
        Assert.Equal(result.DecisionBudget, replay.DecisionBudget);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(Substitution.Agent)]
    [InlineData(Substitution.Parent)]
    [InlineData(Substitution.Grant)]
    [InlineData(Substitution.Child)]
    [InlineData(Substitution.Scope)]
    [InlineData(Substitution.Project)]
    [InlineData(Substitution.Generation)]
    [InlineData(Substitution.Wrapper)]
    public async Task Different_identity_or_untrusted_evidence_cannot_close_the_original_allocation(Substitution change) {
        await using var fixture = await Fixture.CreateAsync();
        var request = change switch {
            Substitution.Agent => fixture.Request with { AgentId = Guid.NewGuid() },
            Substitution.Parent => fixture.Request with { ParentBinding = AgentPlanningDecisionTestData.Envelope() },
            Substitution.Grant => fixture.Request with { Start = fixture.Request.Start with {
                Allocation = fixture.Request.Start.Allocation with { Grant = new(1, 0) }
            } },
            Substitution.Child => fixture.Request with { ExecutionRunId = Guid.NewGuid() },
            Substitution.Scope => fixture.Request with { ContextScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")) },
            _ => fixture.Request
        };
        ProcessExecutionOwnerEvidence evidence = await fixture.FailAsync(request);
        var selected = await fixture.SelectedAsync();
        var profile = fixture.Profile;
        if (change == Substitution.Project) {
            var project = selected.OriginalProject!;
            selected = selected with { OriginalProject = new(project.DatabaseProfileId, Guid.NewGuid(), project.LifetimeId) };
        } else if (change == Substitution.Generation) {
            profile = new(profile.ProfileId, profile.Fingerprint, new(profile.Generation.Value + 1));
        } else if (change == Substitution.Wrapper) {
            evidence = new ProcessExecutionOwnerEvidence<string>("caller-supplied-zero-work");
        }
        Assert.Throws<InvalidDataException>(() => ProjectTaskPlanProcessStartupOutcome.Read(selected, evidence, profile));
        var child = await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(fixture.Parent.Child.Address);
        Assert.Null(child!.Closure);
        Assert.Null(child.Settlement);
        Assert.Empty(fixture.Entry.Wire.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Live_evidence_cannot_override_existing_or_unstable_storage(bool unstable) {
        await using var fixture = await Fixture.CreateAsync();
        var evidence = await fixture.FailAsync();
        var reader = fixture.Reader;
        var selected = await fixture.SelectedAsync();
        if (!unstable) {
            await fixture.Entry.Files.Store.SaveExecutionRunDetailAsync(new(fixture.Entry.Files.Original with {
                Id = fixture.Request.ExecutionRunId, ChatSessionId = null, ToolAdmission = null, MetadataJson = "{}",
                State = ExecutionState.Preparing, Outcome = null, CompletedAtUtc = null, PendingApprovals = []
            }, null, [], []));
            var outcome = Assert.IsType<ProcessDecisionChildResult.Pending>(await reader.ReadAsync(selected, evidence));
            Assert.Equal(ProcessDecisionChildPendingReason.UnresolvedEffects, outcome.Reason);
        } else {
            var root = fixture.Services.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
            var layout = new FileSandboxWorkspaceStorageLayout(root,
                WorkspaceScopeDescriptor.Organization(fixture.Profile.ProfileId.ToString("N")));
            var pending = Path.Combine(layout.ExecutionStorageRoot, "pending-run-detail-update.json");
            Assert.False(File.Exists(pending));
            await File.WriteAllTextAsync(pending, "retained unfinished publication");
            try {
                await Assert.ThrowsAsync<IOException>(() => reader.ReadAsync(selected, evidence));
                Assert.Equal("retained unfinished publication", await File.ReadAllTextAsync(pending));
            } finally {
                File.Delete(pending);
            }
        }
        var child = await fixture.Services.GetRequiredService<IProcessDecisionChildReader>().ReadAsync(fixture.Parent.Child.Address);
        Assert.Null(child!.Settlement);
        Assert.Empty(fixture.Entry.Wire.Requests);
    }

    [Fact]
    public async Task Lost_settlement_acknowledgement_replays_after_full_host_restart_without_live_evidence() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-closed-startup");
        var profile = environment.CreatePostgreSqlProfile("closed-startup");
        SubmitStrategyResultCommand command;
        RuntimeCommandContext context;
        ProcessRunId runId;
        long ticks;
        var lost = new IOException("closed-startup-commit-ack");
        await using (var fixture = await Fixture.CreateAsync(environment: environment, profile: profile)) {
            var evidence = await fixture.FailAsync();
            ticks = evidence.Value.WorkTicks;
            var live = fixture.Command(evidence, StrategyOutcome.Failed);
            var options = ProcessInstancePlanPersistenceMapper.CreateSerializerOptions();
            var serialized = JsonSerializer.Serialize(live, options);
            command = JsonSerializer.Deserialize<SubmitStrategyResultCommand>(serialized, options)!;
            Assert.Null(command.Result.OwnerEvidence);
            Assert.Equal(serialized, JsonSerializer.Serialize(command, options));
            Assert.True(ProcessStrategyResultSnapshot.TryCapture(command.Result, out _, out _));
            runId = fixture.Process.State.RunId;
            context = fixture.ResultContext;
            await using var database = Context(fixture.Services, new AfterCommitFault(lost));
            var owner = fixture.Process.Owner(database);
            var coordinator = new ProcessDecisionResultCoordinator(fixture.Services.GetRequiredService<IProcessDecisionResultQuery>(),
                fixture.Services.GetServices<IProcessDecisionChildOutcomeReader>(), owner, owner,
                NullLogger<ProcessDecisionResultCoordinator>.Instance);
            var state = (await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId))!;
            Assert.Same(lost, await Assert.ThrowsAsync<IOException>(() => coordinator.SubmitAsync(state, context, live)));
            Assert.Empty(fixture.Entry.Wire.Requests);
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions {
            TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
                foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                    services.Remove(descriptor);
                }
                services.RemoveAll<IProcessDecisionChildOutcomeReader>();
                services.AddSingleton<IProcessDecisionChildOutcomeReader, ReadForbidden>();
            }
        });
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedServices = restartedScope.ServiceProvider;
        var retained = (await restartedServices.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(runId))!;
        var replay = await restartedServices.GetRequiredService<ProcessDecisionResultCoordinator>().SubmitAsync(retained, context, command);
        Assert.True(replay.Outcome == ProcessRuntimeTransitionOutcome.Duplicate,
            string.Join("; ", replay.Diagnostics.Select(item => $"{item.Code}: {item.Message}")));
        Assert.Equal(ProcessRuntimeStepStatus.Failed, replay.State.Steps.Single().Status);
        Assert.Equal(ticks, replay.DecisionBudget!.Change.Allocation.Settlement!.Consumed.WorkTicks);
        Assert.Single(replay.State.AppliedResults);
    }

    public enum Substitution { Agent, Parent, Grant, Child, Scope, Project, Generation, Wrapper }

    private sealed class ReadForbidden : IProcessDecisionChildOutcomeReader {
        public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;
        public Task<ProcessDecisionChildResult> ReadAsync(ProcessDecisionChildMaterialBinding original, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Exact committed replay cannot read transient owner evidence.");
    }

    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => throw failure;
    }

    private sealed record Fixture(ProjectTaskPlanExecutionEntryTests.EntryFixture Entry, ProcessStepMaterialFixture Process,
        ProcessDecisionChildMaterialBinding Parent, AgentInheritedPlanningWorkspaceRequest Request,
        AgentToolProfileBinding Profile) : IAsyncDisposable {
        internal IServiceProvider Services => Entry.Files.Sql.Services;
        internal IProcessDecisionLiveOutcomeReader Reader => Assert.IsAssignableFrom<IProcessDecisionLiveOutcomeReader>(
            Assert.Single(Services.GetServices<IProcessDecisionChildOutcomeReader>(), item => item.Contract == Parent.ExecutionContract));
        internal RuntimeCommandContext ResultContext => new(RuntimeCommandId.New(),
            new(ProcessEventActorKind.System, new("startup-failure-fixture")), new("startup-failure-fixture"), DateTimeOffset.UtcNow);

        internal static async Task<Fixture> CreateAsync(ReadBasisMode mode = ReadBasisMode.StepSnapshot,
            CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null) {
            var entry = await ProjectTaskPlanExecutionEntryTests.EntryFixture.CreateAsync(testEnvironment: environment, activeProfile: profile);
            ProcessStepMaterialFixture? process = null;
            try {
                process = await ProcessStepMaterialFixture.CreateAsync(mode, existingSql: entry.Files.Sql,
                    executorId: entry.Files.Agent.Id, allowTaskPublication: true);
                var services = entry.Files.Sql.Services;
                var prepared = await services.GetRequiredService<ProcessDecisionStepPreparationCoordinator>()
                    .PrepareAsync(process.State, process.Claim, TimeSpan.FromSeconds(30));
                Assert.True(prepared.EnrolledNow);
                var parent = Assert.IsType<ProcessDecisionChildMaterialBinding>(prepared.Child);
                var database = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
                AgentToolProfileBinding binding = new(database.Id, database.Runtime.Fingerprint,
                    services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration());
                var envelope = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
                    ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));
                AgentInheritedPlanningWorkspaceRequest request = new(parent.Child.Address.ChildId.Value,
                    new(new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant), AgentInheritedPlanningStartMode.FreshEnrollment),
                    entry.Files.Agent.Id, "Prepare the original task plan.", AgentExecutionOperationId.New(), ExecutionInvocationContext.Empty,
                    AgentPlanningOwner.ProjectTaskPlan, WorkspaceScopeDescriptor.Project(entry.Files.Sql.Project.ProjectId.ToString("D")),
                    envelope, (_, _) => throw new InvalidOperationException("Preparation fails before binding."),
                    _ => throw new InvalidOperationException("Preparation cannot fabricate source evidence."));
                return new(entry, process, parent, request, binding);
            } catch {
                if (process is not null) {
                    await process.DisposeAsync();
                }
                await entry.DisposeAsync();
                throw;
            }
        }

        internal async Task<ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>> FailAsync(
            AgentInheritedPlanningWorkspaceRequest? request = null, bool cancelled = false, int seconds = 2) {
            request ??= Request;
            var clock = new AgentPlanningLiveOwnerFixture.PlanningClock(DateTimeOffset.UtcNow);
            var journal = new AgentToolAdmissionJournal(Entry.Files.Store, Profile, clock);
            var admission = new AgentInheritedPlanningRunAdmission(Entry.Files.Store, Entry.Files.Store, journal);
            var failure = await Assert.ThrowsAsync<AgentInheritedPlanningPreparationException>(() =>
                admission.AdmitWorkspaceAsync(request, request.RequireOriginalParent, _ => {
                    clock.Advance(TimeSpan.FromSeconds(seconds));
                    throw cancelled ? new OperationCanceledException("closed-startup-cancellation") : new IOException("closed-startup-failure");
                }, default));
            return new(failure.Failure);
        }

        internal async Task<ProcessDecisionResultChild> SelectedAsync()
            => (await Services.GetRequiredService<IProcessDecisionResultQuery>().ReadAsync(Parent.Child.Address.RunId,
                Parent.Child.Address.StepInstanceId, new(Parent.Child.Address.ChildId.Value)))!.Child!;

        internal SubmitStrategyResultCommand Command(ProcessExecutionOwnerEvidence evidence, StrategyOutcome outcome) {
            var key = Guid.NewGuid();
            return new(Process.Claim.StepInstanceId, Process.Claim.OwnerId, Process.Claim.ClaimToken, new(key),
                new(new("startup-failure-fixture"), "1.0.0", key, outcome, [], [], [], [], "sha256:" + new string('a', 64)) {
                    ExecutionRunId = new(Parent.Child.Address.ChildId.Value), OwnerEvidence = evidence
                });
        }

        public async ValueTask DisposeAsync() {
            await Process.DisposeAsync();
            await Entry.DisposeAsync();
        }
    }
}
