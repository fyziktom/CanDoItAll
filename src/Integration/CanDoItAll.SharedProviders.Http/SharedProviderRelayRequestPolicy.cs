using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.SharedProviders.Http;

public sealed class SharedProviderRelayRequestPolicy : ISharedProviderRelayRequestPolicy
{
    private const int MaximumDepth = 32;
    private const int MaximumMessages = 256;
    private const int MaximumTools = 128;
    private const int MaximumStopSequences = 4;
    private const int MaximumTextCharacters = 1024 * 1024;
    private const int MaximumSchemaCharacters = 256 * 1024;
    private const string ResponsesStorePropertyName = "store";

    private static readonly FrozenSet<string> ChatProperties = Set(
        "model",
        "messages",
        "stream",
        "tools",
        "tool_choice",
        "parallel_tool_calls",
        "response_format",
        "temperature",
        "top_p",
        "stop",
        "max_tokens",
        "max_completion_tokens");

    private static readonly FrozenSet<string> ResponsesProperties = Set(
        "model",
        "input",
        "instructions",
        "stream",
        "tools",
        "tool_choice",
        "text",
        ResponsesStorePropertyName,
        "temperature",
        "top_p",
        "max_output_tokens");

    private static readonly FrozenSet<string> ImagesProperties = Set(
        "model",
        "prompt",
        "n",
        "size",
        "quality",
        "response_format",
        "output_format");

    public SharedProviderRelayRequestPolicyResult Normalize(
        SharedProviderRelayOperation operation,
        ReadOnlyMemory<byte> payloadUtf8,
        SharedProviderRelaySupportDescriptor support)
    {
        ArgumentNullException.ThrowIfNull(support);
        if (!Enum.IsDefined(operation) || !support.Operations.Contains(operation))
        {
            return Reject(
                SharedProviderFailureCategory.Validation,
                "shared_provider_operation_not_supported",
                "The requested operation is not supported.",
                "model");
        }

        if (payloadUtf8.IsEmpty || payloadUtf8.Length > support.MaximumRequestBytes)
        {
            return Reject(
                SharedProviderFailureCategory.Validation,
                "shared_provider_request_too_large",
                "The request body exceeds the allowed size.",
                parameter: null);
        }

        try
        {
            RejectDuplicateProperties(payloadUtf8.Span);
            using var document = JsonDocument.Parse(payloadUtf8, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaximumDepth
            });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Validation("The request body must be a JSON object.");
            }

