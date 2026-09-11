using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Workspace;

public sealed class WorkspaceFileWorkflowExecutor(IWorkspaceFileService files, IWorkspacePathResolutionService? paths = null) : IWorkflowExecutor
{
    public static WorkflowExecutorDescriptor DisclosureDescriptor { get; } = BuiltInWorkflowExecutorDescriptors.StorageFile with {
        ProviderReadOwner = WorkflowWorkspaceProviderReadEvidence.Owner
    };
    public WorkflowExecutorDescriptor Descriptor => DisclosureDescriptor;

    public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowStorageFileExecutorSettings>(context.SettingsJson);
        var capture = WorkflowWorkspaceProviderReadEvidence.RequiresFileEvidence(settings)
            ? WorkflowWorkspaceReadCapture.Begin(context, input, () => files.ExecutionScope) : null;
        if (capture is not null) {
            capture.RequireSameScope((paths ?? throw new InvalidOperationException(
                "Protected Workflow file reads require the actual owner's path resolver.")).ExecutionScope);
            capture.CapturePath(paths, WorkflowWorkspaceReadCapture.RequestedPath(settings), allowMissing: true);
            if (settings.Operation == WorkflowStorageFileOperation.DiffText) {
                capture.CapturePath(paths, settings.DestinationPath, allowMissing: true);
            }
        }
        object result = settings.Operation switch
        {
            WorkflowStorageFileOperation.List => EnsureSucceeded(FilterList(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxResults), settings)),
            WorkflowStorageFileOperation.ListDirectory => EnsureSucceeded(FilterList(files.ListDirectory(EmptyToNull(settings.Path), settings.MaxResults), settings)),
            WorkflowStorageFileOperation.Tree => EnsureSucceeded(FilterList(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxFiles), settings)),
            WorkflowStorageFileOperation.Exists => EnsureStatObserved(files.StatPath(Require(settings.Path, nameof(settings.Path)))),
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

        if (capture is not null) {
            capture.RevalidatePaths(paths!);
            capture.CaptureFileResult(paths!, result, settings);
        }
        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result) with {
            ProviderReadEvidence = capture?.Complete() ?? []
        });
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

        var selected = result.Entries.Select((entry, index) => (Entry: entry, Index: index))
            .Where(item => settings.IncludeGlobs.Count == 0 || settings.IncludeGlobs.Any(pattern => MatchesGlob(item.Entry.RelativePath, pattern)))
            .Where(item => settings.ExcludeGlobs.All(pattern => !MatchesGlob(item.Entry.RelativePath, pattern)))
            .ToArray();
        var selection = result.ReadSelection;
        if (selection is not null && selection.Count != result.Entries.Count) {
            throw new InvalidOperationException("The Workflow file owner's selected targets do not match its listing.");
        }

        return result with
        {
            Entries = selected.Select(item => item.Entry).ToArray(),
            ReadSelection = selection is null ? null : new(selection.GetRootPath(), selection.IsWorkspacePath,
                selected.Select(item => selection.GetSelectedPaths()[item.Index]).ToImmutableArray()),
            IsTruncated = result.IsTruncated || selected.Length < result.Entries.Count
        };
    }

    private static object BuildDryRunDelete(
        IWorkspaceFileService files,
        WorkflowStorageFileExecutorSettings settings)
    {
        var stat = EnsureStatObserved(files.StatPath(Require(settings.Path, nameof(settings.Path))));
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
        where T : IWorkspaceToolOperationResult
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.Message);
        }

        return result;
    }

    private static WorkspacePathStatResult EnsureStatObserved(WorkspacePathStatResult result)
    {
        if (!result.Succeeded && !result.IsKnownMissing())
        {
            throw new InvalidOperationException(result.Message);
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
