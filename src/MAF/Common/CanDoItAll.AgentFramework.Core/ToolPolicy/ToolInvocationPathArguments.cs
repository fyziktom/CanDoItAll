using System.Collections;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Core;

public sealed record ToolInvocationPathArgument(
    string Name,
    string Value,
    int? ElementIndex = null);

public sealed record ToolInvocationPathArgumentSet(
    IReadOnlyList<ToolInvocationPathArgument> Values,
    IReadOnlyList<string> UnsupportedArgumentNames)
{
    public static ToolInvocationPathArgumentSet Empty { get; } = new([], []);

    public bool IsComplete => UnsupportedArgumentNames.Count == 0;
}

public static class ToolInvocationPathArgumentResolver
{
    private const string ScriptArgumentsArgumentName = "arguments";
    private static readonly HashSet<string> ScriptArgumentPathToolNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile
    };
    private static readonly HashSet<string> ExactPathArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "directory",
        "directories",
        "file",
        "files",
        "folder",
        "folders",
        "path",
        "paths",
        "root",
        "roots",
        "script",
        "scripts",
        "source",
        "sources",
        "target",
        "targets"
    };
    private static readonly HashSet<string> ExactCollectionPathArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "directories",
        "files",
        "folders",
        "paths",
        "roots",
        "scripts",
        "sources",
        "targets"
    };
    private static readonly string[] PathArgumentNameSuffixes =
    [
        "Directory",
        "Directories",
        "Path",
        "Paths"
    ];
    private static readonly string[] CollectionPathArgumentNameSuffixes =
    [
        "Directories",
        "Paths"
    ];

    public static ToolInvocationPathArgumentSet Resolve(
        IEnumerable<KeyValuePair<string, object?>> arguments)
        => Resolve(toolName: null, arguments);

    public static ToolInvocationPathArgumentSet Resolve(
        string? toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var values = new List<ToolInvocationPathArgument>();
        var unsupportedArgumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolveScriptArguments = !string.IsNullOrWhiteSpace(toolName) &&
                                     ScriptArgumentPathToolNames.Contains(toolName);

        foreach (var argument in arguments)
        {
            if (resolveScriptArguments && IsScriptArgumentsArgumentName(argument.Key))
            {
                var resolvedScriptValues = new List<(string Value, int? ElementIndex)>();
                if (!TryResolveScriptArgumentPaths(argument.Value, resolvedScriptValues))
                {
                    unsupportedArgumentNames.Add(argument.Key);
                    continue;
                }

                values.AddRange(resolvedScriptValues.Select(item => new ToolInvocationPathArgument(
                    argument.Key,
                    item.Value,
                    item.ElementIndex)));
                continue;
            }

            if (!IsPathLikeArgumentName(argument.Key))
            {
                continue;
            }

            var resolvedValues = new List<(string Value, int? ElementIndex)>();
            if (!TryResolveValues(argument.Key, argument.Value, resolvedValues))
            {
                unsupportedArgumentNames.Add(argument.Key);
                continue;
            }

            values.AddRange(resolvedValues
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => new ToolInvocationPathArgument(
                    argument.Key,
                    item.Value,
                    item.ElementIndex)));
        }

        return new ToolInvocationPathArgumentSet(
            values,
            unsupportedArgumentNames
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public static bool IsScriptArgumentsArgumentName(string? argumentName)
        => string.Equals(argumentName, ScriptArgumentsArgumentName, StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveScriptArgumentPaths(
        object? value,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        return value switch
        {
            null => true,
            JsonElement { ValueKind: JsonValueKind.Array } element =>
                TryResolveScriptJsonArray(element, resolvedValues),
            JsonElement => false,
            string => false,
            IEnumerable<string> textValues =>
                TryResolveScriptStringEnumerable(textValues, resolvedValues),
            IEnumerable enumerable =>
                TryResolveScriptEnumerable(enumerable, resolvedValues),
            _ => false
        };
    }

    private static bool TryResolveScriptJsonArray(
        JsonElement array,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            AddScriptPathCandidate(item.GetString(), index, resolvedValues);
            index++;
        }

        return true;
    }

    private static bool TryResolveScriptStringEnumerable(
        IEnumerable<string> values,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                return false;
            }

            AddScriptPathCandidate(value, index, resolvedValues);
            index++;
        }

        return true;
    }

    private static bool TryResolveScriptEnumerable(
        IEnumerable values,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var value in values)
        {
            var text = value switch
            {
                string stringValue => stringValue,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null
            };
            if (text is null)
            {
                return false;
            }

            AddScriptPathCandidate(text, index, resolvedValues);
            index++;
        }

        return true;
    }

    private static void AddScriptPathCandidate(
        string? argument,
        int index,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        if (WorkspaceScriptArgumentPathParser.TryParse(argument, out var candidate))
        {
            resolvedValues.Add((candidate.Path, index));
        }
    }

    public static bool IsPathLikeArgumentName(string? argumentName)
    {
        if (string.IsNullOrWhiteSpace(argumentName))
        {
            return false;
        }

        return ExactPathArgumentNames.Contains(argumentName) ||
               PathArgumentNameSuffixes.Any(suffix =>
                   argumentName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveValues(
        string argumentName,
        object? value,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        switch (value)
        {
            case null:
                return true;
            case string text when RequiresCollection(argumentName):
                return false;
            case string text:
                resolvedValues.Add((text, null));
                return true;
            case JsonElement { ValueKind: JsonValueKind.String } element when RequiresCollection(argumentName):
                return false;
            case JsonElement { ValueKind: JsonValueKind.String } element:
                resolvedValues.Add((element.GetString() ?? string.Empty, null));
                return true;
            case JsonElement { ValueKind: JsonValueKind.Array } when !RequiresCollection(argumentName):
                return false;
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                return TryResolveJsonArray(element, resolvedValues);
            case JsonElement:
                return false;
            case IEnumerable<string> when !RequiresCollection(argumentName):
                return false;
            case IEnumerable<string> textValues:
                return TryResolveStringEnumerable(textValues, resolvedValues);
            case IEnumerable when !RequiresCollection(argumentName):
                return false;
            case IEnumerable enumerable:
                return TryResolveEnumerable(enumerable, resolvedValues);
            default:
                return false;
        }
    }

    private static bool TryResolveJsonArray(
        JsonElement array,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = item.GetString();
            if (value is null)
            {
                return false;
            }

            resolvedValues.Add((value, index));
            index++;
        }

        return true;
    }

    private static bool TryResolveStringEnumerable(
        IEnumerable<string> values,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var value in values)
        {
            if (value is null)
            {
                return false;
            }

            resolvedValues.Add((value, index));
            index++;
        }

        return true;
    }

    private static bool TryResolveEnumerable(
        IEnumerable values,
        ICollection<(string Value, int? ElementIndex)> resolvedValues)
    {
        var index = 0;
        foreach (var value in values)
        {
            var text = value switch
            {
                string stringValue => stringValue,
                JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
                _ => null
            };
            if (text is null)
            {
                return false;
            }

            resolvedValues.Add((text, index));
            index++;
        }

        return true;
    }

    private static bool RequiresCollection(string argumentName)
    {
        return ExactCollectionPathArgumentNames.Contains(argumentName) ||
               CollectionPathArgumentNameSuffixes.Any(suffix =>
                   argumentName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }
}
