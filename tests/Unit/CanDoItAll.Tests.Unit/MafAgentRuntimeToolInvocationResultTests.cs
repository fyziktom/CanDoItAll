using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeToolInvocationResultTests
{
    [Fact]
    public void IsSuccessfulToolInvocationResult_reads_direct_workspace_result()
    {
        var result = CreateWorkspaceCommandResult(succeeded: false, message: "Template 'webapp' is not approved.");

        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);

        Assert.False(succeeded);
    }

    [Fact]
    public void IsSuccessfulToolInvocationResult_reads_nested_result_envelope()
    {
        var result = new ToolResultEnvelope
        {
            Result = CreateWorkspaceCommandResult(succeeded: false, message: "Template 'webapp' is not approved.")
        };

        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);

        Assert.False(succeeded);
    }

    [Fact]
    public void ResolveToolInvocationFailureMessage_reads_nested_result_envelope()
    {
        var result = new ToolResultEnvelope
        {
            Result = CreateWorkspaceCommandResult(succeeded: false, message: "Template 'webapp' is not approved.")
        };

        var message = MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(result);

        Assert.Equal("Template 'webapp' is not approved.", message);
    }

    private static WorkspaceCommandExecutionResult CreateWorkspaceCommandResult(bool succeeded, string message)
    {
        var now = DateTimeOffset.UtcNow;
        var receipt = new WorkspaceToolReceipt(
            Operation: "workspace_dotnet_new",
            MutatesWorkspace: true,
            Boundary: "test",
            Outcome: succeeded ? "Succeeded" : "Denied",
            Message: message,
            ReceiptRelativePath: string.Empty,
            TargetPaths: [],
            ArtifactReferences: [],
            StartedAtUtc: now,
            CompletedAtUtc: now);

        return new WorkspaceCommandExecutionResult(
            Succeeded: succeeded,
            Message: message,
            Receipt: receipt,
            ToolName: "workspace_dotnet_new",
            RecipeId: "dotnet_new",
            RiskClass: "WorkspaceMutation",
            ApprovalRequired: true,
            Boundary: ExecutionBoundaryDescriptor.Unknown,
            WorkingDirectory: ".",
            ArgumentsSummary: "new webapp -n TrailheadSnackBox.Web",
            ExitCode: succeeded ? 0 : -1,
            StdoutPreview: string.Empty,
            StderrPreview: message,
            StdoutTruncated: false,
            StderrTruncated: false);
    }

    private sealed class ToolResultEnvelope
    {
        public object? Result { get; init; }
    }
}
