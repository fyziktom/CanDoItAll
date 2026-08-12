using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

internal static class DockerRecipeContract
{
    private const int MaximumRecipeArgumentCount = 16;
    private const int MaximumRecipeArgumentBytes = 16 * 1024;
    private const int MaximumDockerArgumentCount = 128;
    private const int MaximumDockerArgumentBytes = 32 * 1024;
    private const int MaximumPortMappings = 16;
    private const int MaximumEnvironmentVariables = 32;
    private const int MaximumLabels = 32;
    private const int MaximumMounts = 16;
    private const double MaximumLogsSinceDurationSeconds = 30 * 24 * 60 * 60;
    private static readonly Regex PortMappingPattern = new(
        "^[0-9]{1,5}:[0-9]{1,5}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DurationPattern = new(
        "^(?:[0-9]+(?:\\.[0-9]+)?(?:ns|us|µs|ms|s|m|h))+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DurationPartPattern = new(
        "(?<value>[0-9]+(?:\\.[0-9]+)?)(?<unit>ns|us|µs|ms|s|m|h)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Rfc3339Pattern = new(
        "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void ValidateRecipeArguments(
        PluginHostToolRecipeId recipeId,
        IReadOnlyDictionary<string, string> arguments)
    {
        if (arguments.Count > MaximumRecipeArgumentCount)
        {
            throw new InvalidOperationException(
                $"Docker recipe exceeds the {MaximumRecipeArgumentCount}-argument limit.");
        }

        long argumentBytes = arguments.Sum(argument =>
            (long)Encoding.UTF8.GetByteCount(argument.Key) + Encoding.UTF8.GetByteCount(argument.Value));
        if (argumentBytes > MaximumRecipeArgumentBytes)
        {
            throw new InvalidOperationException(
                $"Docker recipe exceeds the {MaximumRecipeArgumentBytes}-byte argument limit.");
        }

        ValidateReservedCollectionCount(arguments, "environmentVariables", MaximumEnvironmentVariables);
        ValidateReservedCollectionCount(arguments, "labels", MaximumLabels);
        ValidateReservedCollectionCount(arguments, "mounts", MaximumMounts);

        string[] allowedArguments = recipeId.Value switch
        {
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerListContainers.Value, StringComparison.OrdinalIgnoreCase) => [],
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerPullImage.Value, StringComparison.OrdinalIgnoreCase) => ["image"],
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerStartContainer.Value, StringComparison.OrdinalIgnoreCase) =>
                ["image", "containerName", "pullIfMissing", "portMappings"],
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerReadLogs.Value, StringComparison.OrdinalIgnoreCase) =>
                ["containerName", "tail", "since"],
            _ => throw new InvalidOperationException($"Host-tool recipe '{recipeId}' is not supported.")
        };
        string? unsupportedArgument = arguments.Keys.FirstOrDefault(key =>
            !allowedArguments.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (unsupportedArgument is not null)
        {
            throw new InvalidOperationException(
                $"Docker recipe argument '{unsupportedArgument}' is not supported by recipe '{recipeId}'.");
        }
    }

    public static IReadOnlyList<string> ReadPortMappings(IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("portMappings", out var rawMappings) || string.IsNullOrWhiteSpace(rawMappings))
        {
            return [];
        }

        string[] mappings = rawMappings.Split(',', StringSplitOptions.TrimEntries);
        if (mappings.Length > MaximumPortMappings)
        {
            throw new InvalidOperationException(
                $"Docker recipe argument 'portMappings' exceeds the {MaximumPortMappings}-item limit.");
        }

        if (mappings.Any(string.IsNullOrEmpty))
        {
            throw new InvalidOperationException("Docker recipe argument 'portMappings' contains an empty item.");
        }

        return mappings.Select(ValidatePortMapping).ToArray();
    }

    public static bool GetBoolean(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        bool defaultValue)
    {
        if (!arguments.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, bool.FalseString, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new InvalidOperationException($"Docker recipe argument '{key}' must be 'true' or 'false'.");
    }

    public static int GetInteger(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        int defaultValue,
        int min,
        int max)
    {
        if (!arguments.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < min ||
            parsed > max)
        {
            throw new InvalidOperationException(
                $"Docker recipe argument '{key}' must be an integer from {min} through {max}.");
        }

        return parsed;
    }

    public static bool IsValidLogsSince(string value)
    {
        if (value.Length is 0 or > 64 ||
            value.StartsWith("-", StringComparison.Ordinal) ||
            value.Any(char.IsWhiteSpace) ||
            value.Any(char.IsControl))
        {
            return false;
        }

        if (Rfc3339Pattern.IsMatch(value))
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }

        if (!DurationPattern.IsMatch(value))
        {
            return false;
        }

        double totalSeconds = 0;
        foreach (Match match in DurationPartPattern.Matches(value))
        {
            if (!double.TryParse(
                    match.Groups["value"].Value,
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out double amount) ||
                !double.IsFinite(amount))
            {
                return false;
            }

            double multiplier = match.Groups["unit"].Value switch
            {
                "h" => 60 * 60,
                "m" => 60,
                "s" => 1,
                "ms" => 0.001,
                "us" or "µs" => 0.000001,
                "ns" => 0.000000001,
                _ => 0
            };
            totalSeconds += amount * multiplier;
            if (!double.IsFinite(totalSeconds) || totalSeconds > MaximumLogsSinceDurationSeconds)
            {
                return false;
            }
        }

        return totalSeconds > 0;
    }

    public static void ValidateDockerArguments(IReadOnlyList<string> arguments)
    {
        if (arguments.Count > MaximumDockerArgumentCount)
        {
            throw new InvalidOperationException(
                $"Docker command exceeds the {MaximumDockerArgumentCount}-argument limit.");
        }

        long argumentBytes = arguments.Sum(argument => (long)Encoding.UTF8.GetByteCount(argument));
        if (argumentBytes > MaximumDockerArgumentBytes)
        {
            throw new InvalidOperationException(
                $"Docker command exceeds the {MaximumDockerArgumentBytes}-byte argument limit.");
        }
    }

    private static string ValidatePortMapping(string portMapping)
    {
        if (!PortMappingPattern.IsMatch(portMapping))
        {
            throw new InvalidOperationException($"Docker port mapping '{portMapping}' is invalid.");
        }

        var parts = portMapping.Split(':');
        var hostPort = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var containerPort = int.Parse(parts[1], CultureInfo.InvariantCulture);
        if (hostPort is < 1 or > 65535 || containerPort is < 1 or > 65535)
        {
            throw new InvalidOperationException($"Docker port mapping '{portMapping}' is outside the valid port range.");
        }

        return portMapping;
    }

    private static void ValidateReservedCollectionCount(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        int maximumCount)
    {
        if (!arguments.TryGetValue(key, out var value) || string.IsNullOrEmpty(value))
        {
            return;
        }

        if (value.Split(',', StringSplitOptions.None).Length > maximumCount)
        {
            throw new InvalidOperationException(
                $"Docker recipe argument '{key}' exceeds the {maximumCount}-item limit.");
        }
    }
}
