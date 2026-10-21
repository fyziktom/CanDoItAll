using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "LongRunning")]
[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class ProcessPlanningReviewReadScalingTests(ITestOutputHelper output) {
    [Theory]
    [InlineData(1, ReadBasisMode.RunSnapshot)]
    [InlineData(128, ReadBasisMode.RunSnapshot)]
    [InlineData(1024, ReadBasisMode.RunSnapshot)]
    [InlineData(1, ReadBasisMode.StepSnapshot)]
    [InlineData(128, ReadBasisMode.StepSnapshot)]
    [InlineData(1024, ReadBasisMode.StepSnapshot)]
    public async Task Warm_HTTP_review_records_complete_read_cost_at_each_accepted_plan_size(int steps, ReadBasisMode mode) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var setup = Stopwatch.GetTimestamp();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(
            mode, environment: environment, profile: profile, steps: steps, promptCharacters: 256);
        var probe = new ProcessReadProbe();
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            services.AddSingleton<TimeProvider>(fixture.Clock);
            services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(probe));
        }, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        ProcessPlanningReviewHttpAccessTests.UseToken(host);
        var options = host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var route = $"/api/processes/runs/{fixture.State.RunId.Value:D}/steps/{fixture.Request.Child.StepInstanceId.Value:D}/planning/{fixture.Waiting.Id:D}/review";
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "review-http-setup", Steps = steps, Mode = mode.ToString(),
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(setup).TotalMilliseconds
        }));
        for (var warmup = 0; warmup < 2; warmup++) {
            _ = await ReadAsync();
        }
        int? queries = null;
        for (var sample = 0; sample < 5; sample++) {
            probe.Reset();
            var allocated = GC.GetTotalAllocatedBytes(true);
            var started = Stopwatch.GetTimestamp();
            var bytes = await ReadAsync();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
            var commands = probe.Reads;
            Assert.True(commands > 0);
            queries ??= commands;
            Assert.Equal(queries, commands);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "review-http-selected-read", Steps = steps, Mode = mode.ToString(), Sample = sample,
                ElapsedMilliseconds = elapsed, ProcessAllocatedBytes = allocatedBytes,
                ProcessSqlCommands = commands, ResponseUtf8Bytes = bytes
            }));
            Assert.True(elapsed < 1000, $"Warm review exceeded the one-second finite-host reference: {elapsed:F1} ms.");
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        Assert.Equal(fixture.Waiting.Revision, (await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id))!.Run.Revision);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);

        async Task<int> ReadAsync() {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var response = await host.Client.GetAsync(route, deadline.Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var bytes = await response.Content.ReadAsByteArrayAsync(deadline.Token);
            var view = JsonSerializer.Deserialize<ProcessPlanningReviewApiView>(bytes, options)!;
            Assert.Equal(ProcessPlanningReviewState.AwaitingChoice, view.State);
            Assert.Equal(fixture.Waiting.Id, view.Run.Id);
            Assert.Equal(AgentPlanningDecisionExpectation.From(fixture.Waiting), view.Expected);
            Assert.Equal(ProcessPlanningReviewApiExpectation.From(fixture.Request), view.Process);
            Assert.Contains(AgentPlanningReviewChoice.Approve, view.AvailableChoices);
            Assert.Contains(AgentPlanningReviewChoice.Reject, view.AvailableChoices);
            return bytes.Length;
        }
    }

    private sealed class ProcessReadProbe : DbCommandInterceptor {
        private int reads;
        internal int Reads => Volatile.Read(ref reads);
        internal void Reset() => Interlocked.Exchange(ref reads, 0);

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref reads);
            return ValueTask.FromResult(result);
        }
    }
}
