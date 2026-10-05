using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessProjectAdmissionPersistenceTests {
    [Fact]
    public async Task Native_root_process_records_keep_cursor_pages_and_the_accepted_report_cutoff_after_a_later_run() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admission = await CreateProjectAsync(services);
        var foreign = await CreateProjectAsync(services);
        var coordinator = Coordinator(services);
        await using var owner = Context(services);
        var unit = new EfProcessRuntimeUnitOfWork(owner, coordinatedTransaction: coordinator, projectAdmissionPolicy: Policy(services, coordinator));
        var clock = new ProjectionLifetimeClock();
        var records = new EfProcessRunRecordStore(owner);
        var projector = new ProcessRuntimeProjectionProjector(new EfProcessProjectionStore(owner), ProcessProjectionJsonCodec.Default, clock, records);
        long sequence = 0;
        async Task<ProcessRunId> CommitAsync(ProcessProjectAdmission target, DateTimeOffset? at = null) {
            var initial = ProcessProjectAdmissionFixture.Initial(target);
            initial = initial with {
                InitialAssignments = initial.InitialAssignments!.Select(assignment => assignment with {
                    LaunchVariables = new Dictionary<string, string> {
                        [ProcessRuntimeLaunchVariables.ProjectId] = target.ProjectId.ToString("D")
                    }
                }).ToArray()
            };
            if (at is { } timestamp) {
                initial = initial with {
                    OriginalState = initial.OriginalState with { UpdatedAtUtc = timestamp },
                    Mutation = initial.Mutation with {
                        State = initial.Mutation.State with { UpdatedAtUtc = timestamp },
                        Events = initial.Mutation.Events.Select(item => item with { OccurredAtUtc = timestamp }).ToArray()
                    }
                };
            }
            var started = await unit.CommitAsync(initial);
            Assert.True(started.Succeeded);
            var cancelled = ProcessProjectAdmissionFixture.Cancel(started.State);
            Assert.True((await unit.CommitAsync(cancelled)).Succeeded);
            long runSequence = 0;
            foreach (var envelope in initial.Mutation.Events.Concat(cancelled.Mutation.Events)) {
                clock.Now = clock.Now.AddSeconds(1);
                await projector.ProjectAsync(new(++sequence, ++runSequence, envelope), new(new("insights-paging"), clock.Now, sequence));
            }
            return started.State.RunId;
        }
        var expected = new HashSet<Guid>();
        for (var index = 0; index < 25; index++) {
            expected.Add((await CommitAsync(admission)).Value);
        }
        await CommitAsync(foreign);
        var assembler = services.GetRequiredService<ProcessRunRecordAssembler>();
        async Task AssembleFactsAsync() {
            var claims = await records.ClaimFactsAsync(new(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), 100));
            foreach (var claim in claims) {
                var current = Assert.IsType<ProcessRunRecord>(await records.GetAsync(claim.RunId));
                Assert.True(await records.CompleteFactsAsync(await assembler.AssembleAsync(claim, current)));
            }
        }
        await AssembleFactsAsync();
        var reports = services.GetRequiredService<ProjectManagerSummaryQueryService>();
        var options = new ProjectManagerSummaryOptions { TimeRange = ProjectManagerSummaryTimeRange.All };
        var report = await reports.LoadAsync(new(admission.ProjectId, "Native Process report", options.Scope, [admission.ProjectId], 0, false), options);
        var request = new ProjectManagerActivityPageRequest(report, ProjectManagerActivityKind.Process, ProjectManagerActivityStatusFilter.All);
        var first = await reports.QueryActivityPageAsync(request);
        Assert.Equal(25, first.TotalCount);
        Assert.Equal(20, first.Items.Count);
        Assert.NotNull(first.NextProcessCursor);
        Assert.Equal(0, first.Totals.UnknownHistoricalCostCount);
        var late = await CommitAsync(admission, report.AsOfUtc.AddMinutes(1));
        await AssembleFactsAsync();
        var second = await reports.QueryActivityPageAsync(request with { PageIndex = 1, ProcessCursor = first.NextProcessCursor, KnownAggregate = first.Aggregate });
        Assert.Equal(5, second.Items.Count);
        Assert.Equal(first.TotalCount, second.TotalCount);
        Assert.Same(first.Totals, second.Totals);
        Assert.Equal(expected.Order(), first.Items.Concat(second.Items).Select(item => item.Id.Value).Order());
        Assert.DoesNotContain(first.Items.Concat(second.Items), item => item.Id.Value == late.Value);
        var backward = await reports.QueryActivityPageAsync(request with { KnownAggregate = first.Aggregate });
        Assert.Equal(first.Items.Select(item => item.Id), backward.Items.Select(item => item.Id));
    }
}
