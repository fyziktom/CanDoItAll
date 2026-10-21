using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;
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
    public Task Warm_HTTP_review_records_complete_read_cost_at_each_accepted_plan_size(int steps, ReadBasisMode mode)
        => ReadScenarioAsync(steps, mode, trace: false);

    [Theory]
    [InlineData(1, ReadBasisMode.RunSnapshot)]
    [InlineData(1, ReadBasisMode.StepSnapshot)]
    [InlineData(1024, ReadBasisMode.StepSnapshot)]
    public Task Profile_HTTP_review_through_existing_telemetry(int steps, ReadBasisMode mode)
        => ReadScenarioAsync(steps, mode, trace: true);

    private async Task ReadScenarioAsync(int steps, ReadBasisMode mode, bool trace) {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        var setup = Stopwatch.GetTimestamp();
        await using var fixture = await ProjectTaskPlanReviewFixture.CreateAsync(
            mode, environment: environment, profile: profile, steps: steps, promptCharacters: 256);
        using var probe = new ProcessReadProbe(trace);
        var route = $"/api/processes/runs/{fixture.State.RunId.Value:D}/steps/{fixture.Request.Child.StepInstanceId.Value:D}/planning/{fixture.Waiting.Id:D}/review";
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            services.AddSingleton<TimeProvider>(fixture.Clock);
            var review = services.Last(item => item.ServiceType == typeof(ProjectTaskPlanProcessReviewReader));
            Assert.NotNull(review.ImplementationType);
            services.AddScoped<ProjectTaskPlanProcessReviewReader>(provider => {
                using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("process.planning.review.resolve");
                return (ProjectTaskPlanProcessReviewReader)ActivatorUtilities.CreateInstance(provider, review.ImplementationType);
            });
            services.Configure<ProcessRuntimeDispatchQueueOptions>(options => options.EnableRecovery = false);
            services.ConfigureDbContext<ProcessPersistenceDbContext>((_, options) => options.AddInterceptors(probe));
            var factory = services.Last(item => item.ServiceType == typeof(ICanDoItAllAgentWorkspaceFactory));
            Assert.NotNull(factory.ImplementationType);
            services.AddScoped<ICanDoItAllAgentWorkspaceFactory>(provider => {
                Assert.False(probe.IsObservedRequest, "A planning review read must not construct the execution workspace.");
                return (ICanDoItAllAgentWorkspaceFactory)ActivatorUtilities.CreateInstance(provider, factory.ImplementationType);
            });
        }, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        probe.Observe(host.App.Services.GetRequiredService<IHttpContextAccessor>(), route);
        ProcessPlanningReviewHttpAccessTests.UseToken(host);
        var options = host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        output.WriteLine(JsonSerializer.Serialize(new {
            Workload = "review-http-setup", Steps = steps, Mode = mode.ToString(), Profiled = trace,
            ElapsedMilliseconds = Stopwatch.GetElapsedTime(setup).TotalMilliseconds,
            ProcessRecoveryEnabled = false, SqlObservation = "selected-http-request"
        }));
        for (var warmup = 0; warmup < 2; warmup++) {
            _ = await ReadAsync();
        }
        int? queries = null;
        var maximumElapsed = 0d;
        for (var sample = 0; sample < 5; sample++) {
            probe.Reset();
            var allocated = GC.GetTotalAllocatedBytes(true);
            var started = Stopwatch.GetTimestamp();
            var bytes = await ReadAsync();
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated;
            var commands = probe.Reads;
            maximumElapsed = Math.Max(maximumElapsed, elapsed);
            Assert.True(commands > 0);
            queries ??= commands;
            Assert.Equal(queries, commands);
            output.WriteLine(JsonSerializer.Serialize(new {
                Workload = "review-http-selected-read", Steps = steps, Mode = mode.ToString(), Sample = sample, Profiled = trace,
                ElapsedMilliseconds = elapsed, ProcessAllocatedBytes = allocatedBytes,
                ProcessSqlCommands = commands, ProcessSqlMilliseconds = probe.SqlMilliseconds, ResponseUtf8Bytes = bytes,
                Trace = probe.Summarize()
            }));
        }
        Assert.Single(fixture.Entry.Wire.Requests);
        Assert.Equal(fixture.Waiting.Revision, (await fixture.Entry.Files.Store.GetExecutionRunDetailAsync(fixture.Waiting.Id))!.Run.Revision);
        await fixture.Entry.Files.Sql.VerifyAsync(0, 0);
        if (!trace) {
            Assert.True(maximumElapsed < 1000, $"Warm review exceeded the one-second finite-host reference: {maximumElapsed:F1} ms.");
        }

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

    private sealed record ReadActivity(string Name, string? PayloadType, double Milliseconds, long PayloadBytes);
    private sealed record ReadActivitySummary(string Name, string? PayloadType, int Count, double InclusiveMilliseconds, long PayloadBytes);

    private sealed class ProcessReadProbe : DbCommandInterceptor, IDisposable {
        private const int MaximumActivities = 4096;
        private readonly ConcurrentQueue<ReadActivity> activities = new();
        private readonly ActivityListener? listener;
        private IHttpContextAccessor? context;
        private PathString route;
        private int reads;
        private long sqlTicks;
        private int activityCount;

        internal ProcessReadProbe(bool trace) {
            if (!trace) {
                return;
            }
            listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => {
                    if (IsObservedRequest && Interlocked.Increment(ref activityCount) <= MaximumActivities) {
                        activities.Enqueue(new(activity.OperationName,
                            (activity.GetTagItem("payload.type") ?? activity.Parent?.GetTagItem("payload.type")) as string,
                            activity.Duration.TotalMilliseconds, activity.GetTagItem("payload.bytes") is long bytes ? bytes : 0));
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        internal int Reads => Volatile.Read(ref reads);
        internal double SqlMilliseconds => TimeSpan.FromTicks(Interlocked.Read(ref sqlTicks)).TotalMilliseconds;

        internal bool IsObservedRequest => context?.HttpContext?.Request is { } request &&
            HttpMethods.IsGet(request.Method) && request.Path == route;

        internal void Observe(IHttpContextAccessor accessor, PathString path) {
            context = accessor;
            route = path;
        }

        internal void Reset() {
            Interlocked.Exchange(ref reads, 0);
            Interlocked.Exchange(ref sqlTicks, 0);
            activities.Clear();
            Interlocked.Exchange(ref activityCount, 0);
        }

        internal ReadActivitySummary[] Summarize() {
            Assert.True(Volatile.Read(ref activityCount) <= MaximumActivities, "The diagnostic activity bound was exceeded.");
            return activities.GroupBy(item => (item.Name, item.PayloadType))
                .Select(group => new ReadActivitySummary(group.Key.Name, group.Key.PayloadType, group.Count(),
                    group.Sum(item => item.Milliseconds), group.Sum(item => item.PayloadBytes)))
                .OrderByDescending(item => item.InclusiveMilliseconds).ToArray();
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (IsObservedRequest) {
                Interlocked.Increment(ref reads);
                Interlocked.Add(ref sqlTicks, eventData.Duration.Ticks);
            }
            return ValueTask.FromResult(result);
        }

        public void Dispose() => listener?.Dispose();
    }
}
