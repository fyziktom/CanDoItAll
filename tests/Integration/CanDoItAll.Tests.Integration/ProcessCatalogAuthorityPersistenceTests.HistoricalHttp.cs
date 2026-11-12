using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Fact]
    public async Task Historical_HTTP_pages_real_original_receipts_through_deferred_DI_and_enforces_current_read_policy() {
        var clock = new NativeClock();
        var probe = new HistoricalHttpProbe();
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            ToolProducerHarness(clock).ConfigureServices!(services);
            probe.ObserveRegistration(services);
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var seed = await SeedHistoricalReceiptsAsync(services, clock, process: true, receiptCount: 5);
        var originalRun = await HistoricalFileDigestAsync(seed.RunPath);
        UseHistoricalReadToken(host, ApiAccessScopeNames.ReadAgents);

        var first = await ReadHistoricalHttpPageAsync(host, probe, seed.Request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.LookupLimit, first.Page.Stop);
        AssertHistoricalHttpAccepted(seed.Intents.Take(4), first, expectedCatalogReads: 3, expectedTargets: 4);
        var cursor = Assert.IsType<AgentHistoricalReceiptCursor>(first.Page.NextCursor);
        Assert.Equal(4, cursor.NextProposalOrdinal);
        Assert.Equal(seed.Run.Revision, cursor.RunRevision);
        Assert.Equal(seed.Run.ToolAdmission!.Revision, cursor.JournalRevision);
        using (var wire = JsonDocument.Parse(first.Json)) {
            Assert.Equal(JsonValueKind.String, wire.RootElement.GetProperty("runRevision").ValueKind);
            Assert.Equal(JsonValueKind.String, wire.RootElement.GetProperty("journalRevision").ValueKind);
            Assert.Equal(JsonValueKind.String, wire.RootElement.GetProperty("nextCursor").GetProperty("runRevision").ValueKind);
        }

        var nextRequest = HistoricalHttpRequest(seed.Request, cursor);
        var next = await ReadHistoricalHttpPageAsync(host, probe, nextRequest);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, next.Page.Stop);
        Assert.Null(next.Page.NextCursor);
        AssertHistoricalHttpAccepted(seed.Intents.Skip(4), next, expectedCatalogReads: 3, expectedTargets: 1);
        Assert.Equal(5, first.Page.Observations.Concat(next.Page.Observations).Select(item => item.Binding.IntentId).Distinct().Count());

        await seed.Owner.RevokeAsync(Revocation.Write);
        var readOnly = await ReadHistoricalHttpPageAsync(host, probe, nextRequest);
        AssertHistoricalHttpAccepted(seed.Intents.Skip(4), readOnly, expectedCatalogReads: 3, expectedTargets: 1);
        Assert.Equal(next.Page.Request, readOnly.Page.Request);
        Assert.Equal(next.Page.CurrentContext, readOnly.Page.CurrentContext);
        Assert.Equal(next.Page.RunRevision, readOnly.Page.RunRevision);
        Assert.Equal(next.Page.JournalRevision, readOnly.Page.JournalRevision);
        Assert.Equal(next.Page.Stop, readOnly.Page.Stop);
        Assert.Equal(next.Page.Observations, readOnly.Page.Observations);
        Assert.Null(readOnly.Page.NextCursor);

        await seed.Owner.RevokeAsync(Revocation.Read);
        var denied = await ReadHistoricalHttpPageAsync(host, probe, nextRequest);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ReadDenied, denied.Page.Stop);
        Assert.Empty(denied.Page.Observations);
        Assert.Null(denied.Page.NextCursor);
        Assert.Null(denied.Page.RunRevision);
        Assert.Null(denied.Page.JournalRevision);
        Assert.Equal(0, denied.FinalTargets);
        Assert.Equal(4, probe.Resolutions);
        Assert.Equal(originalRun, await HistoricalFileDigestAsync(seed.RunPath));
        await AssertHistoricalReceiptsUnchangedAsync(services, seed);
    }

    [Fact]
    public async Task Historical_HTTP_validates_authentication_bounded_body_and_lossless_cursor_before_owner_resolution() {
        var clock = new NativeClock();
        var probe = new HistoricalHttpProbe();
        await using var host = await ApiTestHost.CreateAsync(true, services => {
            ToolProducerHarness(clock).ConfigureServices!(services);
            probe.ObserveRegistration(services);
        });
        await using var scope = host.App.Services.CreateAsyncScope();
        var seed = await SeedHistoricalReceiptsAsync(scope.ServiceProvider, clock, process: false);
        var options = HistoricalHttpJson(host);
        var originalRun = await HistoricalFileDigestAsync(seed.RunPath);
        var valid = JsonSerializer.Serialize(seed.Request, options);

        await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent(valid), HttpStatusCode.Unauthorized, handlerRan: false);
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-token");
        await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent(valid), HttpStatusCode.Unauthorized, handlerRan: false);
        UseHistoricalReadToken(host, ApiAccessScopeNames.ReadProjects);
        await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent(valid), HttpStatusCode.Forbidden, handlerRan: false);
        UseHistoricalReadToken(host, ApiAccessScopeNames.ReadAgents);

        var page = await ReadHistoricalHttpPageAsync(host, probe, seed.Request);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ExhaustedSnapshot, page.Page.Stop);
        AssertHistoricalHttpAccepted(seed.Intents, page, expectedCatalogReads: 2, expectedTargets: 4);
        var cursor = new AgentHistoricalReceiptCursor(seed.Run.Id, page.Page.RunRevision!.Value,
            page.Page.JournalRevision!.Value, 0, page.Page.CurrentContext);
        var paged = HistoricalHttpRequest(seed.Request, cursor);
        var pagedJson = JsonSerializer.Serialize(paged, options);

        await AssertHistoricalHttpRefusalAsync(host, probe, Guid.NewGuid(), HistoricalJsonContent(valid), HttpStatusCode.BadRequest);
        await AssertHistoricalHttpRefusalAsync(host, probe, Guid.Empty, HistoricalJsonContent(valid), HttpStatusCode.BadRequest);
        await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent("null"), HttpStatusCode.BadRequest);
        var invalidReference = JsonNode.Parse(valid)!.AsObject();
        invalidReference["agentId"] = Guid.Empty;
        await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent(invalidReference.ToJsonString()), HttpStatusCode.BadRequest);
        using (var plain = new StringContent(valid, Encoding.UTF8, "text/plain")) {
            await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, plain, HttpStatusCode.UnsupportedMediaType, handlerRan: false);
        }
        var oversized = new byte[AgentHistoricalReceiptReviewRequest.MaximumUtf8Bytes + 1];
        Array.Fill(oversized, (byte)' ');
        using (var declared = new ByteArrayContent(oversized)) {
            declared.Headers.ContentType = new("application/json");
            await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, declared, HttpStatusCode.RequestEntityTooLarge);
        }
        using (var streaming = new HistoricalStreamingContent(oversized)) {
            await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, streaming, HttpStatusCode.RequestEntityTooLarge);
        }

        string[] invalidRevisionTokens = ["1", "9007199254740993", "null", "true", "\"0\"", "\"01\"", "\"+1\"", "\"-1\"",
            "\" 1\"", "\"1 \"", "\"1\\n\"", "\"1.0\"", "\"1e0\"", "\"9223372036854775808\""];
        foreach (var property in new[] { "runRevision", "journalRevision" }) {
            foreach (var token in invalidRevisionTokens) {
                var body = JsonNode.Parse(pagedJson)!.AsObject();
                body["cursor"]![property] = JsonNode.Parse(token);
                await AssertHistoricalHttpRefusalAsync(host, probe, seed.Run.Id, HistoricalJsonContent(body.ToJsonString()), HttpStatusCode.BadRequest);
            }
        }
        foreach (var revision in new[] { 9_007_199_254_740_993L, long.MaxValue }) {
            var changed = HistoricalHttpRequest(seed.Request, new(seed.Run.Id, revision, revision, 0, page.Page.CurrentContext));
            var observed = await ReadHistoricalHttpPageAsync(host, probe, changed);
            Assert.Equal(AgentHistoricalReceiptReviewStop.SnapshotChanged, observed.Page.Stop);
            Assert.Empty(observed.Page.Observations);
            Assert.Null(observed.Page.NextCursor);
            Assert.Null(observed.Page.RunRevision);
            Assert.Null(observed.Page.JournalRevision);
            Assert.Equal(revision, observed.Page.Request.Cursor!.RunRevision);
            Assert.Equal(revision, observed.Page.Request.Cursor.JournalRevision);
            using var wire = JsonDocument.Parse(observed.Json);
            Assert.Equal(revision.ToString(CultureInfo.InvariantCulture), wire.RootElement.GetProperty("request")
                .GetProperty("cursor").GetProperty("runRevision").GetString());
        }
        var current = page.Page.CurrentContext;
        var staleContext = new AgentHistoricalReceiptReadContext(current.ProfileId,
            new(checked(current.Generation.Value + 1)), current.Scope);
        var stale = HistoricalHttpRequest(seed.Request, new(seed.Run.Id, cursor.RunRevision, cursor.JournalRevision, 0, staleContext));
        var contextChanged = await ReadHistoricalHttpPageAsync(host, probe, stale);
        Assert.Equal(AgentHistoricalReceiptReviewStop.ContextChanged, contextChanged.Page.Stop);
        Assert.Empty(contextChanged.Page.Observations);
        Assert.Null(contextChanged.Page.NextCursor);
        Assert.Null(contextChanged.Page.RunRevision);
        Assert.Null(contextChanged.Page.JournalRevision);
        Assert.Empty(contextChanged.CatalogBytes);
        Assert.Equal(0, contextChanged.FinalTargets);
        Assert.Equal(4, probe.Resolutions);
        Assert.Equal(originalRun, await HistoricalFileDigestAsync(seed.RunPath));
        await AssertHistoricalReceiptsUnchangedAsync(scope.ServiceProvider, seed);
    }

    private static AgentHistoricalReceiptReviewRequest HistoricalHttpRequest(AgentHistoricalReceiptReviewRequest original,
        AgentHistoricalReceiptCursor cursor)
        => new(original.ExecutionRunId, original.ChatSessionId, original.AgentId, original.ExecutionCreatedAtUtc, cursor);

    private static string HistoricalHttpRoute(Guid runId) => $"/api/agents/execution-runs/{runId:D}/saved-receipts/read";

    private static JsonSerializerOptions HistoricalHttpJson(ApiTestHost host)
        => host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;

    private static StringContent HistoricalJsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    private static void UseHistoricalReadToken(ApiTestHost host, string scope) {
        var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "historical-http-fixture", Scopes = [scope], LifetimeMinutes = 10
        });
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
    }

    private static async Task AssertHistoricalHttpRefusalAsync(ApiTestHost host, HistoricalHttpProbe probe, Guid routeRunId,
        HttpContent body, HttpStatusCode status, bool handlerRan = true) {
        var resolutions = probe.Resolutions;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, HistoricalHttpRoute(routeRunId)) { Content = body };
        using var response = await host.Client.SendAsync(request, timeout.Token);
        Assert.Equal(status, response.StatusCode);
        if (handlerRan) {
            Assert.True(response.Headers.CacheControl?.NoStore);
        }
        Assert.Equal(resolutions, probe.Resolutions);
    }

    private static async Task<HistoricalHttpPage> ReadHistoricalHttpPageAsync(ApiTestHost host, HistoricalHttpProbe probe,
        AgentHistoricalReceiptReviewRequest original) {
        probe.Sql.Reset();
        ConcurrentQueue<long> bytes = new();
        using var root = new Activity("historical-http-page")
            .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded).Start();
        using var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => {
                if (activity.TraceId == root.TraceId && activity.OperationName == "workspace.historical.read" &&
                    activity.GetTagItem("payload.type") is "HistoricalCatalog") {
                    bytes.Enqueue(Assert.IsType<long>(activity.GetTagItem("payload.bytes")));
                }
            }
        };
        ActivitySource.AddActivityListener(listener);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Post, HistoricalHttpRoute(original.ExecutionRunId)) {
            Content = HistoricalJsonContent(JsonSerializer.Serialize(original, HistoricalHttpJson(host)))
        };
        request.Headers.Add("traceparent", root.Id);
        using var response = await host.Client.SendAsync(request, timeout.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        var page = JsonSerializer.Deserialize<AgentHistoricalReceiptReview>(json, HistoricalHttpJson(host));
        Assert.NotNull(page);
        Assert.Equal(original, page.Request);
        Assert.True(Encoding.UTF8.GetByteCount(json) <= AgentHistoricalReceiptReview.MaximumUtf8Bytes);
        using (var document = JsonDocument.Parse(json)) {
            AssertHistoricalWireSanitized(document.RootElement);
        }
        var fingerprint = host.App.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Runtime.Fingerprint;
        Assert.False(string.IsNullOrWhiteSpace(fingerprint));
        Assert.DoesNotContain(fingerprint, json, StringComparison.Ordinal);
        return new(page, json, bytes.ToArray(), probe.Sql.FinalTargets);
    }

    private static void AssertHistoricalHttpAccepted(IEnumerable<AgentToolBusinessIntentId> intents, HistoricalHttpPage response,
        int expectedCatalogReads, int expectedTargets) {
        Assert.Equal(intents, response.Page.Observations.Select(item => item.Binding.IntentId));
        Assert.All(response.Page.Observations, item => Assert.Equal(AgentHistoricalReceiptKnowledge.Accepted, item.Knowledge));
        Assert.Equal(expectedCatalogReads, response.CatalogBytes.Count);
        Assert.All(response.CatalogBytes, count => Assert.True(count > 0));
        Assert.Equal(expectedTargets, response.FinalTargets);
    }

    private static void AssertHistoricalWireSanitized(JsonElement value) {
        if (value.ValueKind == JsonValueKind.Object) {
            foreach (var property in value.EnumerateObject()) {
                Assert.DoesNotContain(property.Name, new[] { "fingerprint", "sourcePreparation", "argumentsJson", "acceptedPayload", "receipt", "backgroundSource" });
                AssertHistoricalWireSanitized(property.Value);
            }
        } else if (value.ValueKind == JsonValueKind.Array) {
            foreach (var item in value.EnumerateArray()) {
                AssertHistoricalWireSanitized(item);
            }
        }
    }

    private sealed record HistoricalHttpPage(AgentHistoricalReceiptReview Page, string Json,
        IReadOnlyList<long> CatalogBytes, int FinalTargets);

    private sealed class HistoricalHttpProbe {
        private int resolutions;
        internal int Resolutions => Volatile.Read(ref resolutions);
        internal HistoricalSqlProbe Sql { get; } = new();

        internal void ObserveRegistration(IServiceCollection services) {
            var descriptor = Assert.Single(services, item => item.ServiceType == typeof(Func<IAgentHistoricalReceiptReviewService>));
            var original = descriptor.ImplementationFactory
                ?? throw new InvalidOperationException("The production historical delegate must retain its service factory registration.");
            services.Remove(descriptor);
            services.Add(new(descriptor.ServiceType, provider => {
                var resolve = Assert.IsType<Func<IAgentHistoricalReceiptReviewService>>(original(provider));
                return new Func<IAgentHistoricalReceiptReviewService>(() => {
                    Interlocked.Increment(ref resolutions);
                    return resolve();
                });
            }, descriptor.Lifetime));
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(Sql), ServiceLifetime.Singleton);
        }
    }

    private sealed class HistoricalStreamingContent : HttpContent {
        private readonly byte[] bytes;

        internal HistoricalStreamingContent(byte[] bytes) {
            this.bytes = bytes;
            Headers.ContentType = new("application/json");
        }

        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();
    }
}
