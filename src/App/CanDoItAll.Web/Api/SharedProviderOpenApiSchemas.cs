using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class SharedProviderOpenApiSchemas {
    private const int MaximumTextCharacters = 1024 * 1024;
    private const int MaximumSchemaCharacters = 256 * 1024;
    private const string NonEmptyGuidPattern = "^(?!00000000-0000-0000-0000-000000000000$)[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$";

    public static Task TransformSchemaAsync(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken) {
        var type = System.Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type == typeof(IReadOnlyList<SharedProviderCapability>) || type == typeof(IReadOnlyList<SharedProviderReasoningEffort>)) {
            schema.Type = JsonSchemaType.Array;
            schema.Items = EnumSchema(type.GenericTypeArguments[0]);
            return Task.CompletedTask;
        }
        OpenApiSchema? replacement = type == typeof(SharedProviderPublicationId) || type == typeof(SharedProviderSourceInstanceId)
            ? new() { Type = JsonSchemaType.String, Format = "uuid", Pattern = NonEmptyGuidPattern, MinLength = 36, MaxLength = 36 }
            : type == typeof(SharedProviderPublicRevision)
                ? new() { Type = JsonSchemaType.String, Pattern = "^sha256:[0-9a-f]{64}$", MinLength = 71, MaxLength = 71 }
                : type == typeof(SharedProviderRoutingModelId)
                    ? RoutingModel()
                    : type == typeof(SharedProviderProtocolVersion)
                        ? Tokens(SharedProviderProtocol.CurrentSchemaVersion)
                        : null;
        if (type == typeof(SharedProviderPurpose) || type == typeof(SharedProviderTransport) ||
            type == typeof(SharedProviderCapability) || type == typeof(SharedProviderHealthState) ||
            type == typeof(SharedProviderThinkingSupport) || type == typeof(SharedProviderThinkingControl) ||
            type == typeof(SharedProviderReasoningEffort)) {
            replacement = EnumSchema(type);
        }
        if (replacement is not null) {
            var nullable = schema.Type?.HasFlag(JsonSchemaType.Null) == true;
            schema.Type = replacement.Type | (nullable ? JsonSchemaType.Null : 0);
            schema.Format = replacement.Format;
            schema.Pattern = replacement.Pattern;
            schema.MinLength = replacement.MinLength;
            schema.MaxLength = replacement.MaxLength;
            schema.Enum = replacement.Enum;
            if (nullable && schema.Enum is { Count: > 0 }) {
                schema.Enum.Add(null!);
            }
            schema.Properties?.Clear();
            schema.Required?.Clear();
            schema.AdditionalProperties = null;
        }
        return Task.CompletedTask;
    }

    private static OpenApiSchema EnumSchema(Type type) => new() {
        Type = JsonSchemaType.String,
        Enum = Enum.GetValues(type).Cast<object>().Select(value => JsonSerializer.SerializeToNode(value, type)!).ToList()
    };

    public static OpenApiSchema Request(SharedProviderRelayOperation operation) {
        var schema = operation switch {
            SharedProviderRelayOperation.ChatCompletions => Chat(),
            SharedProviderRelayOperation.Responses => Responses(),
            SharedProviderRelayOperation.ImageGenerations => Images(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        schema.Description = "Strict, case-sensitive OpenAI-compatible subset. Unknown and duplicate properties are rejected. " +
            "JSON depth is at most 32 and the UTF-8 body is bounded by the endpoint/provider limit. " +
            "Text limits count UTF-16 code units. Model capabilities and configured token/image limits are checked at dispatch. " +
            "Only client-executed function tools and data-URI image input are supported; no remote URLs, files, audio, " +
            "hosted tools, stored responses, background jobs, or previous_response_id. " +
            "After streaming headers, upstream failure aborts the transport and does not produce a successful terminal event.";
        return schema;
    }

    private static OpenApiSchema Chat() {
        var properties = TextGenerationProperties(false);
        properties["messages"] = Array(Any(
            ChatMessage("system"), ChatMessage("developer"), ChatMessage("user"),
            ChatMessage("assistant"), ChatMessage("tool")), 1, 256);
        properties["stream_options"] = Object(new() { ["include_usage"] = Boolean() }, "include_usage");
        properties["reasoning_effort"] = Nullable(Tokens("none", "minimal", "low", "medium", "high", "xhigh", "max"));
        properties["stop"] = Any(Text(256), Array(Text(256), 1, 4));
        properties["max_tokens"] = OutputTokens();
        properties["max_completion_tokens"] = OutputTokens();
        var schema = Object(properties, "model", "messages");
        schema.Not = new OpenApiSchema { Required = new HashSet<string> { "max_tokens", "max_completion_tokens" } };
        schema.AnyOf = [
            new OpenApiSchema { Not = new OpenApiSchema { Required = new HashSet<string> { "stream_options" } } },
            new OpenApiSchema { Required = new HashSet<string> { "stream" }, Properties = new Dictionary<string, IOpenApiSchema> {
                ["stream"] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)!] }
            } }
        ];
        return schema;
    }

    private static OpenApiSchema Responses() {
        var properties = TextGenerationProperties(true);
        properties["input"] = Any(Text(), Array(ResponseInput(), 1, 256));
        properties["instructions"] = Text();
        properties["max_output_tokens"] = OutputTokens();
        properties["reasoning"] = Nullable(Object(new() {
            ["effort"] = Nullable(Tokens("none", "minimal", "low", "medium", "high", "xhigh", "max"))
        }));
        properties["store"] = FalseOnly("Responses are stateless. Omitted store is normalized to false.");
        properties["background"] = FalseOnly("Only foreground execution is supported.");
        return Object(properties, "model", "input");
    }

    private static Dictionary<string, IOpenApiSchema> TextGenerationProperties(bool responses) => new() {
        ["model"] = RoutingModel(),
        ["stream"] = Boolean(),
        ["temperature"] = Number("0", "2"),
        ["top_p"] = Number("0", "1"),
        ["parallel_tool_calls"] = Boolean(),
        ["tools"] = Array(FunctionTool(responses), 0, 128),
        ["tool_choice"] = ToolChoice(responses),
        [responses ? "text" : "response_format"] = responses
            ? Object(new() { ["format"] = ResponseFormat(true) }, "format")
            : ResponseFormat(false)
    };

    private static OpenApiSchema Images() => Object(new() {
        ["model"] = RoutingModel(), ["prompt"] = Text(),
        ["n"] = new OpenApiSchema {
            Type = JsonSchemaType.Integer, Minimum = "1",
            Maximum = SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount.ToString(CultureInfo.InvariantCulture),
            Description = "Defaults to one. The resolved provider can enforce a lower limit (currently four for image adapters)."
        },
        ["size"] = Tokens("256x256", "512x512", "1024x1024", "1024x1536", "1536x1024", "auto"),
        ["quality"] = Tokens("standard", "hd", "low", "medium", "high", "auto"),
        ["response_format"] = Tokens("b64_json"),
        ["output_format"] = Tokens("png", "jpeg", "webp")
    }, "model", "prompt");

    private static OpenApiSchema ChatMessage(string role) {
        var properties = new Dictionary<string, IOpenApiSchema> {
            ["role"] = Tokens(role),
            ["content"] = MessageContent(false, role)
        };
        if (role != "tool") {
            properties["name"] = Name();
        }
        if (role == "tool") {
            properties["tool_call_id"] = Name();
            return Object(properties, "role", "content", "tool_call_id");
        }
        if (role != "assistant") {
            return Object(properties, "role", "content");
        }
        var content = properties["content"];
        properties["content"] = Any(content, new OpenApiSchema { Type = JsonSchemaType.Null });
        properties["tool_calls"] = Array(Object(new() {
            ["id"] = Name(), ["type"] = Tokens("function"),
            ["function"] = Object(new() { ["name"] = Name(), ["arguments"] = Text(MaximumSchemaCharacters) }, "name", "arguments")
        }, "id", "type", "function"), 1, 128);
        var assistant = Object(properties, "role");
        assistant.AnyOf = [
            new OpenApiSchema { Required = new HashSet<string> { "content" },
                Properties = new Dictionary<string, IOpenApiSchema> { ["content"] = content } },
            new OpenApiSchema { Required = new HashSet<string> { "tool_calls" } }
        ];
        return assistant;
    }

    private static OpenApiSchema ResponseInput() {
        var variants = new List<IOpenApiSchema>();
        foreach (var role in new[] { "system", "developer", "user", "assistant" }) {
            variants.Add(Object(new() {
                ["type"] = Tokens("message"), ["role"] = Tokens(role), ["content"] = MessageContent(true, role)
            }, "role", "content"));
        }
        variants.Add(Object(new() {
            ["type"] = Tokens("function_call"), ["id"] = Name(), ["call_id"] = Name(), ["name"] = Name(),
            ["arguments"] = Text(MaximumSchemaCharacters), ["status"] = Tokens("in_progress", "completed", "incomplete")
        }, "type", "call_id", "name", "arguments"));
        variants.Add(Object(new() {
            ["type"] = Tokens("function_call_output"), ["call_id"] = Name(), ["output"] = Text()
        }, "type", "call_id", "output"));
        var reasoning = Object(new() {
            ["type"] = Tokens("reasoning"), ["id"] = Name(),
            ["summary"] = ReasoningParts("summary_text"), ["content"] = ReasoningParts("reasoning_text"),
            ["encrypted_content"] = Nullable(Text()), ["status"] = Nullable(Tokens("in_progress", "completed", "incomplete"))
        }, "type");
        reasoning.AnyOf = new[] { "summary", "content", "encrypted_content" }
            .Select(name => (IOpenApiSchema)new OpenApiSchema { Required = new HashSet<string> { name } }).ToList();
        variants.Add(reasoning);
        return new() { AnyOf = variants };
    }

    private static OpenApiSchema ReasoningParts(string type) =>
        Array(Object(new() { ["type"] = Tokens(type), ["text"] = Text(minimum: 0) }, "type", "text"), 0, 256);

    private static OpenApiSchema MessageContent(bool responses, string role) {
        var textProperties = new Dictionary<string, IOpenApiSchema> {
            ["type"] = Tokens(responses ? role == "assistant" ? "output_text" : "input_text" : "text"),
            ["text"] = Text()
        };
        if (responses && role == "assistant") {
            textProperties["annotations"] = new OpenApiSchema { Type = JsonSchemaType.Array, MaxItems = 0 };
        }
        var textPart = Object(textProperties, "type", "text");
        var parts = role == "user" ? Any(textPart, ImagePart(responses)) : textPart;
        return Any(Text(), Array(parts, 1, 256));
    }

    private static OpenApiSchema ImagePart(bool responses) {
        var url = new OpenApiSchema {
            Type = JsonSchemaType.String,
            Pattern = "^data:image/(png|jpeg|webp);base64,(?=[A-Za-z0-9+/])(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$",
            Description = "Nonempty valid base64 with no whitespace. The complete request-body limit bounds image input."
        };
        return Object(new() {
            ["type"] = Tokens(responses ? "input_image" : "image_url"),
            ["image_url"] = responses ? url : Object(new() {
                ["url"] = url, ["detail"] = Tokens("auto", "low", "high")
            }, "url")
        }, "type", "image_url");
    }

    private static OpenApiSchema FunctionTool(bool responses) {
        var definition = Object(new() {
            ["name"] = Name(),
            ["description"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 4096 },
            ["parameters"] = JsonObjectValue(),
            ["strict"] = new OpenApiSchema { Type = JsonSchemaType.Boolean | JsonSchemaType.Null }
        }, "name");
        if (!responses) {
            return Object(new() { ["type"] = Tokens("function"), ["function"] = definition }, "type", "function");
        }
        definition.Properties!["type"] = Tokens("function");
        definition.Required!.Add("type");
        return definition;
    }

    private static OpenApiSchema ToolChoice(bool responses) {
        var choice = responses
            ? Object(new() { ["type"] = Tokens("function"), ["name"] = Name() }, "type", "name")
            : Object(new() { ["type"] = Tokens("function"),
                ["function"] = Object(new() { ["name"] = Name() }, "name") }, "type", "function");
        choice.Description = "The selected name must occur in this request's tools array.";
        return Any(Tokens("none", "auto", "required"), choice);
    }

    private static OpenApiSchema ResponseFormat(bool responses) {
        var schema = Object(new() {
            ["name"] = Name(), ["schema"] = JsonObjectValue(), ["description"] = Text(4096), ["strict"] = Boolean()
        }, "name", "schema");
        OpenApiSchema format;
        if (responses) {
            schema.Properties!["type"] = Tokens("json_schema");
            schema.Required!.Add("type");
            format = schema;
        } else {
            format = Object(new() { ["type"] = Tokens("json_schema"), ["json_schema"] = schema }, "type", "json_schema");
        }
        return Any(Object(new() { ["type"] = Tokens("text", "json_object") }, "type"), format);
    }

    private static OpenApiSchema RoutingModel() => new() {
        Type = JsonSchemaType.String, MinLength = 80, MaxLength = 80,
        Pattern = "^sp1\\.(?!0{32}\\.)[0-9a-f]{32}\\.[A-Za-z0-9_-]{42}[AEIMQUYcgkosw048]$",
        Description = "Opaque canonical sp1 routing identifier returned by the catalog; never substitute an upstream model name."
    };

    private static OpenApiSchema Name() => new() {
        Type = JsonSchemaType.String, MinLength = 1, MaxLength = 128, Pattern = "^[A-Za-z0-9_.-]+$"
    };

    private static OpenApiSchema Text(int maximum = MaximumTextCharacters, int minimum = 1) => new() {
        Type = JsonSchemaType.String, MinLength = minimum, MaxLength = maximum, Pattern = "^[^\\u0000]*$"
    };

    private static OpenApiSchema JsonObjectValue() => new() {
        Type = JsonSchemaType.Object, AdditionalPropertiesAllowed = true,
        Description = $"An arbitrary JSON object whose raw JSON text is at most {MaximumSchemaCharacters} UTF-16 code units."
    };

    private static OpenApiSchema OutputTokens() => new() {
        Type = JsonSchemaType.Integer, Minimum = "1",
        Maximum = SharedProviderRelaySupportDescriptor.MaximumAllowedOutputTokens.ToString(CultureInfo.InvariantCulture),
        Description = "The resolved provider's configured maximum may be lower."
    };

    private static OpenApiSchema Number(string minimum, string maximum) => new() {
        Type = JsonSchemaType.Number, Minimum = minimum, Maximum = maximum
    };

    private static OpenApiSchema Boolean() => new() { Type = JsonSchemaType.Boolean };
    private static OpenApiSchema FalseOnly(string description) => new() {
        Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(false)!], Description = description
    };
    private static OpenApiSchema Nullable(IOpenApiSchema schema) => Any(schema, new OpenApiSchema { Type = JsonSchemaType.Null });
    private static OpenApiSchema Tokens(params string[] values) => new() {
        Type = JsonSchemaType.String, Enum = values.Select(value => (JsonNode)JsonValue.Create(value)!).ToList()
    };
    private static OpenApiSchema Any(params IOpenApiSchema[] variants) => new() { AnyOf = variants };
    private static OpenApiSchema Array(IOpenApiSchema item, int minimum, int maximum) => new() {
        Type = JsonSchemaType.Array, Items = item, MinItems = minimum, MaxItems = maximum
    };
    private static OpenApiSchema Object(Dictionary<string, IOpenApiSchema> properties, params string[] required) => new() {
        Type = JsonSchemaType.Object, Properties = properties,
        Required = required.ToHashSet(StringComparer.Ordinal), AdditionalPropertiesAllowed = false
    };
}
