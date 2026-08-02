using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class MafAgentRuntimeToolInvocationResultTests
{
    [Fact]
    public void Agent_visible_tool_failure_is_mapped_without_exposing_exception_details()
    {
        var exception = new TestAgentToolFailureException(
            "InvalidMetadata",
            "metadataJson contains an incompatible value at '$.workflow'.",
            "Sensitive inner exception details.",
            canRetryWithCorrectedInput: true);

        var mapped = MafAgentToolFailureMapper.TryMap(exception, out var result);

        Assert.True(mapped);
        Assert.False(result.Succeeded);
        Assert.Equal("InvalidMetadata", result.ErrorCode);
        Assert.Equal("metadataJson contains an incompatible value at '$.workflow'.", result.Message);
        Assert.True(result.CanRetryWithCorrectedInput);
        Assert.DoesNotContain("Sensitive", result.Message, StringComparison.Ordinal);
        Assert.False(MafRuntimeToolInvocationResultClassifier.IsSuccessful(result));
    }

    [Fact]
    public void Non_agent_visible_exception_is_not_mapped()
    {
        var mapped = MafAgentToolFailureMapper.TryMap(
            new InvalidOperationException("Sensitive runtime failure."),
            out _);

        Assert.False(mapped);
    }

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

    [Fact]
    public void ResolveToolInvocationFailureMessage_reads_marshaled_json_result()
    {
        using var document = JsonDocument.Parse(
            """{"succeeded":false,"message":"process.step_outcome.branch_key_required"}""");

        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(document.RootElement);
        var message = MafRuntimeToolInvocationResultClassifier.ResolveFailureMessage(document.RootElement);

        Assert.False(succeeded);
        Assert.Equal("process.step_outcome.branch_key_required", message);
    }

    [Fact]
    public void IsSuccessfulToolInvocationResult_rejects_mcp_is_error_result()
    {
        using var document = JsonDocument.Parse(
            """{"isError":true,"content":[{"type":"text","text":"browserBackend.callTool failed"}]}""");

        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(document.RootElement);

        Assert.False(succeeded);
    }

    [Fact]
    public void IsSuccessfulToolInvocationResult_rejects_compacted_mcp_is_error_text()
    {
        const string result = "Browser MCP tool browser_snapshot completed. isError=true";

        var succeeded = MafRuntimeToolInvocationResultClassifier.IsSuccessful(result);

        Assert.False(succeeded);
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_reads_direct_workspace_receipt()
    {
        var executionRunId = Guid.NewGuid();
        var result = CreateWorkspaceCommandResult(
            succeeded: true,
            message: "Completed.",
            executionRunId: executionRunId);

        var resolvedExecutionRunId =
            MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
                "workspace_dotnet_new",
                result);

        Assert.Equal(executionRunId, resolvedExecutionRunId);
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_reads_marshaled_result_envelope()
    {
        var executionRunId = Guid.NewGuid();
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(new
            {
                result = new
                {
                    receipt = new
                    {
                        operation = "workspace_dotnet_new",
                        executionRunId
                    }
                }
            }));

        var resolvedExecutionRunId =
            MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
                "workspace_dotnet_new",
                document.RootElement);

        Assert.Equal(executionRunId, resolvedExecutionRunId);
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_rejects_receipts_embedded_in_user_controlled_json()
    {
        var executionRunId = Guid.NewGuid();
        var receipt = new
        {
            operation = "workspace_dotnet_new",
            executionRunId
        };
        object[] payloads =
        [
            new { content = new { receipt } },
            new { data = new { receipt } },
            new { result = new { content = new { receipt } } }
        ];

        foreach (var payload in payloads)
        {
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(payload));

            Assert.Null(MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
                "workspace_dotnet_new",
                document.RootElement));
        }
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_rejects_receipt_for_a_different_workspace_tool()
    {
        var executionRunId = Guid.NewGuid();
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(new
            {
                receipt = new
                {
                    operation = "workspace_read_file",
                    executionRunId
                }
            }));

        var resolvedExecutionRunId =
            MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
                "workspace_dotnet_new",
                document.RootElement);

        Assert.Null(resolvedExecutionRunId);
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_rejects_unbound_receipt()
    {
        var result = CreateWorkspaceCommandResult(succeeded: true, message: "Completed.");

        var resolvedExecutionRunId =
            MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
                "workspace_dotnet_new",
                result);

        Assert.Null(resolvedExecutionRunId);
    }

    [Fact]
    public void ResolveDurableReceiptExecutionRunId_rejects_untrusted_spoofed_receipt()
    {
        var executionRunId = Guid.NewGuid();
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(new
            {
                result = new
                {
                    receipt = new
                    {
                        operation = "workspace_dotnet_new",
                        executionRunId
                    }
                }
            }));
        var reflectedSpoof = new
        {
            Receipt = new
            {
                ExecutionRunId = executionRunId
            }
        };

        Assert.Null(MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
            "untrusted_mcp_tool",
            document.RootElement));
        Assert.Null(MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
            "untrusted_mcp_tool",
            reflectedSpoof));
        Assert.Null(MafRuntimeToolInvocationResultClassifier.ResolveDurableReceiptExecutionRunId(
            "workspace_dotnet_new",
            reflectedSpoof));
    }

    private static WorkspaceCommandExecutionResult CreateWorkspaceCommandResult(
        bool succeeded,
        string message,
        Guid? executionRunId = null)
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
            CompletedAtUtc: now)
        {
            ExecutionRunId = executionRunId
        };

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

    private sealed class TestAgentToolFailureException(
        string errorCode,
        string safeMessage,
        string exceptionMessage,
        bool canRetryWithCorrectedInput) : Exception(exceptionMessage), IAgentToolFailure
    {
        public string ErrorCode { get; } = errorCode;

        public string SafeMessage { get; } = safeMessage;

        public bool IsSafeToExpose => true;

        public bool CanRetryWithCorrectedInput { get; } = canRetryWithCorrectedInput;
    }
}
