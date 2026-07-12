using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionContextParser
{
    internal const string Schema = "dotnet.solution-context/v1";

    public bool TryParse(
        string artifactContent,
        out DotNetSolutionContext context,
        out string issue)
    {
        context = null!;
        issue = string.Empty;
        if (!TryExtractSingleJsonBlock(artifactContent, out var json, out issue))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issue = "The .NET solution context JSON root must be an object.";
                return false;
            }

            var root = document.RootElement;
            if (!string.Equals(ReadRequiredString(root, "schema"), Schema, StringComparison.Ordinal))
            {
                issue = $"The .NET solution context must declare schema '{Schema}'.";
                return false;
            }

            if (!TryReadProvisioningMode(root, out var provisioningMode, out issue))
            {
                return false;
            }

            var solution = ReadRequiredObject(root, "solution");
            var initialization = ReadInitializationPlan(root, provisioningMode);
            context = new DotNetSolutionContext(
                provisioningMode,
                ReadRequiredString(solution, "file"),
                ReadStringArray(solution, "candidateFiles"),
                ReadRequiredStringArray(root, "requiredProjectFiles"),
                ReadStringArray(root, "testProjectFiles"),
                initialization);
            return true;
        }
        catch (JsonException)
        {
            issue = "The .NET solution context JSON is malformed.";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            issue = exception.Message;
            return false;
        }
    }

    private static bool TryReadProvisioningMode(
        JsonElement root,
        out DotNetSolutionProvisioningMode provisioningMode,
        out string issue)
    {
        provisioningMode = default;
        var value = ReadRequiredString(root, "provisioningMode");
        if (string.Equals(value, "initialize", StringComparison.OrdinalIgnoreCase))
        {
            provisioningMode = DotNetSolutionProvisioningMode.Initialize;
            issue = string.Empty;
            return true;
        }

        if (string.Equals(value, "verify-existing", StringComparison.OrdinalIgnoreCase))
        {
            provisioningMode = DotNetSolutionProvisioningMode.VerifyExisting;
            issue = string.Empty;
            return true;
        }

        issue = "The .NET solution context provisioningMode must be 'initialize' or 'verify-existing'.";
        return false;
    }

    private static DotNetInitializationPlan? ReadInitializationPlan(
        JsonElement root,
        DotNetSolutionProvisioningMode provisioningMode)
    {
        var hasInitialization = root.TryGetProperty("initialization", out var initialization);
        if (provisioningMode == DotNetSolutionProvisioningMode.VerifyExisting)
        {
            if (hasInitialization && initialization.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
            {
                throw new InvalidOperationException("A verify-existing .NET solution context must not declare an initialization plan.");
            }

            return null;
        }

        if (!hasInitialization || initialization.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("An initialize .NET solution context requires object 'initialization'.");
        }

        var application = ReadRequiredObject(initialization, "application");
        var tests = ReadRequiredObject(initialization, "tests");
        return new DotNetInitializationPlan(
            ReadRequiredString(initialization, "solutionName"),
            new DotNetInitializationApplication(
                ReadRequiredString(application, "name"),
                ReadRequiredString(application, "directory"),
                ReadRequiredString(application, "file"),
                ReadRequiredString(application, "template"),
                ReadStringArray(application, "templateOptions"),
                ReadOptionalString(application, "archetype")),
            new DotNetInitializationTestProject(
                ReadRequiredString(tests, "name"),
                ReadRequiredString(tests, "directory"),
                ReadRequiredString(tests, "file"),
                ReadRequiredString(tests, "template"),
                ReadRequiredString(tests, "frameworkPreference")),
            ReadRequiredString(initialization, "targetFramework"));
    }

    private static bool TryExtractSingleJsonBlock(
        string artifactContent,
        out string json,
        out string issue)
    {
        json = string.Empty;
        issue = string.Empty;
        if (string.IsNullOrWhiteSpace(artifactContent))
        {
            issue = "The bound .NET solution context artifact is empty.";
            return false;
        }

        var blocks = new List<string>();
        var currentLines = new List<string>();
        var inJsonBlock = false;
        foreach (var line in artifactContent.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.Trim();
            if (!inJsonBlock && string.Equals(trimmed, "```json", StringComparison.OrdinalIgnoreCase))
            {
                inJsonBlock = true;
                currentLines.Clear();
                continue;
            }

            if (inJsonBlock && string.Equals(trimmed, "```", StringComparison.Ordinal))
            {
                blocks.Add(string.Join("\n", currentLines));
                inJsonBlock = false;
                continue;
            }

            if (inJsonBlock)
            {
                currentLines.Add(line);
            }
        }

        if (inJsonBlock)
        {
            issue = "The .NET solution context JSON code block is not closed.";
            return false;
        }

        if (blocks.Count != 1)
        {
            issue = "The bound .NET solution context artifact must contain exactly one fenced json code block.";
            return false;
        }

        json = blocks[0];
        return true;
    }

    private static JsonElement ReadRequiredObject(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The .NET solution context requires object '{propertyName}'.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement parent, string propertyName)
    {
        var value = ReadOptionalString(parent, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"The .NET solution context requires non-empty string '{propertyName}'.");
        }

        return value;
    }

    private static string ReadOptionalString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"The .NET solution context property '{propertyName}' must be a string.");
        }

        return value.GetString()?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> ReadRequiredStringArray(JsonElement parent, string propertyName)
    {
        var values = ReadStringArray(parent, propertyName);
        if (values.Count == 0)
        {
            throw new InvalidOperationException($"The .NET solution context requires non-empty array '{propertyName}'.");
        }

        return values;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The .NET solution context property '{propertyName}' must be an array.");
        }

        var values = new List<string>();
        foreach (var element in value.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
            {
                throw new InvalidOperationException($"The .NET solution context array '{propertyName}' must contain non-empty strings.");
            }

            values.Add(element.GetString()!.Trim());
        }

        return values;
    }
}