            var root = document.RootElement;
            var allowedProperties = operation switch
            {
                SharedProviderRelayOperation.ChatCompletions => ChatProperties,
                SharedProviderRelayOperation.Responses => ResponsesProperties,
                SharedProviderRelayOperation.ImageGenerations => ImagesProperties,
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            if (!HasOnlyProperties(root, allowedProperties, out var unknownProperty))
            {
                return Validation(
                    "The request contains an unsupported field.",
                    unknownProperty);
            }

            if (!TryReadRoutingModelId(root, out var routingModelId))
            {
                return Reject(
                    SharedProviderFailureCategory.NotFound,
                    "shared_provider_model_not_found",
                    "The requested shared-provider model was not found.",
                    "model");
            }

            var requiredCapabilities = new HashSet<SharedProviderCapability>();
            var validation = operation switch
            {
                SharedProviderRelayOperation.ChatCompletions =>
                    ValidateChat(root, support, requiredCapabilities),
                SharedProviderRelayOperation.Responses =>
                    ValidateResponses(root, support, requiredCapabilities),
                SharedProviderRelayOperation.ImageGenerations =>
                    ValidateImages(root, support, requiredCapabilities),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            if (validation is not null)
            {
                return validation;
            }

            var stream = root.TryGetProperty("stream", out var streamElement) &&
                streamElement.ValueKind == JsonValueKind.True;
            if (stream)
            {
                requiredCapabilities.Add(SharedProviderCapability.Streaming);
            }

            var imageCount = operation == SharedProviderRelayOperation.ImageGenerations
                ? ReadValidatedImageCount(root)
                : 0;
            return new SharedProviderRelayRequestPolicyResult.Accepted(
                new SharedProviderRelayNormalizedRequest(
                    operation,
                    routingModelId,
                    stream,
                    Canonicalize(root, operation),
                    requiredCapabilities,
                    imageCount));
        }
        catch (JsonException)
        {
            return Validation("The request body is malformed JSON.");
        }
        catch (DecoderFallbackException)
        {
            return Validation("The request body is not valid UTF-8.");
        }
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateChat(
        JsonElement root,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities)
    {
        requiredCapabilities.Add(SharedProviderCapability.ChatCompletions);
        if (!root.TryGetProperty("messages", out var messages) ||
            messages.ValueKind != JsonValueKind.Array ||
            messages.GetArrayLength() is < 1 or > MaximumMessages)
        {
            return Validation("A bounded messages array is required.", "messages");
        }

        foreach (var message in messages.EnumerateArray())
        {
            var failure = ValidateChatMessage(message, support, requiredCapabilities);
            if (failure is not null)
            {
                return failure;
            }
        }

        return ValidateCommonTextFeatures(
            root,
            support,
            requiredCapabilities,
            SharedProviderRelayOperation.ChatCompletions,
            "response_format");
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateResponses(
        JsonElement root,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities)
    {
        requiredCapabilities.Add(SharedProviderCapability.Responses);
        if (!root.TryGetProperty("input", out var input))
        {
            return Validation("The Responses input field is required.", "input");
        }

        if (input.ValueKind == JsonValueKind.String)
        {
            if (!IsBoundedText(input.GetString(), MaximumTextCharacters))
            {
                return Validation("The Responses input text is invalid.", "input");
            }
        }
        else if (input.ValueKind == JsonValueKind.Array)
        {
            if (input.GetArrayLength() is < 1 or > MaximumMessages)
            {
                return Validation("The Responses input array is invalid.", "input");
            }

            foreach (var item in input.EnumerateArray())
            {
                var failure = ValidateResponseInputItem(item, support, requiredCapabilities);
                if (failure is not null)
                {
                    return failure;
                }
            }
        }
        else
        {
            return Validation("The Responses input field is invalid.", "input");
        }

        if (root.TryGetProperty("instructions", out var instructions) &&
            (instructions.ValueKind != JsonValueKind.String ||
                !IsBoundedText(instructions.GetString(), MaximumTextCharacters)))
        {
            return Validation("The Responses instructions field is invalid.", "instructions");
        }

        if (root.TryGetProperty(ResponsesStorePropertyName, out var store) &&
            store.ValueKind != JsonValueKind.False)
        {
            return Validation(
                "Only non-stored Responses requests are supported.",
                ResponsesStorePropertyName);
        }

        return ValidateCommonTextFeatures(
            root,
            support,
            requiredCapabilities,
            SharedProviderRelayOperation.Responses,
            "text");
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateImages(
        JsonElement root,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities)
    {
        requiredCapabilities.Add(SharedProviderCapability.ImageGenerations);
        requiredCapabilities.Add(SharedProviderCapability.Base64Json);
        if (!support.SupportsBase64Images)
        {
            return Validation("The resolved provider does not support base64 image responses.", "response_format");
        }

        if (!root.TryGetProperty("prompt", out var prompt) ||
            prompt.ValueKind != JsonValueKind.String ||
            !IsBoundedText(prompt.GetString(), MaximumTextCharacters))
        {
            return Validation("A bounded image prompt is required.", "prompt");
        }

        if (!TryReadImageCount(root, out var count) ||
            count is < 1 || count > support.MaximumImageCount)
        {
            return Validation("The requested image count is invalid.", "n");
        }

        if (root.TryGetProperty("size", out var size) &&
            (size.ValueKind != JsonValueKind.String ||
                size.GetString() is not ("256x256" or "512x512" or "1024x1024" or "1024x1536" or "1536x1024" or "auto")))
        {
            return Validation("The requested image size is unsupported.", "size");
        }

        if (root.TryGetProperty("quality", out var quality) &&
            (quality.ValueKind != JsonValueKind.String ||
                quality.GetString() is not ("standard" or "hd" or "low" or "medium" or "high" or "auto")))
        {
            return Validation("The requested image quality is unsupported.", "quality");
        }

        if (root.TryGetProperty("response_format", out var responseFormat) &&
            (responseFormat.ValueKind != JsonValueKind.String ||
                !string.Equals(responseFormat.GetString(), "b64_json", StringComparison.Ordinal)))
        {
            return Validation("Only b64_json image responses are supported.", "response_format");
        }

        if (root.TryGetProperty("output_format", out var outputFormat) &&
            (outputFormat.ValueKind != JsonValueKind.String ||
                outputFormat.GetString() is not ("png" or "jpeg" or "webp")))
        {
            return Validation("The requested image output format is unsupported.", "output_format");
        }

        return null;
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateCommonTextFeatures(
        JsonElement root,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities,
        SharedProviderRelayOperation operation,
        string structuredPropertyName)
    {
        if (root.TryGetProperty("stream", out var stream) &&
            stream.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return Validation("The stream field must be a boolean.", "stream");
        }

        if (stream.ValueKind == JsonValueKind.True &&
            support.StreamingMode != SharedProviderStreamingMode.ServerSentEvents)
        {
            return Validation("The resolved provider does not support streaming.", "stream");
        }

        IReadOnlySet<string> declaredFunctionNames = Set();
        if (root.TryGetProperty("tools", out var tools))
        {
            if (!support.SupportsFunctionTools)
            {
                return Validation("The resolved provider does not support function tools.", "tools");
            }

            var toolsFailure = ValidateFunctionTools(tools, operation, out declaredFunctionNames);
            if (toolsFailure is not null)
            {
                return toolsFailure;
            }

            requiredCapabilities.Add(SharedProviderCapability.FunctionTools);
        }

        if (root.TryGetProperty("tool_choice", out var toolChoice))
        {
            if (!support.SupportsFunctionTools ||
                !IsValidToolChoice(toolChoice, operation, declaredFunctionNames))
            {
                return Validation("The tool_choice field is unsupported or invalid.", "tool_choice");
            }

            requiredCapabilities.Add(SharedProviderCapability.FunctionTools);
        }

        if (root.TryGetProperty("parallel_tool_calls", out var parallel))
        {
            if (parallel.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return Validation("The parallel_tool_calls field must be a boolean.", "parallel_tool_calls");
            }

            if (!support.SupportsParallelFunctionTools)
            {
                return Validation(
                    "The resolved provider does not support parallel function tools.",
                    "parallel_tool_calls");
            }

            requiredCapabilities.Add(SharedProviderCapability.ParallelFunctionTools);
        }

        if (root.TryGetProperty(structuredPropertyName, out var structured))
        {
            var structuredFailure = ValidateStructuredOutput(
                structured,
                operation,
                structuredPropertyName,
                support.SupportsStructuredOutput);
            if (structuredFailure is not null)
            {
                return structuredFailure;
            }

            if (RequiresStructuredOutput(structured, structuredPropertyName))
            {
                requiredCapabilities.Add(SharedProviderCapability.StructuredOutput);
            }
        }

        foreach (var numericProperty in new[] { "temperature", "top_p" })
        {
            if (root.TryGetProperty(numericProperty, out var numeric) &&
                (numeric.ValueKind != JsonValueKind.Number ||
                    !numeric.TryGetDouble(out var value) ||
                    !double.IsFinite(value) ||
                    value < 0 ||
                    value > (numericProperty == "temperature" ? 2 : 1)))
            {
                return Validation("A generation parameter is outside its supported range.", numericProperty);
            }
        }

        if (root.TryGetProperty("stop", out var stop) && !IsValidStop(stop))
        {
            return Validation("The stop field is invalid.", "stop");
        }

        var outputProperties = new[] { "max_tokens", "max_completion_tokens", "max_output_tokens" };
        var presentOutputProperties = outputProperties
            .Where(property => root.TryGetProperty(property, out _))
            .ToArray();
        if (presentOutputProperties.Length > 1)
        {
            return Validation("Only one output-token limit may be supplied.", presentOutputProperties[1]);
        }

        if (presentOutputProperties.Length == 1)
        {
            var property = presentOutputProperties[0];
            var output = root.GetProperty(property);
            if (output.ValueKind != JsonValueKind.Number ||
                !output.TryGetInt32(out var outputTokens) ||
                outputTokens is <= 0 ||
                outputTokens > support.MaximumOutputTokens)
            {
                return Validation("The output-token limit is invalid.", property);
            }
        }

        return null;
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateChatMessage(
        JsonElement message,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities)
    {
        if (message.ValueKind != JsonValueKind.Object)
        {
            return Validation("A chat message must be an object.", "messages");
        }

        if (!message.TryGetProperty("role", out var role) ||
            role.ValueKind != JsonValueKind.String ||
            role.GetString() is not ("system" or "developer" or "user" or "assistant" or "tool"))
        {
            return Validation("A chat message role is invalid.", "messages");
        }

        string roleName = role.GetString()!;
        var allowedProperties = roleName switch
        {
            "system" or "developer" or "user" => Set("role", "content", "name"),
            "assistant" => Set("role", "content", "name", "tool_calls"),
            "tool" => Set("role", "content", "tool_call_id"),
            _ => throw new InvalidOperationException("The validated chat role is unsupported.")
        };
        if (!HasOnlyProperties(message, allowedProperties, out var unknown))
        {
            return Validation("A chat message contains an unsupported field.", unknown ?? "messages");
        }

        bool hasContent = message.TryGetProperty("content", out var content);
        bool hasToolCalls = message.TryGetProperty("tool_calls", out var toolCalls);
        if (hasContent && content.ValueKind != JsonValueKind.Null)
        {
            var contentFailure = ValidateMessageContent(
                content,
                support,
                requiredCapabilities,
                SharedProviderRelayOperation.ChatCompletions,
                allowsImageInput: roleName == "user");
            if (contentFailure is not null)
            {
                return contentFailure;
            }
        }

        bool permitsContentlessMessage = roleName == "assistant" && hasToolCalls;
        if ((!hasContent || content.ValueKind == JsonValueKind.Null) && !permitsContentlessMessage)
        {
            return Validation("Chat message content is required for this role.", "messages");
        }

        if (message.TryGetProperty("name", out var name) && !IsName(name))
        {
            return Validation("A chat message name is invalid.", "messages");
        }

        bool hasToolCallId = message.TryGetProperty("tool_call_id", out var toolCallId);
        if (roleName == "tool" && !hasToolCallId)
        {
            return Validation("A chat tool-call id is required for the tool role.", "messages");
        }

        if (hasToolCallId)
        {
            if (!support.SupportsFunctionTools || !IsName(toolCallId))
            {
                return Validation("A chat tool-call id is invalid.", "messages");
            }

            requiredCapabilities.Add(SharedProviderCapability.FunctionTools);
        }

        if (hasToolCalls)
        {
            if (!support.SupportsFunctionTools || !ValidateToolCalls(toolCalls))
            {
                return Validation("A chat tool call is invalid.", "messages");
            }

            requiredCapabilities.Add(SharedProviderCapability.FunctionTools);
        }

        return null;
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateResponseInputItem(
        JsonElement item,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return Validation("A Responses input item must be an object.", "input");
        }

        if (item.TryGetProperty("type", out var type) &&
            (type.ValueKind != JsonValueKind.String ||
                type.GetString() is not ("message" or "function_call_output")))
        {
            return Validation("A Responses input item type is unsupported.", "input");
        }

        var isFunctionOutput = type.ValueKind == JsonValueKind.String &&
            string.Equals(type.GetString(), "function_call_output", StringComparison.Ordinal);
        if (isFunctionOutput)
        {
            if (!HasOnlyProperties(item, Set("type", "call_id", "output"), out var unknown))
            {
                return Validation("A function output contains an unsupported field.", unknown ?? "input");
            }

            if (!support.SupportsFunctionTools ||
                !item.TryGetProperty("call_id", out var callId) ||
                !IsName(callId) ||
                !item.TryGetProperty("output", out var functionOutput) ||
                functionOutput.ValueKind != JsonValueKind.String ||
                !IsBoundedText(functionOutput.GetString(), MaximumTextCharacters))
            {
                return Validation("A function output is invalid.", "input");
            }

            requiredCapabilities.Add(SharedProviderCapability.FunctionTools);
            return null;
        }

        if (!HasOnlyProperties(item, Set("role", "content", "type"), out var messageUnknown))
        {
            return Validation("A Responses input message contains an unsupported field.", messageUnknown ?? "input");
        }

        if (!item.TryGetProperty("role", out var role) ||
            role.ValueKind != JsonValueKind.String ||
            role.GetString() is not ("system" or "developer" or "user" or "assistant"))
        {
            return Validation("A Responses input message role is required.", "input");
        }

        if (!item.TryGetProperty("content", out var content) || content.ValueKind == JsonValueKind.Null)
        {
            return Validation("Responses input message content is required.", "input");
        }

        return ValidateMessageContent(
            content,
            support,
            requiredCapabilities,
            SharedProviderRelayOperation.Responses,
            allowsImageInput: true);
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateMessageContent(
        JsonElement content,
        SharedProviderRelaySupportDescriptor support,
        ISet<SharedProviderCapability> requiredCapabilities,
        SharedProviderRelayOperation operation,
        bool allowsImageInput)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            return IsBoundedText(content.GetString(), MaximumTextCharacters)
                ? null
                : Validation("Message text is invalid.", "messages");
        }

        if (content.ValueKind != JsonValueKind.Array || content.GetArrayLength() is < 1 or > MaximumMessages)
        {
            return Validation("Message content is invalid.", "messages");
        }

        var expectedTextPartType = operation switch
        {
            SharedProviderRelayOperation.ChatCompletions => "text",
            SharedProviderRelayOperation.Responses => "input_text",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object ||
                !part.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String)
            {
                return Validation("A message content part is invalid.", "messages");
            }

            if (string.Equals(type.GetString(), expectedTextPartType, StringComparison.Ordinal))
            {
                if (!HasOnlyProperties(part, Set("type", "text"), out _) ||
                    !part.TryGetProperty("text", out var text) ||
                    text.ValueKind != JsonValueKind.String ||
                    !IsBoundedText(text.GetString(), MaximumTextCharacters))
                {
                    return Validation("A text content part is invalid.", "messages");
                }

                continue;
            }

            var imagePartType = type.GetString();
            if ((operation, imagePartType) is
                (SharedProviderRelayOperation.ChatCompletions, "image_url") or
                (SharedProviderRelayOperation.Responses, "input_image"))
            {
                if (!allowsImageInput)
                {
                    return Validation("Image content is supported only in user input.", "messages");
                }

                if (!support.SupportsVisionInput)
                {
                    return Validation("The resolved provider does not support vision input.", "messages");
                }

                if (!IsValidDataImagePart(part, operation))
                {
                    return Validation("Only bounded data-URI image input is supported.", "messages");
                }

                requiredCapabilities.Add(SharedProviderCapability.VisionInput);
                continue;
            }

            return Validation("A message content type is unsupported.", "messages");
        }

        return null;
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateFunctionTools(
        JsonElement tools,
        SharedProviderRelayOperation operation,
        out IReadOnlySet<string> declaredFunctionNames)
    {
        declaredFunctionNames = Set();
        if (tools.ValueKind != JsonValueKind.Array || tools.GetArrayLength() is < 1 or > MaximumTools)
        {
            return Validation("The tools field is invalid.", "tools");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in tools.EnumerateArray())
        {
            if (!TryReadFunctionToolName(tool, operation, out var name))
            {
                return Validation("Only bounded client-executed function tools are supported.", "tools");
            }

            names.Add(name);
        }

        declaredFunctionNames = names.ToFrozenSet(StringComparer.Ordinal);
        return null;
    }

    private static bool TryReadFunctionToolName(
        JsonElement tool,
        SharedProviderRelayOperation operation,
        out string name)
    {
        name = string.Empty;
        if (tool.ValueKind != JsonValueKind.Object ||
            !tool.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "function", StringComparison.Ordinal))
        {
            return false;
        }

        if (operation == SharedProviderRelayOperation.ChatCompletions)
        {
            if (!HasOnlyProperties(tool, Set("type", "function"), out _) ||
                !tool.TryGetProperty("function", out var function) ||
                !IsValidFunctionDefinition(function))
            {
                return false;
            }

            name = function.GetProperty("name").GetString()!;
            return true;
        }

        if (operation != SharedProviderRelayOperation.Responses ||
            !HasOnlyProperties(tool, Set("type", "name", "description", "parameters", "strict"), out _) ||
            !tool.TryGetProperty("name", out var responseName) ||
            !IsName(responseName))
        {
            return false;
        }

        if (tool.TryGetProperty("description", out var description) &&
            (description.ValueKind != JsonValueKind.String ||
                !IsBoundedText(description.GetString(), 4096)))
        {
            return false;
        }

        if (tool.TryGetProperty("parameters", out var parameters) &&
            (parameters.ValueKind != JsonValueKind.Object ||
                parameters.GetRawText().Length > MaximumSchemaCharacters))
        {
            return false;
        }

        if (tool.TryGetProperty("strict", out var strict) &&
            strict.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        name = responseName.GetString()!;
        return true;
    }

    private static bool IsValidFunctionDefinition(JsonElement function)
    {
        if (function.ValueKind != JsonValueKind.Object ||
            !HasOnlyProperties(function, Set("name", "description", "parameters", "strict"), out _) ||
            !function.TryGetProperty("name", out var name) ||
            !IsName(name))
        {
            return false;
        }

        if (function.TryGetProperty("description", out var description) &&
            (description.ValueKind != JsonValueKind.String ||
                !IsBoundedText(description.GetString(), 4096)))
        {
            return false;
        }

        if (function.TryGetProperty("parameters", out var parameters) &&
            (parameters.ValueKind != JsonValueKind.Object || parameters.GetRawText().Length > MaximumSchemaCharacters))
        {
            return false;
        }

        return !function.TryGetProperty("strict", out var strict) ||
            strict.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    private static bool IsValidToolChoice(
        JsonElement toolChoice,
        SharedProviderRelayOperation operation,
        IReadOnlySet<string> declaredFunctionNames)
    {
        if (toolChoice.ValueKind == JsonValueKind.String)
        {
            return toolChoice.GetString() is "none" or "auto" or "required";
        }

        if (toolChoice.ValueKind != JsonValueKind.Object ||
            !toolChoice.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "function", StringComparison.Ordinal))
        {
            return false;
        }

        JsonElement name;
        if (operation == SharedProviderRelayOperation.ChatCompletions)
        {
            if (!HasOnlyProperties(toolChoice, Set("type", "function"), out _) ||
                !toolChoice.TryGetProperty("function", out var function) ||
                function.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(function, Set("name"), out _) ||
                !function.TryGetProperty("name", out name))
            {
                return false;
            }
        }
        else if (operation == SharedProviderRelayOperation.Responses)
        {
            if (!HasOnlyProperties(toolChoice, Set("type", "name"), out _) ||
                !toolChoice.TryGetProperty("name", out name))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        return IsName(name) && declaredFunctionNames.Contains(name.GetString()!);
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected? ValidateStructuredOutput(
        JsonElement structured,
        SharedProviderRelayOperation operation,
        string propertyName,
        bool supportsStructuredOutput)
    {
        JsonElement format;
        if (propertyName == "text")
        {
            if (structured.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(structured, Set("format"), out _) ||
                !structured.TryGetProperty("format", out format))
            {
                return Validation("The Responses text format is invalid.", propertyName);
            }
        }
        else
        {
            format = structured;
        }

        if (format.ValueKind != JsonValueKind.Object ||
            !format.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String)
        {
            return Validation("The structured-output format is invalid.", propertyName);
        }

        var typeName = type.GetString();
        if (typeName == "text")
        {
            return HasOnlyProperties(format, Set("type"), out _)
                ? null
                : Validation("The text response format is invalid.", propertyName);
        }

        if (!supportsStructuredOutput || typeName is not ("json_object" or "json_schema"))
        {
            return Validation("The resolved provider does not support the requested structured output.", propertyName);
        }

        if (typeName == "json_object")
        {
            return HasOnlyProperties(format, Set("type"), out _)
                ? null
                : Validation("The JSON-object response format is invalid.", propertyName);
        }

        JsonElement schemaContainer;
        if (operation == SharedProviderRelayOperation.ChatCompletions)
        {
            if (!HasOnlyProperties(format, Set("type", "json_schema"), out _) ||
                !format.TryGetProperty("json_schema", out schemaContainer))
            {
                return Validation("The JSON-schema response format is invalid.", propertyName);
            }
        }
        else if (operation == SharedProviderRelayOperation.Responses)
        {
            schemaContainer = format;
        }
        else
        {
            return Validation("The JSON-schema response format is invalid.", propertyName);
        }

        var allowedSchemaProperties = operation == SharedProviderRelayOperation.Responses
            ? Set("type", "name", "description", "schema", "strict")
            : Set("name", "description", "schema", "strict");
        if (schemaContainer.ValueKind != JsonValueKind.Object ||
            !HasOnlyProperties(schemaContainer, allowedSchemaProperties, out _) ||
            !schemaContainer.TryGetProperty("name", out var name) ||
            !IsName(name) ||
            !schemaContainer.TryGetProperty("schema", out var schema) ||
            schema.ValueKind != JsonValueKind.Object ||
            schema.GetRawText().Length > MaximumSchemaCharacters ||
            schemaContainer.TryGetProperty("description", out var description) &&
                (description.ValueKind != JsonValueKind.String ||
                    !IsBoundedText(description.GetString(), 4096)) ||
            schemaContainer.TryGetProperty("strict", out var strict) &&
                strict.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return Validation("The JSON-schema response format is invalid.", propertyName);
        }

        return null;
    }

    private static bool RequiresStructuredOutput(JsonElement structured, string propertyName)
    {
        var format = propertyName == "text" && structured.TryGetProperty("format", out var nested)
            ? nested
            : structured;
        return format.ValueKind == JsonValueKind.Object &&
            format.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            type.GetString() is "json_object" or "json_schema";
    }

    private static bool ValidateToolCalls(JsonElement toolCalls)
    {
        if (toolCalls.ValueKind != JsonValueKind.Array ||
            toolCalls.GetArrayLength() is < 1 or > MaximumTools)
        {
            return false;
        }

        foreach (var toolCall in toolCalls.EnumerateArray())
        {
            if (toolCall.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(toolCall, Set("id", "type", "function"), out _) ||
                !toolCall.TryGetProperty("id", out var id) ||
                !IsName(id) ||
                !toolCall.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                !string.Equals(type.GetString(), "function", StringComparison.Ordinal) ||
                !toolCall.TryGetProperty("function", out var function) ||
                function.ValueKind != JsonValueKind.Object ||
                !HasOnlyProperties(function, Set("name", "arguments"), out _) ||
                !function.TryGetProperty("name", out var name) ||
                !IsName(name) ||
                !function.TryGetProperty("arguments", out var arguments) ||
                arguments.ValueKind != JsonValueKind.String ||
                !IsBoundedText(arguments.GetString(), MaximumSchemaCharacters))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidDataImagePart(
        JsonElement part,
        SharedProviderRelayOperation operation)
    {
        if (operation == SharedProviderRelayOperation.ChatCompletions)
        {
            return HasOnlyProperties(part, Set("type", "image_url"), out _) &&
                part.TryGetProperty("image_url", out var imageUrl) &&
                imageUrl.ValueKind == JsonValueKind.Object &&
                HasOnlyProperties(imageUrl, Set("url", "detail"), out _) &&
                imageUrl.TryGetProperty("url", out var chatImageUrl) &&
                (!imageUrl.TryGetProperty("detail", out var detail) ||
                    detail.ValueKind == JsonValueKind.String &&
                    detail.GetString() is "auto" or "low" or "high") &&
                IsDataImageUrl(chatImageUrl);
        }

        if (operation == SharedProviderRelayOperation.Responses)
        {
            return HasOnlyProperties(part, Set("type", "image_url"), out _) &&
                part.TryGetProperty("image_url", out var responsesImageUrl) &&
                IsDataImageUrl(responsesImageUrl);
        }

        return false;
    }

    private static bool IsDataImageUrl(JsonElement url)
    {
        if (url.ValueKind != JsonValueKind.String ||
            url.GetString() is not { } value ||
            value.Length > MaximumTextCharacters)
        {
            return false;
        }

        foreach (string prefix in new[]
        {
            "data:image/png;base64,",
            "data:image/jpeg;base64,",
            "data:image/webp;base64,"
        })
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var encoded = value.AsSpan(prefix.Length);
            if (encoded.IsEmpty || ContainsWhitespace(encoded))
            {
                return false;
            }

            var decoded = new byte[(encoded.Length + 3) / 4 * 3];
            return Convert.TryFromBase64Chars(encoded, decoded, out var bytesWritten) && bytesWritten > 0;
        }

        return false;
    }

    private static bool ContainsWhitespace(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidStop(JsonElement stop)
    {
        if (stop.ValueKind == JsonValueKind.String)
        {
            return IsBoundedText(stop.GetString(), 256);
        }

        return stop.ValueKind == JsonValueKind.Array &&
            stop.GetArrayLength() is >= 1 and <= MaximumStopSequences &&
            stop.EnumerateArray().All(item =>
                item.ValueKind == JsonValueKind.String && IsBoundedText(item.GetString(), 256));
    }

    private static bool IsName(JsonElement value)
        => value.ValueKind == JsonValueKind.String &&
            value.GetString() is { Length: > 0 and <= 128 } text &&
            text.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    private static bool IsBoundedText(string? value, int maximumCharacters)
        => value is { Length: > 0 } &&
            value.Length <= maximumCharacters &&
            !value.Any(character => character is '\0');

    private static bool TryReadImageCount(JsonElement root, out int imageCount)
    {
        imageCount = 1;
        return !root.TryGetProperty("n", out var count) ||
            (count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out imageCount));
    }

    private static int ReadValidatedImageCount(JsonElement root)
        => TryReadImageCount(root, out var imageCount)
            ? imageCount
            : throw new InvalidOperationException("The image count must be validated before normalization.");

    private static bool TryReadRoutingModelId(
        JsonElement root,
        out SharedProviderRoutingModelId routingModelId)
    {
        routingModelId = default;
        return root.TryGetProperty("model", out var model) &&
            model.ValueKind == JsonValueKind.String &&
            SharedProviderRoutingModelIdCodec.TryParse(model.GetString(), out routingModelId, out _);
    }

    private static bool HasOnlyProperties(
        JsonElement element,
        IReadOnlySet<string> allowed,
        out string? unknownProperty)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                unknownProperty = property.Name;
                return false;
            }
        }

        unknownProperty = null;
        return true;
    }

    private static byte[] Canonicalize(
        JsonElement root,
        SharedProviderRelayOperation operation)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            if (operation == SharedProviderRelayOperation.Responses &&
                !root.TryGetProperty(ResponsesStorePropertyName, out _))
            {
                writer.WriteBoolean(ResponsesStorePropertyName, false);
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void RejectDuplicateProperties(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumDepth
        });
        var objectProperties = new Stack<HashSet<string>>();
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    objectProperties.Push(new HashSet<string>(StringComparer.Ordinal));
                    break;
                case JsonTokenType.EndObject:
                    objectProperties.Pop();
                    break;
                case JsonTokenType.PropertyName:
                    var propertyName = reader.GetString() ?? throw new JsonException();
                    if (objectProperties.Count == 0 || !objectProperties.Peek().Add(propertyName))
                    {
                        throw new JsonException("Duplicate JSON member.");
                    }

                    break;
            }
        }
    }

    private static SharedProviderRelayRequestPolicyResult.Rejected Validation(
        string message,
        string? parameter = null)
        => Reject(
            SharedProviderFailureCategory.Validation,
            "shared_provider_request_invalid",
            message,
            parameter);

    private static SharedProviderRelayRequestPolicyResult.Rejected Reject(
        SharedProviderFailureCategory category,
        string code,
        string message,
        string? parameter)
        => new(new SharedProviderFailure(
            category,
            new SharedProviderFailureCode(code),
            message,
            parameter));

    private static FrozenSet<string> Set(params string[] values)
        => values.ToFrozenSet(StringComparer.Ordinal);
}
