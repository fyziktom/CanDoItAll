using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.DotNetWatch.Configuration;

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        List<string> failures = [];

        var workspaceRoot = ResolvePath(Environment.CurrentDirectory, options.Server.WorkspaceRoot);
        if (!Directory.Exists(workspaceRoot))
        {
            failures.Add($"Workspace root '{workspaceRoot}' does not exist.");
        }

        var solutionPath = ResolvePath(workspaceRoot, options.Server.SolutionPath);
        if (!File.Exists(solutionPath))
        {
            failures.Add($"Solution path '{solutionPath}' does not exist.");
        }

        var defaultProjectPath = ResolvePath(workspaceRoot, options.DefaultApp.ProjectPath);
        if (!File.Exists(defaultProjectPath))
        {
            failures.Add($"Default app project path '{defaultProjectPath}' does not exist.");
        }

        if (options.Process.GracefulStopTimeoutMs <= 0)
        {
            failures.Add("Process:GracefulStopTimeoutMs must be greater than zero.");
        }

        if (options.Process.ForceKillAfterMs < options.Process.GracefulStopTimeoutMs)
        {
            failures.Add("Process:ForceKillAfterMs must be greater than or equal to Process:GracefulStopTimeoutMs.");
        }

        if (options.Logs.BufferCapacity <= 0)
        {
            failures.Add("Logs:BufferCapacity must be greater than zero.");
        }

        if (options.Logs.MaxFileSizeMb <= 0)
        {
            failures.Add("Logs:MaxFileSizeMb must be greater than zero.");
        }

        if (options.Health.Enabled && options.Health.Urls.Length == 0)
        {
            failures.Add("Health:Urls must contain at least one value when Health:Enabled is true.");
        }

        if (options.Health.TimeoutMs <= 0 || options.Health.PollIntervalMs <= 0)
        {
            failures.Add("Health timeouts and poll intervals must be positive.");
        }

        if (options.Waits.DefaultAppWaitTimeoutMs <= 0 || options.Waits.DefaultOperationWaitTimeoutMs <= 0 || options.Waits.DefaultPollIntervalMs <= 0)
        {
            failures.Add("Wait defaults must be positive.");
        }

        if (options.Backend.StartupTimeoutMs <= 0 || options.Backend.StartupPollIntervalMs <= 0)
        {
            failures.Add("Backend startup timeouts and poll intervals must be positive.");
        }

        if (options.Bridge.PingTimeoutMs <= 0)
        {
            failures.Add("Bridge:PingTimeoutMs must be positive.");
        }

        if (options.Bridge.RepairRetryCount < 0)
        {
            failures.Add("Bridge:RepairRetryCount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(options.Backend.BindHost))
        {
            failures.Add("Backend:BindHost must not be empty.");
        }

        if (options.AtomicRuntime.RollbackRetentionCount <= 0)
        {
            failures.Add("AtomicRuntime:RollbackRetentionCount must be positive.");
        }

        if (options.Endpoints.CandidateHttpPortStart <= 0 ||
            options.Endpoints.CandidateHttpPortEnd <= 0 ||
            options.Endpoints.CandidateHttpPortEnd < options.Endpoints.CandidateHttpPortStart)
        {
            failures.Add("Endpoints candidate HTTP port range must be positive and ordered.");
        }

        if (options.ShadowHost.RetainedBuildCount <= 0)
        {
            failures.Add("ShadowHost:RetainedBuildCount must be positive.");
        }

        if (options.WorkflowGuidance.MaxSerializedCharacters <= 0)
        {
            failures.Add("WorkflowGuidance:MaxSerializedCharacters must be positive.");
        }

        foreach (var allowedRoot in options.Security.AllowedProjectRoots)
        {
            var resolvedRoot = ResolvePath(workspaceRoot, allowedRoot);
            if (!resolvedRoot.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Allowed project root '{allowedRoot}' resolves outside the workspace.");
            }
        }

        foreach (var externalRoot in options.Security.AllowedExternalProjectRoots)
        {
            var resolvedRoot = ResolvePath(workspaceRoot, externalRoot);
            if (!Directory.Exists(resolvedRoot))
            {
                failures.Add($"Allowed external project root '{externalRoot}' resolves to a missing directory '{resolvedRoot}'.");
            }
        }

        if (options.Security.AllowedEnvironmentKeys.Any(static key => key.Contains('*')))
        {
            failures.Add("Security:AllowedEnvironmentKeys cannot contain wildcard entries.");
        }

        var logFolder = ResolvePath(workspaceRoot, options.Logs.Folder);
        try
        {
            Directory.CreateDirectory(logFolder);
        }
        catch (Exception ex)
        {
            failures.Add($"Log folder '{logFolder}' could not be created: {ex.Message}");
        }

        var registryPath = ResolvePath(workspaceRoot, options.Process.RegistryPath);
        try
        {
            var registryDirectory = Path.GetDirectoryName(registryPath);
            if (!string.IsNullOrWhiteSpace(registryDirectory))
            {
                Directory.CreateDirectory(registryDirectory);
            }
        }
        catch (Exception ex)
        {
            failures.Add($"Registry path '{registryPath}' is not writable: {ex.Message}");
        }

        foreach (var backendPath in new[] { options.Backend.RegistrationPath, options.Backend.LaunchLockPath })
        {
            var resolvedBackendPath = ResolvePath(workspaceRoot, backendPath);
            try
            {
                var directory = Path.GetDirectoryName(resolvedBackendPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Backend path '{resolvedBackendPath}' is not writable: {ex.Message}");
            }
        }

        foreach (var workspacePath in new[] { options.AtomicRuntime.SlotRoot, options.Endpoints.LeasePath })
        {
            var resolvedWorkspacePath = ResolvePath(workspaceRoot, workspacePath);
            try
            {
                var directory = Directory.Exists(resolvedWorkspacePath)
                    ? resolvedWorkspacePath
                    : Path.GetDirectoryName(resolvedWorkspacePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Workspace-managed path '{resolvedWorkspacePath}' is not writable: {ex.Message}");
            }
        }

        foreach (var healthUrl in options.Health.Urls)
        {
            if (!Uri.TryCreate(healthUrl, UriKind.Absolute, out var uri))
            {
                failures.Add($"Health URL '{healthUrl}' is not a valid absolute URI.");
                continue;
            }

            if (!options.Security.AllowExternalHealthHosts &&
                !options.Health.AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            {
                failures.Add($"Health URL host '{uri.Host}' is not allowed.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static string ResolvePath(string basePath, string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }
}
