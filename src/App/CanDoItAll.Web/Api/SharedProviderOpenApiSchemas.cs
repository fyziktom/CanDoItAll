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
        properties["messages"] = Describe(Array(Any(
            ChatMessage("system"), ChatMessage("developer"), ChatMessage("user"),
            ChatMessage("assistant"), ChatMessage("tool")), 1, 256),
            "Conversation so far, 1 to 256 messages in order; each is one of the role variants. Every assistant " +
            "tool call must be answered by a tool message with its tool_call_id before the next other message.");
        properties["stream_options"] = Describe(Object(new() {
            ["include_usage"] = Describe(Boolean(), "True asks the provider to report token usage in the stream.")
        }, "include_usage"), "Streaming options; accepted only when stream is true.");
        properties["reasoning_effort"] = Describe(
            Nullable(Tokens("none", "minimal", "low", "medium", "high", "xhigh", "max")),
            "Reasoning effort for this request; it must be one of the model's thinking.allowedEfforts in the " +
            "catalog, so any value is rejected for a model without thinking support. Null or omitted applies the " +
            "publisher's default effort, if any.");
        properties["stop"] = Describe(Any(Text(256), Array(Text(256), 1, 4)),
            "One stop sequence, or an array of 1 to 4, each 1 to 256 UTF-16 code units.");
        properties["max_tokens"] = Describe(OutputTokens(),
            "Maximum number of tokens to generate; the resolved provider's configured maximum may be lower. Send at " +
            "most one of max_tokens and max_completion_tokens.");
        properties["max_completion_tokens"] = Describe(OutputTokens(),
            "Maximum number of tokens to generate; the resolved provider's configured maximum may be lower. Send at " +
            "most one of max_tokens and max_completion_tokens.");
        var schema = Object(properties, "model", "messages");
        schema.Not = new OpenApiSchema { Required = new HashSet<string> { "max_tokens", "max_completion_tokens" } };
        schema.AnyOf = [
            new OpenApiSchema { Not = new OpenApiSchema { Required = new HashSet<string> { "stream_options" } } },
            new OpenApiSchema { Required = new HashSet<string> { "stream" }, Properties = new Dictionary<string, IOpenApiSchema> {
                ["stream"] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Enum = [JsonValue.Create(true)!],
                    Description = "Must be true when stream_options is present." }
            } }
        ];
        return schema;
    }

    private static OpenApiSchema Responses() {
        var properties = TextGenerationProperties(true);
        properties["input"] = Describe(Any(Text(), Array(ResponseInput(), 1, 256)),
            "Input text of 1 to 1,048,576 UTF-16 code units, or 1 to 256 input items: messages, earlier function " +
            "calls with their outputs, and earlier reasoning items.");
        properties["instructions"] = Describe(Text(),
            "Instructions for the model, 1 to 1,048,576 UTF-16 code units.");
        properties["max_output_tokens"] = Describe(OutputTokens(),
            "Maximum number of output tokens; the resolved provider's configured maximum may be lower.");
        properties["reasoning"] = Describe(Nullable(Object(new() {
            ["effort"] = Describe(Nullable(Tokens("none", "minimal", "low", "medium", "high", "xhigh", "max")),
                "Reasoning effort; it must be one of the model's thinking.allowedEfforts in the catalog, so any " +
                "value is rejected for a model without thinking support. Null applies the publisher's default " +
                "effort, if any.")
        })), "Reasoning settings. Null or omitted applies the publisher's default effort, if any.");
        properties["store"] = FalseOnly("Responses are stateless. Omitted store is normalized to false.");
        properties["background"] = FalseOnly("Only foreground execution is supported.");
        return Object(properties, "model", "input");
    }

    private static Dictionary<string, IOpenApiSchema> TextGenerationProperties(bool responses) => new() {
        ["model"] = RoutingModel(),
        ["stream"] = Describe(Boolean(),
            "True returns the result as server-sent events (text/event-stream) and requires the streaming " +
            "capability. Omitted or false returns one JSON response."),
        ["temperature"] = Describe(Number("0", "2"),
            "Sampling temperature from 0 through 2. The relay removes it for models whose catalog thinking entry " +
            "sets omitTemperature."),
        ["top_p"] = Describe(Number("0", "1"), "Nucleus sampling probability mass from 0 through 1."),
        ["parallel_tool_calls"] = Describe(Boolean(),
            "Whether the model may call several tools at once; true requires the parallel-function-tools capability."),
        ["tools"] = Describe(Array(FunctionTool(responses), 0, 128),
            "Client-executed function tools the model may call; when present, 1 to 128 tools. The host never runs " +
            "them: tool calls are returned to the client. Requires the function-tools capability."),
        ["tool_choice"] = Describe(ToolChoice(responses),
            "Which tool the model may use: none, auto or required, or an object naming one function from tools. " +
            "Requires the function-tools capability."),
        [responses ? "text" : "response_format"] = responses
            ? Describe(Object(new() {
                ["format"] = Describe(ResponseFormat(true),
                    "Output format: plain text, any JSON object, or JSON that follows a JSON Schema. The JSON " +
                    "formats require the structured-output capability.")
            }, "format"), "Text output settings.")
            : Describe(ResponseFormat(false),
                "Output format: plain text, any JSON object, or JSON that follows a JSON Schema. The JSON formats " +
                "require the structured-output capability.")
    };

    private static OpenApiSchema Images() => Object(new() {
        ["model"] = RoutingModel(),
        ["prompt"] = Describe(Text(), "Description of the images to generate, 1 to 1,048,576 UTF-16 code units."),
        ["n"] = new OpenApiSchema {
            Type = JsonSchemaType.Integer, Minimum = "1",
            Maximum = SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount.ToString(CultureInfo.InvariantCulture),
            Description = "Defaults to one. The resolved provider can enforce a lower limit (currently four for image adapters)."
        },
        ["size"] = Describe(Tokens("256x256", "512x512", "1024x1024", "1024x1536", "1536x1024", "auto"),
            "Image size in pixels, or auto. Omitted uses the resolved provider's default."),
        ["quality"] = Describe(Tokens("standard", "hd", "low", "medium", "high", "auto"),
            "Image quality. Omitted uses the resolved provider's default."),
        ["response_format"] = Describe(Tokens("b64_json"),
            "Result encoding; only b64_json. Images are always returned as base64 data, never as URLs."),
        ["output_format"] = Describe(Tokens("png", "jpeg", "webp"),
            "Image file format. Omitted uses the resolved provider's default.")
    }, "model", "prompt");

    private static OpenApiSchema ChatMessage(string role) {
        var properties = new Dictionary<string, IOpenApiSchema> {
            ["role"] = Describe(Tokens(role), $"Author of the message: {role}."),
            ["content"] = MessageContent(false, role)
        };
        if (role != "tool") {
            properties["name"] = Describe(Name(),
                "Optional participant name, 1 to 128 ASCII letters, digits, underscores, dots or hyphens.");
        }
        if (role == "tool") {
            properties["tool_call_id"] = Describe(Name(),
                "Identifier of the assistant tool call this message answers; it must match an unanswered call of the " +
                "preceding assistant message. Requires the function-tools capability.");
            return Object(properties, "role", "content", "tool_call_id");
        }
        if (role != "assistant") {
            return Object(properties, "role", "content");
        }
        var content = properties["content"];
        properties["content"] = Describe(Any(content, new OpenApiSchema { Type = JsonSchemaType.Null }),
            "Message text or text parts; null or omitted when the message only makes tool calls.");
        properties["tool_calls"] = Describe(Array(Object(new() {
            ["id"] = Describe(Name(), "Identifier of the tool call; the answering tool message repeats it."),
            ["type"] = Describe(Tokens("function"), "Kind of tool call; only function."),
            ["function"] = Describe(Object(new() {
                ["name"] = Describe(Name(), "Name of the called function."),
                ["arguments"] = Describe(Text(MaximumSchemaCharacters),
                    "Arguments produced by the model, as JSON text of 1 to 262,144 UTF-16 code units.")
            }, "name", "arguments"), "The called function and its arguments.")
        }, "id", "type", "function"), 1, 128),
            "Tool calls the assistant made earlier, 1 to 128 with distinct ids; each must be answered by a tool " +
            "message before the next other message. Requires the function-tools capability.");
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
                ["type"] = Describe(Tokens("message"), "Kind of input item: message. It may be omitted for messages."),
                ["role"] = Describe(Tokens(role), $"Author of the message: {role}."),
                ["content"] = MessageContent(true, role)
            }, "role", "content"));
        }
        variants.Add(Object(new() {
            ["type"] = Describe(Tokens("function_call"),
                "Kind of input item: function_call, a tool call the model made. Requires the function-tools " +
                "capability."),
            ["id"] = Describe(Name(), "Identifier of the item as returned by the provider."),
            ["call_id"] = Describe(Name(), "Identifier of the call; the matching function_call_output repeats it."),
            ["name"] = Describe(Name(), "Name of the called function."),
            ["arguments"] = Describe(Text(MaximumSchemaCharacters),
                "Arguments produced by the model, as JSON text of 1 to 262,144 UTF-16 code units."),
            ["status"] = Describe(Tokens("in_progress", "completed", "incomplete"),
                "Status of the call as reported by the provider.")
        }, "type", "call_id", "name", "arguments"));
        variants.Add(Object(new() {
            ["type"] = Describe(Tokens("function_call_output"),
                "Kind of input item: function_call_output, the client's result of a function call. Requires the " +
                "function-tools capability."),
            ["call_id"] = Describe(Name(), "call_id of the function_call that this output answers."),
            ["output"] = Describe(Text(), "Result of the function as text, 1 to 1,048,576 UTF-16 code units.")
        }, "type", "call_id", "output"));
        var reasoning = Object(new() {
            ["type"] = Describe(Tokens("reasoning"),
                "Kind of input item: reasoning, a reasoning item the provider returned earlier. It needs at least " +
                "one of summary, content and encrypted_content."),
            ["id"] = Describe(Name(), "Identifier of the reasoning item as returned by the provider."),
            ["summary"] = Describe(ReasoningParts("summary_text"), "Reasoning summary parts, at most 256."),
            ["content"] = Describe(ReasoningParts("reasoning_text"), "Reasoning text parts, at most 256."),
            ["encrypted_content"] = Describe(Nullable(Text()),
                "Encrypted reasoning content as returned by the provider; null is allowed."),
            ["status"] = Describe(Nullable(Tokens("in_progress", "completed", "incomplete")),
                "Status of the item as reported by the provider; null is allowed.")
        }, "type");
        reasoning.AnyOf = new[] { "summary", "content", "encrypted_content" }
            .Select(name => (IOpenApiSchema)new OpenApiSchema { Required = new HashSet<string> { name } }).ToList();
        variants.Add(reasoning);
        return new() { AnyOf = variants };
    }

    private static OpenApiSchema ReasoningParts(string type) =>
        Array(Object(new() {
            ["type"] = Describe(Tokens(type), $"Kind of part: {type}."),
            ["text"] = Describe(Text(minimum: 0),
                "Text of the part, at most 1,048,576 UTF-16 code units; may be empty.")
        }, "type", "text"), 0, 256);

    private static OpenApiSchema MessageContent(bool responses, string role) {
        var textType = responses ? role == "assistant" ? "output_text" : "input_text" : "text";
        var textProperties = new Dictionary<string, IOpenApiSchema> {
            ["type"] = Describe(Tokens(textType), $"Kind of part: {textType}."),
            ["text"] = Describe(Text(), "Text of the part, 1 to 1,048,576 UTF-16 code units.")
        };
        if (responses && role == "assistant") {
            textProperties["annotations"] = new OpenApiSchema { Type = JsonSchemaType.Array, MaxItems = 0,
                Description = "Must be empty; annotations are not supported." };
        }
        var textPart = Object(textProperties, "type", "text");
        var parts = role == "user" ? Any(textPart, ImagePart(responses)) : textPart;
        return Describe(Any(Text(), Array(parts, 1, 256)), role == "user"
            ? "Message text of 1 to 1,048,576 UTF-16 code units, or 1 to 256 text and image parts."
            : "Message text of 1 to 1,048,576 UTF-16 code units, or 1 to 256 text parts.");
    }

    private static OpenApiSchema ImagePart(bool responses) {
        var url = new OpenApiSchema {
            Type = JsonSchemaType.String,
            Pattern = "^data:image/(png|jpeg|webp);base64,(?=[A-Za-z0-9+/])(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$",
            Description = "Nonempty valid base64 with no whitespace. The complete request-body limit bounds image input."
        };
        var type = responses ? "input_image" : "image_url";
        return Object(new() {
            ["type"] = Describe(Tokens(type),
                $"Kind of part: {type}. Image parts are accepted only in user messages and require the vision-input " +
                "capability."),
            ["image_url"] = responses ? url : Describe(Object(new() {
                ["url"] = url,
                ["detail"] = Describe(Tokens("auto", "low", "high"), "Requested level of image detail.")
            }, "url"), "The image as a data URI, with an optional detail level.")
        }, "type", "image_url");
    }

    private static OpenApiSchema FunctionTool(bool responses) {
        var definition = Object(new() {
            ["name"] = Describe(Name(), "Function name, 1 to 128 ASCII letters, digits, underscores, dots or hyphens."),
            ["description"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 4096,
                Description = "What the function does, for the model; at most 4,096 UTF-16 code units." },
            ["parameters"] = Describe(JsonObjectValue(),
                "JSON Schema of the function's arguments, as a JSON object whose raw JSON text is at most 262,144 " +
                "UTF-16 code units."),
            ["strict"] = new OpenApiSchema { Type = JsonSchemaType.Boolean | JsonSchemaType.Null,
                Description = "True asks the model to follow the parameter schema exactly; null is allowed." }
        }, "name");
        if (!responses) {
            return Object(new() {
                ["type"] = Describe(Tokens("function"), "Kind of tool; only function."),
                ["function"] = Describe(definition, "The function the model may call.")
            }, "type", "function");
        }
        definition.Properties!["type"] = Describe(Tokens("function"), "Kind of tool; only function.");
        definition.Required!.Add("type");
        return definition;
    }

    private static OpenApiSchema ToolChoice(bool responses) {
        var choice = responses
            ? Object(new() {
                ["type"] = Describe(Tokens("function"), "Kind of choice; only function."),
                ["name"] = Describe(Name(), "Name of a function declared in tools.")
            }, "type", "name")
            : Object(new() { ["type"] = Describe(Tokens("function"), "Kind of choice; only function."),
                ["function"] = Describe(Object(new() {
                    ["name"] = Describe(Name(), "Name of a function declared in tools.")
                }, "name"), "The function the model must call.") }, "type", "function");
        choice.Description = "The selected name must occur in this request's tools array.";
        return Any(Tokens("none", "auto", "required"), choice);
    }

    private static OpenApiSchema ResponseFormat(bool responses) {
        var schema = Object(new() {
            ["name"] = Describe(Name(),
                "Name of the output schema, 1 to 128 ASCII letters, digits, underscores, dots or hyphens."),
            ["schema"] = Describe(JsonObjectValue(),
                "JSON Schema the output must follow, as a JSON object whose raw JSON text is at most 262,144 UTF-16 " +
                "code units."),
            ["description"] = Describe(Text(4096),
                "Description of the output for the model, 1 to 4,096 UTF-16 code units."),
            ["strict"] = Describe(Boolean(), "True asks the model to follow the schema exactly.")
        }, "name", "schema");
        OpenApiSchema format;
        if (responses) {
            schema.Properties!["type"] = Describe(Tokens("json_schema"), "Kind of format: json_schema.");
            schema.Required!.Add("type");
            format = schema;
        } else {
            format = Object(new() {
                ["type"] = Describe(Tokens("json_schema"), "Kind of format: json_schema."),
                ["json_schema"] = Describe(schema, "Name and JSON Schema of the structured output.")
            }, "type", "json_schema");
        }
        return Any(Object(new() {
            ["type"] = Describe(Tokens("text", "json_object"),
                "Kind of format: text for plain text, or json_object for any JSON object.")
        }, "type"), format);
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
    private static OpenApiSchema Describe(OpenApiSchema schema, string description) {
        schema.Description = description;
        return schema;
    }
    private static OpenApiSchema Array(IOpenApiSchema item, int minimum, int maximum) => new() {
        Type = JsonSchemaType.Array, Items = item, MinItems = minimum, MaxItems = maximum
    };
    private static OpenApiSchema Object(Dictionary<string, IOpenApiSchema> properties, params string[] required) => new() {
        Type = JsonSchemaType.Object, Properties = properties,
        Required = required.ToHashSet(StringComparer.Ordinal), AdditionalPropertiesAllowed = false
    };
}
