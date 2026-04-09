using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Processes;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.Processes";

    [Required]
    public string RepositoryRoot { get; set; } = ".";

    public bool EnsureCurrentProfileReadyOnStartup { get; set; } = true;
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

        if (string.IsNullOrWhiteSpace(options.Server.Name))
        {
            failures.Add("Server.Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.RepositoryRoot))
        {
            failures.Add("Server.RepositoryRoot is required.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}

public sealed class RuntimeConfiguration
{
    public RuntimeConfiguration(IOptions<McpServerOptions> options)
    {
        var server = options.Value.Server;
        Name = server.Name.Trim();
        RepositoryRoot = ResolveAbsolutePath(server.RepositoryRoot, Environment.CurrentDirectory);
        EnsureCurrentProfileReadyOnStartup = server.EnsureCurrentProfileReadyOnStartup;
    }

    public string Name { get; }

    public string RepositoryRoot { get; }

    public bool EnsureCurrentProfileReadyOnStartup { get; }

    private static string ResolveAbsolutePath(string path, string basePath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }
}
