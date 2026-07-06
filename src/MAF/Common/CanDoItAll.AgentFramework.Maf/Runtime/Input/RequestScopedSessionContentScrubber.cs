using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Maf;

internal static class RequestScopedSessionContentScrubber
{
    public static string? RemoveRequestScopedDataContent(string? serializedSessionJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionJson))
        {
            return serializedSessionJson;
        }

        try
        {
            var root = JsonNode.Parse(serializedSessionJson);
            if (root is null)
            {
                return serializedSessionJson;
            }

            return RemoveRequestScopedDataContentNodes(root)
                ? root.ToJsonString(MafRuntimeJson.SerializerOptions)
                : serializedSessionJson;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonNode node)
    {
        return node switch
        {
            JsonObject jsonObject => RemoveRequestScopedDataContentNodes(jsonObject),
            JsonArray jsonArray => RemoveRequestScopedDataContentNodes(jsonArray),
            _ => false
        };
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonObject jsonObject)
    {
        var removedAny = false;
        foreach (var property in jsonObject.ToList())
        {
            if (property.Value is not null)
            {
                removedAny |= RemoveRequestScopedDataContentNodes(property.Value);
            }
        }

        return removedAny;
    }

    private static bool RemoveRequestScopedDataContentNodes(JsonArray jsonArray)
    {
        var removedAny = false;
        var dataContentIndexes = new List<int>();
        for (var index = 0; index < jsonArray.Count; index++)
        {
            var item = jsonArray[index];
            if (IsRequestScopedDataContentNode(item))
            {
                dataContentIndexes.Add(index);
                continue;
            }

            if (item is not null)
            {
                removedAny |= RemoveRequestScopedDataContentNodes(item);
            }
        }

        for (var index = dataContentIndexes.Count - 1; index >= 0; index--)
        {
            jsonArray.RemoveAt(dataContentIndexes[index]);
            removedAny = true;
        }

        if (dataContentIndexes.Count > 0 && jsonArray.Count == 0)
        {
            jsonArray.Add(new JsonObject
            {
                ["$type"] = "text",
                ["text"] = "[Request-scoped attachment omitted from persisted session state.]"
            });
        }

        return removedAny;
    }

    private static bool IsRequestScopedDataContentNode(JsonNode? node)
    {
        return node is JsonObject jsonObject &&
               jsonObject.TryGetPropertyValue("$type", out var typeNode) &&
               string.Equals(typeNode?.GetValue<string>(), "data", StringComparison.OrdinalIgnoreCase);
    }
}
