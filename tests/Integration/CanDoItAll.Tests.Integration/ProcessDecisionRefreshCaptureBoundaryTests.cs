using System.Text.Json;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Integration.ProcessDecisionRefreshCaptureFixture;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessDecisionRefreshCaptureBoundaryTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Capacity_refusal_precedes_reservation_and_content_reads(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var original = await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadAsync(state, admission);
        var local = original.Original;
        var head = local.Head!;
        var child = local.Child!;
        var binding = child.Original;
        List<ProcessDecisionRefreshCaptureSelection> exhausted = [
            original with { Original = local with { Budget = local.Budget with {
                AllocationCount = DecisionBudgetBalance.MaximumAllocations - 1, Revision = 600
            } } },
            original with { RootBudget = original.RootBudget with {
                AllocationCount = DecisionBudgetBalance.MaximumAllocations - (mode == ReadBasisMode.RunSnapshot ? 2 : 1), Revision = 600
            } },
            original with { Original = local with { Head = head with {
                Reference = new(head.Reference.DecisionId, ProcessDecisionGeneration.MaximumGenerations)
            } } },
            original with { Original = local with { Child = child with { Original = binding with {
                Material = binding.Material with { Reference = new(binding.Material.Reference.ScopeId,
                    ProcessDecisionGeneration.MaximumGenerations, binding.Material.Reference.MaterialId) }
            } } } },
            original with { Original = local with { Budget = local.Budget with {
                ConsumedWorkTicks = local.Budget.Ceiling.MaximumWorkDuration.Ticks
            } } },
            original with { RootBudget = original.RootBudget with { ConsumedWorkTicks = original.RootBudget.Ceiling.MaximumWorkDuration.Ticks } }
        ];
        if (original.SharedScope is { } shared) {
            exhausted.Add(original with { SharedScope = shared with { TotalMemberships = ProcessDecisionRunScopeState.MaximumMemberships } });
        }
        foreach (var selection in exhausted) {
            Query query = new(selection, new(null, null, null, null, null, null));
            Assert.Equal(ProcessDecisionRefreshCaptureStatus.ReviewRequired,
                (await Coordinator(fixture, query).PrepareAsync(state, fixture.CommandContext, admission)).Status);
            Assert.Equal(1, query.SelectionReads);
        }
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId)));
        Assert.Equal(new(null, null, null, null, null, null),
            await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadProgressAsync(state.RunId, admission));
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Replay_rejects_inconsistent_stage_evidence_before_reading_source_or_invoking_capture(ReadBasisMode mode) {
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(mode, freezeClock: false);
        var admission = await AdmitAsync(fixture);
        var state = await AccountAsync(fixture, admission);
        var result = await fixture.Services.GetRequiredService<ProcessDecisionRefreshCaptureCoordinator>()
            .PrepareAsync(state, fixture.CommandContext, admission);
        Assert.Equal(ProcessDecisionRefreshCaptureStatus.Advanced, result.Status);
        var progress = await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadProgressAsync(state.RunId, admission);
        var successor = progress.Successor!;
        var budget = successor.Budget!;
        var refresh = Assert.IsType<ProcessDecisionRefresh>(successor.Decision!.Command);
        List<ProcessDecisionRefreshCaptureProgress> changed = [
            progress with { LocalReservation = null },
            progress with { LocalAbort = successor with { Decision = null } },
            progress with { Successor = successor with { Decision = successor.Decision with {
                Command = refresh with { AdmissionCommandId = RuntimeCommandId.New() }
            } } },
            progress with { Successor = successor with { Budget = budget with { Change = budget.Change with {
                Allocation = budget.Change.Allocation with { Grant = budget.Change.Allocation.Grant with { WorkTicks = budget.Change.Allocation.Grant.WorkTicks + 1 } }
            } } } },
            progress with { Successor = successor with { Budget = budget with { Funding = budget.Funding! with { RootRunId = new(Guid.NewGuid()) } } } }
        ];
        if (mode == ReadBasisMode.RunSnapshot) {
            changed.Add(progress with { RootCompletion = null });
            changed.Add(progress with { RootReservation = null });
        }
        foreach (var inconsistent in changed) {
            Query query = new(null, inconsistent);
            await Assert.ThrowsAsync<InvalidDataException>(() => Coordinator(fixture, query).PrepareAsync(result.State, fixture.CommandContext, admission));
            Assert.Equal(0, query.SelectionReads);
        }
        Assert.Equal(JsonSerializer.Serialize(result.State), JsonSerializer.Serialize(await fixture.Services.GetRequiredService<IProcessRuntimeStateStore>().LoadAsync(state.RunId)));
        Assert.Equal(progress, await fixture.Services.GetRequiredService<IProcessDecisionRefreshCaptureQuery>().ReadProgressAsync(state.RunId, admission));
    }

    private static ProcessDecisionRefreshCaptureCoordinator Coordinator(ProjectTaskPlanReviewFixture fixture, Query query)
        => new(query, fixture.Services.GetRequiredService<IProcessDecisionRootQuery>(), fixture.Services.GetRequiredService<IProcessDecisionResultQuery>(), [],
            fixture.Services.GetRequiredService<IProcessRuntimeStateStore>(), fixture.Services.GetRequiredService<IProcessRuntimeUnitOfWork>(),
            fixture.Clock, NullLogger<ProcessDecisionRefreshCaptureCoordinator>.Instance);

    private sealed class Query(ProcessDecisionRefreshCaptureSelection? selection, ProcessDecisionRefreshCaptureProgress progress)
        : IProcessDecisionRefreshCaptureQuery {
        internal int SelectionReads { get; private set; }
        public Task<ProcessDecisionRefreshCaptureSelection> ReadAsync(ProcessRuntimeStateSnapshot state, RuntimeCommandId admissionId,
            CancellationToken cancellationToken = default) {
            SelectionReads++;
            return Task.FromResult(selection ?? throw new InvalidOperationException("Invalid replay must be refused before source selection."));
        }
        public Task<ProcessDecisionRefreshCaptureProgress> ReadProgressAsync(CanDoItAll.Processes.Abstractions.ProcessRunId runId,
            RuntimeCommandId admissionId, CancellationToken cancellationToken = default) => Task.FromResult(progress);
    }
}
