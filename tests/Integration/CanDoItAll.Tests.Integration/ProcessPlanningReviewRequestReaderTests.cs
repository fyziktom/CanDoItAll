using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration.Api;

public sealed class ProcessPlanningReviewRequestReaderTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Probe(string Name);

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"name\":null}")]
    [InlineData("{\"Name\":\"wrong-case\"}")]
    [InlineData("{\"name\":\"first\",\"name\":\"second\"}")]
    [InlineData("{\"name\":\"first\",\"Name\":\"second\"}")]
    [InlineData("{\"name\":\"valid\",\"unknown\":1}")]
    public async Task Ambiguous_or_incomplete_JSON_is_rejected(string body) {
        var request = Request(body);
        await Assert.ThrowsAsync<JsonException>(() => ProcessPlanningReviewRequestReader.ReadAsync<Probe>(request, Json, default));
    }

    [Fact]
    public async Task Deep_nested_JSON_is_rejected_before_deserialization() {
        var request = Request("{\"nested\":" + new string('[', 18) + "0" + new string(']', 18) + "}");
        await Assert.ThrowsAnyAsync<JsonException>(() =>
            ProcessPlanningReviewRequestReader.ReadAsync<Dictionary<string, JsonElement>>(request, Json, default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_known_or_streamed_body_is_bounded_before_dispatch(bool knownLength) {
        var request = Request(new string(' ', ProcessPlanningReviewRequestReader.MaximumUtf8Bytes * 4));
        request.ContentLength = knownLength ? request.Body.Length : null;

        var exception = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
            ProcessPlanningReviewRequestReader.ReadAsync<Probe>(request, Json, default));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
        Assert.Equal(knownLength ? 0 : ProcessPlanningReviewRequestReader.MaximumUtf8Bytes + 1, request.Body.Position);
    }

    [Fact]
    public async Task Exact_body_limit_remains_accepted() {
        var emptySize = JsonSerializer.SerializeToUtf8Bytes(new Probe(""), Json).Length;
        var value = new Probe(new string('x', ProcessPlanningReviewRequestReader.MaximumUtf8Bytes - emptySize));
        var request = Request(JsonSerializer.Serialize(value, Json));
        Assert.Equal(ProcessPlanningReviewRequestReader.MaximumUtf8Bytes, request.Body.Length);

        Assert.Equal(value, await ProcessPlanningReviewRequestReader.ReadAsync<Probe>(request, Json, default));
    }

    [Fact]
    public async Task Non_JSON_content_is_refused_without_reading_the_body() {
        var request = Request("{\"name\":\"valid\"}");
        request.ContentType = "text/plain";

        var exception = await Assert.ThrowsAsync<BadHttpRequestException>(() =>
            ProcessPlanningReviewRequestReader.ReadAsync<Probe>(request, Json, default));

        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, exception.StatusCode);
        Assert.Equal(0, request.Body.Position);
    }

    [Fact]
    public async Task Accounting_request_retains_large_revisions_and_exact_identity_through_JSON() {
        var value = AccountingRequest();
        var text = JsonSerializer.Serialize(value, Json);
        using var document = JsonDocument.Parse(text);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("expected").GetProperty("runRevision").ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("process").GetProperty("decisionRevision").ValueKind);
        var actual = await ProcessPlanningReviewRequestReader.ReadAsync<ProcessPlanningAccountingApiSubmission>(Request(text), Json, default);

        Assert.Equal(value, actual);
        var request = actual.ToDomain(value.Process.RunId, value.Process.StepInstanceId, value.Process.ExecutionRunId);
        Assert.Equal(value.Expected, request.Expected);
        Assert.Equal(value.ActionId, request.Process.CompletionKey.Value);
        Assert.Equal(9_007_199_254_740_993, request.Process.Decision.Revision);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("01")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("9223372036854775808")]
    public void Accounting_requires_canonical_positive_Int64_revisions(string revision) {
        var value = AccountingRequest();
        value = value with { Process = value.Process with { DecisionRevision = revision } };
        Assert.Throws<ArgumentException>(() => value.ToDomain(value.Process.RunId, value.Process.StepInstanceId, value.Process.ExecutionRunId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Accounting_requires_exact_route_identity(int changedPart) {
        var value = AccountingRequest();
        Assert.Throws<ArgumentException>(() => value.ToDomain(
            changedPart == 0 ? Guid.NewGuid() : value.Process.RunId,
            changedPart == 1 ? Guid.NewGuid() : value.Process.StepInstanceId,
            changedPart == 2 ? Guid.NewGuid() : value.Process.ExecutionRunId));
    }

    [Fact]
    public void Accounting_cannot_substitute_another_Agent_or_empty_action() {
        var value = AccountingRequest();
        var changed = value with { Expected = new(Guid.NewGuid(), 1, 1, Guid.NewGuid(), 1) };
        Assert.Throws<ArgumentException>(() => changed.ToDomain(value.Process.RunId, value.Process.StepInstanceId, value.Process.ExecutionRunId));
        changed = value with { ActionId = Guid.Empty };
        Assert.Throws<ArgumentException>(() => changed.ToDomain(value.Process.RunId, value.Process.StepInstanceId, value.Process.ExecutionRunId));
    }

    private static HttpRequest Request(string text) {
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json";
        request.Body = new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
        return request;
    }

    private static ProcessPlanningAccountingApiSubmission AccountingRequest() {
        var child = Guid.NewGuid();
        return new(Guid.NewGuid(), new(child, 9_007_199_254_740_993, 8, Guid.NewGuid(), 1),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), child, Guid.NewGuid(), 1,
                "9007199254740993", "accounting-reader-fixture", Guid.NewGuid(), Guid.NewGuid(), "7"));
    }
}
