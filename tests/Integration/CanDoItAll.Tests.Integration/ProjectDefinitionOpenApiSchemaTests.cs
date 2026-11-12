using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;
using static CanDoItAll.Tests.Integration.Api.OpenApiSchemaAssertions;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectDefinitionOpenApiSchemaTests {
    private const string CreatePath = "/api/projects/definitions";
    private const string ReplacePath = "/api/projects/{projectId}/definition";
    private const string ScalarsPath = "/api/projects/{projectId}/definition/scalars";
    private const string LookupPath = "/api/projects/definition-operations/{namespaceId}/{epoch}/{operationId}";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Fact]
    public async Task OpenAPI_original_expectation_context_and_versions_match_serialized_wire_objects() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var context = Response(root, Operation(root, "/api/projects/definition-write-context", "get"), "200");
        Assert.Equal("uuid", Property(root, context, "namespace").GetProperty("format").GetString());
        var caller = Property(root, context, "callerBinding");
        Assert.Equal("string", caller.GetProperty("type").GetString());
        Assert.Equal(68, caller.GetProperty("minLength").GetInt32());
        Assert.Equal(68, caller.GetProperty("maxLength").GetInt32());
        Assert.Matches(caller.GetProperty("pattern").GetString()!, "pc1:" + new string('a', 64));
        Assert.DoesNotMatch(caller.GetProperty("pattern").GetString()!, "pc1:" + new string('A', 64));

        var requestVersion = Resolve(root, Property(root, context, "requestVersions").GetProperty("items"));
        AssertObject(requestVersion, "normalization", "schema");
        AssertObject(Resolve(root, Property(root, context, "resultVersions").GetProperty("items")), "value");
        var request = Request(root, Operation(root, ReplacePath, "put"));
        AssertObject(Property(root, request, "version"), "normalization", "schema");
        var expectation = Property(root, Property(root, request, "command"), "expected");
        AssertObject(expectation, "project", "revision");
        var annotatedAdmission = expectation.GetProperty("properties").GetProperty("project");
        Assert.Equal(new[] { "allOf", "description" }, Names(annotatedAdmission));
        Assert.Contains("Original database profile", annotatedAdmission.GetProperty("description").GetString());
        Assert.Equal("#/components/schemas/ProjectWriteAdmission", Assert.Single(annotatedAdmission.GetProperty("allOf").EnumerateArray()).GetProperty("$ref").GetString());
        var admission = Property(root, expectation, "project");
        AssertObject(admission, "databaseProfileId", "lifetimeId", "projectId");
        foreach (var name in new[] { "databaseProfileId", "lifetimeId", "projectId" }) {
            var identifier = Property(root, admission, name);
            Assert.Equal("uuid", identifier.GetProperty("format").GetString());
            Assert.DoesNotMatch(identifier.GetProperty("pattern").GetString()!, Guid.Empty.ToString("D"));
        }
        var sample = JsonSerializer.SerializeToElement(Expected(), Json);
        Assert.Equal(new[] { "project", "revision" }, Names(sample));
        Assert.Equal(new[] { "databaseProfileId", "lifetimeId", "projectId" }, Names(sample.GetProperty("project")));
        Assert.Equal("{\"schema\":1,\"normalization\":1}", JsonSerializer.Serialize(ProjectDefinitionRequestVersion.VersionOne, Json));
        Assert.Equal("{\"value\":1}", JsonSerializer.Serialize(ProjectDefinitionResultVersion.VersionOne, Json));

        var read = Response(root, Operation(root, ReplacePath, "get"), "200");
        foreach (var status in new[] { "403", "404", "409" }) {
            var error = Response(root, Operation(root, ReplacePath, "get"), status);
            Assert.True(error.GetProperty("properties").TryGetProperty("errors", out _));
            Assert.False(error.GetProperty("properties").TryGetProperty("status", out _));
        }
        AssertObject(Property(root, read, "expectation"), "project", "revision");
        Assert.Equal("uuid", Property(root, Property(root, read, "writeContext"), "namespace").GetProperty("format").GetString());
        var receipt = Property(root, Response(root, Operation(root, CreatePath, "post"), "201"), "receipt");
        AssertObject(Property(root, receipt, "version"), "value");
        AssertObject(Property(root, Property(root, receipt, "acknowledgement"), "definition"), "project", "revision");
    }

    [Fact]
    public async Task OpenAPI_commands_preserve_full_arrays_original_expectation_nulls_and_numeric_enums() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var create = Property(root, Request(root, Operation(root, CreatePath, "post")), "command");
        var replace = Property(root, Request(root, Operation(root, ReplacePath, "put")), "command");
        var scalar = Property(root, Request(root, Operation(root, ScalarsPath, "put")), "command");
        AssertRequired(create, "options", "phases", "scalars");
        AssertRequired(replace, "expected", "options", "phases", "scalars");
        AssertObject(scalar, "expected", "scalars");
        AssertNullable(root, Property(root, create, "externalIdentity"));
        AssertNullable(root, Property(root, replace, "assignExternalIdentity"));
        foreach (var command in new[] { create, replace }) {
            Assert.False(command.GetProperty("additionalProperties").GetBoolean());
            var phases = Property(root, command, "phases");
            var options = Property(root, command, "options");
            Assert.Equal("array", phases.GetProperty("type").GetString());
            Assert.Equal("array", options.GetProperty("type").GetString());
            var phase = Resolve(root, phases.GetProperty("items"));
            var option = Resolve(root, options.GetProperty("items"));
            AssertNullable(root, Property(root, phase, "id"));
            AssertNullable(root, Property(root, phase, "startDateUtc"));
            AssertNullable(root, Property(root, phase, "endDateUtc"));
            AssertNullable(root, Property(root, option, "id"));
            AssertNumericEnum<ProjectPhaseStatus>(Property(root, phase, "status"));
            AssertNumericEnum<ProjectOptionCategory>(Property(root, option, "category"));
        }
        var scalars = Property(root, scalar, "scalars");
        AssertNullable(root, Property(root, scalars, "targetDateUtc"));
        AssertNumericEnum<ProjectStatus>(Property(root, scalars, "status"));
        var desired = new ProjectDefinitionScalars("Keep all fields", "Description", "Objective", ProjectStatus.Draft, "Planning", null);
        var sample = new ReplaceProjectDefinitionCommand(Expected(), desired,
            [new(null, "First", "Goal", default, null, null), new(Guid.NewGuid(), "Second", "", default, null, null)],
            [new(null, default, "Option", "Notes")], null);
        var wire = JsonSerializer.SerializeToElement(sample, Json);
        Assert.Equal(new[] { "assignExternalIdentity", "expected", "options", "phases", "scalars" }, Names(wire));
        Assert.Equal(new[] { "First", "Second" }, wire.GetProperty("phases").EnumerateArray().Select(value => value.GetProperty("name").GetString()));
        Assert.Equal(JsonValueKind.Null, wire.GetProperty("phases")[0].GetProperty("id").ValueKind);
        Assert.Equal(JsonValueKind.Number, wire.GetProperty("scalars").GetProperty("status").ValueKind);
        var scalarWire = JsonSerializer.SerializeToElement(new UpdateProjectDefinitionScalarsCommand(sample.Expected, desired), Json);
        Assert.Equal(new[] { "expected", "scalars" }, Names(scalarWire));
        Assert.Equal(wire.GetProperty("expected").GetRawText(), scalarWire.GetProperty("expected").GetRawText());
    }

    [Fact]
    public async Task OpenAPI_outcomes_lookup_union_and_failure_statuses_describe_actual_response_bodies() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var create = Operation(root, CreatePath, "post");
        foreach (var operation in new[] { create, Operation(root, ReplacePath, "put"), Operation(root, ScalarsPath, "put") }) {
            AssertStatus(root, Response(root, operation, "200"), "accepted");
            foreach (var status in new[] { "400", "404", "409", "410", "413" }) {
                var rejected = Response(root, operation, status);
                AssertStatus(root, rejected, "rejected");
                AssertNullable(root, Property(root, rejected, "operation"));
            }
            var unsupportedMedia = Response(root, operation, "415");
            Assert.True(unsupportedMedia.GetProperty("properties").TryGetProperty("errors", out _));
            Assert.False(unsupportedMedia.GetProperty("properties").TryGetProperty("status", out _));
            var unavailable = Response(root, operation, "503");
            Assert.Equal("status", unavailable.GetProperty("discriminator").GetProperty("propertyName").GetString());
            var unavailableCases = unavailable.GetProperty("oneOf").EnumerateArray().Select(value => Resolve(root, value)).ToArray();
            Assert.Equal(2, unavailableCases.Length);
            AssertStatus(root, unavailableCases[0], "rejected");
            AssertStatus(root, unavailableCases[1], "unconfirmed");
            var forbidden = Response(root, operation, "403").GetProperty("anyOf").EnumerateArray().Select(value => Resolve(root, value)).ToArray();
            Assert.Equal(2, forbidden.Length);
            Assert.Single(forbidden, value => value.GetProperty("properties").TryGetProperty("status", out _));
            Assert.All(forbidden, value => Assert.True(value.GetProperty("properties").TryGetProperty("errors", out _)));
        }
        AssertStatus(root, Response(root, create, "201"), "accepted");
        var lookup = Operation(root, LookupPath, "get");
        var caller = Assert.Single(lookup.GetProperty("parameters").EnumerateArray(), value => value.GetProperty("name").GetString() == "callerBinding");
        Assert.Equal("query", caller.GetProperty("in").GetString());
        Assert.True(caller.GetProperty("required").GetBoolean());
        var union = Response(root, lookup, "200");
        Assert.Equal("status", union.GetProperty("discriminator").GetProperty("propertyName").GetString());
        var states = union.GetProperty("discriminator").GetProperty("mapping").EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal);
        Assert.Equal(new[] { "callerContextChanged", "closedAbsent", "datasetChanged", "detailUnavailable", "expired", "foundFull", "foundScalars", "invalidContext", "notObserved" }, states);
        foreach (var status in new[] { "404", "409", "410" }) {
            Assert.Equal(union.GetRawText(), Response(root, lookup, status).GetRawText());
        }
        Assert.Equal(2, Response(root, lookup, "403").GetProperty("anyOf").GetArrayLength());

        using var invalid = await host.Client.PostAsync(CreatePath, new StringContent("{}", Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);
        using var body = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("status", out _));
        Assert.False(body.RootElement.TryGetProperty("operation", out _));
        Assert.NotEmpty(body.RootElement.GetProperty("errors").EnumerateArray());
    }

    private static ProjectDefinitionExpectation Expected() => new(new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid());

    private static string[] Names(JsonElement value) => value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    private static JsonElement Operation(JsonElement root, string path, string method) => root.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static JsonElement Request(JsonElement root, JsonElement operation) => Resolve(root,
        operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));

    private static JsonElement Response(JsonElement root, JsonElement operation, string status) => Resolve(root,
        operation.GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/json").GetProperty("schema"));

    private static JsonElement Property(JsonElement root, JsonElement schema, string name) => Resolve(root, schema.GetProperty("properties").GetProperty(name));

    private static void AssertObject(JsonElement schema, params string[] names) {
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.Equal(names, Names(schema.GetProperty("properties")));
        AssertRequired(schema, names);
    }

    private static void AssertRequired(JsonElement schema, params string[] names) => Assert.Equal(names,
        schema.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));

    private static void AssertNumericEnum<T>(JsonElement schema) where T : struct, Enum {
        Assert.Equal("integer", schema.GetProperty("type").GetString());
        Assert.Equal(Enum.GetValues<T>().Select(value => Convert.ToInt32(value)).Order(), schema.GetProperty("enum").EnumerateArray().Select(value => value.GetInt32()).Order());
    }

    private static void AssertNullable(JsonElement root, JsonElement schema) {
        if (schema.TryGetProperty("anyOf", out var variants)) {
            Assert.Contains(variants.EnumerateArray(), value => Resolve(root, value).TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "null");
            return;
        }
        Assert.Contains(schema.GetProperty("type").EnumerateArray(), value => value.GetString() == "null");
    }

    private static void AssertStatus(JsonElement root, JsonElement schema, string status) {
        Assert.Contains(schema.GetProperty("required").EnumerateArray(), value => value.GetString() == "status");
        Assert.Equal(status, Assert.Single(Property(root, schema, "status").GetProperty("enum").EnumerateArray()).GetString());
    }
}
