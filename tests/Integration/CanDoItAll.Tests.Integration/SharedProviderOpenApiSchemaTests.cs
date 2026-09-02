using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.SharedProviders.Http;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class SharedProviderOpenApiSchemaTests(SharedProviderCatalogApiFixture fixture, ITestOutputHelper output)
    : IClassFixture<SharedProviderCatalogApiFixture> {
    [Fact]
    public async Task OpenApi_CustomProtocolScalarsAndEnums_MatchWireShape() {
        using var document = await ReadDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var scalarSamples = new Dictionary<string, string> {
            [nameof(SharedProviderPublicationId)] = SharedProviderPublicationId.New().ToString(),
            [nameof(SharedProviderSourceInstanceId)] = SharedProviderSourceInstanceId.New().ToString(),
            [nameof(SharedProviderPublicRevision)] = "sha256:" + new string('a', 64),
            [nameof(SharedProviderProtocolVersion)] = SharedProviderProtocol.CurrentSchemaVersion,
            [nameof(SharedProviderRoutingModelId)] = Model.Value
        };
        foreach (var (name, sample) in scalarSamples) {
            var schema = Resolve(document.RootElement, schemas.GetProperty(name));
            Assert.Equal("string", schema.GetProperty("type").GetString());
            Assert.False(schema.TryGetProperty("properties", out var properties) && properties.EnumerateObject().Any());
            if (schema.TryGetProperty("pattern", out var pattern)) {
                Assert.Matches(pattern.GetString()!, sample);
                Assert.DoesNotMatch(pattern.GetString()!, "invalid");
            }
        }
        var root = document.RootElement;
        var catalog = Resolve(root, schemas.GetProperty(nameof(SharedProviderCatalogDocument)));
        var publication = Resolve(root, PropertySchema(root, catalog, "providers").GetProperty("items"));
        var model = Resolve(root, PropertySchema(root, publication, "models").GetProperty("items"));
        var thinking = PropertySchema(root, model, "thinking");
        var enumSchemas = new Dictionary<Type, JsonElement> {
            [typeof(SharedProviderPurpose)] = PropertySchema(root, publication, "purpose"),
            [typeof(SharedProviderTransport)] = PropertySchema(root, publication, "transport"),
            [typeof(SharedProviderCapability)] = Resolve(root, PropertySchema(root, model, "capabilities").GetProperty("items")),
            [typeof(SharedProviderHealthState)] = PropertySchema(root, PropertySchema(root, publication, "health"), "state"),
            [typeof(SharedProviderThinkingSupport)] = PropertySchema(root, thinking, "support"),
            [typeof(SharedProviderThinkingControl)] = PropertySchema(root, thinking, "control"),
            [typeof(SharedProviderReasoningEffort)] = Resolve(root, PropertySchema(root, thinking, "allowedEfforts").GetProperty("items"))
        };
        foreach (var (type, schema) in enumSchemas) {
            Assert.Equal("string", schema.GetProperty("type").GetString());
            var expected = Enum.GetValues(type).Cast<object>().Select(value => JsonSerializer.Serialize(value, type)).Order();
            Assert.Equal(expected, schema.GetProperty("enum").EnumerateArray().Select(item => item.GetRawText()).Order());
        }
    }

    [Theory]
    [InlineData(SharedProviderRelayOperation.ChatCompletions, "chat/completions", "messages")]
    [InlineData(SharedProviderRelayOperation.Responses, "responses", "input")]
    [InlineData(SharedProviderRelayOperation.ImageGenerations, "images/generations", "prompt")]
    public async Task OpenApi_OperationSchemas_MatchAcceptedSubset(
        SharedProviderRelayOperation operation, string suffix, string inputProperty) {
        using var document = await ReadDocumentAsync();
        var schema = RequestSchema(document.RootElement, suffix);
        Assert.Equal("object", schema.GetProperty("type").GetString());
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { inputProperty, "model" }.Order(),
            schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Order());
        var properties = schema.GetProperty("properties");
        Assert.True(properties.TryGetProperty(inputProperty, out _));
        Assert.Equal("string", properties.GetProperty("model").GetProperty("type").GetString());

        if (operation == SharedProviderRelayOperation.ChatCompletions) {
            Assert.Equal(256, properties.GetProperty("messages").GetProperty("maxItems").GetInt32());
            Assert.Contains("include_usage", properties.GetProperty("stream_options").GetProperty("required").ToString());
            Assert.True(properties.TryGetProperty("reasoning_effort", out _));
            Assert.True(schema.TryGetProperty("anyOf", out _));
            Assert.True(schema.TryGetProperty("not", out _));
        } else if (operation == SharedProviderRelayOperation.Responses) {
            Assert.False(properties.GetProperty("store").GetProperty("enum")[0].GetBoolean());
            Assert.False(properties.GetProperty("background").GetProperty("enum")[0].GetBoolean());
            Assert.True(properties.TryGetProperty("parallel_tool_calls", out _));
            Assert.True(properties.TryGetProperty("reasoning", out _));
        } else {
            Assert.Equal("b64_json", properties.GetProperty("response_format").GetProperty("enum")[0].GetString());
            Assert.False(properties.TryGetProperty("stream", out _));
        }

        var cases = Cases(operation).ToArray();
        foreach (var (payload, accepted) in cases) {
            var actual = new SharedProviderRelayRequestPolicy().Normalize(operation, Encoding.UTF8.GetBytes(payload), Support);
            Assert.Equal(accepted, actual is SharedProviderRelayRequestPolicyResult.Accepted);
        }
        output.WriteLine(JsonSerializer.Serialize(new {
            SchemaConformance = true, Operation = operation.ToString(), Schema = schema,
            Cases = cases.Select(item => new { Payload = JsonSerializer.Deserialize<JsonElement>(item.Payload), item.Accepted })
        }));
    }

    [Fact]
    public async Task OpenApi_UnsupportedFieldsAndFeatures_AreExplicit() {
        using var document = await ReadDocumentAsync();
        var paths = document.RootElement.GetProperty("paths").EnumerateObject()
            .Where(item => item.Name.StartsWith("/api/shared-providers/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(5, paths.Length);
        foreach (var suffix in new[] { "chat/completions", "responses", "images/generations" }) {
            var schema = RequestSchema(document.RootElement, suffix);
            var properties = schema.GetProperty("properties");
            foreach (var field in new[] { "previous_response_id", "file_ids", "metadata", "audio" }) {
                Assert.False(properties.TryGetProperty(field, out _));
            }
            Assert.Contains("abort", schema.GetProperty("description").GetString());
            Assert.Contains("stored responses", schema.GetProperty("description").GetString());
        }
    }

    private async Task<JsonDocument> ReadDocumentAsync() =>
        JsonDocument.Parse(await fixture.Host.Client.GetStringAsync("/openapi/v1.json"));

    private static JsonElement RequestSchema(JsonElement root, string suffix) =>
        Resolve(root, root.GetProperty("paths").GetProperty("/api/shared-providers/openai/v1/" + suffix)
            .GetProperty("post").GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema"));

    private static JsonElement PropertySchema(JsonElement root, JsonElement schema, string property) =>
        Resolve(root, schema.GetProperty("properties").GetProperty(property));

    private static JsonElement Resolve(JsonElement root, JsonElement schema) {
        if (schema.TryGetProperty("oneOf", out var nullableVariants)) {
            schema = nullableVariants.EnumerateArray().Single(item => item.TryGetProperty("$ref", out _));
        }
        while (schema.TryGetProperty("$ref", out var reference)) {
            schema = root;
            foreach (var segment in reference.GetString()![2..].Split('/')) {
                schema = schema.GetProperty(segment.Replace("~1", "/").Replace("~0", "~"));
            }
        }
        return schema;
    }

    private static IEnumerable<(string Payload, bool Accepted)> Cases(SharedProviderRelayOperation operation) {
        var prefix = "\"model\":" + JsonSerializer.Serialize(Model.Value) + ",";
        var valid = operation switch {
            SharedProviderRelayOperation.ChatCompletions => new[] {
                "\"messages\":[{\"role\":\"user\",\"content\":\"Hello\"}]",
                "\"messages\":[{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"c1\",\"type\":\"function\",\"function\":{\"name\":\"probe\",\"arguments\":\"{}\"}}]}],\"stream\":true,\"stream_options\":{\"include_usage\":true},\"reasoning_effort\":\"high\"",
                "\"messages\":[{\"role\":\"user\",\"content\":[{\"type\":\"image_url\",\"image_url\":{\"url\":\"data:image/png;base64,AQID\",\"detail\":\"auto\"}}]}],\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":\"answer\",\"schema\":{},\"strict\":true}}"
            },
            SharedProviderRelayOperation.Responses => new[] {
                "\"input\":\"Hello\",\"store\":false,\"background\":false,\"parallel_tool_calls\":true,\"reasoning\":{\"effort\":\"xhigh\"}",
                "\"input\":[{\"type\":\"reasoning\",\"summary\":[],\"encrypted_content\":null},{\"role\":\"assistant\",\"content\":[{\"type\":\"output_text\",\"text\":\"answer\",\"annotations\":[]}]}]",
                "\"input\":[{\"type\":\"function_call\",\"call_id\":\"c1\",\"name\":\"probe\",\"arguments\":\"{}\",\"status\":\"completed\"},{\"type\":\"function_call_output\",\"call_id\":\"c1\",\"output\":\"ok\"}],\"tools\":[{\"type\":\"function\",\"name\":\"probe\",\"parameters\":{},\"strict\":null}],\"tool_choice\":{\"type\":\"function\",\"name\":\"probe\"},\"text\":{\"format\":{\"type\":\"json_schema\",\"name\":\"answer\",\"schema\":{}}}"
            },
            _ => new[] { "\"prompt\":\"Hello\"", "\"prompt\":\"Hello\",\"n\":4,\"response_format\":\"b64_json\",\"output_format\":\"webp\",\"size\":\"auto\",\"quality\":\"high\"" }
        };
        foreach (var fields in valid) {
            yield return ("{" + prefix + fields + "}", true);
        }
        yield return ("{" + prefix + valid[0] + ",\"unsupported\":true}", false);
        yield return ("{" + valid[0] + "}", false);
        var invalid = operation switch {
            SharedProviderRelayOperation.ChatCompletions => new[] {
                "\"messages\":[]", "\"messages\":[{\"role\":\"tool\",\"content\":\"ok\"}]",
                "\"messages\":[{\"role\":\"assistant\",\"content\":null}]",
                valid[0] + ",\"stream_options\":{\"include_usage\":true}",
                valid[0] + ",\"max_tokens\":1,\"max_completion_tokens\":2",
                valid[0] + ",\"reasoning_effort\":\"wrong\""
            },
            SharedProviderRelayOperation.Responses => new[] {
                "\"input\":\"Hello\",\"store\":true", "\"input\":\"Hello\",\"background\":true",
                "\"input\":\"Hello\",\"reasoning\":{\"summary\":\"auto\"}",
                "\"input\":[{\"type\":\"reasoning\"}]", "\"input\":[{\"role\":\"user\",\"content\":[{\"type\":\"input_file\",\"file_id\":\"x\"}]}]"
            },
            _ => new[] { "\"prompt\":\"Hello\",\"response_format\":\"url\"", "\"prompt\":\"Hello\",\"stream\":true", "\"prompt\":\"Hello\",\"n\":0" }
        };
        foreach (var fields in invalid) {
            yield return ("{" + prefix + fields + "}", false);
        }
    }

    private static readonly SharedProviderRoutingModelId Model =
        SharedProviderRoutingModelIdCodec.Create(SharedProviderPublicationId.New(), "schema-model");
    private static readonly SharedProviderRelaySupportDescriptor Support = new(
        Enum.GetValues<SharedProviderRelayOperation>().ToHashSet(), SharedProviderStreamingMode.ServerSentEvents,
        true, true, true, true, true, 4 * 1024 * 1024, 128 * 1024, 4);
}
