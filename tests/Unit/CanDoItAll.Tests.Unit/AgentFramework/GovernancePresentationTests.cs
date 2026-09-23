using CanDoItAll.AgentFramework.UI.Governance;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class GovernancePresentationTests {
    [Fact]
    public void Presentation_contract_contains_only_explicit_immutable_value_types() {
        var records = new HashSet<Type> {
            typeof(GovernancePresentation), typeof(GovernanceViewState), typeof(GovernanceLaneState),
            typeof(GovernanceAgentOption), typeof(GovernanceRunPresentation), typeof(GovernanceDetailPresentation),
            typeof(GovernanceEntry), typeof(ExecutionMetricsPresentation), typeof(ExecutionMetricTotals)
        };
        var scalars = new HashSet<Type> { typeof(string), typeof(Guid), typeof(int), typeof(decimal), typeof(bool),
            typeof(GovernanceTone), typeof(GovernanceReadPhase) };
        foreach (var record in records) {
            Assert.True(record.IsSealed);
            foreach (var property in record.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ImmutableArray<>)) {
                    type = type.GetGenericArguments()[0];
                }
                Assert.True(scalars.Contains(type) || records.Contains(type),
                    $"Unapproved presentation value type: {record.Name}.{property.Name}");
            }
        }
    }

    [Fact]
    public void Opaque_domain_values_are_omitted_from_every_projected_section() {
        var denied = "opaque-fixture-" + Guid.NewGuid().ToString("N");
        var run = Run() with {
            MetadataJson = denied, SerializedSessionStateJson = denied, InputSummary = denied, ResultSummary = denied,
            StructuredOutputRawOutput = denied, StructuredOutputValidationErrorsJson = denied,
            PendingApprovals = [new("approval", "call", "Tool", "tool", denied, denied)]
        };
        var detail = new ExecutionRunDetail(run, null,
            [new(Guid.NewGuid(), run.AgentId, null, DateTimeOffset.UnixEpoch, ExecutionState.Running, "Execution", denied)], []) {
            Approvals = [new("approval", run.Id, "call", "Tool", "tool", denied, denied,
                ExecutionApprovalStatus.Pending, DateTimeOffset.UnixEpoch, null, "", "", denied)],
            Artifacts = [new(Guid.NewGuid(), run.Id, "output", "Artifact", "artifacts/result.txt", "text/plain", "Tool", denied, DateTimeOffset.UnixEpoch)],
            Checkpoints = [new(Guid.NewGuid(), run.Id, denied, denied, "Saved", ExecutionState.Running, [denied],
                DateTimeOffset.UnixEpoch, null, denied, denied, denied, denied, denied, denied, denied, denied, denied)]
        };
        var presentation = GovernancePresentationMapping.Detail(detail, "Agent");
        Assert.False(JsonSerializer.Serialize(presentation).Contains(denied, StringComparison.Ordinal),
            "An opaque domain field entered the allowlisted presentation.");
        Assert.Single(presentation.Approvals);
        Assert.Single(presentation.Artifacts);
        Assert.Single(presentation.Checkpoints);
        Assert.Single(presentation.Timeline);
    }

    [Fact]
    public void Mutable_domain_collections_cannot_change_a_presentation() {
        var run = Run();
        var logs = new List<ExecutionLogEntry> { new(Guid.NewGuid(), run.AgentId, null,
            DateTimeOffset.UnixEpoch, ExecutionState.Running, "Planning", "Private execution prose") };
        var detail = new ExecutionRunDetail(run, null, logs, []);
        var presentation = GovernancePresentationMapping.Detail(detail, "Agent");
        logs.Clear();
        Assert.Single(presentation.Timeline);
        Assert.Equal("Planning", presentation.Timeline[0].Title);
    }

    [Fact]
    public void Section_and_timeline_limits_preserve_total_counts() {
        var run = Run();
        var metrics = Enumerable.Range(0, 40).Select(i => new AgentRunMetric(Guid.NewGuid(), run.AgentId, null,
            DateTimeOffset.UnixEpoch.AddMinutes(i), RunOutcome.Succeeded, "Provider", "model", 100, 3, 4, 1)).ToArray();
        var logs = Enumerable.Range(0, 40).Select(i => new ExecutionLogEntry(Guid.NewGuid(), run.AgentId, null,
            DateTimeOffset.UnixEpoch.AddMinutes(i), ExecutionState.Running, "Phase", "Not presented")).ToArray();
        var approvals = Enumerable.Range(0, 40).Select(i => new ExecutionApprovalRecord(i.ToString(CultureInfo.InvariantCulture), run.Id,
            "call", "Tool", "tool", "Not presented", "{}", ExecutionApprovalStatus.Pending,
            DateTimeOffset.UnixEpoch.AddMinutes(i), null, "", "", "")).ToArray();
        var projected = GovernancePresentationMapping.Detail(new(run, null, logs, metrics) { Approvals = approvals }, "Agent");
        Assert.Equal(40, projected.ApprovalCount);
        Assert.Equal(GovernancePresentationMapping.SectionRowLimit, projected.Approvals.Length);
        Assert.Equal(12, projected.Timeline.Length);
        Assert.Equal(10, projected.Metrics.Rows.Length);
        Assert.Equal(40, projected.Metrics.Totals.Runs);
        Assert.Equal(120, projected.Metrics.Totals.InputTokens);
    }

    [Fact]
    public void Label_truncation_is_bounded_and_does_not_split_unicode() {
        var value = string.Concat(Enumerable.Repeat("a😀", 1000));
        var result = GovernancePresentationMapping.Text(value);
        Assert.True(result.Length <= GovernancePresentationMapping.LabelLimit);
        Assert.EndsWith("…", result);
        Assert.DoesNotContain("\uFFFD", result);
        Assert.Equal("safe label", GovernancePresentationMapping.Text("  safe  label\u202E"));
    }

    [Theory]
    [InlineData("en-US", 840)]
    [InlineData("cs-CZ", -720)]
    [InlineData("en-US", -240)]
    [InlineData("cs-CZ", 120)]
    public void Timestamp_is_UTC_and_culture_independent(string culture, int offsetMinutes) {
        var prior = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var value = new DateTimeOffset(2026, 10, 25, 1, 30, 0, TimeSpan.Zero).ToOffset(TimeSpan.FromMinutes(offsetMinutes));
            Assert.Equal("2026-10-25 01:30:00 UTC", GovernancePresentationMapping.Timestamp(value));
            Assert.Equal("1234.5", GovernancePresentationMapping.Number(1234.5m));
        } finally {
            CultureInfo.CurrentCulture = prior;
        }
    }

    [Fact]
    public void Missing_timestamp_is_explicit() => Assert.Equal("Not recorded", GovernancePresentationMapping.Timestamp(null));

    [Theory]
    [InlineData("/private/result.txt", false)]
    [InlineData("Z:/private/result.txt", false)]
    [InlineData("//host/private/result.txt", false)]
    [InlineData("https://example.invalid/result", false)]
    [InlineData("artifacts/../private/result.txt", false)]
    [InlineData("artifacts/result.txt", true)]
    public void Artifact_paths_accept_relative_display_only(string path, bool allowed)
        => Assert.Equal(allowed ? path : "Artifact path omitted", GovernancePresentationMapping.RelativeArtifactPath(path));

    [Theory]
    [InlineData("  /private/output.txt  ")]
    [InlineData("  C:\\private\\output.txt ")]
    [InlineData(" \\host\\share\\output.txt ")]
    [InlineData("file:///private/output.txt")]
    [InlineData("//host/share/output.txt")]
    [InlineData("https://host/output?token=fixture")]
    [InlineData("output/file?query")]
    [InlineData("output/file#fragment")]
    [InlineData("output/./file")]
    [InlineData("output/../file")]
    [InlineData("output//file")]
    [InlineData("output/file/")]
    [InlineData("output/ .. /file")]
    [InlineData("output/\u202efile")]
    [InlineData("output/file\t")]
    [InlineData("\uff0fprivate\uff0ffile")]
    [InlineData("output\u2215file")]
    [InlineData("\u2044private/file")]
    [InlineData("\uff3cprivate\\file")]
    [InlineData("%2Fprivate/file")]
    public void Ambiguous_artifact_paths_are_omitted(string value)
        => Assert.Equal("Artifact path omitted", GovernancePresentationMapping.RelativeArtifactPath(value));

    [Theory]
    [InlineData("  artifacts/report.txt  ", "artifacts/report.txt")]
    [InlineData("artifacts\\report.txt", "artifacts/report.txt")]
    [InlineData("reports/quarter one.txt", "reports/quarter one.txt")]
    [InlineData("reports/re\u0301sume\u0301.txt", "reports/r\u00e9sum\u00e9.txt")]
    public void Relative_artifact_paths_are_normalized_before_display(string value, string expected)
        => Assert.Equal(expected, GovernancePresentationMapping.RelativeArtifactPath(value));

    [Fact]
    public void Reusable_poison_domain_fixture_cannot_enter_any_presentation_record() {
        var source = CanDoItAll.AgentFramework.UiSandbox.GovernancePoisonFixture.Create();
        var sourceJson = JsonSerializer.Serialize(source);
        var mapped = JsonSerializer.Serialize(GovernancePresentationMapping.Detail(source, "Fixture agent"));
        foreach (var sentinel in CanDoItAll.AgentFramework.UiSandbox.GovernancePoisonFixture.Sentinels) {
            Assert.Contains(sentinel, sourceJson);
            Assert.DoesNotContain(sentinel, mapped);
        }
    }

    private static ExecutionRunRecord Run() => new(Guid.NewGuid(), Guid.NewGuid(), null, "Run", "manual", "", "", "", "", "",
        "{}", "Input", "Result", "Provider", "model", ExecutionState.Completed, RunOutcome.Succeeded,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, null, "", null, []);
}
