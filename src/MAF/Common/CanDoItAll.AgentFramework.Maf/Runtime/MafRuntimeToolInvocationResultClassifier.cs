using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeToolInvocationResultClassifier
{
    private static readonly string[] ResultEnvelopePropertyNames =
    [
        "Result",
        "Value",
        "Content",
        "Contents",
        "Data"
    ];

    public static bool IsSuccessful(object? result)
    {
        return TryResolveSuccess(result, [], out var succeeded)
            ? succeeded
            : true;
    }

    public static string ResolveFailureMessage(object? result)
    {
        return TryResolveFailureMessage(result, [], out var message)
            ? message
            : "Tool invocation returned an unsuccessful result.";
    }

    private static bool TryResolveSuccess(
        object? result,
        HashSet<object> visited,
        out bool succeeded)
    {
        succeeded = true;
        if (result is null)
        {
            return false;
        }

        if (result is string text)
        {
            if (TextIndicatesFailure(text))
            {
                succeeded = false;
                return true;
            }

            return false;
        }

        if (result is JsonElement jsonElement &&
            TryResolveJsonSuccess(jsonElement, out succeeded))
        {
            return true;
        }

        var type = result.GetType();
        if (!type.IsValueType && !visited.Add(result))
        {
            return false;
        }

        if (TryReadBooleanProperty(result, "Succeeded", out succeeded) ||
            TryReadBooleanProperty(result, "Success", out succeeded) ||
            TryReadBooleanProperty(result, "IsSuccess", out succeeded))
        {
            return true;
        }

        if (TryReadFailureExitSummary(result, out succeeded))
        {
            return true;
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryReadObjectProperty(result, propertyName, out var propertyValue) &&
                TryResolveSuccess(propertyValue, visited, out succeeded))
            {
                return true;
            }
        }

        if (result is System.Collections.IEnumerable enumerable)
        {
            var sawResolvedResult = false;
            foreach (var item in enumerable)
            {
                if (!TryResolveSuccess(item, visited, out var itemSucceeded))
                {
                    continue;
                }

                sawResolvedResult = true;
                if (!itemSucceeded)
                {
                    succeeded = false;
                    return true;
                }
            }

            if (sawResolvedResult)
            {
                succeeded = true;
                return true;
            }
        }

        var resultText = result.ToString();
        if (TextIndicatesFailure(resultText))
        {
            succeeded = false;
            return true;
        }

        return false;
    }

    private static bool TryResolveFailureMessage(
        object? result,
        HashSet<object> visited,
        out string message)
    {
        message = string.Empty;
        if (result is null)
        {
            return false;
        }

        if (result is string text)
        {
            message = text;
            return !string.IsNullOrWhiteSpace(message);
        }

        if (result is JsonElement jsonElement &&
            TryResolveJsonFailureMessage(jsonElement, out message))
        {
            return true;
        }

        var type = result.GetType();
        if (!type.IsValueType && !visited.Add(result))
        {
            return false;
        }

        if (TryReadStringProperty(result, "Message", out message) ||
            TryReadStringProperty(result, "ErrorMessage", out message) ||
            TryReadStringProperty(result, "FailureMessage", out message))
        {
            return true;
        }

        if (TryReadStringProperty(result, "StderrPreview", out message))
        {
            return true;
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryReadObjectProperty(result, propertyName, out var propertyValue) &&
                TryResolveFailureMessage(propertyValue, visited, out message))
            {
                return true;
            }
        }

        if (result is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (TryResolveFailureMessage(item, visited, out message))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadBooleanProperty(object instance, string propertyName, out bool value)
    {
        value = false;
        var property = instance.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(bool) &&
            property.GetIndexParameters().Length == 0 &&
            property.GetValue(instance) is bool propertyValue)
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private static bool TryResolveJsonSuccess(JsonElement element, out bool succeeded)
    {
        succeeded = true;
        if (element.ValueKind == JsonValueKind.String)
        {
            return TryResolveSuccess(element.GetString(), [], out succeeded);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryResolveJsonSuccess(item, out succeeded) && !succeeded)
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var propertyName in new[] { "succeeded", "success", "isSuccess" })
        {
            if (TryGetJsonProperty(element, propertyName, out var property) &&
                property.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                succeeded = property.GetBoolean();
                return true;
            }
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryGetJsonProperty(element, propertyName, out var property) &&
                TryResolveJsonSuccess(property, out succeeded))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveJsonFailureMessage(JsonElement element, out string message)
    {
        message = string.Empty;
        if (element.ValueKind == JsonValueKind.String)
        {
            message = element.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(message);
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryResolveJsonFailureMessage(item, out message))
                {
                    return true;
                }
            }

            return false;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var propertyName in new[] { "message", "errorMessage", "failureMessage", "stderrPreview" })
        {
            if (TryGetJsonProperty(element, propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                message = property.GetString()!;
                return true;
            }
        }

        foreach (var propertyName in ResultEnvelopePropertyNames)
        {
            if (TryGetJsonProperty(element, propertyName, out var property) &&
                TryResolveJsonFailureMessage(property, out message))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryReadStringProperty(object instance, string propertyName, out string value)
    {
        value = string.Empty;
        var property = instance.GetType().GetProperty(propertyName);
        if (property?.PropertyType == typeof(string) &&
            property.GetIndexParameters().Length == 0 &&
            property.GetValue(instance) is string propertyValue &&
            !string.IsNullOrWhiteSpace(propertyValue))
        {
            value = propertyValue;
            return true;
        }

        return false;
    }

    private static bool TryReadObjectProperty(object instance, string propertyName, out object? value)
    {
        value = null;
        var property = instance.GetType().GetProperty(propertyName);
        if (property is null ||
            property.GetIndexParameters().Length != 0)
        {
            return false;
        }

        value = property.GetValue(instance);
        return value is not null;
    }

    private static bool TryReadFailureExitSummary(object instance, out bool succeeded)
    {
        succeeded = true;
        if (!TryReadStringProperty(instance, "ExitSummary", out var exitSummary))
        {
            return false;
        }

        if (exitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
            exitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase))
        {
            succeeded = false;
            return true;
        }

        return false;
    }

    private static bool TextIndicatesFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Succeeded = False", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Succeeded=False", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\"succeeded\":false", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("succeeded: false", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Failed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Failed", StringComparison.OrdinalIgnoreCase);
    }
}
