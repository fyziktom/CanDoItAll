using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkflowExecutorPolicyObservabilityTests
{
    [Fact]
    public async Task WorkflowPayloadPolicy_redacts_bounds_and_references_artifact_for_oversized_json()
    {
        var settings = WorkflowSettings.Default with
        {
            ArtifactPolicy = WorkflowSettings.Default.ArtifactPolicy with
            {
                MaxInlinePayloadCharacters = 128
            }
        };
        var policy = new WorkflowPayloadPolicyService(new StaticWorkflowSettingsService(settings));
        var result = await policy.ApplyAsync(new WorkflowPayloadPolicyRequest(
            RunId: WorkflowRunId.New(),
            Scope: WorkflowPayloadPolicyScope.ExecutorOutput,
            Payload: $$"""{"token":"raw-token-value","payload":"{{new string('x', 512)}}"}""",
            ArtifactKind: WorkflowArtifactKind.Json,
            Name: "node-output.json",
            ContentType: "application/json",
            CreatedAtUtc: DateTimeOffset.UtcNow)
        {
            NodeId = new WorkflowNodeId("node-1"),
            CaptureArtifact = true
        });

        Assert.True(result.InlineTruncated);
        Assert.True(result.InlinePayload.Length <= settings.ArtifactPolicy.MaxInlinePayloadCharacters);
        Assert.DoesNotContain("raw-token-value", result.InlinePayload, StringComparison.Ordinal);
        Assert.NotNull(result.Artifact);
        Assert.Equal(result.Artifact!.StoragePath, result.Reference);
        Assert.Equal(WorkflowArtifactKind.Json, result.Artifact.Kind);
        Assert.Equal(new WorkflowNodeId("node-1"), result.Artifact.NodeId);
        Assert.Contains("truncated", result.Artifact.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowSettings_default_policy_allows_runtime_payload_artifact_kinds()
    {
        Assert.Contains(WorkflowArtifactKind.Json, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
        Assert.Contains(WorkflowArtifactKind.Text, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
        Assert.Contains(WorkflowArtifactKind.File, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
        Assert.Contains(WorkflowArtifactKind.ToolReceipt, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
        Assert.Contains(WorkflowArtifactKind.PreviewSimulation, WorkflowSettings.Default.ArtifactPolicy.AllowedArtifactKinds);
    }

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
    public void Redaction_removes_normalized_and_plural_sensitive_json_properties()
    {
        const string connectionSecret = "Server=sensitive-host;Password=sensitive-password";
        const string dashedSecret = "dashed-property-secret";
        var redacted = WorkflowExecutorRedaction.RedactSettingsJson($$"""
            {
              "ConnectionStrings": {
                "DefaultConnection": "{{connectionSecret}}"
              },
              "api-key": "{{dashedSecret}}",
              "api.key": "dot-property-secret",
              "private key": "space-property-secret",
              "safe": "visible"
            }
            """);

        Assert.DoesNotContain(connectionSecret, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(dashedSecret, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("dot-property-secret", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("space-property-secret", redacted, StringComparison.Ordinal);
        Assert.Contains("visible", redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redaction_preserves_nested_nodes_and_replaces_sensitive_values()
    {
        var redacted = WorkflowExecutorRedaction.RedactSettingsJson("""
            {
              "safeObject": {
                "enabled": true,
                "values": [1, { "name": "visible", "token": "nested-secret" }]
              },
              "safeArray": [{ "label": "alpha" }, false]
            }
            """);

        using var document = JsonDocument.Parse(redacted);
        var root = document.RootElement;
        var safeObject = root.GetProperty("safeObject");
        var values = safeObject.GetProperty("values");

        Assert.True(safeObject.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, values[0].GetInt32());
        Assert.Equal("visible", values[1].GetProperty("name").GetString());
        Assert.Equal("[REDACTED]", values[1].GetProperty("token").GetString());
        Assert.Equal("alpha", root.GetProperty("safeArray")[0].GetProperty("label").GetString());
        Assert.False(root.GetProperty("safeArray")[1].GetBoolean());
        Assert.DoesNotContain("nested-secret", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[\"safe\",\"api_key=array-secret\"]", "array-secret")]
    [InlineData("\"authorization=Bearer root-secret\"", "root-secret")]
    public void Redaction_removes_secrets_from_json_string_values(
        string json,
        string secret)
    {
        var redacted = WorkflowExecutorRedaction.RedactJson(json, 4096);

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redaction_removes_bearer_value_when_authorization_is_key_value_text()
    {
        const string secret = "raw-bearer-secret";

        var redacted = WorkflowExecutorRedaction.RedactText(
            $"authorization=Bearer {secret}");

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("api_key='single quoted secret value'", "single quoted secret value")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
    [InlineData("authorization='quoted authorization secret'", "quoted authorization secret")]
    public void Redaction_removes_quoted_and_basic_authorization_values(
        string value,
        string secret)
    {
        var redacted = WorkflowExecutorRedaction.RedactText(value);

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("credential")]
    [InlineData("credentials")]
    [InlineData("connectionString")]
    [InlineData("cookie")]
    [InlineData("privateKey")]
    [InlineData("accessKey")]
    [InlineData("header")]
    [InlineData("api.key")]
    [InlineData("private key")]
    [InlineData("AccountKey")]
    [InlineData("AWSAccessKeyId")]
    [InlineData("Pwd")]
    [InlineData("SharedAccessSignature")]
    [InlineData("sig")]
    [InlineData("auth")]
    [InlineData("subscriptionKey")]
    [InlineData("x-api-signature")]
    [InlineData("requestSignature")]
    public void Redaction_removes_extended_sensitive_key_value_text(string key)
    {
        const string secret = "extended-redaction-secret";

        var redacted = WorkflowExecutorRedaction.RedactText($"{key}={secret}");

        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public void Redaction_removes_connection_strings_and_multiline_private_keys()
    {
        const string databaseHost = "sensitive-db-host";
        const string privateKeyBody = "sensitive-private-key-body";
        var value = $"connectionString=Server={databaseHost};User Id=app;Password=secret{Environment.NewLine}" +
                    $"privateKey=-----BEGIN PRIVATE KEY-----{Environment.NewLine}" +
                    $"{privateKeyBody}{Environment.NewLine}" +
                    "-----END PRIVATE KEY-----";

        var redacted = WorkflowExecutorRedaction.RedactText(value);

        Assert.DoesNotContain(databaseHost, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(privateKeyBody, redacted, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", redacted, StringComparison.Ordinal);
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
    public void PluginPolicy_validator_rejects_non_idempotent_external_write_retry_policy()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.unsafe-retry") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalWrite(
                WorkflowExecutorExternalMutationKind.None,
                requiresCommitIdempotencyKey: true,
                allowsIdempotentRetry: false,
                "$.externalSideEffectReceipt.idempotencyKey",
                "test-receipt/v1")
        };
        var executor = new RecordingPluginExecutor(descriptor);
        var node = CreateNode(
            descriptor.Id,
            "{}",
            WorkflowExecutorExecutionPolicy.Default with
            {
                MaxRetryAttempts = 1
            });
        var validator = new WorkflowDefinitionValidator(new WorkflowExecutorCatalog([executor]));

        var result = validator.Validate(CreateDefinition(node), []);

        Assert.Contains(result.Issues, issue =>
            issue.Code == WorkflowValidationIssueCode.InvalidExecutionPolicy &&
            issue.Message.Contains("writes external state", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PluginPolicy_invoker_rejects_non_idempotent_external_write_retry_before_execution()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.unsafe-retry-invoker") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalWrite(
                WorkflowExecutorExternalMutationKind.None,
                requiresCommitIdempotencyKey: true,
                allowsIdempotentRetry: false,
                "$.externalSideEffectReceipt.idempotencyKey",
                "test-receipt/v1")
        };
        var executor = new RecordingPluginExecutor(descriptor);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
        var node = CreateNode(
            descriptor.Id,
            "{}",
            WorkflowExecutorExecutionPolicy.Default with
            {
                MaxRetryAttempts = 1
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(
                CreateDefinition(node),
                node,
                new WorkflowNodeInput("{}")).AsTask());

        Assert.Contains("writes external state", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task PluginPolicy_invoker_allows_retry_for_idempotent_processed_marker_contract()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.safe-marker-retry") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData | WorkflowExecutorCapabilityFlags.IdempotentExternalMarker,
                WorkflowExecutorApprovalRequirement.NotRequired),
            SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
                "$.externalSideEffectReceipt.idempotencyKey",
                "test-receipt/v1")
        };
        var executor = new RecordingPluginExecutor(descriptor)
        {
            FailuresBeforeSuccess = 1,
            OutputPayloadJson = """{"status":"processed"}"""
        };
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
        var node = CreateNode(
            descriptor.Id,
            "{}",
            WorkflowExecutorExecutionPolicy.Default with
            {
                MaxRetryAttempts = 1,
                RetryDelayMilliseconds = 1
            });

        var result = await invoker.ExecuteAsync(
            CreateDefinition(node),
            node,
            new WorkflowNodeInput("{}"));

        Assert.Equal("""{"status":"processed"}""", result.PayloadJson);
        Assert.Equal(2, executor.InvocationCount);
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

    [Fact]
    public async Task PluginPolicy_invoker_executes_approval_required_executor_after_approval()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.approved") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.WritesExternalData,
                WorkflowExecutorApprovalRequirement.RequiredForExternalEffect)
        };
        var executor = new RecordingPluginExecutor(descriptor)
        {
            OutputPayloadJson = """{"status":"approved"}"""
        };
        var invoker = new WorkflowExecutorInvoker(
            new WorkflowExecutorCatalog([executor]),
            [executor],
            approvalGate: new ApprovingApprovalGate("Approved for test."));

        var result = await invoker.ExecuteAsync(
            CreateDefinition(descriptor.Id, "{\"connectionId\":\"conn-1\"}"),
            CreateNode(descriptor.Id, "{\"connectionId\":\"conn-1\"}"),
            new WorkflowNodeInput("{}"));

        Assert.Equal("""{"status":"approved"}""", result.PayloadJson);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task WorkflowExternalRequestApprovalGate_creates_redacted_pending_request_without_executing()
    {
        var descriptor = CreatePluginDescriptor("plugin.policy.pending-approval") with
        {
            PermissionPolicy = new WorkflowExecutorPermissionPolicy(
                WorkflowExecutorCapabilityFlags.RunsHostCommand | WorkflowExecutorCapabilityFlags.UsesSecrets,
                WorkflowExecutorApprovalRequirement.AlwaysRequired)
        };
        var executor = new RecordingPluginExecutor(descriptor);
        var invoker = new WorkflowExecutorInvoker(
            new WorkflowExecutorCatalog([executor]),
            [executor],
            approvalGate: new WorkflowExternalRequestApprovalGate());
        using var auditScope = WorkflowExecutorExecutionAuditScope.Push(WorkflowRunId.New());

        var exception = await Assert.ThrowsAsync<WorkflowExternalRequestPendingException>(() =>
            invoker.ExecuteAsync(
                CreateDefinition(descriptor.Id, "{\"token\":\"raw-token-value\"}"),
                CreateNode(descriptor.Id, "{\"token\":\"raw-token-value\"}"),
                new WorkflowNodeInput("{}")).AsTask());

        Assert.Equal(WorkflowExternalRequestKind.Approval, exception.Request.Kind);
        Assert.Equal(new WorkflowNodeId("plugin-node"), exception.Request.NodeId);
        Assert.Contains("plugin.policy.pending-approval", exception.Request.RequestJson, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", exception.Request.RequestJson, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-token-value", exception.Request.RequestJson, StringComparison.Ordinal);
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

    private static WorkflowNode CreateNode(
        WorkflowExecutorId executorId,
        string settingsJson,
        WorkflowExecutorExecutionPolicy? policy = null)
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
                ExecutionPolicy = policy ?? WorkflowExecutorExecutionPolicy.Default
            });

    private static WorkflowDefinition CreateDefinition(WorkflowExecutorId executorId, string settingsJson)
    {
        var node = CreateNode(executorId, settingsJson);
        return CreateDefinition(node);
    }

    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
    {
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

        public int FailuresBeforeSuccess { get; init; }

        public int InvocationCount { get; private set; }

        public ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
            WorkflowExecutorExecutionContext context,
            WorkflowNodeInput input,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            if (InvocationCount <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Transient executor failure for retry-policy test.");
            }

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

    private sealed class ApprovingApprovalGate(string message) : IWorkflowExecutorApprovalGate
    {
        public ValueTask<WorkflowExecutorApprovalDecision> RequestApprovalAsync(
            WorkflowExecutorApprovalRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new WorkflowExecutorApprovalDecision(true, message));
        }
    }

    private sealed class StaticWorkflowSettingsService(WorkflowSettings settings) : IWorkflowSettingsService
    {
        public Task<WorkflowSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(settings);
        }

        public Task<WorkflowSettings> SaveSettingsAsync(
            WorkflowSettings updatedSettings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(updatedSettings);
        }
    }
}
