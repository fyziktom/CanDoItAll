using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Web.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.ProjectDeletionLegacyTestState;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDeletionLegacyInstallationTests {
    [Fact]
    public async Task Competing_installation_cannot_rebind_while_the_first_actor_holds_selection() {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        var other = new ProjectDeletionPreparationContext(new(fixture.Original.Completion.DatabaseProfileId,
            fixture.Original.Completion.Namespace, new(Guid.NewGuid())), fixture.Original.Project);
        var otherPlan = Assert.IsType<ProjectDeletionRecoveryResult.LegacyObserved>(await fixture.Installer.InspectAsync(other)).Plan;
        string before = await NativeStateAsync(fixture.Data);
        var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeSource = async () => {
            acquired.SetResult();
            await proceed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        };
        var first = fixture.InstallAsync(plan.Digest);
        Task<ProjectDeletionRecoveryResult>? second = null;
        ProjectDeletionRecoveryResult[] outcomes = [];
        try {
            await acquired.Task.WaitAsync(TimeSpan.FromSeconds(15));
            second = fixture.Installer.InstallAsync(other, otherPlan.Digest,
                ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped);
        } finally {
            proceed.TrySetResult();
            outcomes = second is null ? [await first] : await Task.WhenAll(first, second);
        }
        Assert.Equal(2, outcomes.Length);
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(outcomes[0]);
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(outcomes[1]);
        await RequireGateAsync(fixture, released: false);
        Assert.Equal(before, await NativeStateAsync(fixture.Data));
        int acquisitions = fixture.SourceAcquisitions;
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(acquisitions, fixture.SourceAcquisitions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installation_preserves_native_work_then_exact_recovery_releases_the_gate(bool completedPartner) {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        if (completedPartner) {
            await using var edit = fixture.Data.Projects();
            var agent = await edit.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            agent.Status = AgentProjectStructureAccessRevocationStatus.Completed;
            agent.CompletedAtUtc = agent.UpdatedAtUtc;
            await edit.SaveChangesAsync();
        }
        string before = await NativeStateAsync(fixture.Data);
        var plan = await fixture.InspectAsync();
        Assert.False(plan.Installed);
        Assert.Equal(2, plan.Participants.Count);
        Assert.Equal(completedPartner ? 1 : 0, plan.Participants.Count(item => item.Completed));
        Assert.Equal(0, fixture.SourceAcquisitions);
        var installed = Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.False(installed.CleanupUnconfirmed);
        Assert.False(fixture.SourceHeld);
        Assert.Equal(plan.Digest, installed.InstallationPlan);
        await RequireGateAsync(fixture, released: false);
        Assert.Equal(before, await NativeStateAsync(fixture.Data));
        Assert.Equal(fixture.Original.Completion.Namespace, (await fixture.Data.Coordinator.EnsureReadyAsync(fixture.Data.Profile)).Namespace);
        var repeated = Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(installed, repeated);
        Assert.Equal(1, fixture.SourceAcquisitions);
        var reinspection = await fixture.InspectAsync();
        Assert.True(reinspection.Installed);
        Assert.Equal(plan.Digest, reinspection.Digest);
        var releasePlan = Assert.IsType<ProjectDeletionRecoveryResult.Observed>(await fixture.Recovery.InspectAsync(fixture.Original.Completion)).Plan;
        var released = Assert.IsType<ProjectDeletionRecoveryResult.Released>(await fixture.Recovery.ReconcileAsync(
            fixture.Original.Completion, releasePlan.Digest, ProjectDeletionRecoveryPrecondition.AllWriterHostsStopped));
        Assert.False(released.CleanupUnconfirmed);
        await RequireGateAsync(fixture, released: true);
        Assert.Equal(before, await NativeStateAsync(fixture.Data));
        Assert.IsType<ProjectDeletionRecoveryResult.Released>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(2, fixture.SourceAcquisitions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installation_is_not_replayed_after_lost_commit_or_cleanup_acknowledgement(bool loseCommit) {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        string before = await NativeStateAsync(fixture.Data);
        fixture.LoseCommitAcknowledgement = loseCommit;
        fixture.FailSourceRelease = !loseCommit;
        var installed = Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.True(installed.CleanupUnconfirmed);
        Assert.Equal(loseCommit, fixture.CommitAcknowledgementLost);
        Assert.False(fixture.SourceHeld);
        await RequireGateAsync(fixture, released: false);
        Assert.Equal(before, await NativeStateAsync(fixture.Data));
        fixture.LoseCommitAcknowledgement = false;
        fixture.FailSourceRelease = false;
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(1, fixture.SourceAcquisitions);
        Assert.Equal(installed with { CleanupUnconfirmed = true }, ProjectDeletionRecoveryCommand.PreserveAfterCleanupFailure(installed));
    }

    [Fact]
    public async Task Lost_commit_and_unavailable_independent_observation_stay_unconfirmed_until_exact_retry() {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        fixture.LoseCommitAcknowledgement = true;
        fixture.FailObservationAfterCommit = true;
        Assert.IsType<ProjectDeletionRecoveryResult.Unconfirmed>(await fixture.InstallAsync(plan.Digest));
        await RequireGateAsync(fixture, released: false);
        fixture.FailObservationAfterCommit = false;
        fixture.LoseCommitAcknowledgement = false;
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(1, fixture.SourceAcquisitions);
    }

    [Fact]
    public async Task Second_native_binding_failure_rolls_back_the_gate_and_first_binding() {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        string before = await HeadersAndNativeAsync(fixture);
        fixture.FailSecondBinding = true;
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.InstallAsync(plan.Digest));
        Assert.False(fixture.SourceHeld);
        Assert.Equal(before, await HeadersAndNativeAsync(fixture));
        await using var context = fixture.Data.Projects();
        Assert.Empty(await context.Set<ProjectDeletionCompletionRecord>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_native_state_is_refused_before_commit_even_after_preflight(bool afterPreflight) {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        async Task ChangeAsync() {
            await using var context = fixture.Data.Projects();
            (await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync()).ErrorMessage = "processing:replacement";
            await context.SaveChangesAsync();
        }
        if (afterPreflight) {
            fixture.BeforeSource = ChangeAsync;
        } else {
            await ChangeAsync();
        }
        var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.InstallAsync(plan.Digest));
        Assert.Equal(ProjectDeletionRecoveryRefusal.PlanChanged, refused.Reason);
        Assert.Equal(afterPreflight ? 1 : 0, fixture.SourceAcquisitions);
        await using var read = fixture.Data.Projects();
        Assert.Empty(await read.Set<ProjectDeletionCompletionRecord>().ToArrayAsync());
        Assert.Null((await read.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync()).CompletionId);
        Assert.Null((await read.Set<ProjectCrossModuleMutationRecord>().SingleAsync()).CompletionId);
    }

    [Theory]
    [InlineData(InvalidSubject.MissingRetirement)]
    [InlineData(InvalidSubject.ImportedRetirement)]
    [InlineData(InvalidSubject.RecreatedProject)]
    [InlineData(InvalidSubject.ResidualWorkbench)]
    [InlineData(InvalidSubject.AmbiguousWorkbench)]
    [InlineData(InvalidSubject.MixedBinding)]
    [InlineData(InvalidSubject.AllCompleted)]
    public async Task Unsupported_subjects_refuse_without_writing_a_gate_or_adopting_native_work(InvalidSubject fault) {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        await using (var context = fixture.Data.Projects()) {
            var retirement = await context.Set<ProjectRetirementRecord>().SingleAsync();
            var agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            var work = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync();
            switch (fault) {
                case InvalidSubject.MissingRetirement:
                    context.Remove(retirement);
                    break;
                case InvalidSubject.ImportedRetirement:
                    retirement.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
                    break;
                case InvalidSubject.RecreatedProject:
                    context.Add(new Project { Id = fixture.Original.Project.ProjectId, Name = "Recreated", Slug = "recreated",
                        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                    break;
                case InvalidSubject.ResidualWorkbench:
                    context.Add(new ProjectObjectRecord { ProjectId = fixture.Original.Project.ProjectId, NodeKey = "residual",
                        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                    break;
                case InvalidSubject.AmbiguousWorkbench:
                    context.Add(new ProjectCrossModuleMutationRecord { ProjectId = work.ProjectId, MutationKind = work.MutationKind,
                        ScopeNodeKey = work.ScopeNodeKey, PayloadJson = work.PayloadJson, CreatedAtUtc = work.CreatedAtUtc, UpdatedAtUtc = work.UpdatedAtUtc });
                    break;
                case InvalidSubject.MixedBinding:
                    agent.CompletionId = fixture.Original.Completion.Id.Value;
                    agent.CompletionNamespaceId = fixture.Original.Completion.Namespace.Value;
                    break;
                case InvalidSubject.AllCompleted:
                    agent.Status = AgentProjectStructureAccessRevocationStatus.Completed;
                    agent.CompletedAtUtc = agent.UpdatedAtUtc;
                    work.Status = ProjectCrossModuleMutationStatus.Completed;
                    work.CompletedAtUtc = work.UpdatedAtUtc;
                    break;
            }
            await context.SaveChangesAsync();
        }
        string before = await HeadersAndNativeAsync(fixture);
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.Installer.InspectAsync(fixture.Original));
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.InstallAsync(new(new string('a', 64))));
        Assert.Equal(before, await HeadersAndNativeAsync(fixture));
        Assert.Equal(0, fixture.SourceAcquisitions);
        await using var read = fixture.Data.Projects();
        Assert.Empty(await read.Set<ProjectDeletionCompletionRecord>().ToArrayAsync());
    }

    [Fact]
    public async Task Another_completion_id_cannot_replace_an_installed_subject() {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
        var other = new ProjectDeletionPreparationContext(new(fixture.Original.Completion.DatabaseProfileId,
            fixture.Original.Completion.Namespace, new(Guid.NewGuid())), fixture.Original.Project);
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.Installer.InspectAsync(other));
        Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.Installer.InstallAsync(other, plan.Digest,
            ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped));
        await RequireGateAsync(fixture, released: false);
        Assert.Equal(1, fixture.SourceAcquisitions);
    }

    [Fact]
    public async Task A_held_selection_refuses_boundedly_and_the_same_plan_succeeds_after_release() {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        using (fixture.Data.Files().Acquire(fixture.Original.Completion.DatabaseProfileId)) {
            Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.InstallAsync(plan.Digest));
        }
        Assert.Equal(0, fixture.SourceAcquisitions);
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_native_or_lifecycle_exclusion_refuses_without_waiting_under_the_source_lease(bool lifecycle) {
        await using var fixture = await ProjectDeletionLegacyInstallationFixture.CreateAsync();
        var plan = await fixture.InspectAsync();
        await using (var owner = fixture.Data.Projects()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            if (lifecycle) {
                var binding = (await fixture.Data.Files().ObserveActiveBindingAsync(fixture.Original.Project.DatabaseProfileId)).Record.Plan.Binding;
                await DatasetLifecycleStore.RequireActiveForMutationAsync(owner, binding);
            } else {
                Assert.True(await SerializableMutationScope.TryAcquireRelationalScopeLocksAsync(owner,
                    [ProjectMutationScopeKeys.ForProject(fixture.Original.Project.ProjectId)], CancellationToken.None));
            }
            var refused = Assert.IsType<ProjectDeletionRecoveryResult.Refused>(await fixture.InstallAsync(plan.Digest));
            Assert.Equal(ProjectDeletionRecoveryRefusal.ExclusionBusy, refused.Reason);
            Assert.False(fixture.SourceHeld);
        }
        Assert.IsType<ProjectDeletionRecoveryResult.InstalledAwaitingRelease>(await fixture.InstallAsync(plan.Digest));
    }

    private static async Task RequireGateAsync(ProjectDeletionLegacyInstallationFixture fixture, bool released) {
        await using var context = fixture.Data.Projects();
        var gate = Assert.Single(await context.Set<ProjectDeletionCompletionRecord>().AsNoTracking().ToArrayAsync());
        Assert.Equal(fixture.Original.Completion.Id.Value, gate.CompletionId);
        Assert.Equal(ProjectDeletionCompletionOrigin.LegacyInstallation, gate.OriginKind);
        Assert.Equal(released ? ProjectDeletionCompletionReleaseState.Released : ProjectDeletionCompletionReleaseState.AwaitingOwnerRelease, gate.ReleaseState);
        var evidence = ProjectDeletionRecoveryCodec.Read(gate.RecoveryPayload, fixture.Original);
        var origin = Assert.IsType<ProjectDeletionOriginEvidence.LegacyInstallation>(evidence.Origin);
        Assert.Equal(ProjectDeletionLegacyPrecondition.AllWriterAndFormerSourceOwnerHostsStopped, origin.Precondition);
        Assert.Equal(fixture.Source, evidence.Source);
        Assert.Equal(2, evidence.RequirePreparationParticipants().Length);
        var agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().SingleAsync();
        var work = await context.Set<ProjectCrossModuleMutationRecord>().AsNoTracking().SingleAsync();
        Assert.Equal(fixture.Original, AgentProjectDeletionBinding.Read(agent, fixture.Original.Project.DatabaseProfileId));
        Assert.True(ProjectWorkbenchDeletionPayload.Matches(ProjectWorkbenchDeletionPayload.Read(work), fixture.Original));
    }

    private static async Task<string> HeadersAndNativeAsync(ProjectDeletionLegacyInstallationFixture fixture) {
        await using var context = fixture.Data.Projects();
        return JsonSerializer.Serialize(new {
            Native = await NativeStateAsync(fixture.Data),
            Agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.CompletionId, row.CompletionNamespaceId }).ToArrayAsync(),
            Workbench = await context.Set<ProjectCrossModuleMutationRecord>().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.CompletionId, row.CompletionNamespaceId }).ToArrayAsync()
        });
    }

    public enum InvalidSubject {
        MissingRetirement, ImportedRetirement, RecreatedProject, ResidualWorkbench, AmbiguousWorkbench, MixedBinding, AllCompleted
    }
}
