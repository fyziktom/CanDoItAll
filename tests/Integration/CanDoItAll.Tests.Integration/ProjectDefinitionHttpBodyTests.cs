using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Http;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectDefinitionHttpBodyTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Fact]
    public async Task Valid_frozen_request_retains_operation_and_original_command() {
        var expected = Request();
        var request = Http(JsonSerializer.SerializeToUtf8Bytes(expected, Json));

        var actual = await ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default);

        Assert.Equal(expected.Operation, actual.Operation);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Command.Scalars, actual.Command.Scalars);
        Assert.Equal(expected.Command.Phases, actual.Command.Phases);
        Assert.Equal(expected.Command.Options, actual.Command.Options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_ceiling_counts_decoded_UTF8_bytes_instead_of_characters(bool exceed) {
        var value = new string('é', ProjectDefinitionLimits.VersionOneMaximum.MaxFieldUtf8Bytes / 2 + (exceed ? 1 : 0));
        var body = Encoding.UTF8.GetBytes("{\"description\":" + JsonSerializer.Serialize(value) + "}");

        if (exceed) {
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.Throws<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ValidateTokens(body)).StatusCode);
        } else {
            ProjectDefinitionHttpBody.ValidateTokens(body);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Collection_ceiling_is_checked_before_immutable_command_construction(bool exceed) {
        var rows = Enumerable.Repeat("{}", ProjectDefinitionLimits.VersionOneMaximum.MaxPhaseCount + (exceed ? 1 : 0));
        var body = Encoding.UTF8.GetBytes("{\"phases\":[" + string.Join(',', rows) + "]}");

        if (exceed) {
            Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.Throws<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ValidateTokens(body)).StatusCode);
        } else {
            ProjectDefinitionHttpBody.ValidateTokens(body);
        }
    }

    [Theory]
    [InlineData("name")]
    [InlineData("Name")]
    public void Duplicate_properties_cannot_select_a_different_model_binding_value(string duplicate) {
        var body = Encoding.UTF8.GetBytes($"{{\"name\":\"first\",\"{duplicate}\":\"second\"}}");
        Assert.Throws<JsonException>(() => ProjectDefinitionHttpBody.ValidateTokens(body));
    }

    [Fact]
    public void Excessive_depth_is_rejected_before_deserialization() {
        var depth = ProjectDefinitionHttpBody.MaximumDepth + 1;
        var body = Encoding.UTF8.GetBytes(new string('[', depth) + "0" + new string(']', depth));
        Assert.ThrowsAny<JsonException>(() => ProjectDefinitionHttpBody.ValidateTokens(body));
    }

    [Fact]
    public async Task Missing_JSON_content_type_is_refused_without_reading_the_body() {
        var request = Http([]);
        request.ContentType = "text/plain";
        request.Body = new MustNotReadStream();
        var failure = await Assert.ThrowsAsync<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default));
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, failure.StatusCode);
    }

    [Fact]
    public async Task Oversize_declared_body_is_refused_before_reading_any_bytes() {
        var request = Http([]);
        request.ContentLength = ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes + 1L;
        request.Body = new MustNotReadStream();
        var failure = await Assert.ThrowsAsync<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default));
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, failure.StatusCode);
    }

    [Fact]
    public async Task Chunked_body_is_bounded_independently_of_ContentLength() {
        var maximum = ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes;
        var request = Http(new byte[maximum + 1024]);
        request.ContentLength = null;
        var failure = await Assert.ThrowsAsync<BadHttpRequestException>(() => ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default));
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, failure.StatusCode);
        Assert.Equal(maximum + 1L, request.Body.Position);
    }

    [Fact]
    public async Task Exactly_maximum_raw_bytes_remain_accepted_without_truncation() {
        var expected = Request();
        var raw = JsonSerializer.Serialize(expected, Json).PadRight(ProjectDefinitionLimits.VersionOneMaximum.MaxRawRequestBytes);
        var request = Http(Encoding.UTF8.GetBytes(raw));
        var actual = await ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default);
        Assert.Equal(expected.Operation, actual.Operation);
        Assert.Equal(request.Body.Length, request.Body.Position);
    }

    [Fact]
    public async Task Unknown_members_do_not_smuggle_an_alternate_caller() {
        var raw = JsonSerializer.Serialize(Request(), Json);
        var request = Http(Encoding.UTF8.GetBytes(raw.Insert(1, "\"actor\":\"someone-else\",")));
        await Assert.ThrowsAsync<JsonException>(() => ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default));
    }

    [Fact]
    public async Task Malformed_JSON_is_not_treated_as_a_default_command() {
        var request = Http("{\"operation\":"u8.ToArray());
        await Assert.ThrowsAnyAsync<JsonException>(() => ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, Json, default));
    }

    private static HttpRequest Http(byte[] body) {
        var request = new DefaultHttpContext().Request;
        request.ContentType = "application/json";
        request.ContentLength = body.Length;
        request.Body = new MemoryStream(body);
        return request;
    }

    private static ProjectDefinitionRequest<CreateProjectDefinitionCommand> Request()
        => new(new(new(Guid.NewGuid()), new(1), Guid.NewGuid(), new(ProjectCallerBinding.VersionOnePrefix + new string('a', 64))),
            ProjectDefinitionRequestVersion.VersionOne, new(new("Bounded request", "", "", ProjectStatus.Draft, "", null), [], [], null));

    private sealed class MustNotReadStream : MemoryStream {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The body must be rejected before reading it.");
    }
}
