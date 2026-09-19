using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolArgumentBindingFailureMapper
{
    private const int MaximumPathLength = 160;

    private static readonly JsonSerializerOptions ArgumentSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool TryCreatePreInvocationFailure(
        AIFunction? function,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        out AgentToolFailureResult result)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        result = default!;
        if (function is null || function.JsonSchema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        JsonElement argumentObject;
        try
        {
            argumentObject = JsonSerializer.SerializeToElement(
                arguments.ToDictionary(argument => argument.Key, argument => argument.Value),
                ArgumentSerializerOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            result = CreateFailure("$", "could not be represented as JSON");
            return true;
        }

        if (!TryValidateNode(function.JsonSchema, argumentObject, "$", out var failure) ||
            !TryBindParameters(function, arguments, out failure))
        {
            result = failure;
            return true;
        }

        return false;
    }

    // Tool input records validate their values in constructors, which run while the function binds its arguments.
    // Binding the same JSON before dispatch turns such a rejection into a typed failure the model can correct,
    // instead of an exception raised from inside the invocation.
    private static bool TryBindParameters(
        AIFunction function,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        out AgentToolFailureResult failure)
    {
        failure = default!;
        if (function.UnderlyingMethod is not { } method)
        {
            return true;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments)
        {
            values[argument.Key] = argument.Value;
        }

        foreach (var parameter in method.GetParameters())
        {
            if (parameter.Name is not { Length: > 0 } name ||
                !values.TryGetValue(name, out var value) ||
                value is null ||
                parameter.ParameterType.IsInstanceOfType(value))
            {
                continue;
            }

            try
            {
                var element = value as JsonElement? ??
                              JsonSerializer.SerializeToElement(value, function.JsonSerializerOptions);
                _ = element.Deserialize(parameter.ParameterType, function.JsonSerializerOptions);
            }
            // A serializer contract failure (for example a constructor it cannot bind) would fail the invocation's own
            // binding in the same way, so it is rejected here too, before anything is dispatched.
            catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or
                InvalidOperationException or NotSupportedException)
            {
                failure = CreateFailure(
                    AppendPath("$", name),
                    exception is ArgumentException rejected
                        ? $"was rejected: {DescribeRejection(rejected)}"
                        : "could not be read as the tool's argument type");
                return false;
            }
        }

        return true;
    }

    private static string DescribeRejection(ArgumentException exception)
    {
        var message = exception.ParamName is { Length: > 0 } parameterName
            ? exception.Message.Replace($" (Parameter '{parameterName}')", string.Empty, StringComparison.Ordinal)
            : exception.Message;
        message = message.Trim().TrimEnd('.');
        return message.Length <= MaximumPathLength ? message : message[..MaximumPathLength];
    }

    private static bool TryValidateNode(
        JsonElement schema,
        JsonElement value,
        string path,
        out AgentToolFailureResult failure)
    {
        if (schema.ValueKind == JsonValueKind.True)
        {
            failure = default!;
            return true;
        }

        if (schema.ValueKind == JsonValueKind.False)
        {
            failure = CreateFailure(path, "is not accepted by the tool schema");
            return false;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            failure = default!;
            return true;
        }

        if (schema.TryGetProperty("anyOf", out var anyOf) && anyOf.ValueKind == JsonValueKind.Array)
        {
            AgentToolFailureResult? firstFailure = null;
            foreach (var candidate in anyOf.EnumerateArray())
            {
                if (TryValidateNode(candidate, value, path, out var candidateFailure))
                {
                    failure = default!;
                    return true;
                }

                firstFailure ??= candidateFailure;
            }

            failure = firstFailure ?? CreateFailure(path, "does not match the tool schema");
            return false;
        }

        if (schema.TryGetProperty("enum", out var allowedValues) &&
            allowedValues.ValueKind == JsonValueKind.Array &&
            !allowedValues.EnumerateArray().Any(candidate => JsonElement.DeepEquals(candidate, value)))
        {
            failure = CreateFailure(path, "has a value that is not allowed by the tool schema");
            return false;
        }

        if (!MatchesDeclaredType(schema, value))
        {
            failure = CreateFailure(path, "has an invalid JSON type");
            return false;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required) &&
                required.ValueKind == JsonValueKind.Array)
            {
                foreach (var requiredProperty in required.EnumerateArray())
                {
                    var propertyName = requiredProperty.GetString();
                    if (!string.IsNullOrWhiteSpace(propertyName) &&
                        !value.TryGetProperty(propertyName, out _))
                    {
                        failure = CreateFailure(
                            AppendPath(path, propertyName),
                            "is required and is missing");
                        return false;
                    }
                }
            }

            if (schema.TryGetProperty("properties", out var properties) &&
                properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (value.TryGetProperty(property.Name, out var propertyValue) &&
                        !TryValidateNode(
                            property.Value,
                            propertyValue,
                            AppendPath(path, property.Name),
                            out failure))
                    {
                        return false;
                    }
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Array &&
            schema.TryGetProperty("items", out var itemSchema))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (!TryValidateNode(itemSchema, item, $"{path}[{index}]", out failure))
                {
                    return false;
                }

                index++;
            }
        }

        failure = default!;
        return true;
    }

    private static bool MatchesDeclaredType(JsonElement schema, JsonElement value)
    {
        if (!schema.TryGetProperty("type", out var declaredType))
        {
            return true;
        }

        return declaredType.ValueKind switch
        {
            JsonValueKind.String => MatchesType(declaredType.GetString(), value),
            JsonValueKind.Array => declaredType.EnumerateArray().Any(type =>
                type.ValueKind == JsonValueKind.String && MatchesType(type.GetString(), value)),
            _ => true
        };
    }

    private static bool MatchesType(string? declaredType, JsonElement value)
    {
        return declaredType switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => true
        };
    }

    private static string AppendPath(string path, string propertyName)
    {
        var appended = $"{path}.{propertyName}";
        return appended.Length <= MaximumPathLength ? appended : "$";
    }

    private static AgentToolFailureResult CreateFailure(string path, string reason)
    {
        return new AgentToolFailureResult(
            Succeeded: false,
            ErrorCode: "InvalidToolArguments",
            Message: $"Argument at '{path}' {reason}. Correct it to match the tool schema.",
            CanRetryWithCorrectedInput: true)
        {
            EffectState = AgentToolEffectState.NotCommitted
        };
    }
}