using System.Globalization;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Chat;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.AgentFramework.Workflows.UI;
using CanDoItAll.Modules.AgentFramework.Pages;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentDeliveryPresentationPolicyTests(ITestOutputHelper output) {
    [Theory]
    [InlineData("notes/report.txt", "notes/report.txt")]
    [InlineData("  notes\\report.txt  ", "notes/report.txt")]
    [InlineData("artifacts/my report.json", "artifacts/my report.json")]
    [InlineData("výstupy/řešení.md", "výstupy/řešení.md")]
    [InlineData("資料/結果.txt", "資料/結果.txt")]
    public void Relative_attachment_paths_have_one_resource_preserving_canonical_value(string input, string expected) {
        Assert.True(RelativeAttachmentPath.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
        Assert.Equal(expected, RelativeAttachmentPath.NormalizeOrNull(actual));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" /host/private.txt ")]
    [InlineData("C:\\host\\private.txt")]
    [InlineData("C:relative.txt")]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("//server/share/file.txt")]
    [InlineData("https://example.test/file")]
    [InlineData("file:notes/file.txt")]
    [InlineData("./notes/file.txt")]
    [InlineData("notes/../file.txt")]
    [InlineData("notes/./file.txt")]
    [InlineData("notes//file.txt")]
    [InlineData("notes\\/file.txt")]
    [InlineData("notes/file.txt/")]
    [InlineData("notes/ file.txt")]
    [InlineData("notes /file.txt")]
    [InlineData("notes./file.txt")]
    [InlineData("notes/file\nname.txt")]
    [InlineData("\nnotes/file.txt")]
    [InlineData("notes/\u202efile.txt")]
    [InlineData("notes/\u200bfile.txt")]
    [InlineData("notes\u2215file.txt")]
    [InlineData("notes\u2044file.txt")]
    [InlineData("notes\uff0ffile.txt")]
    [InlineData("notes\uff3cfile.txt")]
    [InlineData("notes/\uff0e\uff0e/file.txt")]
    [InlineData("notes/%2e%2e/file.txt")]
    [InlineData("notes/file.txt?download=1")]
    public void Unsafe_attachment_paths_are_rejected_without_rewriting_the_resource(string? input) {
        Assert.False(RelativeAttachmentPath.TryNormalize(input, out var actual));
        Assert.Empty(actual);
        Assert.Null(RelativeAttachmentPath.NormalizeOrNull(input));
    }

    [Fact]
    public void Relative_attachment_path_length_is_bounded() {
        Assert.True(RelativeAttachmentPath.TryNormalize(new string('a', RelativeAttachmentPath.MaximumLength), out _));
        Assert.False(RelativeAttachmentPath.TryNormalize(new string('a', RelativeAttachmentPath.MaximumLength + 1), out _));
    }

    [Theory]
    [InlineData("cs-CZ", 12)]
    [InlineData("ar-SA", -7)]
    [InlineData("en-US", 0)]
    public void Chat_and_workflow_times_ignore_culture_and_input_offset(string cultureName, int offsetHours) {
        output.WriteLine($"Local timezone: {TimeZoneInfo.Local.Id}; offset: {TimeZoneInfo.Local.BaseUtcOffset}; requested TZ: {Environment.GetEnvironmentVariable("TZ") ?? "default"}");
        var original = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            var timestamp = new DateTimeOffset(2026, 9, 9, 12, 34, 56, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(offsetHours));
            Assert.Equal("2026-09-09 12:34:56 UTC", ChatPresentationTime.Format(timestamp));
            Assert.Equal("2026-09-09 12:34:56 UTC", WorkflowPresentationTime.Format(timestamp));
        } finally {
            CultureInfo.CurrentCulture = original;
            CultureInfo.CurrentUICulture = originalUi;
        }
    }

    [Theory]
    [InlineData(WorkflowEventKind.Error)]
    [InlineData(WorkflowEventKind.ExecutorFailed)]
    public void Unrecognized_workflow_failure_has_no_raw_technical_or_payload_fallback(WorkflowEventKind kind) {
        var record = Event(kind, "InvalidOperationException: SECRET_FAILURE_482\n   at test-only-PRIVATE_STACK_482() in C:\\test-only-PRIVATE_PATH_482\\file.cs:line 7",
            "{\"exception\":\"SECRET_ENVELOPE_482\"}");
        var display = WorkflowEventPresentationPolicy.Map(record);
        Assert.Empty(display.TechnicalDetail);
        Assert.Empty(display.Payload);
        Assert.DoesNotContain("482", display.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Structured_diagnostic_exposes_only_approved_public_and_redacted_fields() {
        var record = Event(WorkflowEventKind.ExecutorFailed, "test-only-PRIVATE_ORIGINAL_482", "{}");
        var diagnostic = new WorkflowFailureDiagnosticEnvelope(WorkflowFailureKind.Executor, WorkflowFailureRetryability.RetryableAfterRepair,
            "Settings are invalid.", "Repair the settings.", "Safe technical detail: token=[REDACTED]", "test-only-PRIVATE_CORRELATION_482",
            null, null, record.RunId, null, null,
            WorkflowFailureSourceContext.ForTemplate("test-only-PRIVATE_TEMPLATE_482", "C:\\test-only-PRIVATE_PATH_482\\template.json"), record.CreatedAtUtc);
        record = record with { PayloadJson = Envelope(WorkflowEventPayloadSource.Runtime, "WorkflowExecutorFailed", JsonSerializer.Serialize(diagnostic, JsonOptions)) };
        var display = WorkflowEventPresentationPolicy.Map(record);
        Assert.Equal(WorkflowEventContentKind.Diagnostic, display.Kind);
        Assert.Contains("Repair the settings.", display.Summary, StringComparison.Ordinal);
        Assert.Contains("Safe technical detail", display.TechnicalDetail, StringComparison.Ordinal);
        Assert.Empty(display.Payload);
        Assert.DoesNotContain("482", JsonSerializer.Serialize(display), StringComparison.Ordinal);
    }

    [Fact]
    public void Structured_cancellation_retains_its_safe_guidance_and_redacted_detail() {
        var record = Event(WorkflowEventKind.Cancelled, "Workflow run was cancelled.", "{}");
        var diagnostic = new WorkflowFailureDiagnosticEnvelope(WorkflowFailureKind.Cancellation, WorkflowFailureRetryability.NotRetryable,
            "Workflow run was cancelled.", "Start a new workflow run if execution is still required.",
            "Workflow runtime cancellation was requested.", "test-only-PRIVATE_CORRELATION_482",
            null, null, record.RunId, null, null, WorkflowFailureSourceContext.ForTemplate("test-only-source", ""), record.CreatedAtUtc);
        record = record with { PayloadJson = Envelope(WorkflowEventPayloadSource.Runtime, "WorkflowCancelled", JsonSerializer.Serialize(diagnostic, JsonOptions)) };
        var display = WorkflowEventPresentationPolicy.Map(record);
        Assert.Equal(WorkflowEventContentKind.Diagnostic, display.Kind);
        Assert.Contains("Start a new workflow run", display.Summary, StringComparison.Ordinal);
        Assert.Equal("Workflow runtime cancellation was requested.", display.TechnicalDetail);
        Assert.Empty(display.Payload);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(display), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WorkflowEventKind.Output, WorkflowEventPayloadSource.MafNative, "WorkflowOutputEvent", (int)WorkflowEventContentKind.Output)]
    [InlineData(WorkflowEventKind.ExecutorCompleted, WorkflowEventPayloadSource.CanDoItAllProgress, "WorkflowNodeCompleted", (int)WorkflowEventContentKind.Output)]
    [InlineData(WorkflowEventKind.WaitingForInput, WorkflowEventPayloadSource.ExternalRequest, "WorkflowExternalRequest", (int)WorkflowEventContentKind.ExternalRequest)]
    [InlineData(WorkflowEventKind.Completed, WorkflowEventPayloadSource.ExternalRequest, "WorkflowExternalResponse", (int)WorkflowEventContentKind.ExternalResponse)]
    public void Intended_workflow_payload_keeps_only_bounded_inline_product_content(
        WorkflowEventKind kind, WorkflowEventPayloadSource source, string eventType, int expected) {
        const string content = "{\"text\":\"<script>product content</script>\",\"approved\":true}";
        var record = Event(kind, "Safe event summary", Envelope(source, eventType, content));
        var display = WorkflowEventPresentationPolicy.Map(record);
        Assert.Equal((WorkflowEventContentKind)expected, display.Kind);
        Assert.Equal(content, display.Payload);
        Assert.DoesNotContain("test-only-PRIVATE_REFERENCE_482", JsonSerializer.Serialize(display), StringComparison.Ordinal);
        var longDisplay = WorkflowEventPresentationPolicy.Map(record with {
            PayloadJson = Envelope(source, eventType, new string('a', 40_000))
        });
        Assert.Equal(WorkflowEventPresentationPolicy.MaximumPayloadLength, longDisplay.Payload.Length);
    }

    [Theory]
    [InlineData(WorkflowEventKind.Unknown, WorkflowEventPayloadSource.Runtime, "UnknownEnvelope")]
    [InlineData(WorkflowEventKind.Started, WorkflowEventPayloadSource.Runtime, "WorkflowStarted")]
    [InlineData(WorkflowEventKind.Output, WorkflowEventPayloadSource.Runtime, "UnknownEnvelope")]
    [InlineData(WorkflowEventKind.ExecutorCompleted, WorkflowEventPayloadSource.MafNative, "ExecutorCompletedEvent")]
    public void Internal_or_unknown_workflow_envelopes_are_not_dumped(WorkflowEventKind kind, WorkflowEventPayloadSource source, string eventType) {
        var display = WorkflowEventPresentationPolicy.Map(Event(kind, "Safe summary", Envelope(source, eventType, "test-only-PRIVATE_ENVELOPE_482")));
        Assert.Equal(WorkflowEventContentKind.Internal, display.Kind);
        Assert.Empty(display.Payload);
        Assert.Empty(display.TechnicalDetail);
    }

    [Theory]
    [InlineData("workspace_write_file")]
    [InlineData(AgentToolInvocationPolicyMetadata.ProjectStructureNodeUpdate)]
    [InlineData(AgentToolInvocationPolicyMetadata.HrAgentSettingsUpdate)]
    [InlineData("unknown_tool")]
    public void Approval_and_runtime_argument_displays_share_canonical_nested_sanitation(string toolName) {
        var json = JsonSerializer.Serialize(new {
            apiToken = "test-only-PRIVATE_TOP_482", content = "test-only-PRIVATE_CONTENT_482", prompt = "test-only-PRIVATE_PROMPT_482",
            request = new { projectId = "project-42", credentials = "test-only-PRIVATE_NESTED_482", body = "test-only-PRIVATE_BODY_482" },
            items = new[] { new { nodeId = "node-7", password = "test-only-PRIVATE_ARRAY_482" } },
            other = new string('x', 10_000)
        });
        var display = AgentToolArgumentDisplayFormatter.DescribeArguments(json, toolName);
        Assert.Equal(MafToolInvocationArgumentFormatter.DescribeArguments(json, toolName), display);
        Assert.DoesNotContain("PRIVATE_", display, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 100), display, StringComparison.Ordinal);
        Assert.Contains("project-42", display, StringComparison.Ordinal);
        Assert.Contains("node-7", display, StringComparison.Ordinal);
        Assert.InRange(display.Length, 1, AgentToolArgumentDisplayFormatter.MaximumDisplayLength);
    }

    [Theory]
    [InlineData("{\"token\":\"test-only-PRIVATE_MALFORMED_482\"")]
    [InlineData("not JSON test-only-PRIVATE_MALFORMED_482")]
    [InlineData("{\"id\":1,\"id\":2}")]
    [InlineData("[\"test-only-PRIVATE_ARRAY_482\"]")]
    public void Malformed_approval_arguments_have_no_raw_fallback(string json) {
        Assert.Empty(AgentToolArgumentDisplayFormatter.DescribeArguments(json, "workspace_write_file"));
    }

    [Fact]
    public void Argument_display_has_a_total_bound_even_with_many_safe_fields() {
        var arguments = Enumerable.Range(0, 200).ToDictionary(index => $"item{index}Id", _ => (object?)new string('a', 100));
        Assert.Equal(AgentToolArgumentDisplayFormatter.MaximumDisplayLength,
            AgentToolArgumentDisplayFormatter.SummarizeArguments("unknown_tool", arguments).Length);
        arguments["zzzzId"] = "first-target";
        var first = AgentToolArgumentDisplayFormatter.SummarizeArguments("unknown_tool", arguments);
        arguments["zzzzId"] = "second-target";
        Assert.NotEqual(first, AgentToolArgumentDisplayFormatter.SummarizeArguments("unknown_tool", arguments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static WorkflowEventRecord Event(WorkflowEventKind kind, string message, string payload)
        => new(Guid.NewGuid(), WorkflowRunId.New(), kind, null, message, payload, new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
    private static string Envelope(WorkflowEventPayloadSource source, string type, string inline)
        => JsonSerializer.Serialize(new WorkflowEventPayloadEnvelope(source, type, null, null, null, null, inline,
            inline.Length, false, "C:\\test-only-PRIVATE_REFERENCE_482\\file.json"), JsonOptions);
}
