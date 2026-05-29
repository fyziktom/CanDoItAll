using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorPolicyObservabilityTests
{
    [Fact]
    public void Redaction_removes_secret_values_from_settings_summary()
    {
        var summary = WorkflowExecutorRedaction.RedactSettingsJson("""
            {
              "connectionId": "conn-1",
              "apiKey": "sk-test-secret-value",
              "nested": {
                "token": "raw-token-value",
                "safe": "visible"
              }
            }
            """);

        Assert.Contains("visible", summary, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginPolicy_invoker_rejects_oversized_plugin_output_payload()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.large-output");
        var executor = new RecordingPluginExecutor(descriptor)
        {
            OutputPayloadJson = new string('x', WorkflowExecutorPayloadPolicy.MaxPluginOutputPayloadCharacters + 1)
        };
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
            invoker.ExecuteAsync(
                CreateDefinition(descriptor.Id, "{\"connectionId\":\"conn-1\"}"),
                CreateNode(descriptor.Id, "{\"connectionId\":\"conn-1\"}"),
                new WorkflowNodeInput("{}")).AsTask());

        Assert.IsType<WorkflowExecutorPayloadTooLargeException>(exception.InnerException);
        Assert.DoesNotContain(executor.OutputPayloadJson, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkflowEvent_observer_receives_redacted_plugin_failure()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.failure");
        var executor = new RecordingPluginExecutor(descriptor)
        {
            Failure = new InvalidOperationException("Remote API rejected token=raw-token-value and Authorization: Bearer sk-test-secret-value.")
        };
        var observer = new RecordingWorkflowExecutorExecutionObserver();
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor], observer);
        var settingsJson = """
            {
              "connectionId": "conn-42",
              "password": "raw-password-value"
            }
            """;
        var node = CreateNode(descriptor.Id, settingsJson);

        var exception = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() =>
            invoker.ExecuteAsync(CreateDefinition(descriptor.Id, settingsJson), node, new WorkflowNodeInput("{}")).AsTask());

        Assert.DoesNotContain("raw-token-value", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("raw-password-value", string.Join(Environment.NewLine, observer.Records.Select(record => record.RedactedSettingsSummary)), StringComparison.Ordinal);

        var failed = Assert.Single(observer.Records, record => record.Status == WorkflowExecutorExecutionAuditStatus.Failed);
        Assert.Equal(descriptor.Id, failed.ExecutorId);
        Assert.Equal(node.Id, failed.NodeId);
        Assert.Equal("sample.plugin", failed.PluginId);
        Assert.Equal("conn-42", failed.PluginConnectionId);
        Assert.Contains("[REDACTED]", failed.RedactedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", failed.RedactedMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-test-secret-value", failed.RedactedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PluginPolicy_invoker_rejects_approval_required_executor_without_gate()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.approval") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData,
                WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)
        };
        var executor = new RecordingPluginExecutor(descriptor);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(
                CreateDefinition(descriptor.Id, "{}"),
                CreateNode(descriptor.Id, "{}"),
                new WorkflowNodeInput("{}")).AsTask());

        Assert.Contains("requires approval", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task PluginPolicy_invoker_rejects_denied_approval_before_execution()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.denied") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.RunsHostCommand,
                WorkflowExecutorApprovalRequirement.AlwaysRequired)
        };
        var executor = new RecordingPluginExecutor(descriptor);
        var invoker = new WorkflowExecutorInvoker(
            new WorkflowExecutorCatalog([executor]),
            [executor],
            approvalGate: new DenyingApprovalGate("Host command denied for test."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(
                CreateDefinition(descriptor.Id, "{\"token\":\"raw-token-value\"}"),
                CreateNode(descriptor.Id, "{\"token\":\"raw-token-value\"}"),
                new WorkflowNodeInput("{}")).AsTask());

        Assert.Contains("not approved", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-token-value", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, executor.InvocationCount);
    }

    private static WorkflowExecutorDescriptor CreatePluginDescriptor(string id)
        => new(
            new WorkflowExecutorId(id),
            "Plugin executor",
            "Plugin executor for policy tests.",
            WorkflowExecutorCategoryKind.Utility,
            "extension",
            "plugin.policy",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            "{}",
            "{}",
            WorkflowExecutorExecutionPolicy.Default,
            IsImplemented: true)
        {
            Source = WorkflowExecutorSourceDescriptor.BundledPlugin("sample.plugin", "1.0.0")
        };

    private static WorkflowNode CreateNode(WorkflowExecutorId executorId, string settingsJson)
        => new(
            new WorkflowNodeId("plugin-node"),
            WorkflowNodeKind.Executor,
            "Plugin node",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"))
            {
                ExecutorId = executorId,
                ExecutorSettingsJson = settingsJson,
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });

    private static WorkflowDefinition CreateDefinition(WorkflowExecutorId executorId, string settingsJson)
    {
        var node = CreateNode(executorId, settingsJson);
        return new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Plugin policy workflow",
            "Plugin policy workflow.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
    }

    private sealed class RecordingPluginExecutor(WorkflowExecutorDescriptor descriptor) : IWorkflowExecutor
    {
        public WorkflowExecutorDescriptor Descriptor { get; } = descriptor;

        public string OutputPayloadJson { get; init; } = "{}";

        public Exception? Failure { get; init; }

        public int InvocationCount { get; private set; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            if (Failure is not null)
            {
                throw Failure;
            }

            return ValueTask.FromResult(new WorkflowNodeExecutionResult(
                context.Node.Id,
                OutputPayloadJson,
                context.Descriptor.ResultShape));
        }
    }

    private sealed class RecordingWorkflowExecutorExecutionObserver : IWorkflowExecutorExecutionObserver
    {
        public List<WorkflowExecutorExecutionAuditRecord> Records { get; } = [];

        public ValueTask RecordAsync(
            WorkflowExecutorExecutionAuditRecord auditRecord,
            CancellationToken cancellationToken = default)
        {
            Records.Add(auditRecord);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DenyingApprovalGate(string message) : IWorkflowExecutorApprovalGate
    {
        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
            WorkflowExecutorApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(false, message));
        }
    }
}
