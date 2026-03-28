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

public sealed class RuntimeConfiguration(IOptions<McpServerOptions> options, ServerInstanceIdentity serverInstanceIdentity)
{
    public string SessionId { get; } = CorrelationIdFactory.Create("project-structure");

    public Uri BaseAddress => new(options.Value.Server.BaseUrl.Trim().TrimEnd('/') + "/", UriKind.Absolute);

    public string AgentToken => options.Value.Server.AgentToken.Trim();

    public string AgentName => string.IsNullOrWhiteSpace(options.Value.Server.AgentName)
        ? "CanDoItAll Project Structure Agent"
        : options.Value.Server.AgentName.Trim();

    public string AgentId => serverInstanceIdentity.Id;

    public string MachineName => Environment.MachineName;

    public string RepositoryRoot => Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.Server.RepositoryRoot) ? "." : options.Value.Server.RepositoryRoot);

    public string BranchName => ResolveBranchName(options.Value.Server.BranchName, RepositoryRoot);

    public TimeSpan Timeout => TimeSpan.FromSeconds(options.Value.Server.TimeoutSeconds);

    private static string ResolveBranchName(string? configuredBranch, string repositoryRoot)
    {
        if (!string.IsNullOrWhiteSpace(configuredBranch))
        {
            return configuredBranch.Trim();
        }

        try
        {
            var startInfo = new ProcessStartInfo("git", $"-C \"{repositoryRoot}\" rev-parse --abbrev-ref HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return string.Empty;
            }

            var branchName = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 ? branchName : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
