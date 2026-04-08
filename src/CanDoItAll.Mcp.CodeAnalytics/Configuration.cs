using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.CodeAnalytics;

public sealed class McpServerOptions
{
    [Required]
    public ServerOptions Server { get; set; } = new();

    [Required]
    public AnalysisOptions Analysis { get; set; } = new();
}

public sealed class ServerOptions
{
    [Required]
    public string Name { get; set; } = "CanDoItAll.Mcp.CodeAnalytics";
}

public sealed class AnalysisOptions
{
    [Required]
    public string RepositoryRoot { get; set; } = ".";

    [Required]
    public string DefaultSolutionPath { get; set; } = "CanDoItAll.slnx";

    [Required]
    public string OutputRootPath { get; set; } = Path.Combine(".artifacts", "code-analytics");

    [Required]
    public string GeneratorVersion { get; set; } = "0.1.0";

    [Range(1, 200)]
    public int MaxRecentSnapshots { get; set; } = 20;

    [Range(10, 500)]
    public int MaxDiagramNodes { get; set; } = 80;
}

public sealed class McpServerOptionsValidator : IValidateOptions<McpServerOptions>
{
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        var failures = new List<string>();

        if (options.Server is null)
        {
            failures.Add("Server configuration is required.");
        }
        else if (string.IsNullOrWhiteSpace(options.Server.Name))
        {
            failures.Add("Server.Name is required.");
        }

        if (options.Analysis is null)
        {
            failures.Add("Analysis configuration is required.");
            return ValidateOptionsResult.Fail(failures);
        }

        if (string.IsNullOrWhiteSpace(options.Analysis.RepositoryRoot))
        {
            failures.Add("Analysis.RepositoryRoot is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Analysis.DefaultSolutionPath))
        {
            failures.Add("Analysis.DefaultSolutionPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Analysis.OutputRootPath))
        {
            failures.Add("Analysis.OutputRootPath is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Analysis.GeneratorVersion))
        {
            failures.Add("Analysis.GeneratorVersion is required.");
        }

        if (options.Analysis.MaxRecentSnapshots < 1 || options.Analysis.MaxRecentSnapshots > 200)
        {
            failures.Add("Analysis.MaxRecentSnapshots must be between 1 and 200.");
        }

        if (options.Analysis.MaxDiagramNodes < 10 || options.Analysis.MaxDiagramNodes > 500)
        {
            failures.Add("Analysis.MaxDiagramNodes must be between 10 and 500.");
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
        var analysis = options.Value.Analysis;

        RepositoryRoot = ResolveAbsolutePath(analysis.RepositoryRoot, Environment.CurrentDirectory);
        DefaultSolutionPath = ResolveWorkspacePath(analysis.DefaultSolutionPath, RepositoryRoot);
        OutputRootPath = ResolveWorkspacePath(analysis.OutputRootPath, RepositoryRoot);
        GeneratorVersion = analysis.GeneratorVersion.Trim();
        MaxRecentSnapshots = analysis.MaxRecentSnapshots;
        MaxDiagramNodes = analysis.MaxDiagramNodes;

        Directory.CreateDirectory(OutputRootPath);
    }

    public string RepositoryRoot { get; }

    public string DefaultSolutionPath { get; }

    public string OutputRootPath { get; }

    public string GeneratorVersion { get; }

    public int MaxRecentSnapshots { get; }

    public int MaxDiagramNodes { get; }

    public string ResolveAnalysisPath(string? candidatePath)
    {
        var effectivePath = string.IsNullOrWhiteSpace(candidatePath)
            ? DefaultSolutionPath
            : ResolveWorkspacePath(candidatePath, RepositoryRoot);

        return effectivePath;
    }

    private static string ResolveWorkspacePath(string path, string repositoryRoot)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repositoryRoot, path));
    }

    private static string ResolveAbsolutePath(string path, string basePath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(basePath, path));
    }
}
