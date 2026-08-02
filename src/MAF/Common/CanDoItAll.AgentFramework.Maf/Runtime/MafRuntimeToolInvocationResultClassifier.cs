using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafRuntimeToolInvocationResultClassifier
{
    private static readonly HashSet<string> TrustedWorkspaceReceiptToolNames =
        ToolContractCatalog.WorkspaceToolNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ResultEnvelopePropertyNames =
    [
        "Result",
        "Value",
        "Content",
        "Contents",
        "Data"
    ];

    private static readonly string[] ReceiptEnvelopePropertyNames =
    [
        "Result",
        "Value"
    ];

    private static readonly IReadOnlyDictionary<string, string> TrustedReceiptOperationAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ToolContractCatalog.WorkspaceInspectSpreadsheet] = "workspace_spreadsheet_preview"
        };

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

    public static Guid? ResolveDurableReceiptExecutionRunId(
        string toolName,
        object? result)
    {
        if (!TrustedWorkspaceReceiptToolNames.Contains(toolName))
        {
            return null;
        }

        return TryResolveDurableReceiptExecutionRunId(toolName, result, [], out var executionRunId)
            ? executionRunId
            : null;
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

        if (TryReadBooleanProperty(result, "IsError", out var isError))
        {
            succeeded = !isError;
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

    private static bool TryResolveDurableReceiptExecutionRunId(
        string toolName,
        object? result,
        HashSet<object> visited,
        out Guid executionRunId)
    {
        executionRunId = Guid.Empty;
        if (result is null || result is string)
        {
            return false;
        }

        if (result is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (TryGetJsonProperty(jsonElement, "receipt", out var jsonReceipt) &&
                TryReadExecutionRunId(toolName, jsonReceipt, out executionRunId))
            {
                return true;
            }

            foreach (var propertyName in ReceiptEnvelopePropertyNames)
            {
                if (TryGetJsonProperty(jsonElement, propertyName, out var property) &&
                    TryResolveDurableReceiptExecutionRunId(toolName, property, visited, out executionRunId))
                {
                    return true;
                }
            }

            return false;
        }

        var type = result.GetType();
        if (!type.IsValueType && !visited.Add(result))
        {
            return false;
        }

        if (result is WorkspaceToolReceipt
            {
                Operation: var receiptOperation,
                ExecutionRunId: { } receiptExecutionRunId
            } &&
            receiptExecutionRunId != Guid.Empty &&
            ReceiptOperationMatchesTool(toolName, receiptOperation))
        {
            executionRunId = receiptExecutionRunId;
            return true;
        }

        if (result is ToolExecutionReceiptRecord
            {
                ToolName: var recordToolName,
                ExecutionRunId: var recordExecutionRunId
            } &&
            recordExecutionRunId != Guid.Empty &&
            ReceiptOperationMatchesTool(toolName, recordToolName))
        {
            executionRunId = recordExecutionRunId;
            return true;
        }

        if (IsTrustedBuiltInReceiptResultContract(type) &&
            TryReadObjectProperty(result, "Receipt", out var directReceipt) &&
            directReceipt is WorkspaceToolReceipt
            {
                Operation: var directReceiptOperation,
                ExecutionRunId: { } directReceiptExecutionRunId
            } &&
            directReceiptExecutionRunId != Guid.Empty &&
            ReceiptOperationMatchesTool(toolName, directReceiptOperation))
        {
            executionRunId = directReceiptExecutionRunId;
            return true;
        }

        foreach (var propertyName in ReceiptEnvelopePropertyNames)
        {
            if (TryReadObjectProperty(result, propertyName, out var propertyValue) &&
                TryResolveDurableReceiptExecutionRunId(toolName, propertyValue, visited, out executionRunId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTrustedBuiltInReceiptResultContract(Type type)
    {
        var receiptProperty = type.GetProperty("Receipt");
        if (receiptProperty?.PropertyType != typeof(WorkspaceToolReceipt) ||
            receiptProperty.GetIndexParameters().Length != 0)
        {
            return false;
        }

        var assembly = type.Assembly;
        return assembly == typeof(WorkspaceToolReceipt).Assembly ||
               assembly == typeof(WorkspaceImageContentResult).Assembly ||
               assembly == typeof(MafRuntimeToolInvocationResultClassifier).Assembly;
    }

    private static bool TryReadExecutionRunId(
        string toolName,
        JsonElement receipt,
        out Guid executionRunId)
    {
        executionRunId = Guid.Empty;
        return receipt.ValueKind == JsonValueKind.Object &&
               TryGetJsonProperty(receipt, "operation", out var operationProperty) &&
               operationProperty.ValueKind == JsonValueKind.String &&
               ReceiptOperationMatchesTool(toolName, operationProperty.GetString()) &&
               TryGetJsonProperty(receipt, "executionRunId", out var executionRunIdProperty) &&
               executionRunIdProperty.ValueKind == JsonValueKind.String &&
               Guid.TryParse(executionRunIdProperty.GetString(), out executionRunId) &&
               executionRunId != Guid.Empty;
    }

    private static bool ReceiptOperationMatchesTool(string toolName, string? operation)
    {
        if (string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        var normalizedToolName = ToolContractCatalog.NormalizeToolName(toolName);
        var normalizedOperation = ToolContractCatalog.NormalizeToolName(operation);
        if (string.Equals(normalizedToolName, normalizedOperation, StringComparison.Ordinal))
        {
            return true;
        }

        return TrustedReceiptOperationAliases.TryGetValue(normalizedToolName, out var aliasedOperation) &&
               string.Equals(
                   ToolContractCatalog.NormalizeToolName(aliasedOperation),
                   normalizedOperation,
                   StringComparison.Ordinal);
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

        if (TryGetJsonProperty(element, "isError", out var isErrorProperty) &&
            isErrorProperty.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            succeeded = !isErrorProperty.GetBoolean();
            return true;
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
               text.Contains("isError=true", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("\"isError\":true", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("isError: true", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Denied", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary = Failed", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("ExitSummary: Failed", StringComparison.OrdinalIgnoreCase);
    }
}
