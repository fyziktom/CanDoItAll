using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolInvocationCorrelationKey
{
    private static readonly IReadOnlyDictionary<string, string> IdentityNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["projectId"] = "projectId",
            ["project_id"] = "projectId",
            ["parentNodeKey"] = "parentNodeKey",
            ["parent_node_key"] = "parentNodeKey",
            ["nodeId"] = "nodeId",
            ["node_id"] = "nodeId",
            ["sourceId"] = "sourceId",
            ["source_id"] = "sourceId",
            ["targetId"] = "targetId",
            ["target_id"] = "targetId",
            ["scopeKey"] = "scopeKey",
            ["scope_key"] = "scopeKey",
            ["sourceWorkspacePath"] = "sourceWorkspacePath",
            ["source_workspace_path"] = "sourceWorkspacePath"
        };

    // Workspace tools address their target by path arguments. The same tool re-invoked for the same target path(s)
    // is the same operation, so a corrected retry can resolve an earlier no-effect rejection of that target.
    private const string WorkspaceToolNamePrefix = "workspace_";

    private static readonly IReadOnlyDictionary<string, string> WorkspaceIdentityNames =
        new Dictionary<string, string>(IdentityNames, StringComparer.OrdinalIgnoreCase)
        {
            ["path"] = "path",
            ["sourcePath"] = "sourcePath",
            ["source_path"] = "sourcePath",
            ["destinationPath"] = "destinationPath",
            ["destination_path"] = "destinationPath",
            ["workbookPath"] = "workbookPath",
            ["workbook_path"] = "workbookPath",
            ["outputWorkbookPath"] = "outputWorkbookPath",
            ["output_workbook_path"] = "outputWorkbookPath"
        };

    public static string Create(
        string toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var identityNames = toolName.StartsWith(WorkspaceToolNamePrefix, StringComparison.OrdinalIgnoreCase)
            ? WorkspaceIdentityNames
            : IdentityNames;
        var identities = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var argument in arguments)
        {
            AddIdentity(argument.Key, argument.Value, identities, identityNames);
            if (string.Equals(argument.Key, "request", StringComparison.OrdinalIgnoreCase))
            {
                ReadNestedRequestIdentities(argument.Value, identities, identityNames);
            }
        }

        if (toolName.StartsWith("project_structure_", StringComparison.OrdinalIgnoreCase) &&
            !identities.ContainsKey("projectId"))
        {
            return string.Empty;
        }

        if (identities.Count == 0)
        {
            return string.Empty;
        }

        var canonical = string.Join(
            "|",
            identities.Select(identity =>
                $"{identity.Key.ToLowerInvariant()}={identity.Value}"));
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes($"{toolName.Trim().ToLowerInvariant()}|{canonical}"));
        return Convert.ToHexString(bytes);
    }

    private static void ReadNestedRequestIdentities(
        object? value,
        IDictionary<string, string> identities,
        IReadOnlyDictionary<string, string> identityNames)
    {
        JsonElement request;
        if (value is JsonElement jsonElement)
        {
            request = jsonElement;
        }
        else
        {
            try
            {
                request = JsonSerializer.SerializeToElement(value, JsonSerializerOptions.Web);
            }
            catch (Exception exception) when (exception is JsonException or NotSupportedException)
            {
                return;
            }
        }

        if (request.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var property in request.EnumerateObject())
        {
            AddIdentity(property.Name, property.Value, identities, identityNames);
        }
    }

    private static void AddIdentity(
        string name,
        object? value,
        IDictionary<string, string> identities,
        IReadOnlyDictionary<string, string> identityNames)
    {
        if (identityNames.TryGetValue(name, out var canonicalName) &&
            TryReadScalar(value, out var scalar))
        {
            identities[canonicalName] = scalar;
        }
    }

    private static bool TryReadScalar(object? value, out string scalar)
    {
        scalar = value switch
        {
            Guid guid when guid != Guid.Empty => guid.ToString("D"),
            string text when !string.IsNullOrWhiteSpace(text) => text.Trim(),
            JsonElement { ValueKind: JsonValueKind.String } element =>
                element.GetString()?.Trim() ?? string.Empty,
            JsonElement { ValueKind: JsonValueKind.Number } element =>
                element.GetRawText(),
            _ => string.Empty
        };
        return scalar.Length is > 0 and <= 512;
    }
}