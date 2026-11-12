using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionOperationContractTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid Namespace = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OperationId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly string Binding = "pc1:" + new string('a', 64);

    [Fact]
    public void Operation_request_round_trips_with_scalar_namespace_epoch_and_binding() {
        var operation = new ProjectDefinitionOperation(new(Namespace), new(17), OperationId, new(Binding));
        var request = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(operation,
            ProjectDefinitionRequestVersion.VersionOne, new(new("Project", "", "", ProjectStatus.Draft, "", null), [], [], null));

        var json = JsonSerializer.Serialize(request, Json);
        var restored = JsonSerializer.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(json, Json)!;

        using var parsed = JsonDocument.Parse(json);
        var address = parsed.RootElement.GetProperty("operation");
        Assert.Equal(Namespace, address.GetProperty("namespace").GetGuid());
        Assert.Equal(17, address.GetProperty("epoch").GetInt64());
        Assert.Equal(OperationId, address.GetProperty("id").GetGuid());
        Assert.Equal(Binding, address.GetProperty("callerBinding").GetString());
        Assert.False(address.TryGetProperty("callerScope", out _));
        Assert.Equal(operation, restored.Operation);
        Assert.Equal(request.Version, restored.Version);
        Assert.Equal(request.Command.Scalars, restored.Command.Scalars);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("\"malformed\"")]
    [InlineData("17")]
    public void Request_boundary_rejects_null_empty_or_malformed_namespace(string value) {
        var operation = $$$"""{"namespace":{{{value}}},"epoch":1,"id":"{{{OperationId}}}","callerBinding":"{{{Binding}}}"}""";

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(Envelope(operation), Json));
    }

    [Theory]
    [InlineData("epoch", "0")]
    [InlineData("epoch", "-1")]
    [InlineData("epoch", "null")]
    [InlineData("id", "\"00000000-0000-0000-0000-000000000000\"")]
    [InlineData("callerBinding", "null")]
    [InlineData("callerBinding", "\"pc2:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"")]
    [InlineData("callerBinding", "\"pc1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"")]
    public void Request_boundary_rejects_invalid_operation_values(string property, string value) {
        var operation = new Dictionary<string, JsonElement> {
            ["namespace"] = JsonSerializer.SerializeToElement(Namespace),
            ["epoch"] = JsonSerializer.SerializeToElement(1),
            ["id"] = JsonSerializer.SerializeToElement(OperationId),
            ["callerBinding"] = JsonSerializer.SerializeToElement(Binding)
        };
        operation[property] = JsonDocument.Parse(value).RootElement.Clone();

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(
            Envelope(JsonSerializer.Serialize(operation)), Json));
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("version")]
    [InlineData("command")]
    public void Request_boundary_requires_all_envelope_members(string omitted) {
        using var parsed = JsonDocument.Parse(Envelope(Address()));
        var members = parsed.RootElement.EnumerateObject().Where(property => property.Name != omitted)
            .ToDictionary(property => property.Name, property => property.Value.Clone());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(
            JsonSerializer.Serialize(members), Json));
    }

    [Fact]
    public void Request_version_syntax_is_separate_from_supported_interpretation() {
        var json = Envelope(Address()).Replace("\"schema\":1", "\"schema\":2", StringComparison.Ordinal);
        var request = JsonSerializer.Deserialize<ProjectDefinitionRequest<CreateProjectDefinitionCommand>>(json, Json)!;

        Assert.Equal(2, request.Version.Schema);
        Assert.Equal(1, request.Version.Normalization);
        Assert.Throws<NotSupportedException>(() => new ProjectDefinitionNormalizedIntent(request.Version,
            ProjectDefinitionCommandKind.Create, null, request.Command.Scalars, [], [], null, null,
            ProjectDefinitionEffectBinding.None, ProjectDefinitionLimits.VersionOneMaximum));
    }

    [Fact]
    public void Accepted_full_result_defensively_copies_submission_mappings() {
        var phases = new List<ProjectPhaseAcknowledgement> { new(0, Guid.Parse("77777777-7777-7777-7777-777777777777"), "Phase", "", ProjectPhaseStatus.Planned, null, null) };
        var acknowledgement = new ProjectEditorAcknowledgement(Definition(), "Project", "", "", ProjectStatus.Draft, "", null, phases, [], null);
        var accepted = new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(new(new(Namespace), new(1), OperationId, new(Binding)),
            ProjectDefinitionResultVersion.VersionOne, ProjectDefinitionCommandKind.Create,
            DateTimeOffset.Parse("2026-10-16T12:00:00Z"), DateTimeOffset.Parse("2026-10-23T12:00:00Z"), acknowledgement);

        phases.Clear();

        Assert.Single(accepted.Acknowledgement.Phases);
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionAccepted<ProjectEditorAcknowledgement>(accepted.Operation,
            accepted.Version, ProjectDefinitionCommandKind.UpdateScalars, accepted.AcceptedAtUtc, accepted.ReplayUntilUtc, acknowledgement));
    }

    private static string Address()
        => $$$"""{"namespace":"{{{Namespace}}}","epoch":1,"id":"{{{OperationId}}}","callerBinding":"{{{Binding}}}"}""";

    private static string Envelope(string operation)
        => $$$"""{"operation":{{{operation}}},"version":{"schema":1,"normalization":1},"command":{"scalars":{"name":"Project","description":"","objective":"","status":0,"currentPhase":"","targetDateUtc":null},"phases":[],"options":[],"externalIdentity":null}}""";

    private static ProjectDefinitionExpectation Definition()
        => new(new(Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555")), Guid.Parse("66666666-6666-6666-6666-666666666666"));
}
