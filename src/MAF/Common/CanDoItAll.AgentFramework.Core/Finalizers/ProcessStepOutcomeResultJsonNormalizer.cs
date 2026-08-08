using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Tolerant JSON normalization for the process-step outcome finalizer contract, moved verbatim from
/// <c>MafFinalizerDriver</c> (MAF) so the generic finalizer mechanism no longer needs to know this contract by
/// name. Wired into the catalog as <see cref="AgentFinalizerPolicy.KnownOutputNormalizer"/> for the process-step
/// outcome contract only.
/// </summary>
internal static class ProcessStepOutcomeResultJsonNormalizer
{
    public static FinalizerOutputNormalizationResult Normalize(string rawJson)
    {
        try
        {
            if (JsonNode.Parse(rawJson) is not JsonObject jsonObject)
            {
                return FinalizerOutputNormalizationResult.Failure("The JSON payload was not an object.");
            }

            NormalizeStringArrayProperty(jsonObject, "evidenceRefs");
            NormalizeStringArrayProperty(jsonObject, "nextActions");
            NormalizeProcessStepOutcomeReason(jsonObject);

            var output = jsonObject.Deserialize<ProcessStepOutcomeResult>(AgentOutputJson.SerializerOptions);
            if (output is null)
            {
                return FinalizerOutputNormalizationResult.Failure("The normalized JSON payload deserialized to null.");
            }

            return FinalizerOutputNormalizationResult.Success(
                JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions));
        }
        catch (JsonException exception)
        {
            return FinalizerOutputNormalizationResult.Failure(exception.Message);
        }
    }

    private static void NormalizeProcessStepOutcomeReason(JsonObject jsonObject)
    {
        if (TryReadNonEmptyStringProperty(jsonObject, "reason", out _))
        {
            return;
        }

        if (TryReadNonEmptyStringProperty(jsonObject, "humanReadableSummaryMarkdown", out var humanSummary) ||
            TryReadNonEmptyStringProperty(jsonObject, "branchOutcomeTitle", out humanSummary))
        {
            jsonObject["reason"] = humanSummary;
        }
    }

    private static bool TryReadNonEmptyStringProperty(
        JsonObject jsonObject,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (!jsonObject.TryGetPropertyValue(propertyName, out var node))
        {
            return false;
        }

        value = ConvertJsonNodeToString(node).Trim();
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void NormalizeStringArrayProperty(JsonObject jsonObject, string propertyName)
    {
        if (!jsonObject.TryGetPropertyValue(propertyName, out var value) ||
            value is not JsonArray values)
        {
            return;
        }

        var normalizedValues = new JsonArray();
        foreach (var item in values)
        {
            var text = ConvertJsonNodeToString(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                normalizedValues.Add(text);
            }
        }

        jsonObject[propertyName] = normalizedValues;
    }

    private static string ConvertJsonNodeToString(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            return value.TryGetValue<string>(out var text)
                ? text
                : value.ToJsonString();
        }

        if (node is JsonObject jsonObject)
        {
            return string.Join(
                "; ",
                jsonObject.Select(property => $"{property.Key}: {ConvertJsonNodeToString(property.Value)}"));
        }

        if (node is JsonArray jsonArray)
        {
            return string.Join(", ", jsonArray.Select(ConvertJsonNodeToString));
        }

        return node.ToJsonString();
    }
}
