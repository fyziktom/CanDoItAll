using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowExecutors.Standard.Documents;

public sealed class DocumentToMarkdownWorkflowExecutor(
    IWorkspaceArtifactToolService artifactToolService) : IWorkflowExecutor
{
    public WorkflowExecutorDescriptor Descriptor => BuiltInWorkflowExecutorDescriptors.DocumentToMarkdown;

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = WorkflowExecutorJson.Deserialize<WorkflowDocumentToMarkdownExecutorSettings>(context.SettingsJson);
        if (settings.PreviewCharacters < 0)
        {
            throw new InvalidOperationException("Document-to-Markdown executor setting 'PreviewCharacters' cannot be negative.");
        }

        var sourcePath = WorkflowInputJsonStringResolver.ResolveRequired(
            settings.SourcePath,
            settings.SourcePathJsonPath,
            input,
            "Document-to-Markdown",
            nameof(settings.SourcePath),
            nameof(settings.SourcePathJsonPath));
        var outputPath = string.IsNullOrWhiteSpace(settings.OutputPath)
            ? null
            : settings.OutputPath.Trim();
        var result = await artifactToolService
            .ConvertDocumentToMarkdown(
                sourcePath,
                outputPath,
                settings.PreviewCharacters,
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(CreateFailureMessage(result.Message, result.Diagnostics));
        }

        return WorkflowExecutorJson.Result(context, result);
    }

    private static string CreateFailureMessage(string message, string diagnostics)
    {
        var detail = string.IsNullOrWhiteSpace(diagnostics) ? message : diagnostics;
        return string.IsNullOrWhiteSpace(detail)
            ? "Document-to-Markdown conversion failed."
            : $"Document-to-Markdown conversion failed: {detail}";
    }
}
