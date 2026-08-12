using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel;
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

public enum DockerHostDependencyState
{
    Available,
    Missing,
    InvalidConfiguration,
    PermissionDenied,
    Unavailable,
    TimedOut
}

public enum DockerEndpointKind
{
    Default,
    LocalSocket,
    NamedPipe,
    Remote
}

public sealed record DockerHostCapabilitySnapshot(
    DockerHostDependencyState Executable,
    DockerHostDependencyState Context,
    DockerHostDependencyState Daemon,
    DockerEndpointKind EndpointKind,
    string Message)
{
    public bool IsReady =>
        Executable == DockerHostDependencyState.Available &&
        Context == DockerHostDependencyState.Available &&
        Daemon == DockerHostDependencyState.Available;
}

public interface IDockerHostCapabilityProbe
{
    Task<DockerHostCapabilitySnapshot> ProbeAsync(CancellationToken cancellationToken = default);
}

public interface IDockerHostCapabilitySnapshotProvider
{
    Task<DockerHostCapabilitySnapshot> GetAsync(CancellationToken cancellationToken = default);
}

internal sealed class DockerHostCapabilitySnapshotProvider(IDockerHostCapabilityProbe probe)
    : IDockerHostCapabilitySnapshotProvider, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DockerHostCapabilitySnapshot? snapshot;

    public async Task<DockerHostCapabilitySnapshot> GetAsync(
        CancellationToken cancellationToken = default)
    {
        if (snapshot is not null)
        {
            return snapshot;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            snapshot ??= await probe.ProbeAsync(cancellationToken);
            return snapshot;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}

public sealed class DockerHostToolService(
    IWorkspacePathResolver workspacePathResolver,
    IWorkspaceProcessHost processHost,
    WorkspaceExecutableLocator executableLocator,
    WorkspaceCommandEnvironmentPolicy environmentPolicy,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    ILogger<DockerHostToolService> logger) : IPluginHostToolService, IDockerHostCapabilityProbe
{
    private static readonly Regex ContainerNamePattern = new("^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,127}$", RegexOptions.Compiled);
    private static readonly Regex ImageReferencePattern = new("^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,254}$", RegexOptions.Compiled);
    private static readonly Regex PortMappingPattern = new("^[0-9]{1,5}:[0-9]{1,5}$", RegexOptions.Compiled);
    private static readonly Regex NamedPipeEndpointPattern = new(
        "^npipe:////\\./pipe/[a-zA-Z0-9._-]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly string[] DockerExecutableCandidates = ["docker"];
    private static readonly PluginHostToolRecipeId CapabilityProbeRecipeId = new("docker-capability-probe");
    private static readonly string[] ProtectedDockerEnvironmentNames =
    [
        "DOCKER_CERT_PATH",
        "DOCKER_CONFIG",
        "DOCKER_HOST",
        "SSH_AUTH_SOCK"
    ];

    public async Task<DockerHostCapabilitySnapshot> ProbeAsync(CancellationToken cancellationToken = default)
    {
        DockerRuntimeContext runtime;
        try
        {
            runtime = ResolveRuntimeContext();
        }
        catch (WorkspaceExecutableResolutionException exception)
        {
            DockerHostDependencyState executableState = exception.Failure == WorkspaceExecutableResolutionFailure.Missing
                ? DockerHostDependencyState.Missing
                : DockerHostDependencyState.InvalidConfiguration;
            return new DockerHostCapabilitySnapshot(
                executableState,
                DockerHostDependencyState.Unavailable,
                DockerHostDependencyState.Unavailable,
                DockerEndpointKind.Default,
                "The Docker executable is unavailable or invalid for this host.");
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return new DockerHostCapabilitySnapshot(
                DockerHostDependencyState.Available,
                DockerHostDependencyState.InvalidConfiguration,
                DockerHostDependencyState.Unavailable,
                DockerEndpointKind.Default,
                "Docker host configuration is invalid or unsafe.");
        }

        WorkspaceProcessExecutionResult contextResult = await ExecuteDockerAsync(
            runtime,
            CapabilityProbeRecipeId,
            ["context", "show"],
            timeoutSeconds: 15,
            maxOutputCharacters: 4096,
            cancellationToken);
        DockerHostDependencyState contextState = ClassifyProbeResult(contextResult);
        if (contextState != DockerHostDependencyState.Available)
        {
            return new DockerHostCapabilitySnapshot(
                DockerHostDependencyState.Available,
                contextState,
                DockerHostDependencyState.Unavailable,
                runtime.EndpointKind,
                BuildProbeMessage("context", contextState));
        }

        WorkspaceProcessExecutionResult daemonResult = await ExecuteDockerAsync(
            runtime,
            CapabilityProbeRecipeId,
            ["version", "--format", "{{.Server.Version}}"],
            timeoutSeconds: 20,
            maxOutputCharacters: 4096,
            cancellationToken);
        DockerHostDependencyState daemonState = ClassifyProbeResult(daemonResult);
        return new DockerHostCapabilitySnapshot(
            DockerHostDependencyState.Available,
            DockerHostDependencyState.Available,
            daemonState,
            runtime.EndpointKind,
            daemonState == DockerHostDependencyState.Available
                ? "Docker executable, context, and daemon are available."
                : BuildProbeMessage("daemon", daemonState));
    }

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

        DockerRuntimeContext? runtime = null;
        IReadOnlyList<string> dockerArguments;
        if (string.Equals(
                recipeId.Value,
                PluginHostToolRecipeIds.DockerStartContainer.Value,
                StringComparison.OrdinalIgnoreCase))
        {
            runtime = ResolveRuntimeContext();
            dockerArguments = await BuildStartArgumentsAsync(
                runtime,
                arguments,
                timeoutSeconds,
                maxOutputCharacters,
                cancellationToken);
        }
        else
        {
            dockerArguments = recipeId.Value switch
            {
                var value when string.Equals(value, PluginHostToolRecipeIds.DockerListContainers.Value, StringComparison.OrdinalIgnoreCase) => BuildListArguments(),
                var value when string.Equals(value, PluginHostToolRecipeIds.DockerPullImage.Value, StringComparison.OrdinalIgnoreCase) => BuildPullArguments(arguments),
                var value when string.Equals(value, PluginHostToolRecipeIds.DockerReadLogs.Value, StringComparison.OrdinalIgnoreCase) => BuildLogsArguments(arguments),
                _ => throw new InvalidOperationException($"Host-tool recipe '{recipeId}' is not supported.")
            };
        }

        runtime ??= ResolveRuntimeContext();

        logger.LogInformation(
            "Executing Docker host-tool recipe {RecipeId} for plugin {PluginId}.",
            recipeId.Value,
            pluginId.Value);

        var result = await ExecuteDockerAsync(
            runtime,
            recipeId,
            dockerArguments,
            timeoutSeconds,
            maxOutputCharacters,
            cancellationToken);
        return ToPluginResult(recipeId, result, runtime.EnvironmentVariables);
    }

    private async Task<IReadOnlyList<string>> BuildStartArgumentsAsync(
        DockerRuntimeContext runtime,
        IReadOnlyDictionary<string, string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken)
    {
        var containerName = RequireContainerName(arguments);
        var image = RequireImage(arguments);
        var pullIfMissing = GetBool(arguments, "pullIfMissing", defaultValue: true);
        var containerInventory = await ExecuteDockerAsync(
            runtime,
            PluginHostToolRecipeIds.DockerStartContainer,
            [
                "container",
                "ls",
                "--all",
                "--filter",
                $"name=^/{containerName}$",
                "--format",
                "{{.Names}}"
            ],
            timeoutSeconds: 20,
            maxOutputCharacters,
            cancellationToken);
        EnsureAuthoritativeQuerySucceeded("container inventory", containerInventory, runtime);
        string[] matchingContainers = containerInventory.Stdout
            .Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (matchingContainers.Length > 1 ||
            matchingContainers.Any(name => !string.Equals(name, containerName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Docker container inventory returned an ambiguous result.");
        }

        if (matchingContainers.Length == 1)
        {
            var runningState = await ExecuteDockerAsync(
                runtime,
                PluginHostToolRecipeIds.DockerStartContainer,
                ["inspect", "--format", "{{.State.Running}}", containerName],
                timeoutSeconds: 20,
                maxOutputCharacters,
                cancellationToken);
            EnsureAuthoritativeQuerySucceeded("container running-state", runningState, runtime);
            return runningState.Stdout.Trim() switch
            {
                "true" => ["inspect", "--format", "{{.Id}}", containerName],
                "false" => ["start", containerName],
                _ => throw new InvalidOperationException("Docker container running-state response was invalid.")
            };
        }

        var imageInventory = await ExecuteDockerAsync(
            runtime,
            PluginHostToolRecipeIds.DockerPullImage,
            ["image", "ls", "--filter", $"reference={image}", "--format", "{{.ID}}"],
            timeoutSeconds: 20,
            maxOutputCharacters,
            cancellationToken);
        EnsureAuthoritativeQuerySucceeded("image inventory", imageInventory, runtime);
        bool imageExists = !string.IsNullOrWhiteSpace(imageInventory.Stdout);
        if (!imageExists && !pullIfMissing)
        {
            throw new InvalidOperationException(
                $"Docker image '{image}' is not available locally and pulling is disabled.");
        }

        if (!imageExists)
        {
            var pull = await ExecuteDockerAsync(
                runtime,
                PluginHostToolRecipeIds.DockerPullImage,
                ["pull", image],
                timeoutSeconds: Math.Clamp(timeoutSeconds, 30, 900),
                maxOutputCharacters,
                cancellationToken);
            if (!pull.Started || pull.ExitCode != 0 || pull.TimedOut)
            {
                throw new InvalidOperationException(
                    $"Docker image '{image}' could not be pulled. {BuildFailureMessage(pull, runtime.EnvironmentVariables)}");
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

    private static void EnsureAuthoritativeQuerySucceeded(
        string queryName,
        WorkspaceProcessExecutionResult result,
        DockerRuntimeContext runtime)
    {
        if (result.Started && result.ExitCode == 0 && !result.TimedOut)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Docker {queryName} could not be verified safely. {BuildFailureMessage(result, runtime.EnvironmentVariables)}");
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
        DockerRuntimeContext runtime,
        PluginHostToolRecipeId recipeId,
        IReadOnlyList<string> arguments,
        int timeoutSeconds,
        int maxOutputCharacters,
        CancellationToken cancellationToken)
    {
        var request = new WorkspaceProcessExecutionRequest(
            ToolName: "docker",
            RecipeId: recipeId.Value,
            ExecutablePath: runtime.ExecutablePath,
            Arguments: arguments,
            WorkingDirectory: workspacePathResolver.ResolveWorkspaceRoot(),
            EnvironmentVariables: runtime.EnvironmentVariables,
            TimeoutSeconds: Math.Clamp(timeoutSeconds, 1, 900),
            StdoutLimitCharacters: Math.Clamp(maxOutputCharacters, 1024, 100000),
            StderrLimitCharacters: Math.Clamp(maxOutputCharacters, 1024, 100000));
        return await processHost.ExecuteAsync(request, cancellationToken);
    }

    private static PluginHostToolExecutionResult ToPluginResult(
        PluginHostToolRecipeId recipeId,
        WorkspaceProcessExecutionResult result,
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        var succeeded = result.Started && result.ExitCode == 0 && !result.TimedOut;
        var message = succeeded
            ? "Docker recipe completed."
            : BuildFailureMessage(result, environmentVariables);
        return new PluginHostToolExecutionResult(
            recipeId,
            succeeded,
            result.ExitCode,
            message,
            RedactDockerOutput(result.Stdout, environmentVariables),
            RedactDockerOutput(result.Stderr, environmentVariables),
            result.StdoutTruncated,
            result.StderrTruncated,
            result.Boundary.Mode,
            result.Boundary.IsEnforcedByHost,
            environmentVariables.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }

    private static string BuildFailureMessage(
        WorkspaceProcessExecutionResult result,
        IReadOnlyDictionary<string, string?>? environmentVariables = null)
    {
        if (!result.Started)
        {
            return string.IsNullOrWhiteSpace(result.FailureMessage)
                ? "Docker process did not start."
                : RedactDockerOutput(result.FailureMessage, environmentVariables);
        }

        if (result.TimedOut)
        {
            return RedactDockerOutput(result.FailureMessage, environmentVariables);
        }

        string redactedStderr = RedactDockerOutput(result.Stderr, environmentVariables);
        var stderr = string.IsNullOrWhiteSpace(redactedStderr) ? string.Empty : $" Stderr: {redactedStderr}";
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

    private DockerRuntimeContext ResolveRuntimeContext()
    {
        string executablePath = executableLocator.ResolveExecutablePath(
            DockerExecutableCandidates,
            workspacePathResolver.ResolveWorkspaceRoot());
        IReadOnlyDictionary<string, string?> environmentVariables = environmentPolicy
            .MergeEnvironmentVariables(environmentVariables: null, toolName: "docker");
        DockerEndpointKind endpointKind = ValidateDockerEnvironment(environmentVariables);
        return new DockerRuntimeContext(
            executablePath,
            environmentVariables,
            endpointKind);
    }

    private DockerEndpointKind ValidateDockerEnvironment(
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        ValidateOptionalDockerRoot(environmentVariables, "DOCKER_CONFIG", "Docker configuration root");
        ValidateOptionalDockerRoot(environmentVariables, "DOCKER_CERT_PATH", "Docker certificate root");
        ValidateOptionalDockerRoot(environmentVariables, "SSH_AUTH_SOCK", "Docker SSH agent socket");

        if (environmentVariables.TryGetValue("DOCKER_API_VERSION", out string? apiVersion) &&
            !string.IsNullOrEmpty(apiVersion) &&
            (apiVersion.Length > 16 || apiVersion.Any(character => !char.IsAsciiDigit(character) && character != '.')))
        {
            throw new InvalidOperationException("Docker API version is invalid.");
        }

        if (environmentVariables.TryGetValue("DOCKER_CONTEXT", out string? dockerContext) &&
            !string.IsNullOrEmpty(dockerContext) &&
            (dockerContext.Length > 128 ||
             dockerContext.StartsWith("-", StringComparison.Ordinal) ||
             dockerContext.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))))
        {
            throw new InvalidOperationException("Docker context name is invalid.");
        }

        if (environmentVariables.TryGetValue("DOCKER_HOST", out string? dockerHost) &&
            !string.IsNullOrEmpty(dockerHost))
        {
            return ValidateDockerEndpoint(dockerHost);
        }

        return DockerEndpointKind.Default;
    }

    private void ValidateOptionalDockerRoot(
        IReadOnlyDictionary<string, string?> environmentVariables,
        string variableName,
        string description)
    {
        if (!environmentVariables.TryGetValue(variableName, out string? configuredPath))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException($"{description} is empty.");
        }

        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(configuredPath, description);
        if (!Path.IsPathFullyQualified(configuredPath))
        {
            throw new InvalidOperationException($"{description} must be an absolute native path.");
        }

        if (string.Equals(variableName, "SSH_AUTH_SOCK", StringComparison.Ordinal))
        {
            string? parent = Path.GetDirectoryName(configuredPath);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException("Docker SSH agent socket path is invalid.");
            }

            physicalPathPolicyFactory.Create(parent).EnsureSafePath(configuredPath, allowMissingLeaf: true);
            return;
        }

        physicalPathPolicyFactory.Create(configuredPath);
    }

    private DockerEndpointKind ValidateDockerEndpoint(string value)
    {
        if (value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) ||
            value.Contains('%', StringComparison.Ordinal) ||
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("Docker endpoint configuration is invalid.");
        }

        return uri.Scheme.ToLowerInvariant() switch
        {
            "unix" => ValidateUnixSocketEndpoint(value, uri),
            "npipe" => ValidateNamedPipeEndpoint(value),
            "tcp" or "ssh" or "http" or "https" => ValidateRemoteEndpoint(uri),
            _ => throw new InvalidOperationException("Docker endpoint scheme is not supported.")
        };
    }

    private DockerEndpointKind ValidateUnixSocketEndpoint(string value, Uri uri)
    {
        if (OperatingSystem.IsWindows() ||
            !value.StartsWith("unix:///", StringComparison.Ordinal) ||
            !string.IsNullOrEmpty(uri.Host) ||
            !Path.IsPathFullyQualified(uri.LocalPath))
        {
            throw new InvalidOperationException("Docker Unix socket endpoint is invalid for this host.");
        }

        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(uri.LocalPath, "Docker Unix socket");
        string? parent = Path.GetDirectoryName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(parent))
        {
            throw new InvalidOperationException("Docker Unix socket endpoint does not have a valid parent.");
        }

        physicalPathPolicyFactory.Create(parent).EnsureSafePath(uri.LocalPath, allowMissingLeaf: true);
        return DockerEndpointKind.LocalSocket;
    }

    private static DockerEndpointKind ValidateNamedPipeEndpoint(string value)
    {
        if (!OperatingSystem.IsWindows() || !NamedPipeEndpointPattern.IsMatch(value))
        {
            throw new InvalidOperationException("Docker named-pipe endpoint is invalid for this host.");
        }

        return DockerEndpointKind.NamedPipe;
    }

    private static DockerEndpointKind ValidateRemoteEndpoint(Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Host) ||
            (uri.AbsolutePath.Length > 0 && !string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal)) ||
            (uri.Scheme is "tcp" or "http" or "https") && uri.IsDefaultPort)
        {
            throw new InvalidOperationException("Docker remote endpoint is invalid.");
        }

        return DockerEndpointKind.Remote;
    }

    private static DockerHostDependencyState ClassifyProbeResult(WorkspaceProcessExecutionResult result)
    {
        if (result.TimedOut)
        {
            return DockerHostDependencyState.TimedOut;
        }

        if (result.Started && result.ExitCode == 0)
        {
            return DockerHostDependencyState.Available;
        }

        string diagnostic = $"{result.FailureMessage}\n{result.Stderr}";
        return diagnostic.Contains("permission denied", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
               diagnostic.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            ? DockerHostDependencyState.PermissionDenied
            : DockerHostDependencyState.Unavailable;
    }

    private static string BuildProbeMessage(string dependency, DockerHostDependencyState state)
        => state switch
        {
            DockerHostDependencyState.PermissionDenied =>
                $"Docker {dependency} access was denied. Verify socket, service, and host permissions.",
            DockerHostDependencyState.TimedOut =>
                $"Docker {dependency} probe timed out. Verify the daemon or remote endpoint.",
            DockerHostDependencyState.InvalidConfiguration =>
                $"Docker {dependency} configuration is invalid.",
            _ => $"Docker {dependency} is unavailable. Verify the active context and daemon endpoint."
        };

    private static string RedactDockerOutput(
        string? value,
        IReadOnlyDictionary<string, string?>? environmentVariables)
    {
        string redacted = SensitiveTextRedactor.Redact(value);
        if (environmentVariables is null)
        {
            return redacted;
        }

        foreach (string name in ProtectedDockerEnvironmentNames)
        {
            if (environmentVariables.TryGetValue(name, out string? protectedValue) &&
                !string.IsNullOrEmpty(protectedValue))
            {
                StringComparison comparison = ResolveRedactionComparison(name, protectedValue);
                foreach (string token in EnumerateRedactionTokens(name, protectedValue))
                {
                    redacted = redacted.Replace(
                        token,
                        $"[{name}_REDACTED]",
                        comparison);
                }
            }
        }

        return redacted;
    }

    private static StringComparison ResolveRedactionComparison(string name, string value)
    {
        if (OperatingSystem.IsWindows())
        {
            return StringComparison.OrdinalIgnoreCase;
        }

        return string.Equals(name, "DOCKER_HOST", StringComparison.Ordinal) &&
               !value.StartsWith("unix:", StringComparison.OrdinalIgnoreCase)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
    }

    private static IEnumerable<string> EnumerateRedactionTokens(string name, string value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal) { value };
        if (!string.Equals(name, "DOCKER_HOST", StringComparison.Ordinal))
        {
            try
            {
                string fullPath = Path.GetFullPath(value);
                AddPathRedactionTokens(tokens, fullPath);
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or NotSupportedException or PathTooLongException)
            {
            }
        }
        else if (OperatingSystem.IsWindows())
        {
            tokens.Add(value.Replace('\\', '/'));
            tokens.Add(value.Replace('/', '\\'));
        }
        else if (value.StartsWith("unix:", StringComparison.OrdinalIgnoreCase) &&
                 Uri.TryCreate(value, UriKind.Absolute, out Uri? endpoint))
        {
            AddPathRedactionTokens(tokens, endpoint.AbsolutePath);
        }

        return tokens.OrderByDescending(token => token.Length);
    }

    private static void AddPathRedactionTokens(ISet<string> tokens, string path)
    {
        string trimmedPath = Path.TrimEndingDirectorySeparator(path);
        tokens.Add(path);
        tokens.Add(trimmedPath);
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        tokens.Add(path.Replace('\\', '/'));
        tokens.Add(path.Replace('/', '\\'));
        tokens.Add(trimmedPath.Replace('\\', '/'));
        tokens.Add(trimmedPath.Replace('/', '\\'));
    }

    private sealed record DockerRuntimeContext(
        string ExecutablePath,
        IReadOnlyDictionary<string, string?> EnvironmentVariables,
        DockerEndpointKind EndpointKind);
}
