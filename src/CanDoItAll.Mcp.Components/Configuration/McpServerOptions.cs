using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Configuration;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();

    [Required]
    public CatalogOptions Catalog { get; set; } = new();
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.Components";

    [Required]
    public string WorkspaceRoot { get; set; } = ".";
}

public sealed class CatalogOptions
{
    [Required]
    public string BaseLibRoot { get; set; } = Path.Combine("src", "CanDoItAll.Components.BaseLib");

    [Required]
    public string CanvasLibRoot { get; set; } = Path.Combine("src", "CanDoItAll.Components.CanvasLib");

    [Required]
    public string ChartsRoot { get; set; } = Path.Combine("src", "CanDoItAll.Components.Charts");

    [Required]
    public string MermaidRoot { get; set; } = Path.Combine("src", "CanDoItAll.Components.Mermaid");

    [Required]
    public string SandboxRoot { get; set; } = Path.Combine("src", "CanDoItAll.Components.Sandbox");
}

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        var failures = new List<string>();

        if (options.Server is null)
        {
            failures.Add("Server options are required.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (options.Catalog is null)
        {
            failures.Add("Catalog options are required.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (string.IsNullOrWhiteSpace(options.Server.Name))
        {
            failures.Add("Server.Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Server.WorkspaceRoot))
        {
            failures.Add("Server.WorkspaceRoot is required.");
            return ValidateOptionsResult.Fail(failures);
        }

        var workspaceRoot = Path.GetFullPath(options.Server.WorkspaceRoot);
        if (!Directory.Exists(workspaceRoot))
        {
            failures.Add($"Workspace root '{workspaceRoot}' does not exist.");
            return ValidateOptionsResult.Fail(failures);
        }

        ValidateRoot(workspaceRoot, options.Catalog.BaseLibRoot, "Catalog.BaseLibRoot", failures);
        ValidateRoot(workspaceRoot, options.Catalog.CanvasLibRoot, "Catalog.CanvasLibRoot", failures);
        ValidateRoot(workspaceRoot, options.Catalog.ChartsRoot, "Catalog.ChartsRoot", failures);
        ValidateRoot(workspaceRoot, options.Catalog.MermaidRoot, "Catalog.MermaidRoot", failures);
        ValidateRoot(workspaceRoot, options.Catalog.SandboxRoot, "Catalog.SandboxRoot", failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRoot(string workspaceRoot, string relativePath, string optionName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            failures.Add($"{optionName} is required.");
            return;
        }

        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        if (!Directory.Exists(fullPath))
        {
            failures.Add($"{optionName} '{fullPath}' does not exist.");
        }
    }
}
