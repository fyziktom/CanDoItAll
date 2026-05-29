using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Maf;

public sealed class WorkspaceFileWorkflowExecutor(IWorkspaceFileService files) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.StorageFile;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(context.SettingsJson);
        object result = settings.Operation switch
        {
            WorkflowStorageFileOperation.List => EnsureSucceeded(FilterList(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxResults), settings)),
            WorkflowStorageFileOperation.Tree => EnsureSucceeded(FilterList(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxFiles), settings)),
            WorkflowStorageFileOperation.Exists => EnsureSucceeded(files.StatPath(Require(settings.Path, nameof(settings.Path)))),
            WorkflowStorageFileOperation.Stat => EnsureSucceeded(files.StatPath(Require(settings.Path, nameof(settings.Path)))),
            WorkflowStorageFileOperation.ReadText => EnsureSucceeded(files.ReadTextFile(Require(settings.Path, nameof(settings.Path)), settings.MaxCharacters)),
            WorkflowStorageFileOperation.WriteText => EnsureSucceeded(files.WriteTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input), settings.Overwrite)),
            WorkflowStorageFileOperation.AppendText => EnsureSucceeded(files.AppendTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input))),
            WorkflowStorageFileOperation.CreateDirectory => EnsureSucceeded(files.CreateDirectory(Require(settings.Path, nameof(settings.Path)))),
            WorkflowStorageFileOperation.Delete => settings.DryRun
                ? BuildDryRunDelete(files, settings)
                : EnsureSucceeded(files.DeletePath(Require(settings.Path, nameof(settings.Path)), settings.Recursive)),
            WorkflowStorageFileOperation.Copy => EnsureSucceeded(files.CopyPath(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.Overwrite)),
            WorkflowStorageFileOperation.Move => EnsureSucceeded(files.MovePath(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.Overwrite)),
            WorkflowStorageFileOperation.Hash => EnsureSucceeded(files.HashPath(Require(settings.Path, nameof(settings.Path)), settings.MaxFiles, settings.MaxBytes)),
            WorkflowStorageFileOperation.Zip => EnsureSucceeded(files.ZipPath(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.Overwrite, settings.MaxFiles, settings.MaxBytes)),
            WorkflowStorageFileOperation.Unzip => EnsureSucceeded(files.UnzipArchive(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.Overwrite, settings.MaxFiles, settings.MaxBytes)),
            WorkflowStorageFileOperation.SearchText => EnsureSucceeded(files.SearchText(Require(settings.Query, nameof(settings.Query)), EmptyToNull(settings.Path), settings.MaxResults)),
            WorkflowStorageFileOperation.DiffText => EnsureSucceeded(files.DiffTextFiles(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.MaxLines)),
            _ => throw new InvalidOperationException($"Workspace file operation '{settings.Operation}' is not supported.")
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
    }

    private static WorkspaceFileListResult FilterList(
        WorkspaceFileListResult result,
        WorkflowStorageFileExecutorSettings settings)
    {
        if (!result.Succeeded ||
            settings.IncludeGlobs.Count == 0 && settings.ExcludeGlobs.Count == 0)
        {
            return result;
        }

        var entries = result.Entries
            .Where(entry => settings.IncludeGlobs.Count == 0 || settings.IncludeGlobs.Any(pattern => MatchesGlob(entry.RelativePath, pattern)))
            .Where(entry => settings.ExcludeGlobs.All(pattern => !MatchesGlob(entry.RelativePath, pattern)))
            .ToArray();

        return result with
        {
            Entries = entries,
            IsTruncated = result.IsTruncated || entries.Length < result.Entries.Count
        };
    }

    private static object BuildDryRunDelete(
        IWorkspaceFileService files,
        WorkflowStorageFileExecutorSettings settings)
    {
        var stat = EnsureSucceeded(files.StatPath(Require(settings.Path, nameof(settings.Path))));
        return new
        {
            dryRun = true,
            recursive = settings.Recursive,
            stat.Path,
            stat.Exists,
            stat.PathKind,
            stat.ChildCount,
            message = stat.Exists
                ? $"Dry run: delete would target '{stat.Path}'."
                : $"Dry run: path '{stat.Path}' does not exist."
        };
    }

    private static T EnsureSucceeded<T>(T result)
    {
        var succeededProperty = typeof(T).GetProperty("Succeeded");
        var messageProperty = typeof(T).GetProperty("Message");
        if (succeededProperty?.GetValue(result) is false)
        {
            var message = messageProperty?.GetValue(result)?.ToString() ?? "Workspace operation failed.";
            throw new InvalidOperationException(message);
        }

        return result;
    }

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Workspace file executor setting '{name}' is required.")
            : value.Trim();

    private static string? EmptyToNull(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesGlob(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var normalizedValue = value.Replace('\\', '/');
        var normalizedPattern = pattern.Trim().Replace('\\', '/').TrimStart('/');
        if (normalizedPattern is "*" or "**" or "**/*")
        {
            return true;
        }

        var regex = "^" + Regex.Escape(normalizedPattern)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal)
            .Replace("\\?", "[^/]", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(normalizedValue, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

