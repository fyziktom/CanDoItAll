using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectLifecycleHttpContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Theory]
    [InlineData(ProjectLifecycleCommandKind.Delete)]
    [InlineData(ProjectLifecycleCommandKind.EnsureLink)]
    [InlineData(ProjectLifecycleCommandKind.RemoveLink)]
    [InlineData(ProjectLifecycleCommandKind.ReconnectLink)]
    public async Task Wire_request_preserves_the_entire_original_identity_and_conditional_command(ProjectLifecycleCommandKind kind) {
        var original = Original(kind);
        var request = Http(JsonSerializer.SerializeToUtf8Bytes(ProjectLifecycleApiSubmission.From(original), Json));
        var received = await ProjectDefinitionHttpBody.ReadLifecycleAsync(request, Json, default);
        Assert.Equal(original, received.ToSubmission());
        Assert.Equal(request.Body.Length, request.Body.Position);
    }

    [Theory]
    [InlineData("{\"command\":{\"kind\":\"delete\",\"kind\":\"ensureLink\"}}")]
    [InlineData("{\"command\":{\"kind\":\"unknown\"}}")]
    [InlineData("{\"actor\":\"someone-else\"}")]
    public async Task Duplicate_discriminators_unknown_commands_and_forged_callers_are_rejected_before_dispatch(string body) {
        var request = Http(Encoding.UTF8.GetBytes(body));
        await Assert.ThrowsAnyAsync<JsonException>(() => ProjectDefinitionHttpBody.ReadLifecycleAsync(request, Json, default));
    }

    [Fact]
    public async Task Lifecycle_raw_body_limit_applies_to_chunked_requests_without_truncating_accepted_input() {
        var original = Original(ProjectLifecycleCommandKind.Delete);
        var raw = JsonSerializer.Serialize(ProjectLifecycleApiSubmission.From(original), Json)
            .PadRight(ProjectLifecycleAccepted.MaximumPayloadBytes);
        var exact = Http(Encoding.UTF8.GetBytes(raw));
        exact.ContentLength = null;
        Assert.Equal(original, (await ProjectDefinitionHttpBody.ReadLifecycleAsync(exact, Json, default)).ToSubmission());
        var excess = Http(Encoding.UTF8.GetBytes(raw + " "));
        excess.ContentLength = null;
        var failure = await Assert.ThrowsAsync<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ReadLifecycleAsync(excess, Json, default));
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, failure.StatusCode);
        Assert.Equal(ProjectLifecycleAccepted.MaximumPayloadBytes + 1L, excess.Body.Position);
    }

    [Fact]
    public void Historical_deletion_response_preserves_completion_binding_without_claiming_cleanup_completion() {
        var original = Original(ProjectLifecycleCommandKind.Delete);
        var expected = Assert.IsType<ProjectLifecycleCommand.Delete>(original.Command).Expected.Project;
        var completion = new ProjectDeletionCompletionBinding(new(new(original.Operation.DatabaseProfileId,
            original.Operation.Namespace, new(Guid.NewGuid())), expected), []);
        var now = DateTimeOffset.UtcNow;
        var accepted = new ProjectLifecycleAccepted(original, ProjectLifecycleAccepted.VersionOne, now, now.AddDays(1),
            new ProjectLifecycleAcknowledgement.DeletionCommitted(completion));
        ProjectLifecycleApiResponse response = new ProjectLifecycleApiResponse.Accepted(ProjectLifecycleApiReceipt.From(accepted),
            true, ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, false);
        var body = JsonSerializer.SerializeToUtf8Bytes(response, Json);
        var decoded = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(JsonSerializer.Deserialize<ProjectLifecycleApiResponse>(body, Json));
        Assert.Equal(original, decoded.Receipt.Original.ToSubmission());
        var observedCompletion = Assert.IsType<ProjectLifecycleApiAcknowledgement.DeletionCommitted>(decoded.Receipt.Acknowledgement).Completion;
        Assert.Equal(completion.Original, observedCompletion.Original);
        Assert.Equal(completion.Obligations, observedCompletion.Obligations);
        Assert.Equal(ProjectLifecycleCompletionIssue.OwnerReleaseUnconfirmed, decoded.CompletionIssue);
        Assert.True(decoded.Replayed);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("accepted", document.RootElement.GetProperty("status").GetString());
        Assert.False(document.RootElement.TryGetProperty("cleanupCompleted", out _));
    }

    private static ProjectLifecycleSubmission Original(ProjectLifecycleCommandKind kind) {
        var profile = Guid.NewGuid();
        var parent = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var child = new ProjectWriteAdmission(profile, Guid.NewGuid(), Guid.NewGuid());
        var edge = new ProjectHierarchyEdgeExpectation(Guid.NewGuid(), parent, child);
        ProjectLifecycleCommand command = kind switch {
            ProjectLifecycleCommandKind.Delete => new ProjectLifecycleCommand.Delete(new(parent, Guid.NewGuid())),
            ProjectLifecycleCommandKind.EnsureLink => new ProjectLifecycleCommand.EnsureLink(parent, child),
            ProjectLifecycleCommandKind.RemoveLink => new ProjectLifecycleCommand.RemoveLink(edge),
            ProjectLifecycleCommandKind.ReconnectLink => new ProjectLifecycleCommand.ReconnectLink(edge, new(profile, Guid.NewGuid(), Guid.NewGuid())),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return new(new(profile, new(Guid.NewGuid()), new(1), new(ProjectCallerBinding.VersionOnePrefix + new string('a', 64)), Guid.NewGuid()),
            ProjectLifecycleInterpretation.VersionOne, command);
    }

    private static HttpRequest Http(byte[] body) {
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json";
        request.ContentLength = body.Length;
        request.Body = new MemoryStream(body);
        return request;
    }
}
