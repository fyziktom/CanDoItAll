using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectOperationEpochHttpIntegrationTests {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Fact]
    public async Task HTTP_write_and_lookup_preserve_original_large_epoch_and_refuse_future_context_without_rounding() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        foreach (var value in new[] { 9007199254740993L, long.MaxValue }) {
            var operation = new ProjectDefinitionOperation(context.Namespace, new(value), Guid.NewGuid(), context.CallerBinding);
            var request = Request(operation);
            using var write = await host.Client.PostAsJsonAsync("/api/projects/definitions", request, Json);
            Assert.Equal(HttpStatusCode.Conflict, write.StatusCode);
            var rejected = await write.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal("rejected", rejected.GetProperty("status").GetString());
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), rejected.GetProperty("operation").GetProperty("epoch").GetString());
            Assert.Equal(operation, rejected.GetProperty("operation").Deserialize<ProjectDefinitionOperation>(Json));
            Assert.Contains(rejected.GetProperty("errors").EnumerateArray(), error =>
                error.GetProperty("code").GetString() == ProjectErrorCodes.DefinitionOperationUnknown);

            using var lookup = await host.Client.GetAsync(Lookup(operation, value.ToString(CultureInfo.InvariantCulture)));
            Assert.Equal(HttpStatusCode.Conflict, lookup.StatusCode);
            var observed = await lookup.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.Equal("invalidContext", observed.GetProperty("status").GetString());
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), observed.GetProperty("operation").GetProperty("epoch").GetString());
            Assert.Equal(operation, observed.GetProperty("operation").Deserialize<ProjectDefinitionOperation>(Json));
        }
    }

    [Fact]
    public async Task HTTP_write_and_lookup_refuse_noncanonical_epoch_before_owner_dispatch() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var context = await ContextAsync(host.Client);
        var operation = new ProjectDefinitionOperation(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding);
        var request = JsonSerializer.SerializeToNode(Request(operation), Json)!.AsObject();
        foreach (var spelling in new[] { "0", "01", "+1", "-1", " 1", "1 ", "1.0", "1e1", "9223372036854775808" }) {
            request["operation"]!["epoch"] = spelling;
            using var write = await host.Client.PostAsync("/api/projects/definitions",
                new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, write.StatusCode);
            using var lookup = await host.Client.GetAsync(Lookup(operation, spelling));
            Assert.Equal(HttpStatusCode.BadRequest, lookup.StatusCode);
        }
        request["operation"]!["epoch"] = context.Epoch.Value;
        using var numeric = await host.Client.PostAsync("/api/projects/definitions",
            new StringContent(request.ToJsonString(), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);
        using var untouched = await host.Client.GetAsync(Lookup(operation, context.Epoch.Value.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(HttpStatusCode.OK, untouched.StatusCode);
        Assert.Equal("notObserved", (await untouched.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task OpenAPI_context_request_receipt_and_lookup_epoch_match_canonical_string_wire_contract() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        var context = Resolve(root, paths.GetProperty("/api/projects/definition-write-context").GetProperty("get")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        AssertEpochSchema(Resolve(root, context.GetProperty("properties").GetProperty("epoch")));

        var create = paths.GetProperty("/api/projects/definitions").GetProperty("post");
        var request = Resolve(root, create.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        Assert.Equal(new[] { "command", "operation", "version" }, request.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        AssertOperationSchema(root, Resolve(root, request.GetProperty("properties").GetProperty("operation")));
        var accepted = Resolve(root, create.GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        var receipt = Resolve(root, accepted.GetProperty("properties").GetProperty("receipt"));
        AssertOperationSchema(root, Resolve(root, receipt.GetProperty("properties").GetProperty("operation")));

        var lookup = paths.GetProperty("/api/projects/definition-operations/{namespaceId}/{epoch}/{operationId}").GetProperty("get");
        var epoch = Assert.Single(lookup.GetProperty("parameters").EnumerateArray(), parameter => parameter.GetProperty("name").GetString() == "epoch");
        Assert.Equal("path", epoch.GetProperty("in").GetString());
        Assert.True(epoch.GetProperty("required").GetBoolean());
        AssertEpochSchema(Resolve(root, epoch.GetProperty("schema")));
    }

    private static async Task<ProjectDefinitionWriteContext> ContextAsync(HttpClient client) {
        var wire = await client.GetFromJsonAsync<JsonElement>("/api/projects/definition-write-context", Json);
        Assert.Equal(JsonValueKind.String, wire.GetProperty("epoch").ValueKind);
        return wire.Deserialize<ProjectDefinitionWriteContext>(Json)!;
    }

    private static ProjectDefinitionRequest<CreateProjectDefinitionCommand> Request(ProjectDefinitionOperation operation)
        => new(operation, ProjectDefinitionRequestVersion.VersionOne,
            new(new("Unissued epoch must not create", "", "", ProjectStatus.Draft, "", null), [], [], null));

    private static string Lookup(ProjectDefinitionOperation operation, string epoch)
        => $"/api/projects/definition-operations/{operation.Namespace.Value:D}/{Uri.EscapeDataString(epoch)}/{operation.Id:D}?callerBinding={Uri.EscapeDataString(operation.CallerBinding.Value)}";

    private static void AssertOperationSchema(JsonElement root, JsonElement operation) {
        Assert.Equal(new[] { "callerBinding", "epoch", "id", "namespace" }, operation.GetProperty("required").EnumerateArray().Select(value => value.GetString()).Order(StringComparer.Ordinal));
        AssertEpochSchema(Resolve(root, operation.GetProperty("properties").GetProperty("epoch")));
        Assert.Equal("string", Resolve(root, operation.GetProperty("properties").GetProperty("callerBinding")).GetProperty("type").GetString());
        Assert.Equal("uuid", Resolve(root, operation.GetProperty("properties").GetProperty("namespace")).GetProperty("format").GetString());
    }

    private static void AssertEpochSchema(JsonElement schema) {
        Assert.Equal("string", schema.GetProperty("type").GetString());
        Assert.Equal(ProjectOperationEpochHttpContract.Pattern, schema.GetProperty("pattern").GetString());
        Assert.Equal(1, schema.GetProperty("minLength").GetInt32());
        Assert.Equal(19, schema.GetProperty("maxLength").GetInt32());
        Assert.False(schema.TryGetProperty("format", out _));
        Assert.False(schema.TryGetProperty("properties", out var properties) && properties.EnumerateObject().Any());
    }

    private static JsonElement Resolve(JsonElement root, JsonElement schema) {
        while (schema.TryGetProperty("$ref", out var reference)) {
            schema = root;
            foreach (var segment in reference.GetString()![2..].Split('/')) {
                schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
            }
        }
        return schema;
    }
}
