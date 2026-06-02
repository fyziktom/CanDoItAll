using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Modules.Plugins;

public sealed class GmailDownloadByLabelWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    PluginOAuthService oauthService,
    GmailApiClient gmailApiClient) : IWorkflowExecutor
{
    private const int MaxWorkflowMessages = 1;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Gmail email message batch JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        GmailPluginConstants.PluginId.Value,
        "1.0.0",
        "Gmail",
        GmailPluginConstants.Icon);

    public WorkflowExecutorDescriptor Descriptor => new(
        GmailPluginConstants.DownloadByLabelExecutorId,
        "Gmail messages by label",
        "Downloads the first unprocessed Gmail messages that have the configured label.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        GmailPluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        ResultShape,
        "{\"type\":\"object\"}",
        JsonSerializer.Serialize(new GmailWorkflowExecutorSettings(), JsonOptions),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        Simulation = GmailWorkflowSimulationTemplates.DownloadByLabel,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.ReadsExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.ExternalRead(EmailWorkflowSideEffectConstants.ExternalReadReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview uses simulated Gmail messages without calling Gmail.")
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<GmailWorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Gmail executor settings are invalid.");

        var connectionId = await oauthService.ResolveWorkflowConnectionIdAsync(
            GmailPluginConstants.PluginId,
            GmailPluginConstants.ConnectionKey,
            settings.ConnectionId,
            [GmailPluginConstants.GmailModifyScope],
            cancellationToken);
        var token = await oauthService.GetAccessTokenAsync(
            GmailPluginConstants.PluginId,
            connectionId,
            [GmailPluginConstants.GmailModifyScope],
            cancellationToken);
        var batch = await gmailApiClient.DownloadMessagesByLabelAsync(
            token.AccessToken,
            settings.Label,
            Math.Clamp(settings.MaxMessages, 1, MaxWorkflowMessages),
            cancellationToken);
        if (batch.Count == 0)
        {
            throw new InvalidOperationException($"No Gmail messages were found with label '{settings.Label}'.");
        }

        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, settings, connectionId, batch),
            ResultShape);
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        GmailWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
    {
        var payload = new JsonObject
        {
            ["provider"] = batch.Provider,
            ["filterKind"] = batch.FilterKind,
            ["filterValue"] = batch.FilterValue,
            ["processedLabel"] = settings.ProcessedLabel,
            ["count"] = batch.Count,
            ["messages"] = JsonSerializer.SerializeToNode(batch.Messages, JsonOptions)
        };
        var processing = CreateProcessingPayload(settings, connectionId, batch);
        payload["gmailProcessing"] = processing.DeepClone();

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            CopyIfPresent(workflowInput, payload, "projectId");
            CopyIfPresent(workflowInput, payload, "nodeId");
            CopyIfPresent(workflowInput, payload, "project");
            payload["runContext"] = CreateRunContext(workflowInput, processing);
            payload["workflowInput"] = workflowInput.DeepClone();
        }
        else
        {
            payload["runContext"] = new JsonObject
            {
                ["gmailProcessing"] = processing.DeepClone()
            };
        }

        return payload.ToJsonString(JsonOptions);
    }

    private static JsonObject CreateProcessingPayload(
        GmailWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
    {
        var selectedMessageId = batch.Messages.FirstOrDefault()?.Id ?? string.Empty;
        return new JsonObject
        {
            ["connectionId"] = connectionId.ToString(),
            ["sourceLabel"] = settings.Label,
            ["processedLabel"] = settings.ProcessedLabel,
            ["messageIds"] = JsonSerializer.SerializeToNode(batch.Messages.Select(message => message.Id).ToArray(), JsonOptions),
            ["selectedMessageId"] = selectedMessageId,
            ["idempotencyKey"] = CreateGmailIdempotencyKey(selectedMessageId),
            ["idempotencyKeys"] = JsonSerializer.SerializeToNode(
                batch.Messages.Select(message => CreateGmailIdempotencyKey(message.Id)).ToArray(),
                JsonOptions)
        };
    }

    private static JsonObject CreateRunContext(
        JsonObject workflowInput,
        JsonObject processing)
    {
        var runContext = workflowInput.TryGetPropertyValue("runContext", out var value) && value is JsonObject context
            ? (JsonObject)context.DeepClone()
            : new JsonObject();
        runContext["gmailProcessing"] = processing.DeepClone();
        return runContext;
    }

    private static bool TryParseObject(
        string json,
        out JsonObject value)
    {
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            return value.Count > 0;
        }
        catch (JsonException)
        {
            value = new JsonObject();
            return false;
        }
    }

    private static void CopyIfPresent(
        JsonObject source,
        JsonObject target,
        string propertyName)
    {
        if (source.TryGetPropertyValue(propertyName, out var value) && value is not null)
        {
            target[propertyName] = value.DeepClone();
        }
    }

    private static string CreateGmailIdempotencyKey(string messageId)
        => string.IsNullOrWhiteSpace(messageId)
            ? string.Empty
            : $"{EmailWorkflowSideEffectConstants.GmailIdempotencyPrefix}{messageId.Trim()}";

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(GmailPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var oauthGrant = grantEvaluator.Evaluate(GmailPluginConstants.PluginId, PluginCapabilityKind.OAuth2);
        return oauthGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(oauthGrant.Kind.ToString(), oauthGrant.Message);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class GmailMarkProcessedWorkflowExecutor(
    PluginGrantEvaluator grantEvaluator,
    PluginOAuthService oauthService,
    GmailApiClient gmailApiClient) : IWorkflowExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly WorkflowValueShape ResultShape = new(WorkflowValueShapeKind.Json, "{}", "Gmail processed label mutation JSON.");
    private static readonly WorkflowExecutorSourceDescriptor PluginSource = WorkflowExecutorSourceDescriptor.BundledPlugin(
        GmailPluginConstants.PluginId.Value,
        "1.0.0",
        "Gmail",
        GmailPluginConstants.Icon);

    public WorkflowExecutorDescriptor Descriptor => new(
        GmailPluginConstants.MarkProcessedExecutorId,
        "Gmail mark processed",
        "Adds the processed label to a Gmail message and removes the source label.",
        WorkflowExecutorCategoryKind.Data,
        "mail",
        GmailPluginConstants.SettingsRendererKey.Value,
        WorkflowValueShape.Text,
        ResultShape,
        "{\"type\":\"object\"}",
        JsonSerializer.Serialize(new GmailMarkProcessedWorkflowExecutorSettings(), JsonOptions),
        WorkflowExecutorExecutionPolicy.Default with { TimeoutSeconds = 60 },
        IsImplemented: true)
    {
        Source = PluginSource,
        Availability = ResolveAvailability(),
        Simulation = GmailWorkflowSimulationTemplates.MarkProcessed,
        PermissionPolicy = new WorkflowExecutorPermissionPolicy(
            WorkflowExecutorCapabilityFlags.WritesExternalData |
            WorkflowExecutorCapabilityFlags.UsesNetwork |
            WorkflowExecutorCapabilityFlags.UsesSecrets |
            WorkflowExecutorCapabilityFlags.IdempotentExternalMarker |
            WorkflowExecutorCapabilityFlags.SupportsDeterministicTestMode,
            WorkflowExecutorApprovalRequirement.NotRequired),
        SideEffects = WorkflowExecutorSideEffectDescriptor.IdempotentProcessedMarker(
            "$.externalSideEffectReceipt.idempotencyKey",
            EmailWorkflowSideEffectConstants.ProcessedMarkerReceiptSchema),
        DeterministicTestMode = WorkflowExecutorDeterministicTestModeDescriptor.Supported("Run Preview simulates the Gmail label mutation without changing Gmail.")
    };

    public async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync(
        WorkflowExecutorExecutionContext context,
        WorkflowNodeInput input,
        CancellationToken cancellationToken = default)
    {
        var settings = JsonSerializer.Deserialize<GmailMarkProcessedWorkflowExecutorSettings>(context.SettingsJson, JsonOptions)
                       ?? throw new InvalidOperationException("Gmail mark-processed executor settings are invalid.");

        var messageId = EmailWorkflowPayloadResolver.ResolveInputJsonString(
            input,
            settings.MessageIdJsonPath,
            nameof(settings.MessageIdJsonPath),
            "Gmail mark-processed executor");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Gmail mark-processed executor resolved an empty message id.");
        }

        var connectionId = await oauthService.ResolveWorkflowConnectionIdAsync(
            GmailPluginConstants.PluginId,
            GmailPluginConstants.ConnectionKey,
            settings.ConnectionId,
            [GmailPluginConstants.GmailModifyScope],
            cancellationToken);
        var token = await oauthService.GetAccessTokenAsync(
            GmailPluginConstants.PluginId,
            connectionId,
            [GmailPluginConstants.GmailModifyScope],
            cancellationToken);
        var result = await gmailApiClient.MarkMessageProcessedAsync(
            token.AccessToken,
            messageId,
            settings.SourceLabel,
            settings.ProcessedLabel,
            cancellationToken);

        return new WorkflowNodeExecutionResult(
            context.Node.Id,
            CreatePayload(input, result),
            ResultShape);
    }

    private static string CreatePayload(
        WorkflowNodeInput input,
        GmailMessageLabelMutationResult result)
    {
        var payload = new JsonObject
        {
            ["provider"] = result.Provider,
            ["messageId"] = result.MessageId,
            ["sourceLabel"] = result.SourceLabel,
            ["processedLabel"] = result.ProcessedLabel,
            ["sourceLabelRemoved"] = result.SourceLabelRemoved,
            ["processedLabelAdded"] = result.ProcessedLabelAdded,
            ["sideEffectMode"] = EmailWorkflowSideEffectConstants.CommitMode,
            ["dryRun"] = false
        };
        var receipt = CreateProcessedMarkerReceipt(result);
        payload["committed"] = receipt["mutationApplied"]?.GetValue<bool>() ?? false;
        payload["idempotencyRecord"] = CreateIdempotencyRecord(result.Provider, result.MessageId);
        payload["processedMarker"] = CreateProcessedMarkerRecord(result);
        payload["externalSideEffectReceipt"] = receipt;

        if (TryParseObject(input.PayloadJson, out var workflowInput))
        {
            payload["inputPayload"] = workflowInput.DeepClone();
        }

        return payload.ToJsonString(JsonOptions);
    }

    private static JsonObject CreateProcessedMarkerReceipt(GmailMessageLabelMutationResult result)
        => new()
        {
            ["schemaVersion"] = EmailWorkflowSideEffectConstants.ProcessedMarkerReceiptSchema,
            ["provider"] = result.Provider,
            ["operation"] = EmailWorkflowSideEffectConstants.Operation,
            ["mode"] = EmailWorkflowSideEffectConstants.CommitMode,
            ["dryRun"] = false,
            ["committed"] = result.SourceLabelRemoved || result.ProcessedLabelAdded,
            ["mutationApplied"] = result.SourceLabelRemoved || result.ProcessedLabelAdded,
            ["idempotencyKey"] = CreateGmailIdempotencyKey(result.MessageId),
            ["messageId"] = result.MessageId,
            ["sourceMarkerName"] = result.SourceLabel,
            ["processedMarkerName"] = result.ProcessedLabel,
            ["sourceMarkerRemoved"] = result.SourceLabelRemoved,
            ["processedMarkerAdded"] = result.ProcessedLabelAdded,
            ["retrySafe"] = true
        };

    private static JsonObject CreateIdempotencyRecord(
        string provider,
        string messageId)
        => new()
        {
            ["provider"] = provider,
            ["operation"] = EmailWorkflowSideEffectConstants.Operation,
            ["key"] = CreateGmailIdempotencyKey(messageId),
            ["messageId"] = messageId,
            ["retrySafe"] = true
        };

    private static JsonObject CreateProcessedMarkerRecord(GmailMessageLabelMutationResult result)
        => new()
        {
            ["provider"] = result.Provider,
            ["messageId"] = result.MessageId,
            ["sourceMarkerName"] = result.SourceLabel,
            ["processedMarkerName"] = result.ProcessedLabel,
            ["sourceMarkerRemoved"] = result.SourceLabelRemoved,
            ["processedMarkerAdded"] = result.ProcessedLabelAdded
        };

    private static string CreateGmailIdempotencyKey(string messageId)
        => string.IsNullOrWhiteSpace(messageId)
            ? string.Empty
            : $"{EmailWorkflowSideEffectConstants.GmailIdempotencyPrefix}{messageId.Trim()}";

    private static bool TryParseObject(
        string json,
        out JsonObject value)
    {
        try
        {
            value = JsonNode.Parse(json) as JsonObject ?? new JsonObject();
            return value.Count > 0;
        }
        catch (JsonException)
        {
            value = new JsonObject();
            return false;
        }
    }

    private WorkflowExecutorAvailabilityDescriptor ResolveAvailability()
    {
        var workflowGrant = grantEvaluator.Evaluate(GmailPluginConstants.PluginId, PluginCapabilityKind.WorkflowExecutor);
        if (!workflowGrant.Allowed)
        {
            return WorkflowExecutorAvailabilityDescriptor.Unavailable(workflowGrant.Kind.ToString(), workflowGrant.Message);
        }

        var oauthGrant = grantEvaluator.Evaluate(GmailPluginConstants.PluginId, PluginCapabilityKind.OAuth2);
        return oauthGrant.Allowed
            ? WorkflowExecutorAvailabilityDescriptor.Available()
            : WorkflowExecutorAvailabilityDescriptor.Unavailable(oauthGrant.Kind.ToString(), oauthGrant.Message);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

internal static class GmailWorkflowSimulationTemplates
{
    public static WorkflowExecutorSimulationDescriptor DownloadByLabel { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "provider": "gmail",
          "filterKind": "label",
          "filterValue": "simulated-preview-label",
          "processedLabel": "simulated-preview-processed",
          "count": 1,
          "messages": [
            {
              "id": "simulated-gmail-message-1",
              "threadId": "simulated-gmail-thread-1",
              "subject": "Simulated workflow preview email",
              "from": "preview@example.test",
              "receivedAt": "{{utcNow}}",
              "snippet": "Preview-only Gmail message generated for workflow simulation.",
              "bodyText": "This is simulated Gmail content for a Run Preview flow. It is not fetched from Gmail.",
              "labels": [
                "simulated-preview-label"
              ],
              "webLink": "https://mail.google.com/mail/u/0/#inbox/simulated-gmail-message-1"
            }
          ],
          "gmailProcessing": {
            "connectionId": "preview",
            "sourceLabel": "simulated-preview-label",
            "processedLabel": "simulated-preview-processed",
            "messageIds": [
              "simulated-gmail-message-1"
            ],
            "selectedMessageId": "simulated-gmail-message-1",
            "idempotencyKey": "gmail:simulated-gmail-message-1",
            "idempotencyKeys": [
              "gmail:simulated-gmail-message-1"
            ]
          },
          "projectId": "{{inputPath:$.projectId}}",
          "nodeId": "{{inputPath:$.nodeId}}",
          "project": "{{inputPath:$.project}}",
          "runContext": {
            "workflowNodeId": "{{inputPath:$.runContext.workflowNodeId}}",
            "gmailProcessing": {
              "connectionId": "preview",
              "sourceLabel": "simulated-preview-label",
              "processedLabel": "simulated-preview-processed",
              "messageIds": [
                "simulated-gmail-message-1"
              ],
              "selectedMessageId": "simulated-gmail-message-1",
              "idempotencyKey": "gmail:simulated-gmail-message-1",
              "idempotencyKeys": [
                "gmail:simulated-gmail-message-1"
              ]
            }
          },
          "workflowInput": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate a one-message Gmail label download without calling Gmail.");

    public static WorkflowExecutorSimulationDescriptor MarkProcessed { get; } = WorkflowExecutorSimulationDescriptor.JsonTemplate(
        """
        {
          "provider": "gmail",
          "messageId": "simulated-gmail-message-1",
          "sourceLabel": "simulated-preview-label",
          "processedLabel": "simulated-preview-processed",
          "sourceLabelRemoved": true,
          "processedLabelAdded": true,
          "sideEffectMode": "Preview",
          "dryRun": true,
          "committed": false,
          "idempotencyRecord": {
            "provider": "gmail",
            "operation": "processed-marker",
            "key": "gmail:simulated-gmail-message-1",
            "messageId": "simulated-gmail-message-1",
            "retrySafe": true
          },
          "processedMarker": {
            "provider": "gmail",
            "messageId": "simulated-gmail-message-1",
            "sourceMarkerName": "simulated-preview-label",
            "processedMarkerName": "simulated-preview-processed",
            "sourceMarkerRemoved": true,
            "processedMarkerAdded": true
          },
          "externalSideEffectReceipt": {
            "schemaVersion": "workflow-email-processed-marker/v1",
            "provider": "gmail",
            "operation": "processed-marker",
            "mode": "Preview",
            "dryRun": true,
            "committed": false,
            "mutationApplied": false,
            "idempotencyKey": "gmail:simulated-gmail-message-1",
            "messageId": "simulated-gmail-message-1",
            "sourceMarkerName": "simulated-preview-label",
            "processedMarkerName": "simulated-preview-processed",
            "sourceMarkerRemoved": true,
            "processedMarkerAdded": true,
            "retrySafe": true
          },
          "inputPayload": "{{inputPayload}}",
          "simulation": {
            "nodeId": "{{node.id}}",
            "nodeName": "{{node.name}}",
            "sourceExecutorId": "{{source.executor.id}}",
            "reason": "{{simulation.reason}}",
            "generatedAtUtc": "{{utcNow}}"
          }
        }
        """,
        "Simulate the Gmail label mutation without modifying Gmail.");
}
