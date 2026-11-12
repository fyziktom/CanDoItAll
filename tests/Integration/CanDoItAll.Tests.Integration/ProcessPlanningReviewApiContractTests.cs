using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ProcessPlanningReviewApiContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    [Fact]
    public void Revision_strings_round_trip_beyond_JavaScript_precision_and_completion_is_the_original_action() {
        var request = Submission();
        var text = JsonSerializer.Serialize(request, Json);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            document.RootElement.GetProperty("process").GetProperty("decisionRevision").GetString());
        Assert.Equal(JsonValueKind.String,
            document.RootElement.GetProperty("agent").GetProperty("expected").GetProperty("runRevision").ValueKind);
        var read = JsonSerializer.Deserialize<ProcessPlanningReviewApiSubmission>(text, Json)!;
        Assert.Equal(request, read);
        var domain = read.ToDomain(request.Process.RunId, request.Process.StepInstanceId, request.Process.ExecutionRunId);
        Assert.Equal(request.Agent.ActionId, domain.Process.CompletionKey.Value);
        Assert.Equal(long.MaxValue, domain.Process.Decision.Revision);
        Assert.Equal(long.MaxValue - 1, domain.Process.Budget.Revision);
        Assert.Equal(request.Process, ProcessPlanningReviewApiExpectation.From(domain.Process));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("01")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.0")]
    [InlineData("1e1")]
    [InlineData("9223372036854775808")]
    [InlineData(null)]
    public void Noncanonical_or_missing_revisions_are_not_coerced(string? revision) {
        var request = Submission();
        foreach (var expectation in new[] {
                request.Process with { DecisionRevision = revision! },
                request.Process with { BudgetRevision = revision! }
            }) {
            Assert.Throws<ArgumentException>(() => expectation.ToDomain(request.Agent.ActionId));
        }
    }

    [Theory]
    [InlineData("rootRunId")]
    [InlineData("runId")]
    [InlineData("stepInstanceId")]
    [InlineData("executionRunId")]
    [InlineData("decisionId")]
    [InlineData("waitingResultKey")]
    [InlineData("budgetId")]
    public void Empty_original_identities_fail_before_owner_dispatch(string member) {
        var request = Submission();
        var body = JsonSerializer.SerializeToNode(request, Json)!;
        body["process"]![member] = Guid.Empty;
        var read = body.Deserialize<ProcessPlanningReviewApiSubmission>(Json)!;
        var error = Record.Exception(() => read.ToDomain(request.Process.RunId, request.Process.StepInstanceId, request.Process.ExecutionRunId));
        Assert.True(error is ArgumentException or InvalidDataException, error?.ToString());
    }

    [Fact]
    public void Route_substitution_and_empty_activity_do_not_create_a_command() {
        var request = Submission();
        Assert.Throws<ArgumentException>(() => request.ToDomain(Guid.NewGuid(), request.Process.StepInstanceId, request.Process.ExecutionRunId));
        Assert.Throws<ArgumentException>(() => request.ToDomain(request.Process.RunId, Guid.NewGuid(), request.Process.ExecutionRunId));
        Assert.Throws<ArgumentException>(() => request.ToDomain(request.Process.RunId, request.Process.StepInstanceId, Guid.NewGuid()));
        var activity = request with { ActivityOperationId = default(AgentExecutionOperationId) };
        Assert.Throws<ArgumentException>(() => activity.ToDomain(request.Process.RunId, request.Process.StepInstanceId, request.Process.ExecutionRunId));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("issuer")]
    [InlineData("permissions")]
    [InlineData("completionKey")]
    [InlineData("DecisionRevision")]
    public void Added_authority_identity_or_wrong_case_members_are_rejected(string member) {
        var body = JsonSerializer.SerializeToNode(Submission(), Json)!;
        body["process"]![member] = "not-authority";
        Assert.Throws<JsonException>(() => body.Deserialize<ProcessPlanningReviewApiSubmission>(Json));
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("process")]
    public void Missing_required_owner_request_is_rejected(string member) {
        var body = JsonSerializer.SerializeToNode(Submission(), Json)!.AsObject();
        Assert.True(body.Remove(member));
        Assert.Throws<JsonException>(() => body.Deserialize<ProcessPlanningReviewApiSubmission>(Json));
    }

    internal static ProcessPlanningReviewApiSubmission Submission() {
        var child = Guid.NewGuid();
        var decision = Guid.NewGuid();
        return new(new(Guid.NewGuid(), AgentPlanningReviewChoice.Approve,
                new(child, long.MaxValue, long.MaxValue - 1, Guid.NewGuid(), 1),
                new(new(Guid.NewGuid()), new(Guid.NewGuid()), 1, AgentToolProtocolEnvelope.ComputeDigest("review-proposal")),
                Guid.NewGuid().ToString("D")),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), child, decision, 1,
                "9223372036854775807", "project-task-plan", Guid.NewGuid(), Guid.NewGuid(), "9223372036854775806"));
    }
}
