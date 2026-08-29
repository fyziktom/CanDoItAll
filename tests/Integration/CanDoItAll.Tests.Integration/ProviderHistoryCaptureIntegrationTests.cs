using System.Net;
using System.Text;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.ProviderRuntime;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryCaptureIntegrationTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_driver_retry_commits_before_send_and_never_duplicates_canonical_content(bool canonical) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        await EnableDetailsAsync(fixture);
        using var handler = new WireHandler(fixture) { EmptyFirst = true };
        using var http = new HttpClient(handler);
        var factory = CreateFactory(fixture, http);
        var descriptors = new ProviderProfileRuntimeDescriptorStore();
        await using var pool = new ProviderRuntimePool(descriptors, new ProviderRuntimeHandleFactory(factory));
        var provider = Provider();
        var context = HistoryInvocationContext.Create(canonical ? HistoryWorkload.SimpleChat : HistoryWorkload.Direct,
            owner: canonical ? new(HistorySourceKind.SimpleChat, new("chat"), new("turn")) : null,
            currentTurn: new("current fixture-secret-token", 2));
        var response = await new ProviderBackedLlmInvocationAdapter(descriptors, pool).InvokeAsync(
            new(provider, provider.DefaultModel, [new(LlmMessageRole.System, "prior-system-not-captured"),
                new(LlmMessageRole.User, "prior-user-not-captured"), new(LlmMessageRole.User, "current fixture-secret-token")]) {
                History = context
            });
        Assert.Equal("answer fixture-secret-token", response.ResponseText);
        Assert.Equal(2, handler.Calls);
        await using var db = fixture.Factory.CreateDbContext();
        var entries = await db.Set<HistoryEntryRow>().OrderBy(row => row.StartedAtUtc).ToArrayAsync();
        Assert.Equal(2, entries.Length);
        Assert.All(entries, row => {
            Assert.Equal(HistoryOutcome.Succeeded, row.Outcome);
            Assert.Equal(context.RequestId.Value, row.RequestId);
            Assert.Equal(10, row.InputTokens);
            Assert.Equal(5, row.OutputTokens);
            Assert.Equal(0, row.CachedInputTokens);
        });
        Assert.Equal(2, entries.Select(row => row.AttemptId).Distinct().Count());
        var bodies = await db.Set<HistoryDetailRow>().ToArrayAsync();
        if (canonical) {
            Assert.Empty(bodies);
            Assert.All(entries, row => Assert.Equal(HistoryDetailState.PendingCanonical, row.DetailState));
            Assert.Equal(2, await db.Set<HistoryOwnerRow>().CountAsync());
        } else {
            Assert.Equal(3, bodies.Length);
            var input = Assert.Single(bodies, row => row.Part == HistoryDetailPart.Input);
            Assert.Equal("current [redacted]", fixture.Text.Read(input).Text);
            Assert.All(bodies, row => Assert.DoesNotContain("prior-", fixture.Text.Read(row).Text, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_response_redacts_original_credential_after_rotation_even_without_input(bool hasInput) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        await EnableDetailsAsync(fixture);
        using var handler = new WireHandler(fixture) { RotateCredential = true };
        using var http = new HttpClient(handler);
        var driver = CreateFactory(fixture, http).Resolve<IProviderChatCompletionDriver>(ProviderKind.OpenAi);
        var provider = Provider();
        await driver.CompleteChatAsync(new(provider, provider.DefaultModel, "", [], "current") {
            History = HistoryInvocationContext.Create(HistoryWorkload.Direct,
                currentTurn: hasInput ? new("current", 0) : null)
        });
        await using var db = fixture.Factory.CreateDbContext();
        var body = await db.Set<HistoryDetailRow>().SingleAsync(row => row.Part == HistoryDetailPart.Response);
        Assert.Equal("answer [redacted]", fixture.Text.Read(body).Text);
        Assert.Equal("rotated-fixture-token", fixture.Secrets.Current);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_stream_records_nullable_usage_and_body_after_terminal_frame(bool shared) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        await EnableDetailsAsync(fixture);
        using var handler = new WireHandler(fixture) { Streaming = true };
        using var http = new HttpClient(handler);
        var driver = CreateFactory(fixture, http).Resolve<IProviderStreamingChatCompletionDriver>(ProviderKind.OpenAi);
        var provider = Provider();
        var sourceId = Guid.NewGuid();
        if (shared) {
            provider = provider with { CredentialBinding = new(Guid.NewGuid(), ProviderCredentialPurpose.SourceAccessToken,
                ProviderCredentialConsumerKind.Source, sourceId), ModelSelectionConstraint = new([provider.DefaultModel]) };
        }
        var updates = new List<ProviderChatStreamingUpdate>();
        await foreach (var update in driver.StreamChatAsync(new(provider, provider.DefaultModel, "", [], "current"))) {
            updates.Add(update);
        }
        Assert.Contains(updates, update => update is ProviderChatTextDelta);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(HistoryOutcome.Succeeded, entry.Outcome);
        Assert.Equal(10, entry.InputTokens);
        Assert.Equal(5, entry.OutputTokens);
        Assert.Null(entry.CacheWriteTokens);
        Assert.Equal(shared ? sourceId : (Guid?)null, entry.RemoteSourceId);
        Assert.Equal(shared ? "publisher-fixture-request" : null, entry.RemoteRequestId);
        Assert.Equal(2, await db.Set<HistoryDetailRow>().CountAsync());
    }

    private static async Task EnableDetailsAsync(HistoryPersistenceTestDatabase fixture) {
        await using var db = fixture.Factory.CreateDbContext();
        (await db.Set<HistoryPolicyRow>().SingleAsync()).CaptureMode = HistoryCaptureMode.Detailed;
        await db.SaveChangesAsync();
    }

    private static HistoryProviderDriverFactory CreateFactory(HistoryPersistenceTestDatabase fixture, HttpClient http) {
        var recorder = new HistoryInvocationRecorder(new HistoryPartitionStore(fixture.Factory), fixture.Factory,
            fixture.Runtime, fixture.Capture, fixture.Clock, NullLogger<HistoryInvocationRecorder>.Instance);
        return new(new AgentProviderDriverRegistryBuilder().AddDriver(new OpenAiProviderDriver(http, new Credentials())).Build(),
            recorder, fixture.Clock);
    }

    private static ProviderProfile Provider() => new(Guid.NewGuid(), "History wire fixture", ProviderKind.OpenAi,
        "https://example.invalid/v1", "", "history-model", ProviderTransportKind.ChatCompletions,
        true, true, false, true, false, "{}", "", "", null, ["history-model"]);

    private sealed class Credentials : IProviderDriverCredentialResolver {
        public ProviderDriverCredential Resolve(ProviderProfile provider) => ProviderDriverCredential.Resolved("fixture-secret-token");
    }

    private sealed class WireHandler(HistoryPersistenceTestDatabase fixture) : HttpMessageHandler {
        internal int Calls { get; private set; }
        internal bool EmptyFirst { get; init; }
        internal bool RotateCredential { get; init; }
        internal bool Streaming { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Calls++;
            await using var db = fixture.Factory.CreateDbContext();
            var started = await db.Set<HistoryEntryRow>().Where(row => row.Outcome == HistoryOutcome.Started).ToArrayAsync(cancellationToken);
            Assert.Single(started);
            Assert.Equal(Calls, await db.Set<HistoryEntryRow>().CountAsync(cancellationToken));
            var payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.DoesNotContain("History", payload, StringComparison.Ordinal);
            if (RotateCredential) {
                fixture.Secrets.Current = "rotated-fixture-token";
            }
            var answer = EmptyFirst && Calls == 1 ? "" : "answer fixture-secret-token";
            var json = System.Text.Json.JsonSerializer.Serialize(new {
                id = "fixture", model = "history-model",
                choices = new[] { new { index = 0, message = new { role = "assistant", content = answer }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 10, completion_tokens = 5, prompt_tokens_details = new { cached_tokens = 0 } }
            });
            var stream = """
                data: {"choices":[{"delta":{"content":"answer"},"finish_reason":null}]}

                data: {"choices":[],"usage":{"prompt_tokens":10,"completion_tokens":5,"prompt_tokens_details":{"cached_tokens":0}}}

                data: [DONE]


                """;
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Streaming ? stream : json, Encoding.UTF8,
                Streaming ? "text/event-stream" : "application/json") };
            response.Headers.Add("CanDoItAll-Request-Id", "publisher-fixture-request");
            return response;
        }
    }
}
