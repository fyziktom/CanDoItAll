using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Plugins;

public interface IPluginHostToolService
{
    Task<PluginHostToolExecutionResult> ExecuteAsync(
        PluginId pluginId,
        PluginHostToolRecipeId recipeId,
        IReadOnlyDictionary<string, string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken = default);
}

public sealed class DockerHostToolService(
    IWorkspacePathResolver workspacePathResolver,
    ILogger<DockerHostToolService> logger) : IPluginHostToolService
{
    private static readonly Regex ContainerNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.Compiled);
    private static readonly Regex ImageReferencePattern = new("^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,254}$", RegexOptions.Compiled);
    private static readonly Regex PortMappingPattern = new("^[0-9]{1,5}:[0-9]{1,5}$", RegexOptions.Compiled);
    private static readonly string[] DockerExecutableCandidates = OperatingSystem.IsWindows()
        ? ["docker.exe", "docker.cmd", "docker.bat"]
        : ["docker"];
    private static readonly HashSet<string> EnvironmentAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "PATH",
        "PATHEXT",
        "SYSTEMROOT",
        "SystemRoot",
        "WINDIR",
        "TEMP",
        "TMP",
        "USERPROFILE",
        "HOME",
        "APPDATA",
        "LOCALAPPDATA",
        "DOCKER_HOST",
        "DOCKER_CONTEXT",
        "DOCKER_CONFIG"
    };

    private readonly LocalWorkspaceProcessHost processHost = new();

    public async Task<PluginHostToolExecutionResult> ExecuteAsync(
        PluginId pluginId,
        PluginHostToolRecipeId recipeId,
        IReadOnlyDictionary<string, string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken = default)
    {
        if (pluginId != DockerPluginConstants.PluginId)
        {
            throw new InvalidOperationException($"Host-tool recipe '{recipeId}' is not available for plugin '{pluginId}'.");
        }

        var dockerPath = ResolveDockerExecutable();
        var dockerArguments = recipeId.Value switch
        {
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerListContainers.Value, StringComparison.OrdinalIgnoreCase) => BuildListArguments(),
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerPullImage.Value, StringComparison.OrdinalIgnoreCase) => BuildPullArguments(arguments),
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerStartContainer.Value, StringComparison.OrdinalIgnoreCase) => await BuildStartArgumentsAsync(
                dockerPath,
                arguments,
                timeoutSeconds,
                maxOutputCharacters,
                cancellationToken),
            var value when string.Equals(value, PluginHostToolRecipeIds.DockerReadLogs.Value, StringComparison.OrdinalIgnoreCase) => BuildLogsArguments(arguments),
            _ => throw new InvalidOperationException($"Host-tool recipe '{recipeId}' is not supported.")
        };

        logger.LogInformation(
            "Executing Docker host-tool recipe {RecipeId} for plugin {PluginId}.",
            recipeId.Value,
            pluginId.Value);

        var result = await ExecuteDockerAsync(
            dockerPath,
            recipeId,
            dockerArguments,
            timeoutSeconds,
            maxOutputCharacters,
            cancellationToken);
        return ToPluginResult(recipeId, result, CaptureEnvironment().Keys);
    }

    private async Task<IReadOnlyList<string>> BuildStartArgumentsAsync(
        string dockerPath,
        IReadOnlyDictionary<string, string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken)
    {
        var containerName = RequireContainerName(arguments);
        var image = RequireImage(arguments);
        var pullIfMissing = GetBool(arguments, "pullIfMissing", defaultValue: true);
        var inspectContainer = await ExecuteDockerAsync(
            dockerPath,
            PluginHostToolRecipeIds.DockerStartContainer,
            ["inspect", containerName],
            timeoutSeconds: 20,
            maxOutputCharacters,
            cancellationToken);

        if (inspectContainer.Started && inspectContainer.ExitCode == 0)
        {
            var runningState = await ExecuteDockerAsync(
                dockerPath,
                PluginHostToolRecipeIds.DockerStartContainer,
                ["inspect", "--format", "{{.State.Running}}", containerName],
                timeoutSeconds: 20,
                maxOutputCharacters,
                cancellationToken);
            return runningState.Started &&
                   runningState.ExitCode == 0 &&
                   string.Equals(runningState.Stdout.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                ? ["inspect", "--format", "{{.Id}}", containerName]
                : ["start", containerName];
        }

        if (pullIfMissing)
        {
            var inspectImage = await ExecuteDockerAsync(
                dockerPath,
                PluginHostToolRecipeIds.DockerPullImage,
                ["image", "inspect", image],
                timeoutSeconds: 20,
                maxOutputCharacters,
                cancellationToken);
            if (!inspectImage.Started || inspectImage.ExitCode != 0)
            {
                var pull = await ExecuteDockerAsync(
                    dockerPath,
                    PluginHostToolRecipeIds.DockerPullImage,
                    ["pull", image],
                    timeoutSeconds: Math.Clamp(timeoutSeconds, 30, 900),
                    maxOutputCharacters,
                    cancellationToken);
                if (!pull.Started || pull.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Docker image '{image}' could not be pulled. {BuildFailureMessage(pull)}");
                }
            }
        }

        var runArguments = new List<string>
        {
            "run",
            "-d",
            "--name",
            containerName
        };
        foreach (var portMapping in ReadPortMappings(arguments))
        {
            runArguments.Add("-p");
            runArguments.Add(portMapping);
        }

        runArguments.Add(image);
        return runArguments;
    }

    private static IReadOnlyList<string> BuildListArguments() => ["ps", "--format", "{{json .}}"];

    private static IReadOnlyList<string> BuildPullArguments(IReadOnlyDictionary<string, string> arguments)
        => ["pull", RequireImage(arguments)];

    private static IReadOnlyList<string> BuildLogsArguments(IReadOnlyDictionary<string, string> arguments)
    {
        var containerName = RequireContainerName(arguments);
        var tail = GetInt(arguments, "tail", defaultValue: 120, min: 1, max: 1000);
        var dockerArguments = new List<string>
        {
            "logs",
            "--tail",
            tail.ToString()
        };
        if (arguments.TryGetValue("since", out var since) && !string.IsNullOrWhiteSpace(since))
        {
            var normalizedSince = since.Trim();
            if (normalizedSince.Contains('\r', StringComparison.Ordinal) ||
                normalizedSince.Contains('\n', StringComparison.Ordinal) ||
                normalizedSince.Length > 64)
            {
                throw new InvalidOperationException("Docker logs 'since' value is invalid.");
            }

            dockerArguments.Add("--since");
            dockerArguments.Add(normalizedSince);
        }

        dockerArguments.Add(containerName);
        return dockerArguments;
    }

    private async Task<WorkspaceProcessExecutionResult> ExecuteDockerAsync(
        string dockerPath,
        PluginHostToolRecipeId recipeId,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken)
    {
        var request = new WorkspaceProcessExecutionRequest(
            ToolName: "docker",
            RecipeId: recipeId.Value,
            ExecutablePath: dockerPath,
            Arguments: arguments,
            WorkingDirectory: workspacePathResolver.ResolveWorkspaceRoot(),
            EnvironmentVariables: CaptureEnvironment(),
            TimeoutSeconds: Math.Clamp(timeoutSeconds, 1, 900),
            StdoutLimitCharacters: Math.Clamp(maxOutputCharacters, 1024, 100000),
            StderrLimitCharacters: Math.Clamp(maxOutputCharacters, 1024, 100000));
        return await processHost.ExecuteAsync(request, cancellationToken);
    }

    private static PluginHostToolExecutionResult ToPluginResult(
        PluginHostToolRecipeId recipeId,
        WorkspaceProcessExecutionResult result,
        IEnumerable<string> environmentVariableNames)
    {
        var succeeded = result.Started && result.ExitCode == 0 && !result.TimedOut;
        var message = succeeded
            ? "Docker recipe completed."
            : BuildFailureMessage(result);
        return new PluginHostToolExecutionResult(
            recipeId,
            succeeded,
            result.ExitCode,
            message,
            result.Stdout,
            result.Stderr,
            result.StdoutTruncated,
            result.StderrTruncated,
            result.Boundary.Mode,
            result.Boundary.IsEnforcedByHost,
            environmentVariableNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static string BuildFailureMessage(WorkspaceProcessExecutionResult result)
    {
        if (!result.Started)
        {
            return string.IsNullOrWhiteSpace(result.FailureMessage)
                ? "Docker process did not start."
                : result.FailureMessage;
        }

        if (result.TimedOut)
        {
            return result.FailureMessage;
        }

        var stderr = string.IsNullOrWhiteSpace(result.Stderr) ? string.Empty : $" Stderr: {result.Stderr}";
        return $"Docker process exited with code {result.ExitCode}.{stderr}".Trim();
    }

    private static string RequireImage(IReadOnlyDictionary<string, string> arguments)
    {
        var image = RequireArgument(arguments, "image");
        if (!ImageReferencePattern.IsMatch(image) ||
            image.Contains("--", StringComparison.Ordinal) ||
            image.Contains("..", StringComparison.Ordinal) ||
            image.Contains("://", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Docker image reference is invalid.");
        }

        return image;
    }

    private static string RequireContainerName(IReadOnlyDictionary<string, string> arguments)
    {
        var containerName = RequireArgument(arguments, "containerName");
        if (!ContainerNamePattern.IsMatch(containerName))
        {
            throw new InvalidOperationException("Docker container name is invalid.");
        }

        return containerName;
    }

    private static string RequireArgument(IReadOnlyDictionary<string, string> arguments, string key)
    {
        if (!arguments.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Docker recipe argument '{key}' is required.");
        }

        var normalized = value.Trim();
        if (normalized.Any(char.IsControl) || normalized.Any(char.IsWhiteSpace) || normalized.StartsWith("-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Docker recipe argument '{key}' is invalid.");
        }

        return normalized;
    }

    private static IReadOnlyList<string> ReadPortMappings(IReadOnlyDictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("portMappings", out var rawMappings) || string.IsNullOrWhiteSpace(rawMappings))
        {
            return [];
        }

        return rawMappings
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ValidatePortMapping)
            .ToArray();
    }

    private static string ValidatePortMapping(string portMapping)
    {
        if (!PortMappingPattern.IsMatch(portMapping))
        {
            throw new InvalidOperationException($"Docker port mapping '{portMapping}' is invalid.");
        }

        var parts = portMapping.Split(':');
        var hostPort = int.Parse(parts[0]);
        var containerPort = int.Parse(parts[1]);
        if (hostPort is < 1 or > 65535 || containerPort is < 1 or > 65535)
        {
            throw new InvalidOperationException($"Docker port mapping '{portMapping}' is outside the valid port range.");
        }

        return portMapping;
    }

    private static bool GetBool(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        bool defaultValue)
        => arguments.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed)
            ? parsed
            : defaultValue;

    private static int GetInt(
        IReadOnlyDictionary<string, string> arguments,
        string key,
        int defaultValue,
        int min,
        int max)
        => arguments.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? Math.Clamp(parsed, min, max)
            : defaultValue;

    private static IReadOnlyDictionary<string, string?> CaptureEnvironment()
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in EnvironmentAllowList)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
            {
                result[name] = value;
            }
        }

        return result;
    }

    private static string ResolveDockerExecutable()
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var entry in pathEntries)
        {
            foreach (var candidate in DockerExecutableCandidates)
            {
                var path = Path.Combine(entry, candidate);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return OperatingSystem.IsWindows() ? "docker.exe" : "docker";
    }
}
