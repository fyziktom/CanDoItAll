using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

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
            WorkflowStorageFileOperation.List => EnsureSucceeded(files.ListFiles(EmptyToNull(settings.Path), settings.SearchPattern, settings.MaxResults)),
            WorkflowStorageFileOperation.Stat => EnsureSucceeded(files.StatPath(Require(settings.Path, nameof(settings.Path)))),
            WorkflowStorageFileOperation.ReadText => EnsureSucceeded(files.ReadTextFile(Require(settings.Path, nameof(settings.Path)), settings.MaxCharacters)),
            WorkflowStorageFileOperation.WriteText => EnsureSucceeded(files.WriteTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input), settings.Overwrite)),
            WorkflowStorageFileOperation.AppendText => EnsureSucceeded(files.AppendTextFile(Require(settings.Path, nameof(settings.Path)), WorkflowInputPayloadText.Resolve(settings.Content, settings.ContentFromInput, input))),
            WorkflowStorageFileOperation.SearchText => EnsureSucceeded(files.SearchText(Require(settings.Query, nameof(settings.Query)), EmptyToNull(settings.Path), settings.MaxResults)),
            WorkflowStorageFileOperation.DiffText => EnsureSucceeded(files.DiffTextFiles(Require(settings.Path, nameof(settings.Path)), Require(settings.DestinationPath, nameof(settings.DestinationPath)), settings.MaxLines)),
            _ => throw new InvalidOperationException($"Workspace file operation '{settings.Operation}' is not supported.")
        };

        return ValueTask.FromResult(WorkflowExecutorJson.Result(context, result));
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
}

