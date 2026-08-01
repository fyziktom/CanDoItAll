using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal static class WorkspaceManagedScriptReadbackContractParser
{
    internal static bool TryParse(
        string? value,
        out IReadOnlyList<WorkspaceManagedScriptReadbackCheck> checks,
        out string issue)
    {
        checks = [];
        issue = "Managed script readback checks must be valid JSON.";
        if (string.IsNullOrWhiteSpace(value))
        {
            issue = "Managed script readback checks are missing.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            if (TryParse(document.RootElement, out checks))
            {
                issue = string.Empty;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        issue = "Managed script readback checks must contain non-empty path candidates and text groups.";
        return false;
    }

    private static bool TryParse(
        JsonElement element,
        out IReadOnlyList<WorkspaceManagedScriptReadbackCheck> checks)
    {
        checks = [];
        var elements = element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().ToArray()
            : [element];
        if (elements.Length == 0)
        {
            return false;
        }

        var parsedChecks = new List<WorkspaceManagedScriptReadbackCheck>(elements.Length);
        foreach (var item in elements)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !TryReadNonEmptyStringArray(item, "pathCandidates", out var pathCandidates) ||
                !TryReadNonEmptyStringGroups(item, "requiredTextAnyGroups", out var requiredTextAnyGroups) ||
                !TryReadOptionalBoolean(item, "mustExist", defaultValue: true, out var mustExist))
            {
                return false;
            }

            parsedChecks.Add(new WorkspaceManagedScriptReadbackCheck(
                pathCandidates,
                requiredTextAnyGroups,
                mustExist));
        }

        checks = parsedChecks;
        return true;
    }

    private static bool TryReadNonEmptyStringArray(
        JsonElement element,
        string propertyName,
        out IReadOnlyList<string> values)
    {
        values = [];
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() == 0)
        {
            return false;
        }

        var parsedValues = property.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString()?.Trim() : null)
            .ToArray();
        if (parsedValues.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        values = parsedValues
            .Select(value => value!)
            .ToArray();
        return true;
    }

    private static bool TryReadNonEmptyStringGroups(
        JsonElement element,
        string propertyName,
        out IReadOnlyList<IReadOnlyList<string>> groups)
    {
        groups = [];
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array ||
            property.GetArrayLength() == 0)
        {
            return false;
        }

        var parsedGroups = new List<IReadOnlyList<string>>(property.GetArrayLength());
        foreach (var group in property.EnumerateArray())
        {
            if (group.ValueKind != JsonValueKind.Array ||
                group.GetArrayLength() == 0)
            {
                return false;
            }

            var values = group.EnumerateArray()
                .Select(value => value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : null)
                .ToArray();
            if (values.Any(string.IsNullOrWhiteSpace))
            {
                return false;
            }

            parsedGroups.Add(values.Select(value => value!).ToArray());
        }

        groups = parsedGroups;
        return true;
    }

    private static bool TryReadOptionalBoolean(
        JsonElement element,
        string propertyName,
        bool defaultValue,
        out bool value)
    {
        value = defaultValue;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return true;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }
}
