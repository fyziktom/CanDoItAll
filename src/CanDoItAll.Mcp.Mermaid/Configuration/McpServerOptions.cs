using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Mermaid.Configuration;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.Mermaid";

    [Required]
    public string WorkspaceRoot { get; set; } = ".";
}

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        var failures = new List<string>();

        if (options.Server is null)
        {
            return ValidateOptionsResult.Fail("Server options are required.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.Name))
        {
            failures.Add("Server.Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.WorkspaceRoot))
        {
            failures.Add("Server.WorkspaceRoot is required.");
        }
        else if (!Directory.Exists(Path.GetFullPath(options.Server.WorkspaceRoot)))
        {
            failures.Add($"Server.WorkspaceRoot '{Path.GetFullPath(options.Server.WorkspaceRoot)}' does not exist.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
