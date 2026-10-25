using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class AgentInheritedPlanningStartupTests {
    [Fact]
    public async Task Initial_preparation_binding_and_parent_checks_are_charged_once_in_the_original_child() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var preparation = Preparation(fixture, id);
        var calls = 0;
        var admitted = await Owner(fixture).AdmitAsync(id, Start(fixture), (_, _) => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(3));
            return Task.CompletedTask;
        }, _ => {
            calls++;
            fixture.Clock.Advance(TimeSpan.FromSeconds(1));
            return Task.FromResult(preparation with { BindAsync = async (context, token) => {
                fixture.Clock.Advance(TimeSpan.FromSeconds(2));
                return await preparation.BindAsync(context, token);
            } });
        });
        var work = admitted.Run.ToolAdmission!.PlanningDecision!.Work;
        Assert.Equal(TimeSpan.FromSeconds(6).Ticks, work.ClosedWorkTicks);
        Assert.Equal(AgentPlanningWorkSettlementReason.InitialAdmission, work.LastSettlement!.Reason);
        Assert.Null(work.Open);
        Assert.Empty(admitted.Run.ToolAdmission.PlanningDecision.Current.Steps);
        var retained = await Owner(fixture).AdmitAsync(id, Start(fixture) with { Mode = AgentInheritedPlanningStartMode.ExistingOnly },
            (_, _) => Task.CompletedTask, _ => throw new InvalidOperationException("Recovery cannot prepare again."));
        Assert.False(retained.Created);
        Assert.Equal(JsonSerializer.Serialize(admitted.Run), JsonSerializer.Serialize(retained.Run));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Initial_overrun_is_durable_failed_work_without_provider_admission() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var admitted = await Owner(fixture).AdmitAsync(id, Start(fixture), (_, _) => Task.CompletedTask, _ => {
            fixture.Clock.Advance(TimeSpan.FromSeconds(31));
            return Task.FromResult(Preparation(fixture, id));
        });
        Assert.Equal(RunOutcome.Failed, admitted.Run.Outcome);
        var decision = admitted.Run.ToolAdmission!.PlanningDecision!;
        Assert.Equal(TimeSpan.FromSeconds(31).Ticks, decision.Work.ClosedWorkTicks);
        Assert.Null(decision.Work.Open);
        Assert.Empty(decision.Current.Steps);
        Assert.True(admitted.Run.ToolAdmission.ProviderDispatches.IsDefaultOrEmpty);
        Assert.Equal(AgentPlanningGenerationState.Failed, decision.Current.State);
        Assert.Equal(JsonSerializer.Serialize(admitted.Run), JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunAsync(id)));
    }

    [Fact]
    public async Task Missing_journal_after_failed_initial_work_cannot_be_recreated_by_a_recovery_caller() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var calls = 0;
        Task<AgentInheritedPlanningRunPreparation> Prepare(CancellationToken _) {
            calls++;
            throw new IOException("Initial preparation closed before an admitted journal existed.");
        }
        await Assert.ThrowsAsync<IOException>(() => Owner(fixture).AdmitAsync(id, Start(fixture), (_, _) => Task.CompletedTask, Prepare));
        var failure = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => Owner(fixture).AdmitAsync(id,
            Start(fixture) with { Mode = AgentInheritedPlanningStartMode.ExistingOnly }, (_, _) => Task.CompletedTask, Prepare));
        Assert.Equal(AgentInheritedPlanningRunAdmission.MissingOriginalCode, failure.Code);
        Assert.Equal(1, calls);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_different_parent_allocation_cannot_create_or_read_the_original_child(bool retained) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var start = Start(fixture);
        var owner = Owner(fixture);
        if (retained) {
            await owner.AdmitAsync(id, start, (_, _) => Task.CompletedTask, _ => Task.FromResult(Preparation(fixture, id)));
        }
        var before = await fixture.Files.Store.GetExecutionRunAsync(id);
        var changed = start with { Allocation = start.Allocation with { Id = new(Guid.NewGuid()) } };
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitAsync(id, changed, (_, _) => Task.CompletedTask,
            _ => Task.FromResult(Preparation(fixture, id))));
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunAsync(id)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Invalid_startup_clock_keeps_the_missing_child_unresolved(bool wallClock) {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var failure = await Assert.ThrowsAsync<AgentPlanningAdmissionException>(() => Owner(fixture).AdmitAsync(id, Start(fixture),
            (_, _) => Task.CompletedTask, _ => {
                if (wallClock) {
                    fixture.Clock.Now -= TimeSpan.FromSeconds(1);
                } else {
                    fixture.Clock.Timestamp = -1;
                }
                return Task.FromResult(Preparation(fixture, id));
            }));
        Assert.Equal(AgentPlanningAdmissionRefusal.ClockInvalid, failure.Reason);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(id));
    }

    [Fact]
    public async Task Finite_original_grant_cancels_an_unfinished_initial_preparation() {
        await using var fixture = await AgentInheritedPlanningFixture.CreateAsync();
        var id = Guid.NewGuid();
        var original = Start(fixture);
        var start = original with { Allocation = original.Allocation with { Grant = new(TimeSpan.FromMilliseconds(100).Ticks, 0) } };
        var entered = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Owner(fixture).AdmitAsync(id, start,
            (_, _) => Task.CompletedTask, async token => {
                entered = true;
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                throw new InvalidOperationException("The finite startup deadline did not cancel.");
            }).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(entered);
        Assert.Null(await fixture.Files.Store.GetExecutionRunAsync(id));
    }

    private static AgentInheritedPlanningRunAdmission Owner(AgentInheritedPlanningFixture fixture)
        => new(fixture.Files.Store, fixture.Files.Store, fixture.Journal);

    private static AgentInheritedPlanningStart Start(AgentInheritedPlanningFixture fixture)
        => new(fixture.Original.ToolAdmission!.PlanningDecision!.InheritedOrigin!.Allocation, AgentInheritedPlanningStartMode.FreshEnrollment);

    private static AgentInheritedPlanningRunPreparation Preparation(AgentInheritedPlanningFixture fixture, Guid id) {
        var decision = fixture.Original.ToolAdmission!.PlanningDecision!;
        return new(fixture.Original with { Id = id, ToolAdmission = null, MetadataJson = "{}" }, "Prepare the original plan.",
            decision.Owner, AgentPlanningDecisionTestData.Envelope().Digest, fixture.Files.StorageScope, decision.InheritedOrigin!.ParentBinding,
            (context, _) => Task.FromResult(new AgentInheritedPlanningBinding(decision.OriginalTarget,
                decision.InheritedOrigin with { Source = context.Session.Reference.BackgroundSource! })));
    }
}
