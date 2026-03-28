using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using CanDoItAll.Mcp.Core.Identity;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.ProjectStructure;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.ProjectStructure";

    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    [Required]
    public string AgentToken { get; set; } = string.Empty;

    public string AgentName { get; set; } = "CanDoItAll Project Structure Agent";

    public string RepositoryRoot { get; set; } = ".";

    public string? BranchName { get; set; }

    [Range(5, 600)]
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        var failures = new List<string>();
        if (options.Server is null)
        {
            failures.Add("Server configuration is required.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (string.IsNullOrWhiteSpace(options.Server.BaseUrl) ||
            !Uri.TryCreate(options.Server.BaseUrl.Trim(), UriKind.Absolute, out _))
        {
            failures.Add("Server.BaseUrl must be an absolute URL.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.AgentToken))
        {
            failures.Add("Server.AgentToken is required.");
        }

        if (options.Server.TimeoutSeconds < 5 || options.Server.TimeoutSeconds > 600)
        {
            failures.Add("Server.TimeoutSeconds must be between 5 and 600.");
        }

        if (failures.Count > 0)
        {
            return ValidateOptionsResult.Fail(failures);
        }

        return ValidateOptionsResult.Success;
    }
}

public sealed class RuntimeConfiguration
{
    public RuntimeConfiguration(IOptions<McpServerOptions> options, ServerInstanceIdentity serverInstanceIdentity)
    {
        var server = options.Value.Server;
        SessionId = CorrelationIdFactory.Create("project-structure");
        BaseAddress = new Uri(server.BaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute);
        AgentToken = server.AgentToken.Trim();
        AgentName = string.IsNullOrWhiteSpace(server.AgentName)
            ? "CanDoItAll Project Structure Agent"
            : server.AgentName.Trim();
        AgentId = serverInstanceIdentity.Id;
        MachineName = Environment.MachineName;
        RepositoryRoot = Path.GetFullPath(string.IsNullOrWhiteSpace(server.RepositoryRoot) ? "." : server.RepositoryRoot);
        BranchName = ResolveBranchName(server.BranchName, RepositoryRoot);
        Timeout = TimeSpan.FromSeconds(server.TimeoutSeconds);
    }

    public string SessionId { get; }

    public Uri BaseAddress { get; }

    public string AgentToken { get; }

    public string AgentName { get; }

    public string AgentId { get; }

    public string MachineName { get; }

    public string RepositoryRoot { get; }

    public string BranchName { get; }

    public TimeSpan Timeout { get; }

    private static string ResolveBranchName(string? configuredBranch, string repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredBranch))
        {
            return configuredBranch.Trim();
        }

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("rev-parse");
            startInfo.ArgumentList.Add("--abbrev-ref");
            startInfo.ArgumentList.Add("HEAD");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            process.StandardInput.Close();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                TryKill(process);
                return string.Empty;
            }

            Task.WaitAll([stdoutTask, stderrTask], 1000);
            return process.ExitCode == 0 ? stdoutTask.Result.Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
