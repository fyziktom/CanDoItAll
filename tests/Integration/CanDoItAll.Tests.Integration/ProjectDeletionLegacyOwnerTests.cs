using System.Data;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Web.Infrastructure;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using static CanDoItAll.Tests.Integration.ProjectDeletionLegacyTestState;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDeletionLegacyOwnerTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Terminal_cleanup_failure_preserves_an_uncertain_commit_instead_of_claiming_refusal() {
        var address = new ProjectDeletionCompletionAddress(Guid.NewGuid(), new(Guid.NewGuid()), new(Guid.NewGuid()));
        var result = ProjectDeletionRecoveryCommand.PreserveAfterCleanupFailure(
            new ProjectDeletionRecoveryResult.Unconfirmed(address, false, true));
        Assert.Equal(new ProjectDeletionRecoveryResult.Unconfirmed(address, true, true), result);
        var released = new ProjectDeletionRecoveryResult.Released(address, DateTimeOffset.UnixEpoch);
        Assert.Equal(released with { CleanupUnconfirmed = true }, ProjectDeletionRecoveryCommand.PreserveAfterCleanupFailure(released));
        Assert.Null(ProjectDeletionRecoveryCommand.PreserveAfterCleanupFailure(null));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Enlisted_binding_preserves_native_payload_claim_and_history(bool workbench, bool completed) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await SeedAsync(fixture, completed);
        string before = await NativeStateAsync(fixture);
        await InOwnerAsync(fixture, async transactions => {
            var participant = Participant(fixture, transactions, workbench);
            var legacy = Assert.Single(await participant.InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Exclusive));
            Assert.Null(legacy.Binding);
            Assert.Equal(completed, legacy.State.Completed);
            await participant.BindLegacyAsync(original, legacy);
            var after = Assert.Single(await participant.InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe));
            Assert.Equal(original, after.Binding);
            Assert.Equal(legacy.State, after.State);
            Assert.Equal(legacy.State, await participant.InspectAsync(original, legacy.State.Obligation.RecoveryId, ProjectDeletionRecoveryReadMode.Observe));
            await Assert.ThrowsAsync<InvalidDataException>(() => participant.BindLegacyAsync(original, legacy));
        }, commit: true);
        Assert.Equal(before, await NativeStateAsync(fixture));
        await using var context = fixture.Projects();
        Assert.Empty(await context.Set<ProjectDeletionCompletionRecord>().ToArrayAsync());
        if (!completed) {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Coordinator.EnsureReadyAsync(fixture.Profile));
        }
    }

    [Fact]
    public async Task Binding_both_native_owners_rolls_back_with_the_enclosing_transaction() {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await SeedAsync(fixture);
        string before = await NativeStateAsync(fixture);
        await InOwnerAsync(fixture, async transactions => {
            foreach (bool workbench in new[] { false, true }) {
                var participant = Participant(fixture, transactions, workbench);
                var state = Assert.Single(await participant.InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Exclusive));
                await participant.BindLegacyAsync(original, state);
            }
        }, commit: false);
        await InOwnerAsync(fixture, async transactions => {
            foreach (bool workbench in new[] { false, true }) {
                Assert.Null(Assert.Single(await Participant(fixture, transactions, workbench)
                    .InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe)).Binding);
            }
        }, commit: false);
        Assert.Equal(before, await NativeStateAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_claim_or_error_state_invalidates_the_inspected_plan(bool workbench) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await SeedAsync(fixture);
        ProjectDeletionLegacyParticipantEvidence? expected = null;
        await InOwnerAsync(fixture, async transactions => {
            expected = Assert.Single(await Participant(fixture, transactions, workbench)
                .InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe));
        }, commit: false);
        await using (var context = fixture.Projects()) {
            if (workbench) {
                (await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync()).ErrorMessage = "processing:changed";
            } else {
                (await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync()).LastFailureCode = "changed";
            }
            await context.SaveChangesAsync();
        }
        string before = await NativeStateAsync(fixture);
        await InOwnerAsync(fixture, async transactions => {
            var participant = Participant(fixture, transactions, workbench);
            await Assert.ThrowsAsync<InvalidDataException>(() => participant.BindLegacyAsync(original, expected!));
            Assert.Null(Assert.Single(await participant.InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe)).Binding);
        }, commit: false);
        Assert.Equal(before, await NativeStateAsync(fixture));
    }

    [Theory]
    [InlineData(InvalidNative.AgentForeignProfile)]
    [InlineData(InvalidNative.AgentForeignLifetime)]
    [InlineData(InvalidNative.AgentUnboundLifetime)]
    [InlineData(InvalidNative.WorkbenchForeignLifetime)]
    [InlineData(InvalidNative.WorkbenchUnboundLifetime)]
    [InlineData(InvalidNative.WorkbenchOversized)]
    [InlineData(InvalidNative.WorkbenchInvalidStatus)]
    public async Task Invalid_native_identity_or_payload_refuses_inspection_without_changing_bytes(InvalidNative fault) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await SeedAsync(fixture);
        bool workbench = fault >= InvalidNative.WorkbenchForeignLifetime;
        await using (var context = fixture.Projects()) {
            var agent = await context.Set<AgentProjectStructureAccessRevocationRecord>().SingleAsync();
            var work = await context.Set<ProjectCrossModuleMutationRecord>().SingleAsync();
            switch (fault) {
                case InvalidNative.AgentForeignProfile:
                    agent.DatabaseProfileId = Guid.NewGuid();
                    break;
                case InvalidNative.AgentForeignLifetime:
                    agent.ProjectLifetimeId = Guid.NewGuid();
                    break;
                case InvalidNative.AgentUnboundLifetime:
                    agent.ProjectLifetimeId = null;
                    agent.DatabaseProfileId = null;
                    break;
                case InvalidNative.WorkbenchForeignLifetime:
                case InvalidNative.WorkbenchUnboundLifetime:
                    work.PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload([], [],
                        SourceReference: new(original.Project.DatabaseProfileId, original.Project.ProjectId,
                            fault == InvalidNative.WorkbenchForeignLifetime ? Guid.NewGuid() : null)), Json);
                    break;
                case InvalidNative.WorkbenchOversized:
                    work.PayloadJson = JsonSerializer.Serialize(new DeleteProjectMutationPayload([new string('x', 2 * 1024 * 1024)], [],
                        SourceReference: ProjectAssignmentReference.From(original.Project)), Json);
                    break;
                case InvalidNative.WorkbenchInvalidStatus:
                    work.Status = (ProjectCrossModuleMutationStatus)999;
                    break;
            }
            await context.SaveChangesAsync();
        }
        string before = await NativeStateAsync(fixture);
        await InOwnerAsync(fixture, async transactions => {
            await Assert.ThrowsAsync<InvalidDataException>(() => Participant(fixture, transactions, workbench)
                .InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe));
        }, commit: false);
        Assert.Equal(before, await NativeStateAsync(fixture));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Excessive_subject_headers_refuse_before_payload_materialization(bool workbench) {
        await using var fixture = await DatasetActivationTestFixture.CreateAsync();
        var original = await SeedAsync(fixture);
        await using (var context = fixture.Projects()) {
            for (int index = 0; index < ProjectDeletionCompletionBinding.MaximumParticipants; index++) {
                if (workbench) {
                    context.Add(new ProjectCrossModuleMutationRecord {
                        ProjectId = original.Project.ProjectId, MutationKind = ProjectCrossModuleMutationKind.DeleteProject,
                        ScopeNodeKey = ProjectWorkbenchDeletionParticipant.ProjectDeletionScopeNodeKey, PayloadJson = "{",
                        CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
                    });
                } else {
                    context.Add(new AgentProjectStructureAccessRevocationRecord {
                        Id = Guid.NewGuid(), ProjectId = original.Project.ProjectId, DatabaseProfileId = original.Project.DatabaseProfileId,
                        ProjectLifetimeId = Guid.NewGuid(), CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
                    });
                }
            }
            await context.SaveChangesAsync();
        }
        await InOwnerAsync(fixture, async transactions => {
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Participant(fixture, transactions, workbench)
                .InspectLegacyAsync(original.Project, ProjectDeletionRecoveryReadMode.Observe));
            Assert.Contains(workbench ? "exceeds" : "excessive", failure.Message, StringComparison.Ordinal);
        }, commit: false);
    }

    private static IProjectDeletionLegacyParticipant Participant(DatasetActivationTestFixture fixture,
        CoordinatedDatabaseTransaction transactions, bool workbench)
        => workbench ? new ProjectWorkbenchDeletionRecoveryInspector(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseNpgsql(fixture.Profile.ConnectionString).Options, transactions)
            : new AgentProjectDeletionRecoveryInspector(new DbContextOptionsBuilder<AgentProjectAccessDbContext>()
                .UseNpgsql(fixture.Profile.ConnectionString).Options, transactions);

    private static async Task InOwnerAsync(DatasetActivationTestFixture fixture,
        Func<CoordinatedDatabaseTransaction, Task> action, bool commit) {
        var transactions = CoordinatedDatabaseTransaction.ForProfile(fixture.Profile);
        await using var context = fixture.Projects();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        using (transactions.Enter(context)) {
            await action(transactions);
        }
        if (commit) {
            await transaction.CommitAsync();
        }
    }

    public enum InvalidNative {
        AgentForeignProfile, AgentForeignLifetime, AgentUnboundLifetime,
        WorkbenchForeignLifetime, WorkbenchUnboundLifetime, WorkbenchOversized, WorkbenchInvalidStatus
    }
}
