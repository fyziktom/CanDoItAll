using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkspaceSettingsHttpOutcomeTests {
    [Fact]
    public async Task Postcommit_activity_failure_returns_the_exact_accepted_snapshot_and_typed_diagnostic() {
        var activity = new ActivityFault();
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton<IActivityStream>(activity));
        activity.Fail = true;
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        var request = new ReplaceWorkspaceSettingsCommand(original.Expectation, WorkspaceSettingsValues.Capture(new() {
            WorkspaceName = " Normalized HTTP ", DefaultPromptOutputFormat = " JSON ",
            CurrencyCode = "eur", CurrencyCultureName = "de-DE", Notes = " Six fields "
        }));
        using var saved = await host.Client.PutAsJsonAsync("/api/settings/workspace", request);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await saved.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>());
        Assert.Equal(WorkspaceSettingsSecondaryDiagnostic.ActivityPending, accepted.Diagnostic);
        var observed = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        Assert.Equal(accepted.Snapshot, observed);
        Assert.Equal("Normalized HTTP", accepted.Snapshot.Values.WorkspaceName);
        Assert.Equal("JSON", accepted.Snapshot.Values.DefaultPromptOutputFormat);
        Assert.Equal("EUR", accepted.Snapshot.Values.CurrencyCode);
        Assert.Equal("de-DE", accepted.Snapshot.Values.CurrencyCultureName);
        Assert.Equal("Six fields", accepted.Snapshot.Values.Notes);
        Assert.Null(accepted.Snapshot.Values.DefaultProviderProfileId);
        Assert.NotEqual(original.Expectation, accepted.Snapshot.Expectation);
        var json = JsonSerializer.SerializeToNode(request, JsonSerializerOptions.Web)!.AsObject();
        json["isAdmin"] = true;
        using var rejected = await host.Client.PutAsJsonAsync("/api/settings/workspace", json);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var missing = await host.Client.PutAsJsonAsync("/api/settings/workspace", new { workspaceName = "Incomplete" });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(1, activity.SettingsWrites);
    }

    [Fact]
    public async Task An_original_head_can_accept_once_and_a_stale_http_draft_is_preserved_as_conflict() {
        await using var host = await ApiTestHost.CreateAsync(false);
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        var first = new ReplaceWorkspaceSettingsCommand(original.Expectation,
            new("Winner", null, "Markdown", "USD", "en-US", "Original winner"));
        using var acceptedResponse = await host.Client.PutAsJsonAsync("/api/settings/workspace", first);
        Assert.Equal(HttpStatusCode.OK, acceptedResponse.StatusCode);
        var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await acceptedResponse.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>());
        var stale = new ReplaceWorkspaceSettingsCommand(original.Expectation,
            new("Stale draft", null, "JSON", "EUR", "de-DE", "Keep for comparison"));
        using var refused = await host.Client.PutAsJsonAsync("/api/settings/workspace", stale);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal(WorkspaceSettingsRejectionReason.HeadChanged,
            Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(await refused.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>()).Reason);
        Assert.Equal(accepted.Snapshot, await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"));
        Assert.Equal(original.Expectation, stale.Expected);
        Assert.Equal("Stale draft", stale.Values.WorkspaceName);
    }

    [Fact]
    public async Task Malformed_original_expectations_duplicate_values_and_oversized_bodies_cannot_change_the_head() {
        await using var host = await ApiTestHost.CreateAsync(false);
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        var command = new ReplaceWorkspaceSettingsCommand(original.Expectation, original.Values);
        var valid = JsonSerializer.Serialize(command, JsonSerializerOptions.Web);
        var expected = JsonSerializer.Serialize(original.Expectation, JsonSerializerOptions.Web);
        var values = JsonSerializer.Serialize(original.Values, JsonSerializerOptions.Web);
        var cases = new[] {
            "{}", "null", "[]", "{\"expected\":null,\"values\":" + values + "}",
            "{\"expected\":" + expected + ",\"expected\":" + expected + ",\"values\":" + values + "}",
            "{\"expected\":" + expected + ",\"values\":{\"workspaceName\":\"first\",\"WorkspaceName\":\"second\"}}",
            "{\"expected\":" + expected + ",\"Values\":" + values.Replace("\"workspaceName\":", "\"WorkspaceName\":\"duplicate\",\"workspaceName\":", StringComparison.Ordinal) + "}",
            valid.Replace("\"state\":\"existing\"", "\"state\":\"empty\"", StringComparison.Ordinal)
        };
        Assert.Equal(WorkspaceSettingsHeadState.Existing, original.Expectation.State);
        foreach (var body in cases) {
            using var response = await host.Client.PutAsync("/api/settings/workspace", new StringContent(body, Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(WorkspaceSettingsRejectionReason.InvalidValues,
                Assert.IsType<WorkspaceSettingsSaveOutcome.Rejected>(await response.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>()).Reason);
        }
        using var oversized = await host.Client.PutAsync("/api/settings/workspace",
            new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.NotEmpty((await oversized.Content.ReadFromJsonAsync<JsonObject>())!["errors"]!.AsArray());
        Assert.Equal(original, await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"));
    }

    [Fact]
    public async Task A_later_writer_does_not_replace_the_first_http_acceptance_with_a_current_read() {
        var boundary = new ActivityBoundary();
        await using var host = await ApiTestHost.CreateAsync(false, services => services.AddSingleton<IActivityStream>(boundary));
        var original = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
        boundary.Hold = true;
        var first = host.Client.PutAsJsonAsync("/api/settings/workspace", new ReplaceWorkspaceSettingsCommand(original.Expectation,
            new("First accepted", null, "Markdown", "USD", "en-US", "Exact first result")));
        try {
            await boundary.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var between = (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!;
            using var later = await host.Client.PutAsJsonAsync("/api/settings/workspace", new ReplaceWorkspaceSettingsCommand(between.Expectation,
                new("Later accepted", null, "Markdown", "USD", "en-US", "Current result")));
            Assert.Equal(HttpStatusCode.OK, later.StatusCode);
            boundary.Release.TrySetResult();
            using var firstResponse = await first;
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            var accepted = Assert.IsType<WorkspaceSettingsSaveOutcome.Accepted>(await firstResponse.Content.ReadFromJsonAsync<WorkspaceSettingsSaveOutcome>());
            Assert.Equal(between, accepted.Snapshot);
            Assert.Equal("First accepted", accepted.Snapshot.Values.WorkspaceName);
            Assert.Equal("Later accepted", (await host.Client.GetFromJsonAsync<WorkspaceSettingsSnapshot>("/api/settings/workspace"))!.Values.WorkspaceName);
        } finally {
            boundary.Release.TrySetResult();
            using var completed = await first;
        }
    }

    [Fact]
    public async Task Emitted_schema_requires_the_original_head_and_exposes_closed_attempt_outcomes() {
        await using var host = await ApiTestHost.CreateAsync(false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var path = root.GetProperty("paths").GetProperty("/api/settings/workspace");
        var write = path.GetProperty("put");
        var command = Resolve(write.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal(new[] { "expected", "values" }, Names(command.GetProperty("properties")));
        Assert.False(command.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "expected", "values" }, command.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        var expected = Property(command, "expected");
        var variants = expected.GetProperty("oneOf").EnumerateArray().Select(Resolve).ToArray();
        Assert.Equal(2, variants.Length);
        Assert.Equal("state", expected.GetProperty("discriminator").GetProperty("propertyName").GetString());
        foreach (var variant in variants) {
            Assert.False(variant.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(new[] { "head", "namespace", "state" }, Names(variant.GetProperty("properties")));
            Assert.Equal("uuid", Property(variant, "namespace").GetProperty("format").GetString());
            var state = Assert.Single(Property(variant, "state").GetProperty("enum").EnumerateArray()).GetString();
            var head = Property(variant, "head");
            if (state == "empty") {
                Assert.Equal("null", head.GetProperty("type").GetString());
            } else {
                Assert.Equal("existing", state);
                Assert.Equal(new[] { "id", "revision" }, Names(head.GetProperty("properties")));
                Assert.DoesNotMatch(Property(head, "revision").GetProperty("pattern").GetString()!, Guid.Empty.ToString("D"));
            }
        }
        var values = Property(command, "values");
        Assert.Equal(new[] { "currencyCode", "currencyCultureName", "defaultPromptOutputFormat", "defaultProviderProfileId", "notes", "workspaceName" }, Names(values.GetProperty("properties")));
        Assert.Equal(6, values.GetProperty("required").GetArrayLength());
        Assert.Equal(8192, Property(values, "notes").GetProperty("maxLength").GetInt32());
        Assert.Contains(Property(values, "defaultProviderProfileId").GetProperty("anyOf").EnumerateArray(), value => value.TryGetProperty("type", out var type) && type.GetString() == "null");
        foreach (var (status, discriminator) in new[] { ("200", "accepted"), ("400", "rejected"), ("409", "rejected"), ("503", "unconfirmed") }) {
            var outcome = Resolve(write.GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/json").GetProperty("schema"));
            Assert.Equal(discriminator, Assert.Single(Property(outcome, "status").GetProperty("enum").EnumerateArray()).GetString());
            Assert.False(outcome.GetProperty("additionalProperties").GetBoolean());
            if (discriminator == "accepted") {
                Assert.Equal(new[] { "diagnostic", "snapshot", "status" }, Names(outcome.GetProperty("properties")));
                Assert.Equal(new[] { "expectation", "values" }, Names(Property(outcome, "snapshot").GetProperty("properties")));
            } else if (discriminator == "unconfirmed") {
                Assert.Equal(new[] { "status" }, Names(outcome.GetProperty("properties")));
            }
        }

        JsonElement Property(JsonElement schema, string name) => Resolve(schema.GetProperty("properties").GetProperty(name));

        JsonElement Resolve(JsonElement schema) {
            while (true) {
                if (schema.TryGetProperty("$ref", out var reference)) {
                    schema = root;
                    foreach (var segment in reference.GetString()![2..].Split('/')) {
                        schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
                    }
                } else if (schema.TryGetProperty("allOf", out var allOf)) {
                    Assert.Equal(new[] { "allOf", "description" }, Names(schema));
                    schema = Assert.Single(allOf.EnumerateArray());
                } else {
                    return schema;
                }
            }
        }

        static string[] Names(JsonElement value) => value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();
    }

    private sealed class ActivityBoundary : IActivityStream {
        private int writes;
        public bool Hold { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (Hold && Interlocked.Increment(ref writes) == 1) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class ActivityFault : IActivityStream {
        public bool Fail { get; set; }
        public int SettingsWrites { get; private set; }
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (!Fail) {
                return Task.CompletedTask;
            }
            SettingsWrites++;
            throw new IOException("Owned activity acknowledgement failure.");
        }
    }
}
